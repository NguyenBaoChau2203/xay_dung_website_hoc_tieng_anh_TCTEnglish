/*
 * First-run onboarding pop-up: welcome, 4 steps (goals, topics, level and pace, preview), done.
 * Server-rendered host (_OnboardingHost.cshtml) supplies #tobConfig (JSON) and a Bootstrap modal shell.
 * Markup is built with createElement/textContent only; nothing from the server is written as HTML.
 * Any failure closes quietly: the page behind the pop-up always keeps working.
 */
(function () {
    'use strict';

    var core = window.TctOnboardingCore;
    var root = document.getElementById('tobRoot');
    var configEl = document.getElementById('tobConfig');
    if (!core || !root || !configEl) { return; }

    var cfg;
    try { cfg = JSON.parse(configEl.textContent || '{}'); } catch (e) { return; }

    var modalEl = document.getElementById('tobModal');
    var dialogEl = document.getElementById('tobDialog');
    var bodyEl = document.getElementById('tobBody');
    var liveEl = document.getElementById('tobLive');
    var tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
    if (!modalEl || !dialogEl || !bodyEl) { return; }

    var api = cfg.api || {};
    var catalog = cfg.catalog || null;
    var modal = null;
    var isOpen = false;
    var lastFocus = null;
    var focusTarget = null;
    var state = null;

    function debug(error) {
        if (cfg.development && window.console) { window.console.error('[onboarding]', error); }
    }

    // ------------------------------------------------------------------ small helpers

    function h(tag, attrs, children) {
        var el = document.createElement(tag);
        Object.keys(attrs || {}).forEach(function (name) {
            var value = attrs[name];
            if (value === null || value === undefined || value === false) { return; }
            if (name === 'class') { el.className = value; }
            else if (name === 'text') { el.textContent = value; }
            else { el.setAttribute(name, value === true ? '' : value); }
        });
        (children || []).forEach(function (child) {
            if (child === null || child === undefined) { return; }
            el.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
        });
        return el;
    }

    function icon(classes) { return h('i', { 'class': classes, 'aria-hidden': 'true' }); }

    function announce(message) {
        if (!liveEl) { return; }
        liveEl.textContent = '';
        window.setTimeout(function () { liveEl.textContent = message; }, 30);
    }

    function request(method, url, body) {
        var headers = { Accept: 'application/json' };
        if (tokenInput) { headers.RequestVerificationToken = tokenInput.value; }
        var init = { method: method, headers: headers, credentials: 'same-origin' };
        if (body !== undefined) { headers['Content-Type'] = 'application/json'; init.body = JSON.stringify(body); }
        return fetch(url, init).then(function (response) {
            return response.text().then(function (text) {
                var data = null;
                if (text) { try { data = JSON.parse(text); } catch (e) { data = null; } }
                return { ok: response.ok, status: response.status, data: data };
            });
        });
    }

    function errorText(result) {
        if (result && result.data && result.data.error) { return result.data.error; }
        if (result && result.status === 429) { return 'Bạn đang thao tác quá nhanh. Vui lòng chờ ít phút rồi thử lại.'; }
        return 'Có lỗi xảy ra. Hãy thử lại.';
    }

    var NETWORK_ERROR = 'Không kết nối được. Kiểm tra mạng rồi thử lại.';

    // ------------------------------------------------------------------ state

    function freshState(mode) {
        return {
            mode: mode,
            step: mode === 'first' ? 0 : 1,
            goals: [],
            topics: [],
            level: null,
            pace: 10,
            tab: 'suggested',
            preview: null,
            deckKey: null,
            off: {},
            known: {},
            showAll: {},
            requestId: null,
            busy: false,
            error: null,
            result: null,
            completed: false
        };
    }

    function topicByKey(key) {
        for (var i = 0; i < catalog.topics.length; i++) { if (catalog.topics[i].key === key) { return catalog.topics[i]; } }
        return null;
    }

    // ------------------------------------------------------------------ opening and closing

    function ensureModal() {
        if (!modal && window.bootstrap && window.bootstrap.Modal) {
            modal = new window.bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false, focus: true });
        }
        return modal;
    }

    function show() {
        if (!ensureModal()) { return false; }
        lastFocus = document.activeElement;
        isOpen = true;
        document.body.classList.add('tob-open');
        modal.show();
        return true;
    }

    function hide() {
        if (modal) { modal.hide(); }
    }

    // Bootstrap moves focus to the dialog once it has finished opening: put it back on the first control of the step.
    modalEl.addEventListener('shown.bs.modal', function () {
        if (focusTarget && document.contains(focusTarget) && typeof focusTarget.focus === 'function') { focusTarget.focus(); }
    });

    modalEl.addEventListener('hidden.bs.modal', function () {
        isOpen = false;
        document.body.classList.remove('tob-open');
        bodyEl.textContent = '';
        if (lastFocus && typeof lastFocus.focus === 'function' && document.contains(lastFocus)) {
            try { lastFocus.focus(); } catch (e) { /* element may be gone */ }
        }
        if (state && state.completed && window.location.pathname.indexOf('/Sets') === 0) { window.location.reload(); }
    });

    function open(mode, auto) {
        try {
            state = freshState(mode);
            if (!show()) { return; }
            if (auto) { request('POST', api.prompted).catch(debug); }
            if (catalog) { render(); return; }
            bodyEl.textContent = '';
            bodyEl.appendChild(h('div', { 'class': 'tob-loading', role: 'status', text: 'Đang tải…' }));
            request('GET', api.catalog).then(function (result) {
                if (!result.ok || !result.data) { throw new Error('catalog ' + result.status); }
                catalog = result.data;
                render();
            }).catch(function (error) {
                debug(error);
                closeNow();
            });
        } catch (error) {
            debug(error);
            closeNow();
        }
    }

    function closeNow() {
        isOpen = false;
        try { hide(); } catch (e) { /* nothing to close */ }
    }

    /** "Để sau": the server counts it (the second one ends the prompts) in the first-run mode; a plain close otherwise. */
    function skip() {
        if (state && state.busy) { return; }
        if (state && state.mode === 'first' && !state.completed) { request('POST', api.skip).catch(debug); }
        hide();
    }

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape' || !isOpen || !state || state.busy) { return; }
        event.preventDefault();
        state.step === 5 ? hide() : skip();
    });

    var AGAIN_HASH = '#tao-them-tu-so-thich';

    document.addEventListener('click', function (event) {
        var target = event.target && event.target.closest ? event.target : null;
        if (!target) { return; }

        // The one-time banner on "Bộ thẻ của tôi": remembered on the server, removed at once.
        var dismiss = target.closest('[data-tob-banner-dismiss]');
        if (dismiss) {
            var banner = dismiss.closest('[data-tob-banner]');
            request('POST', api.banner || '/api/onboarding/banner/dismiss').catch(debug);
            if (banner && banner.parentNode) { banner.parentNode.removeChild(banner); }
            return;
        }

        // "Tạo thêm từ sở thích" and "Đổi sở thích" are links to /Sets#tao-them-tu-so-thich so they also work without the script.
        var trigger = target.closest('[data-tob-open], a[href$="' + AGAIN_HASH + '"]');
        if (!trigger) { return; }
        event.preventDefault();
        if (!isOpen) { open('again', false); }
    });

    // ------------------------------------------------------------------ shell and navigation

    function goTo(step) {
        state.step = step;
        state.error = null;
        render();
    }

    function render() {
        bodyEl.textContent = '';
        dialogEl.setAttribute('data-step', String(state.step));
        var views = [renderWelcome, renderGoals, renderTopics, renderLevel, renderPreview, renderDone];
        var node = views[state.step]();
        bodyEl.appendChild(node.element);
        var title = node.element.querySelector('#tobTitle');
        focusTarget = node.element.querySelector(node.focus || '#tobTitle');
        if (focusTarget) { focusTarget.focus(); }
        announce(node.announce || (title ? title.textContent : ''));
    }

    function headBar(step) {
        var showBack = step >= 2 || (step === 1 && state.mode === 'first');
        var segments = [];
        for (var i = 1; i <= 4; i++) { segments.push(h('span', { 'class': 'tob-progress__seg' + (i <= step ? ' is-done' : '') })); }
        var back = showBack
            ? h('button', { type: 'button', 'class': 'tob-back' }, [icon('fa-solid fa-arrow-left'), 'Quay lại'])
            : null;
        if (back) { back.addEventListener('click', function () { goTo(step - 1); }); }
        var skipBtn = h('button', { type: 'button', 'class': 'tob-skip', text: 'Để sau' });
        skipBtn.addEventListener('click', skip);
        return h('div', { 'class': 'tob-head' }, [
            back,
            h('div', { 'class': 'tob-progress', 'aria-hidden': 'true' }, segments),
            h('span', { 'class': 'tob-stepname', text: 'Bước ' + step + '/4' }),
            skipBtn
        ]);
    }

    function stepShell(step, title, lead, content, foot) {
        var scroll = h('div', { 'class': 'tob-scroll' }, [
            h('h2', { 'class': 'tob-title', id: 'tobTitle', tabindex: '-1', text: title }),
            lead,
            content
        ]);
        var children = [headBar(step), scroll];
        if (foot) { children.push(foot); }
        return h('div', { 'class': 'tob-step' }, children);
    }

    function primaryButton(label, iconClass, onClick) {
        var button = h('button', { type: 'button', 'class': 'btn btn-primary tob-foot__main' }, [label + ' ', icon(iconClass)]);
        button.addEventListener('click', onClick);
        return button;
    }

    // ------------------------------------------------------------------ step 0: welcome

    function renderWelcome() {
        var thumbs = h('div', { 'class': 'tob-welcome__thumbs', 'aria-hidden': 'true' },
            (catalog.welcome || []).slice(0, 5).map(function (url) { return h('img', { 'class': 'tob-welcome__thumb', src: url, alt: '' }); }));
        var start = h('button', { type: 'button', 'class': 'btn btn-primary btn-lg tob-start' }, ['Bắt đầu ', icon('fa-solid fa-arrow-right')]);
        start.addEventListener('click', function () { goTo(1); });
        var later = h('button', { type: 'button', 'class': 'btn btn-link', text: 'Để sau' });
        later.addEventListener('click', skip);
        var facts = h('ul', { 'class': 'tob-welcome__facts' }, [
            h('li', {}, [icon('fa-regular fa-clock'), 'Khoảng 1 phút']),
            h('li', {}, [icon('fa-regular fa-image'), 'Thẻ có ảnh, phát âm, ví dụ']),
            h('li', {}, [icon('fa-solid fa-pen'), 'Sửa được bất cứ lúc nào'])
        ]);
        var element = h('div', { 'class': 'tob-step' }, [
            h('div', { 'class': 'tob-welcome' }, [
                thumbs,
                h('h2', { 'class': 'tob-title', id: 'tobTitle', tabindex: '-1', text: core.welcomeTitle(cfg.fullName) }),
                h('p', { 'class': 'tob-lead' }, [
                    'Trả lời 4 câu hỏi ngắn, TCT sẽ tạo ngay ', h('strong', { text: 'bộ thẻ từ vựng riêng' }), ' theo mục tiêu và sở thích của bạn.'
                ]),
                facts,
                h('div', { 'class': 'tob-welcome__actions' }, [start, later])
            ])
        ]);
        return { element: element, focus: '.tob-start' };
    }

    // ------------------------------------------------------------------ roving radio/checkbox helpers

    function bindArrows(container, selector) {
        container.addEventListener('keydown', function (event) {
            var keys = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 };
            var items = Array.prototype.slice.call(container.querySelectorAll(selector));
            var index = items.indexOf(document.activeElement);
            if (index < 0) { return; }
            var next = null;
            if (keys[event.key]) { next = items[(index + keys[event.key] + items.length) % items.length]; }
            else if (event.key === 'Home') { next = items[0]; }
            else if (event.key === 'End') { next = items[items.length - 1]; }
            if (next) { event.preventDefault(); next.focus(); if (next.getAttribute('role') === 'radio') { next.click(); } }
        });
    }

    function checkbox(el, checked, onToggle) {
        el.setAttribute('role', 'checkbox');
        el.setAttribute('aria-checked', checked ? 'true' : 'false');
        el.addEventListener('click', onToggle);
        el.addEventListener('keydown', function (event) {
            if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); onToggle(); }
        });
    }

    // ------------------------------------------------------------------ step 1: goals

    function renderGoals() {
        var hint = h('p', { 'class': 'tob-hint', role: 'status' });
        var cards = [];
        var counter = h('span', { 'class': 'tob-foot__note' });
        var next = primaryButton('Tiếp tục', 'fa-solid fa-arrow-right', function () { if (core.canContinue(1, state)) { goTo(2); } });

        function refresh() {
            cards.forEach(function (entry) {
                entry.el.setAttribute('aria-checked', state.goals.indexOf(entry.key) >= 0 ? 'true' : 'false');
            });
            counter.textContent = 'Đã chọn ' + state.goals.length + '/' + core.MAX_GOALS;
            next.disabled = !core.canContinue(1, state);
        }

        var grid = h('div', { 'class': 'tob-grid-goals', role: 'group', 'aria-labelledby': 'tobTitle' });
        catalog.goals.forEach(function (goal) {
            var media = goal.photo
                ? h('img', { 'class': 'tob-choice__img', src: goal.photo, alt: '', loading: 'lazy' })
                : h('span', { 'class': 'tob-choice__icon tob-choice__icon--' + goal.key }, [icon(goal.icon)]);
            var el = h('div', { 'class': 'tob-choice tob-choice--photo', tabindex: '0' }, [
                media,
                h('span', { 'class': 'tob-choice__body' }, [
                    h('span', { 'class': 'tob-choice__title', text: goal.title }),
                    h('span', { 'class': 'tob-choice__text', text: goal.description })
                ]),
                h('span', { 'class': 'tob-check', 'aria-hidden': 'true' }, [icon('fa-solid fa-check')])
            ]);
            checkbox(el, state.goals.indexOf(goal.key) >= 0, function () {
                var result = core.toggleLimited(state.goals, goal.key, core.MAX_GOALS);
                state.goals = result.list;
                state.preview = null;
                hint.textContent = result.blocked ? 'Bạn chỉ chọn được tối đa 3 mục.' : '';
                if (result.blocked) { announce(hint.textContent); }
                refresh();
            });
            cards.push({ key: goal.key, el: el });
            grid.appendChild(el);
        });

        var foot = h('div', { 'class': 'tob-foot' }, [counter, next]);
        var lead = h('p', { 'class': 'tob-lead', text: 'Chọn tối đa 3 mục. TCT sẽ tạo bộ thẻ riêng cho bạn trong "Bộ thẻ của tôi".' });
        var element = stepShell(1, 'Bạn học tiếng Anh để làm gì?', lead, h('div', {}, [grid, hint]), foot);
        refresh();
        return { element: element };
    }

    // ------------------------------------------------------------------ step 2: topics

    var TABS = [
        { key: 'suggested', label: 'Gợi ý cho bạn' },
        { key: 'A', label: 'Cơ bản A1–A2' },
        { key: 'B', label: 'Trung cấp B1–B2' },
        { key: 'C', label: 'Giao tiếp' }
    ];

    function renderTopics() {
        var hint = h('p', { 'class': 'tob-hint', role: 'status' });
        var tiles = h('div', { 'class': 'tob-tiles', role: 'group', 'aria-labelledby': 'tobTitle' });
        var chipText = h('span');
        var chip = h('span', { 'class': 'tob-count-chip' }, [icon('fa-solid fa-check'), chipText]);
        var next = primaryButton('Tiếp tục', 'fa-solid fa-arrow-right', function () { if (core.canContinue(2, state)) { goTo(3); } });
        var tabList = h('div', { 'class': 'tob-tabs', role: 'tablist', 'aria-label': 'Nhóm chủ đề' });
        var tabButtons = [];

        function refreshFooter() {
            chipText.textContent = ' Đã chọn ' + state.topics.length + ' chủ đề';
            next.disabled = !core.canContinue(2, state);
        }

        function buildTiles() {
            tiles.textContent = '';
            core.tabTopics(catalog.topics, state.goals, state.tab).forEach(function (topic) {
                var selected = state.topics.indexOf(topic.key) >= 0;
                var children = [];
                if (topic.cover) { children.push(h('img', { 'class': 'tob-tile__img', src: topic.cover, alt: '', loading: 'lazy' })); }
                else { children.push(h('span', { 'class': 'tob-tile__art', 'aria-hidden': 'true' }, [icon('fa-solid fa-layer-group')])); }
                if (core.isSuggested(topic, state.goals)) { children.push(h('span', { 'class': 'tob-tile__hint', text: 'Gợi ý' })); }
                children.push(h('span', { 'class': 'tob-tile__caption' }, [
                    h('span', { 'class': 'tob-tile__name', text: topic.name }),
                    h('span', { 'class': 'tob-tile__band', text: topic.band })
                ]));
                children.push(h('span', { 'class': 'tob-check', 'aria-hidden': 'true' }, [icon('fa-solid fa-check')]));
                var el = h('button', { type: 'button', 'class': 'tob-tile', 'aria-label': topic.name + ', ' + topic.band }, children);
                checkbox(el, selected, function () {
                    var result = core.toggleLimited(state.topics, topic.key, core.MAX_TOPICS);
                    state.topics = result.list;
                    state.preview = null;
                    hint.textContent = result.blocked ? 'Chọn tối đa 8 chủ đề để bộ thẻ gọn và dễ học.' : '';
                    if (result.blocked) { announce(hint.textContent); }
                    el.setAttribute('aria-checked', state.topics.indexOf(topic.key) >= 0 ? 'true' : 'false');
                    refreshFooter();
                });
                tiles.appendChild(el);
            });
        }

        function selectTab(key) {
            state.tab = key;
            tabButtons.forEach(function (entry) {
                entry.el.setAttribute('aria-selected', entry.key === key ? 'true' : 'false');
                entry.el.tabIndex = entry.key === key ? 0 : -1;
            });
            buildTiles();
        }

        TABS.forEach(function (tab) {
            var el = h('button', { type: 'button', 'class': 'tob-tab', role: 'tab', text: tab.label });
            el.addEventListener('click', function () { selectTab(tab.key); });
            tabButtons.push({ key: tab.key, el: el });
            tabList.appendChild(el);
        });
        tabList.appendChild(h('span', { 'class': 'tob-tabs__count', text: catalog.topics.length + ' chủ đề' }));
        bindArrows(tabList, '[role="tab"]');

        var lead = h('p', { 'class': 'tob-lead' }, [
            'Các chủ đề có nhãn ', h('span', { 'class': 'tob-hint-word', text: 'Gợi ý' }), ' hợp với mục tiêu bạn vừa chọn. Chọn ít nhất 3.'
        ]);
        var foot = h('div', { 'class': 'tob-foot' }, [
            chip,
            h('span', { 'class': 'tob-foot__note', text: 'Bạn đổi lại được bất cứ lúc nào trong Bộ thẻ của tôi' }),
            next
        ]);
        var element = stepShell(2, 'Bạn muốn học từ vựng về chủ đề nào?', lead, h('div', {}, [tabList, tiles, hint]), foot);
        selectTab(state.tab);
        refreshFooter();
        return { element: element };
    }

    // ------------------------------------------------------------------ step 3: level and pace

    var LEVELS = [
        { key: 'A1', name: 'Mới bắt đầu', text: 'Tôi biết vài từ đơn giản như hello, thank you.' },
        { key: 'A2', name: 'Cơ bản', text: 'Tôi đọc được câu ngắn: ', example: 'I usually have rice for lunch.' },
        { key: 'B1', name: 'Trung cấp', text: 'Tôi hiểu đoạn hội thoại hằng ngày khi nói chậm.' },
        { key: 'B2', name: 'Khá', text: 'Tôi theo được tin tức và phim có phụ đề Anh.' }
    ];
    var PACES = [
        { value: 5, name: 'Nhẹ nhàng' }, { value: 10, name: 'Vừa sức' }, { value: 15, name: 'Chăm chỉ' }, { value: 20, name: 'Tăng tốc' }
    ];

    function radioGroup(items, labelledBy, className, build, getChecked, onPick) {
        var group = h('div', { 'class': className, role: 'radiogroup', 'aria-labelledby': labelledBy });
        var els = [];
        items.forEach(function (item) {
            var el = build(item);
            el.setAttribute('role', 'radio');
            el.addEventListener('click', function () { onPick(item); sync(); });
            els.push({ item: item, el: el });
            group.appendChild(el);
        });
        function sync() {
            var anyChecked = els.some(function (entry) { return getChecked(entry.item); });
            els.forEach(function (entry, index) {
                var checked = getChecked(entry.item);
                entry.el.setAttribute('aria-checked', checked ? 'true' : 'false');
                entry.el.tabIndex = checked || (!anyChecked && index === 0) ? 0 : -1;
            });
        }
        sync();
        bindArrows(group, '[role="radio"]');
        return group;
    }

    function renderLevel() {
        if (!state.level) { state.level = core.defaultLevel(state.goals); }
        var levelGroup = radioGroup(LEVELS, 'tobTitle', 'tob-grid-level',
            function (level) {
                var textNodes = [level.text];
                if (level.example) { textNodes.push(h('em', { text: level.example })); }
                return h('button', { type: 'button', 'class': 'tob-choice' }, [
                    h('span', { 'class': 'tob-choice__big', text: level.key }),
                    h('span', { 'class': 'tob-choice__title', text: level.name }),
                    h('span', { 'class': 'tob-choice__text' }, textNodes),
                    h('span', { 'class': 'tob-check', 'aria-hidden': 'true' }, [icon('fa-solid fa-check')])
                ]);
            },
            function (level) { return state.level === level.key; },
            function (level) { state.level = level.key; state.preview = null; });

        var paceGroup = radioGroup(PACES, 'tobPaceTitle', 'tob-grid-pace',
            function (pace) {
                return h('button', { type: 'button', 'class': 'tob-choice tob-choice--pace' }, [
                    h('span', { 'class': 'tob-choice__title', text: pace.value + ' từ' }),
                    h('span', { 'class': 'tob-choice__text', text: pace.name + ' · ~' + pace.value + ' phút' })
                ]);
            },
            function (pace) { return state.pace === pace.value; },
            function (pace) { state.pace = pace.value; });

        var next = primaryButton('Xem trước bộ thẻ', 'fa-solid fa-arrow-right', function () {
            if (core.canContinue(3, state)) { state.requestId = null; goTo(4); }
        });
        var foot = h('div', { 'class': 'tob-foot' }, [
            h('span', { 'class': 'tob-foot__note' }, [icon('fa-solid fa-lock'), ' Chỉ dùng để gợi ý bài học, không chia sẻ với ai.']),
            next
        ]);
        var lead = h('p', { 'class': 'tob-lead', text: 'Không chắc? Chọn mức thấp hơn, TCT sẽ tự nâng dần khi bạn học tốt.' });
        var content = h('div', {}, [
            levelGroup,
            h('h3', { 'class': 'tob-section-title', id: 'tobPaceTitle', text: 'Mỗi ngày học bao nhiêu từ mới?' }),
            paceGroup
        ]);
        return { element: stepShell(3, 'Trình độ và nhịp học của bạn', lead, content, foot) };
    }

    // ------------------------------------------------------------------ step 4: preview

    function choicesPayload() {
        return { goals: state.goals, topics: state.topics, level: state.level, pace: state.pace };
    }

    function renderPreview() {
        if (state.preview) { return renderPreviewReady(); }

        var content = h('div', { 'class': 'tob-loading', role: 'status' }, [h('span', { 'class': 'tob-spinner', 'aria-hidden': 'true' }), 'Đang chuẩn bị bộ thẻ…']);
        var lead = h('p', { 'class': 'tob-lead', text: 'Bấm Đã biết để bỏ những từ bạn đã thuộc. Bạn vẫn sửa, xóa hay thêm từ được sau khi tạo.' });
        var element = stepShell(4, 'Xem trước bộ thẻ sẽ thêm vào "Bộ thẻ của tôi"', lead, content, null);

        var token = {};
        state.previewToken = token;
        request('POST', api.preview, choicesPayload()).then(function (result) {
            if (state.previewToken !== token || state.step !== 4) { return; }
            if (!result.ok || !result.data) {
                content.textContent = '';
                content.className = '';
                var retry = h('button', { type: 'button', 'class': 'btn btn-outline-primary', text: 'Thử lại' });
                retry.addEventListener('click', render);
                content.appendChild(h('div', { 'class': 'tob-error', role: 'alert' }, [h('span', { text: errorText(result) }), retry]));
                return;
            }
            state.preview = result.data;
            state.deckKey = result.data.decks.length ? result.data.decks[0].key : null;
            state.off = {};
            state.known = {};
            state.showAll = {};
            state.requestId = null;
            render();
        }).catch(function (error) {
            debug(error);
            if (state.previewToken !== token) { return; }
            content.textContent = '';
            content.className = '';
            var retry = h('button', { type: 'button', 'class': 'btn btn-outline-primary', text: 'Thử lại' });
            retry.addEventListener('click', render);
            content.appendChild(h('div', { 'class': 'tob-error', role: 'alert' }, [h('span', { text: NETWORK_ERROR }), retry]));
        });
        return { element: element };
    }

    function deckThumb(deck) {
        var first = deck.cards.filter(function (card) { return card.imageUrl; })[0];
        return first ? h('img', { 'class': 'tob-deck__thumb', src: first.imageUrl, alt: '' }) : h('span', { 'class': 'tob-deck__thumb' });
    }

    function renderPreviewReady() {
        var preview = state.preview;
        var decks = preview.decks;
        var hint = h('p', { 'class': 'tob-hint', role: 'status' });
        var deckList = h('ul', { 'class': 'tob-decks' });
        var detail = h('div');
        var totalText = h('span', { 'class': 'tob-foot__note' });
        var createBtn = h('button', { type: 'button', 'class': 'btn btn-primary tob-foot__main' });
        var errorBox = h('div', { 'aria-live': 'assertive' });

        function totals() { return core.previewTotals(decks, state.off, state.known, state.pace); }

        function refreshFooter() {
            var t = totals();
            totalText.textContent = '';
            totalText.appendChild(icon('fa-solid fa-layer-group'));
            totalText.appendChild(document.createTextNode(' ' + t.text));
            createBtn.textContent = '';
            if (state.busy) {
                createBtn.appendChild(h('span', { 'class': 'tob-spinner', 'aria-hidden': 'true' }));
                createBtn.appendChild(document.createTextNode('Đang tạo bộ thẻ…'));
            } else {
                createBtn.appendChild(icon('fa-solid fa-plus'));
                createBtn.appendChild(document.createTextNode(' Thêm ' + t.decks + ' bộ vào Bộ thẻ của tôi'));
            }
            createBtn.disabled = state.busy || !core.canCreate(decks, state.off, state.known);
        }

        function buildDecks() {
            deckList.textContent = '';
            decks.forEach(function (deck) {
                var off = !!state.off[deck.key];
                var kept = core.keptCards(deck, state.known).length;
                var sourceNames = deck.topics.slice(0, 2).map(function (topic) { return '"' + topic.name + '"'; }).join(', ');
                var select = h('button', { type: 'button', 'class': 'tob-deck__select' }, [
                    deckThumb(deck),
                    h('span', { 'class': 'tob-deck__main' }, [
                        h('span', { 'class': 'tob-deck__name', text: deck.name }),
                        h('span', { 'class': 'tob-deck__meta', text: kept + ' từ · từ ' + sourceNames })
                    ])
                ]);
                select.addEventListener('click', function () { state.deckKey = deck.key; buildDecks(); buildDetail(); });
                var toggle = h('button', { type: 'button', 'class': 'tob-switch', role: 'switch', 'aria-label': 'Thêm bộ ' + deck.name, 'aria-checked': off ? 'false' : 'true' });
                toggle.addEventListener('click', function () {
                    var turningOff = !state.off[deck.key];
                    if (turningOff && decks.filter(function (d) { return !state.off[d.key]; }).length <= 1) {
                        hint.textContent = 'Hãy giữ lại ít nhất một bộ thẻ.';
                        announce(hint.textContent);
                        return;
                    }
                    hint.textContent = '';
                    state.off[deck.key] = turningOff;
                    state.requestId = null;
                    buildDecks();
                    refreshFooter();
                });
                deckList.appendChild(h('li', { 'class': 'tob-deck' + (state.deckKey === deck.key ? ' is-current' : '') + (off ? ' is-off' : '') }, [select, toggle]));
            });
        }

        function buildDetail() {
            detail.textContent = '';
            var deck = decks.filter(function (d) { return d.key === state.deckKey; })[0] || decks[0];
            if (!deck) { return; }
            var kept = core.keptCards(deck, state.known).length;
            var dropped = deck.cards.length - kept;
            detail.appendChild(h('div', { 'class': 'tob-deckhead' }, [
                h('h3', { 'class': 'tob-deckhead__name', text: deck.name }),
                h('span', { 'class': 'tob-count-chip', text: kept + '/' + deck.cards.length + ' từ giữ lại' }),
                dropped > 0 ? h('span', { 'class': 'tob-deckhead__dropped', text: 'Bỏ ' + dropped + ' từ đã biết' }) : null
            ]));

            var limit = state.showAll[deck.key] ? deck.cards.length : core.PREVIEW_FIRST;
            var grid = h('div', { 'class': 'tob-cards' });
            deck.cards.slice(0, limit).forEach(function (card) {
                var known = !!state.known[card.cardId];
                var toggle = h('button', { type: 'button', 'class': 'tob-known', 'aria-pressed': known ? 'true' : 'false' },
                    known ? [icon('fa-solid fa-rotate-left'), ' Giữ lại'] : [icon('fa-solid fa-check'), ' Đã biết']);
                toggle.setAttribute('aria-label', (known ? 'Giữ lại từ ' : 'Đã biết từ ') + card.term);
                toggle.addEventListener('click', function () {
                    if (state.known[card.cardId]) { delete state.known[card.cardId]; } else { state.known[card.cardId] = true; }
                    state.requestId = null;
                    buildDecks();
                    buildDetail();
                    refreshFooter();
                    var again = detail.querySelector('[data-card="' + card.cardId + '"] .tob-known');
                    if (again) { again.focus(); }
                });
                var children = [];
                children.push(card.imageUrl ? h('img', { 'class': 'tob-card__img', src: card.imageUrl, alt: '', loading: 'lazy' }) : h('span', { 'class': 'tob-card__img' }));
                children.push(h('span', { 'class': 'tob-card__body' }, [
                    h('span', { 'class': 'tob-card__term', text: card.term }),
                    h('span', { 'class': 'tob-card__meaning', text: card.meaning || '' })
                ]));
                children.push(toggle);
                grid.appendChild(h('div', { 'class': 'tob-card' + (known ? ' is-known' : ''), 'data-card': String(card.cardId) }, children));
            });
            detail.appendChild(grid);
            if (deck.cards.length > limit) {
                var more = h('button', { type: 'button', 'class': 'tob-more', text: 'Xem thêm ' + (deck.cards.length - limit) + ' từ' });
                more.addEventListener('click', function () { state.showAll[deck.key] = true; buildDetail(); });
                detail.appendChild(more);
            }
        }

        createBtn.addEventListener('click', submit);

        function showFailure(message, canRetry) {
            errorBox.textContent = '';
            var children = [h('span', { text: message })];
            if (canRetry) {
                var retry = h('button', { type: 'button', 'class': 'btn btn-outline-danger btn-sm', text: 'Thử lại' });
                retry.addEventListener('click', submit);
                children.push(retry);
            }
            errorBox.appendChild(h('div', { 'class': 'tob-error', role: 'alert' }, children));
        }

        function submit() {
            if (state.busy || !core.canCreate(decks, state.off, state.known)) { return; }
            if (!state.requestId) { state.requestId = core.newRequestId(window.crypto); }
            state.busy = true;
            errorBox.textContent = '';
            refreshFooter();
            var body = choicesPayload();
            body.requestId = state.requestId;
            body.decksOff = decks.filter(function (d) { return state.off[d.key]; }).map(function (d) { return d.key; });
            body.knownCardIds = Object.keys(state.known).map(Number);
            var keptDecks = decks.filter(function (d) { return !state.off[d.key] && core.keptCards(d, state.known).length > 0; });
            request('POST', api.complete, body).then(function (result) {
                state.busy = false;
                if (result.ok && result.data) {
                    state.result = result.data;
                    state.resultDecks = keptDecks;
                    state.completed = true;
                    goTo(5);
                    return;
                }
                refreshFooter();
                showFailure(errorText(result), result.status !== 400 && result.status !== 409);
                if (result.status === 409) { state.requestId = null; }
            }).catch(function (error) {
                debug(error);
                state.busy = false;
                refreshFooter();
                showFailure(NETWORK_ERROR, true);
            });
        }

        var left = h('div', {}, [
            h('p', { 'class': 'tob-decks-title', text: decks.length + ' bộ thẻ · thư mục "Lộ trình của tôi"' }),
            deckList,
            hint,
            h('p', { 'class': 'tob-note' }, [
                'Ưu tiên từ ', h('strong', { text: state.level }), ', xếp từ dễ đến khó. Ảnh, ví dụ và phát âm lấy từ thư viện TCT.'
            ])
        ]);
        var content = h('div', {}, [h('div', { 'class': 'tob-preview' }, [left, detail]), errorBox]);
        var lead = h('p', { 'class': 'tob-lead' }, ['Bấm ', h('strong', { text: 'Đã biết' }), ' để bỏ những từ bạn đã thuộc. Bạn vẫn sửa, xóa hay thêm từ được sau khi tạo.']);
        var foot = h('div', { 'class': 'tob-foot' }, [totalText, createBtn]);
        var element = stepShell(4, 'Xem trước bộ thẻ sẽ thêm vào "Bộ thẻ của tôi"', lead, content, foot);
        buildDecks();
        buildDetail();
        refreshFooter();
        return { element: element, announce: 'Xem trước ' + decks.length + ' bộ thẻ, ' + totals().total + ' từ.' };
    }

    // ------------------------------------------------------------------ step 5: done

    function renderDone() {
        var result = state.result;
        var total = 0;
        result.decks.forEach(function (deck) { total += deck.count; });
        var items = result.decks.map(function (deck, index) {
            var source = (state.resultDecks || [])[index];
            var isPhrase = source && source.goal !== undefined && source.key === 'phrases';
            return h('li', { 'class': 'tob-done__item' }, [
                source ? deckThumb(source) : h('span', { 'class': 'tob-deck__thumb' }),
                h('span', { 'class': 'tob-deck__main' }, [
                    h('span', { 'class': 'tob-deck__name', text: deck.name }),
                    h('span', { 'class': 'tob-deck__meta', text: deck.count + (isPhrase ? ' câu' : ' từ') })
                ]),
                h('span', { 'class': 'tob-done__tick' }, [icon('fa-solid fa-circle-check')])
            ]);
        });
        var openLink = h('a', { 'class': 'btn btn-outline-primary', href: result.folderUrl, text: 'Mở Bộ thẻ của tôi' });
        var learnLink = h('a', { 'class': 'btn btn-primary', href: result.firstLearnUrl || result.folderUrl }, [icon('fa-solid fa-play'), ' Học ' + state.pace + ' từ đầu tiên']);
        var element = h('div', { 'class': 'tob-step' }, [
            h('div', { 'class': 'tob-scroll' }, [
                h('div', { 'class': 'tob-done' }, [
                    h('span', { 'class': 'tob-done__icon', 'aria-hidden': 'true' }, [icon('fa-solid fa-check')]),
                    h('h2', { 'class': 'tob-title', id: 'tobTitle', tabindex: '-1', text: 'Đã thêm ' + result.decks.length + ' bộ thẻ cho bạn!' }),
                    h('p', { 'class': 'tob-lead' }, [total + ' từ đã nằm trong thư mục ', h('strong', { text: 'Lộ trình của tôi' }), ' ở Bộ thẻ của tôi.']),
                    h('ul', { 'class': 'tob-done__list' }, items),
                    h('div', { 'class': 'tob-done__actions' }, [openLink, learnLink])
                ])
            ])
        ]);
        return { element: element };
    }

    // ------------------------------------------------------------------ start

    function start() {
        try {
            if (cfg.shouldPrompt) {
                window.setTimeout(function () { if (!isOpen) { open('first', true); } }, 500);
            } else if (window.location.hash === AGAIN_HASH) {
                window.setTimeout(function () { if (!isOpen) { open('again', false); } }, 300);
            }
        } catch (error) {
            debug(error);
        }
    }

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', start); } else { start(); }
})();
