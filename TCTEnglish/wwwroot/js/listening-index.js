/*
 * /Home/Listening: level row, status chips, topic select and search (client-side filters), level shelves,
 * the YouTube import card and the delete modal, thumbnail fallback.
 * Needs window.TctSets (personal-sets-common.js) and window.TctListening.core (listening-core.js).
 * DOM is built with createElement and textContent only; server data is never turned into markup.
 */
(function () {
    'use strict';

    var core = window.TctListening && window.TctListening.core;
    var Sets = window.TctSets;
    var page = document.querySelector('.tl-page--index');
    if (!core || !Sets || !page) { return; }

    var config = {};
    try { config = JSON.parse(document.getElementById('tlConfig').textContent) || {}; } catch (e) { config = {}; }
    var shelfSize = config.shelfSize > 0 ? config.shelfSize : 4;
    var gridSize = config.gridSize > 0 ? config.gridSize : 8;

    // ------------------------------------------------------------------ thumbnails

    function emptyThumb() {
        var span = Sets.el('span', 'tl-thumb--empty');
        span.setAttribute('aria-hidden', 'true');
        span.appendChild(Sets.icon('fa-solid fa-headphones'));
        return span;
    }

    function swapBrokenThumb(img) {
        if (!img || !img.parentNode || !img.classList.contains('tl-thumb')) { return; }
        img.parentNode.replaceChild(emptyThumb(), img);
    }

    // error events do not bubble: listen in the capture phase for every image, including ones added later
    page.addEventListener('error', function (event) {
        if (event.target && event.target.tagName === 'IMG') { swapBrokenThumb(event.target); }
    }, true);
    Array.prototype.forEach.call(page.querySelectorAll('img.tl-thumb'), function (img) {
        if (img.complete && img.naturalWidth === 0) { swapBrokenThumb(img); }
    });

    // ------------------------------------------------------------------ filters, shelves

    var host = document.getElementById('tlLevelsHost');
    var searchInput = document.getElementById('tlSearch');
    var levelsGroup = document.getElementById('tlLevels');
    var statusGroup = document.getElementById('tlStatuses');
    var topicSelect = document.getElementById('tlTopic');
    var countEl = document.getElementById('tlCount');
    var emptyEl = document.getElementById('tlEmpty');
    var clearBtn = document.getElementById('tlClearFilters');

    if (host && levelsGroup) { initFilters(); }

    function initFilters() {
        var items = Array.prototype.map.call(host.querySelectorAll('.tl-grid__item'), function (el) {
            return {
                id: Number(el.getAttribute('data-id')),
                title: el.getAttribute('data-title') || '',
                topic: el.getAttribute('data-topic') || '',
                level: el.getAttribute('data-level') || '',
                status: el.getAttribute('data-status') || 'new',
                el: el
            };
        });
        var sections = Array.prototype.map.call(host.querySelectorAll('[data-level-section]'), function (section) {
            return {
                level: section.getAttribute('data-level-section'),
                el: section,
                heading: section.querySelector('.tl-section-title'),
                count: section.querySelector('[data-level-count]'),
                all: section.querySelector('[data-level-all]'),
                more: section.querySelector('[data-level-more]')
            };
        });
        var lessons = items.map(function (it) { return { id: it.id, title: it.title, topic: it.topic, level: it.level, status: it.status }; });
        var byId = {};
        items.forEach(function (it) { byId[it.id] = it; });
        var radios = Array.prototype.slice.call(levelsGroup.querySelectorAll('[role="radio"]'));

        var state = core.parseFilterQuery(window.location.search, config.topics);
        if (!config.isAuthenticated) { state.status = 'all'; }
        // a level without lessons is a disabled card: never start on it
        var startRadio = radios.filter(function (r) { return r.getAttribute('data-level') === state.level; })[0];
        if (state.level !== 'all' && (!startRadio || startRadio.disabled)) { state.level = 'all'; }
        var shown = {};

        function resetShown() { sections.forEach(function (s) { shown[s.level] = gridSize; }); }
        resetShown();

        function syncRadios() {
            radios.forEach(function (radio) {
                var on = radio.getAttribute('data-level') === state.level;
                radio.setAttribute('aria-checked', on ? 'true' : 'false');
                radio.tabIndex = on ? 0 : -1;
            });
        }

        function syncPressed(group, attr, value) {
            if (!group) { return; }
            Array.prototype.forEach.call(group.querySelectorAll('button[' + attr + ']'), function (btn) {
                btn.setAttribute('aria-pressed', btn.getAttribute(attr) === value ? 'true' : 'false');
            });
        }

        function render(reveal) {
            var matching = {};
            var matchIds = core.filterLessons(lessons, state);
            matchIds.forEach(function (id) {
                var it = byId[id];
                (matching[it.level] = matching[it.level] || []).push(id);
            });

            var shelves = state.level === 'all';
            var focusTarget = null;
            sections.forEach(function (s) {
                var ids = matching[s.level] || [];
                var inScope = shelves || s.level === state.level;
                var limit = shelves ? shelfSize : shown[s.level];
                var slice = core.paginate(ids, limit);
                var visible = {};
                slice.visible.forEach(function (id) { visible[id] = true; });
                items.forEach(function (it) {
                    if (it.level === s.level) { it.el.hidden = !(inScope && visible[it.id]); }
                });
                s.el.hidden = !inScope || ids.length === 0;
                if (s.count && !config.isAuthenticated) { s.count.textContent = ids.length + ' bài'; }
                if (s.all) {
                    s.all.hidden = !(shelves && ids.length > shelfSize);
                    s.all.textContent = 'Xem cả ' + ids.length + ' bài';
                }
                if (s.more) { s.more.hidden = !(!shelves && inScope && slice.hasMore); }
                if (reveal && reveal.level === s.level && ids.length > reveal.index) {
                    var first = byId[ids[reveal.index]];
                    focusTarget = first && first.el.querySelector('a');
                }
            });

            countEl.textContent = matchIds.length + ' bài nghe';
            if (emptyEl) { emptyEl.hidden = matchIds.length > 0; }

            syncRadios();
            syncPressed(statusGroup, 'data-status', state.status);
            if (topicSelect) { topicSelect.value = state.topic; }

            var query = core.buildFilterQuery(state);
            try { window.history.replaceState(null, '', window.location.pathname + query); } catch (e) { /* ignore */ }
            if (focusTarget) { focusTarget.focus(); }
        }

        function change(patch) {
            Object.keys(patch).forEach(function (k) { state[k] = patch[k]; });
            resetShown();
            render();
        }

        levelsGroup.addEventListener('click', function (event) {
            var radio = event.target.closest('[role="radio"]');
            if (radio && !radio.disabled) { change({ level: radio.getAttribute('data-level') }); }
        });

        // radiogroup: arrows move the selection to the next enabled card, Home/End jump to the ends
        levelsGroup.addEventListener('keydown', function (event) {
            var enabled = radios.filter(function (r) { return !r.disabled; });
            var current = enabled.indexOf(document.activeElement);
            if (current < 0) { return; }
            var next = -1;
            if (event.key === 'ArrowRight' || event.key === 'ArrowDown') { next = (current + 1) % enabled.length; }
            else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') { next = (current - 1 + enabled.length) % enabled.length; }
            else if (event.key === 'Home') { next = 0; }
            else if (event.key === 'End') { next = enabled.length - 1; }
            if (next < 0) { return; }
            event.preventDefault();
            change({ level: enabled[next].getAttribute('data-level') });
            enabled[next].focus();
        });

        if (statusGroup) {
            statusGroup.addEventListener('click', function (event) {
                var btn = event.target.closest('button[data-status]');
                if (btn) { change({ status: btn.getAttribute('data-status') }); }
            });
        }
        if (topicSelect) {
            topicSelect.addEventListener('change', function () { change({ topic: topicSelect.value }); });
        }
        if (searchInput) {
            searchInput.value = state.q;
            searchInput.addEventListener('input', Sets.debounce(function () { change({ q: searchInput.value.trim() }); }, 150));
        }
        sections.forEach(function (s) {
            if (s.all) {
                s.all.addEventListener('click', function () {
                    change({ level: s.level });
                    if (s.heading) {
                        s.heading.tabIndex = -1;
                        s.heading.focus();
                        s.heading.scrollIntoView({ block: 'start', behavior: 'auto' });
                    }
                });
            }
            if (s.more) {
                s.more.addEventListener('click', function () {
                    var from = shown[s.level];
                    shown[s.level] = from + gridSize;
                    render({ level: s.level, index: from });
                });
            }
        });
        if (clearBtn) {
            clearBtn.addEventListener('click', function () {
                state = { level: 'all', topic: 'all', status: 'all', q: '' };
                if (searchInput) { searchInput.value = ''; searchInput.focus(); }
                resetShown();
                render();
            });
        }

        render();
    }

    // ------------------------------------------------------------------ my lessons: import and delete

    var importForm = document.getElementById('tlImport');
    var mineGrid = document.getElementById('tlMineGrid');
    var mineSection = document.getElementById('tlMine');
    var deleteModalEl = document.getElementById('tlDeleteLesson');
    if (importForm) { initImport(); }
    if (deleteModalEl) { initDelete(); }

    // "Xem tất cả" in the YouTube card: scroll to the "Bài nghe của tôi" shelf and focus its heading
    var jumpBtn = document.getElementById('tlMineJump');
    if (jumpBtn) {
        jumpBtn.addEventListener('click', function () {
            var heading = document.getElementById('tl-mine-title');
            if (!heading || !mineSection || mineSection.hidden) { return; }
            heading.focus();
            heading.scrollIntoView({ block: 'start', behavior: 'auto' });
        });
    }

    /** Keeps the count in the YouTube card, the shelf count, the shelf itself and the "Xem tất cả" button in step with the grid. */
    function syncMine() {
        var left = mineGrid ? mineGrid.querySelectorAll('[data-mine-id]').length : 0;
        var cardCount = document.getElementById('tlMineCount');
        var shelfCount = document.getElementById('tlMineShelfCount');
        var jump = document.getElementById('tlMineJumpWrap');
        if (cardCount) { cardCount.textContent = String(left); }
        if (shelfCount) { shelfCount.textContent = left + ' bài'; }
        if (mineSection) { mineSection.hidden = left === 0; }
        if (jump) { jump.hidden = left === 0; }
        return left;
    }

    function initImport() {
        var urlInput = document.getElementById('tlImportUrl');
        var submit = document.getElementById('tlImportSubmit');
        var errorEl = document.getElementById('tlImportError');
        var statusEl = document.getElementById('tlImportStatus');
        var busy = false;

        function setError(text) {
            errorEl.textContent = text || '';
            errorEl.hidden = !text;
            if (text) { urlInput.setAttribute('aria-invalid', 'true'); } else { urlInput.removeAttribute('aria-invalid'); }
        }

        function setStatus(nodes) {
            statusEl.textContent = '';
            (nodes || []).forEach(function (n) { statusEl.appendChild(n); });
        }

        function setBusy(on) {
            busy = on;
            urlInput.disabled = on;
            submit.disabled = on;
            if (on) { submit.setAttribute('aria-busy', 'true'); } else { submit.removeAttribute('aria-busy'); }
        }

        function failure(message, upgrade) {
            setBusy(false);
            var nodes = [document.createTextNode(message)];
            if (upgrade) {
                nodes.push(document.createTextNode(' '));
                var link = Sets.el('a', null, 'Nâng cấp Premium');
                link.href = (config.urls && config.urls.premium) || '/Account/Premium';
                nodes.push(link);
            }
            setStatus(nodes);
        }

        importForm.addEventListener('submit', function (event) {
            event.preventDefault();
            if (busy) { return; }
            setError('');
            setStatus([]);

            var value = urlInput.value.trim();
            if (!core.isYoutubeUrl(value)) {
                setError('Link YouTube không hợp lệ.');
                urlInput.focus();
                return;
            }

            setBusy(true);
            var spinner = Sets.el('span', 'spinner-border spinner-border-sm me-2');
            spinner.setAttribute('role', 'presentation');
            setStatus([spinner, document.createTextNode('Đang lấy phụ đề và tạo bài nghe… Việc này có thể mất đến một phút.')]);

            var form = new FormData();
            form.append('youtubeUrl', value);
            Sets.fetchJson(config.urls.create, { method: 'POST', body: form }).then(function (data) {
                var id = data && Number(data.lessonId);
                if (!id) { failure('Không thêm được bài nghe. Hãy kiểm tra video có phụ đề tiếng Anh rồi thử lại.'); return; }
                setStatus([document.createTextNode('Đã thêm bài nghe.')]);
                window.location.assign(config.urls.practice + id);
            }).catch(function (error) {
                var data = error && error.data;
                var message = data && typeof data.message === 'string' && data.message
                    ? data.message
                    : 'Không thêm được bài nghe. Hãy kiểm tra video có phụ đề tiếng Anh rồi thử lại.';
                failure(message, !!(error && error.status === 403 && data && data.requiresUpgrade));
            });
        });

        urlInput.addEventListener('input', function () { if (errorEl.textContent) { setError(''); } });
    }

    function initDelete() {
        var bodyEl = document.getElementById('tlDeleteBody');
        var confirmBtn = document.getElementById('tlDeleteConfirm');
        var current = null;
        var modal = null;

        function getModal() {
            if (!modal && window.bootstrap && window.bootstrap.Modal) { modal = window.bootstrap.Modal.getOrCreateInstance(deleteModalEl); }
            return modal;
        }

        function afterHidden(fn) { deleteModalEl.addEventListener('hidden.bs.modal', fn, { once: true }); }

        // Bootstrap only restores focus for data-api triggers; this modal is opened from code.
        deleteModalEl.addEventListener('hidden.bs.modal', function () {
            if (current && current.trigger && document.body.contains(current.trigger)) { current.trigger.focus(); }
        });

        document.addEventListener('click', function (event) {
            var trigger = event.target.closest('[data-tl-delete]');
            if (!trigger) { return; }
            var m = getModal();
            if (!m) { Sets.toast('Không mở được hộp thoại. Hãy tải lại trang.', { type: 'danger' }); return; }
            current = { id: trigger.getAttribute('data-tl-delete'), title: trigger.getAttribute('data-title') || '', trigger: trigger };
            bodyEl.textContent = 'Bài “' + current.title + '” và tiến độ học của bài sẽ bị xóa vĩnh viễn.';
            m.show(trigger);
        });

        function removeCard(id) {
            var item = mineGrid && mineGrid.querySelector('[data-mine-id="' + id + '"]');
            if (item) { item.remove(); }
            return syncMine();
        }

        confirmBtn.addEventListener('click', function () {
            if (!current || confirmBtn.disabled) { return; }
            var target = current;
            Sets.pending(confirmBtn, true, 'Đang xóa…');
            var form = new FormData();
            form.append('lessonId', target.id);
            Sets.fetchJson(config.urls.delete, { method: 'POST', body: form }).then(function () {
                Sets.pending(confirmBtn, false);
                afterHidden(function () {
                    var left = removeCard(target.id);
                    Sets.toast('Đã xóa bài nghe.');
                    // focus stays useful: the shelf heading while there are lessons left, otherwise the import field
                    var next = left > 0 ? document.getElementById('tl-mine-title') : document.getElementById('tlImportUrl');
                    if (next) { next.focus(); }
                });
                getModal().hide();
            }).catch(function () {
                Sets.pending(confirmBtn, false);
                afterHidden(function () { Sets.toast('Không xóa được bài nghe. Vui lòng thử lại.', { type: 'danger' }); });
                getModal().hide();
            });
        });
    }
}());
