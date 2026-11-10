/*
 * Practice area: shared DOM parts and the session shell (window.TctPractice.ui).
 * Requires personal-sets-common.js (window.TctSets) and practice-core.js. Renders with createElement/textContent only.
 * Mode files register themselves in window.TctPractice.modes[mode] = function (session) { ... }.
 */
(function () {
    'use strict';

    var T = window.TctSets;
    var P = window.TctPractice = window.TctPractice || {};
    var core = P.core;
    var ui = P.ui = {};
    P.modes = P.modes || {};

    var el = ui.el = T.el;
    var icon = ui.icon = T.icon;

    ui.byId = function (id) { return document.getElementById(id); };

    ui.readConfig = function () {
        try { return JSON.parse(ui.byId('tpConfig').textContent); } catch (e) { return null; }
    };

    function safeStorage(name) {
        try { return window[name]; } catch (e) { return null; }
    }

    function visuallyHidden(text) { return el('span', 'visually-hidden', text); }

    // ------------------------------------------------------------------ state message (client twin of _StateMessage)

    ui.stateMessage = function (options) {
        var kind = options.kind || 'empty';
        var root = el('div', 'tct-state tct-state--' + kind);
        root.setAttribute('role', kind === 'error' ? 'alert' : 'status');
        var iconWrap = el('span', 'tct-state__icon');
        iconWrap.setAttribute('aria-hidden', 'true');
        iconWrap.appendChild(document.createElement('i')).className = kind === 'error' ? 'fa-solid fa-triangle-exclamation' : 'fa-solid fa-inbox';
        root.appendChild(iconWrap);
        root.appendChild(el('p', 'tct-state__title', options.title));
        if (options.message) { root.appendChild(el('p', 'tct-state__message', options.message)); }
        if (options.actionLabel && options.actionUrl) {
            var link = el('a', 'btn btn-primary', options.actionLabel);
            link.href = options.actionUrl;
            root.appendChild(link);
        }
        return root;
    };

    // ------------------------------------------------------------------ keys

    var keyHandlers = [];
    var keysBound = false;

    function isTypingTarget(target) {
        if (!target || !target.tagName) { return false; }
        var tag = target.tagName;
        return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable === true;
    }

    /** handler(event, key) returns true when it handled the key. Ignored while typing and with Ctrl/Meta/Alt held. */
    ui.onKeys = function (handler) {
        keyHandlers.push(handler);
        if (keysBound) { return; }
        keysBound = true;
        document.addEventListener('keydown', function (event) {
            if (event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented) { return; }
            if (isTypingTarget(event.target)) { return; }
            if (document.querySelector('.modal.show')) { return; }
            for (var i = keyHandlers.length - 1; i >= 0; i--) {
                if (keyHandlers[i](event, event.key)) { event.preventDefault(); return; }
            }
        });
    };

    // ------------------------------------------------------------------ small parts

    /** Speaks English text; rate < 1 is slower. Best-effort (no speech support = no-op). */
    ui.speak = function (text, rate) {
        if (!T.canSpeak() || !text) { return; }
        try {
            window.speechSynthesis.cancel();
            var utterance = new window.SpeechSynthesisUtterance(text);
            utterance.lang = 'en-US';
            if (rate) { utterance.rate = rate; }
            window.speechSynthesis.speak(utterance);
        } catch (e) { /* speech is best-effort */ }
    };

    ui.speakButton = function (text, term) {
        var button = el('button', 'btn k-btn--soft k-btn--icon tp-speak tp-hue-primary');
        button.type = 'button';
        button.setAttribute('aria-label', 'Phát âm ' + (term || text));
        button.appendChild(icon('fa-solid fa-volume-high'));
        if (!T.canSpeak()) { button.hidden = true; }
        button.addEventListener('click', function () { T.speak(text); });
        return button;
    };

    ui.visual = function (word) {
        if (!word.imageUrl && !word.emoji) { return null; }
        var box = el('span', 'tp-visual');
        box.setAttribute('aria-hidden', 'true');
        if (word.imageUrl) {
            var img = document.createElement('img');
            img.src = word.imageUrl;
            img.alt = '';
            img.loading = 'lazy';
            box.appendChild(img);
        } else {
            box.textContent = word.emoji;
        }
        return box;
    };

    /** Term, IPA and a speak button. `visual` false hides the image/emoji. */
    ui.termLine = function (word, options) {
        var opts = options || {};
        var line = el('div', 'tp-term-line');
        if (opts.visual !== false) {
            var v = ui.visual(word);
            if (v) { line.appendChild(v); }
        }
        var term = el('p', 'tp-term k-display tct-break', word.term);
        term.lang = 'en';
        line.appendChild(term);
        if (word.phonetic) { line.appendChild(el('span', 'tct-phonetic', word.phonetic)); }
        line.appendChild(ui.speakButton(word.term));
        return line;
    };

    ui.meaningLine = function (word) {
        var box = el('div', 'tp-meaning-box');
        box.appendChild(el('p', 'tp-meaning tct-break', word.definition));
        if (word.meaningEn) { box.appendChild(el('p', 'tp-meaning-en', word.meaningEn)); }
        return box;
    };

    /** Key hints as chips: each item is "<key> <label>" (the first word is the key). */
    ui.keyHints = function (items) {
        var row = el('p', 'tp-keyhint');
        items.forEach(function (item) {
            var cut = item.indexOf(' ');
            var part = el('span', 'tp-keyhint__item');
            part.appendChild(el('kbd', 'k-kbd', cut < 0 ? item : item.slice(0, cut)));
            if (cut > 0) { part.appendChild(el('span', null, item.slice(cut + 1))); }
            row.appendChild(part);
        });
        return row;
    };

    // ------------------------------------------------------------------ options

    /**
     * Multiple-choice buttons. Returns { root, buttons, reveal(pickedId), disable() }.
     * onPick(optionId) is called once; `reveal` marks the right and wrong options and locks the group.
     */
    ui.options = function (question, handlers) {
        var root = el('div', 'tp-options');
        root.setAttribute('role', 'group');
        root.setAttribute('aria-label', 'Các đáp án');
        var buttons = [];
        question.options.forEach(function (option, index) {
            var button = el('button', 'tp-option');
            button.type = 'button';
            button.dataset.optionId = option.id;
            button.appendChild(el('span', 'tp-option__key tp-option__key--number', String(index + 1)));
            button.appendChild(el('span', 'tp-option__key tp-option__key--letter', String.fromCharCode(65 + index)));
            var label = el('span', 'tp-option__label tct-clamp-2', option.label);
            label.title = option.label;
            button.appendChild(label);
            button.appendChild(el('span', 'tp-option__mark'));
            button.addEventListener('click', function () { if (handlers && handlers.onPick) { handlers.onPick(option.id); } });
            buttons.push(button);
            root.appendChild(button);
        });

        function disable() { buttons.forEach(function (b) { b.disabled = true; }); }

        function reveal(pickedId) {
            disable();
            root.classList.add('tp-options--answered');
            buttons.forEach(function (button) {
                var id = button.dataset.optionId;
                var mark = button.querySelector('.tp-option__mark');
                if (id === question.answerId) {
                    button.classList.add('tp-option--correct');
                    mark.appendChild(icon('fa-solid fa-circle-check'));
                    mark.appendChild(visuallyHidden('Đáp án đúng'));
                } else if (id === pickedId) {
                    button.classList.add('tp-option--wrong');
                    mark.appendChild(icon('fa-solid fa-circle-xmark'));
                    mark.appendChild(visuallyHidden('Bạn chọn'));
                }
            });
        }

        return { root: root, buttons: buttons, reveal: reveal, disable: disable };
    };

    // ------------------------------------------------------------------ feedback panel

    var OUTCOME = {
        correct: { title: 'Đúng rồi!', icon: 'fa-solid fa-circle-check' },
        close: { title: 'Gần đúng!', icon: 'fa-solid fa-circle-check' },
        wrong: { title: 'Chưa đúng', icon: 'fa-solid fa-circle-xmark' }
    };

    /** Sticky panel with the full answer card and a focused "Tiếp tục". Returns { next() } to continue from the keyboard. */
    ui.feedbackPanel = function (session, outcome, word, options) {
        var opts = options || {};
        var info = OUTCOME[outcome] || OUTCOME.wrong;
        var panel = el('div', 'tp-feedback tp-feedback--' + outcome + ' k-anim-rise');
        var head = el('div', 'tp-feedback__head');
        var title = el('p', 'tp-feedback__title');
        title.appendChild(icon(info.icon));
        title.appendChild(document.createTextNode(info.title));
        head.appendChild(title);
        if (word) { head.appendChild(ui.speakButton(word.term)); }
        panel.appendChild(head);

        if (word) {
            var answer = el('div', 'tp-feedback__answer');
            var term = el('span', 'tp-feedback__term tct-break', word.term);
            term.lang = 'en';
            answer.appendChild(term);
            if (word.phonetic) { answer.appendChild(el('span', 'tct-phonetic', word.phonetic)); }
            panel.appendChild(answer);
            panel.appendChild(el('p', 'tp-feedback__def m-0', word.definition));
        }
        if (opts.extra) { panel.appendChild(opts.extra); }

        var actions = el('div', 'tp-feedback__actions');
        var tone = outcome === 'correct' ? 'k-btn--success' : outcome === 'close' ? 'k-btn--warning' : 'k-btn--danger';
        var next = el('button', 'btn btn-lg w-100 tp-feedback__next ' + tone);
        next.type = 'button';
        next.appendChild(document.createTextNode(opts.last ? 'Xem kết quả' : 'Tiếp tục'));
        var enter = el('kbd', 'k-kbd', 'Enter');
        enter.setAttribute('aria-hidden', 'true');
        next.appendChild(enter);
        var used = false;
        function go() {
            if (used) { return; }
            used = true;
            session.clearFeedback();
            if (opts.onNext) { opts.onNext(); }
        }
        next.addEventListener('click', go);
        actions.appendChild(next);
        panel.appendChild(actions);

        session.feedbackHost.textContent = '';
        session.feedbackHost.appendChild(panel);
        session.pendingNext = go;
        next.focus({ preventScroll: false });
        return { next: go };
    };

    // ------------------------------------------------------------------ typing

    /**
     * Text input with "Kiểm tra", "Không nhớ" and (optionally) a hint button revealing one more letter per press.
     * onSubmit(text) / onSkip() fire once; call lock() afterwards.
     */
    ui.typingInput = function (word, handlers) {
        var opts = handlers || {};
        var root = el('div', 'tp-typing');
        var id = 'tpTyping' + word.id;
        var label = el('label', 'visually-hidden', 'Gõ từ tiếng Anh');
        label.htmlFor = id;
        var input = el('input', 'form-control tp-typing__input');
        input.id = id;
        input.type = 'text';
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('autocapitalize', 'none');
        input.setAttribute('autocorrect', 'off');
        input.setAttribute('spellcheck', 'false');
        input.lang = 'en';
        root.appendChild(label);
        root.appendChild(input);

        var hintLine = null;
        var hintMask = null;
        var shown = 0;
        var hintButton = null;
        if (opts.hints) {
            hintLine = el('p', 'tp-hint-line');
            hintLine.setAttribute('aria-live', 'polite');
            hintLine.appendChild(visuallyHidden('Gợi ý: '));
            hintMask = el('span');
            hintLine.appendChild(hintMask);
            root.appendChild(hintLine);
        }

        var actions = el('div', 'tp-typing__actions');
        var check = el('button', 'btn btn-primary btn-lg', 'Kiểm tra');
        check.type = 'button';
        var skip = el('button', 'btn btn-outline-primary btn-lg', 'Không nhớ');
        skip.type = 'button';
        actions.appendChild(check);
        actions.appendChild(skip);
        if (opts.hints) {
            hintButton = el('button', 'btn btn-lg k-btn--soft tp-hue-accent');
            hintButton.type = 'button';
            hintButton.appendChild(icon('fa-solid fa-lightbulb me-1'));
            var hintText = document.createTextNode('Gợi ý');
            hintButton.appendChild(hintText);
            hintButton.addEventListener('click', function () {
                var max = core.maxHints(word.term);
                if (shown >= max) { return; }
                shown += 1;
                hintMask.textContent = core.hintMask(word.term, shown);
                hintText.nodeValue = 'Gợi ý thêm';
                if (shown >= max) { hintButton.disabled = true; }
                input.focus();
            });
            actions.appendChild(hintButton);
        }
        root.appendChild(actions);

        var done = false;
        function lock() {
            done = true;
            input.disabled = true;
            check.disabled = true;
            skip.disabled = true;
            if (hintButton) { hintButton.disabled = true; }
        }
        function submit() {
            if (done) { return; }
            if (!input.value.trim()) { input.focus(); return; }
            if (opts.onSubmit) { opts.onSubmit(input.value); }
        }
        check.addEventListener('click', submit);
        skip.addEventListener('click', function () { if (!done && opts.onSkip) { opts.onSkip(); } });
        input.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.isComposing) { event.preventDefault(); submit(); }
        });

        return { root: root, input: input, lock: lock, focus: function () { input.focus(); } };
    };

    /** "Bạn gõ" / "Đáp án" letter diff. Marks differences with underline and highlight plus the row labels. */
    ui.diff = function (typed, answer) {
        var d = core.diffChars(typed, answer);
        var root = el('div', 'tp-diff');
        function row(label, chars, badClass) {
            var line = el('div', 'tp-diff__row');
            line.appendChild(el('span', 'tp-diff__label', label));
            var text = el('span', 'tp-diff__chars');
            chars.forEach(function (c) {
                var span = el('span', c.ok ? null : badClass, c.ch);
                text.appendChild(span);
            });
            line.appendChild(text);
            return line;
        }
        root.appendChild(row('Bạn gõ', d.typed, 'tp-diff__ch--bad'));
        root.appendChild(row('Đáp án', d.answer, 'tp-diff__ch--missing'));
        return root;
    };

    // ------------------------------------------------------------------ summary

    function learnLink(config, ids) {
        var parts = ['words=' + ids.join(',')];
        if (config.setId) { parts.unshift('set=' + config.setId); }
        if (config.query && config.query.from) { parts.push('from=' + encodeURIComponent(config.query.from)); }
        return '/Practice/learn?' + parts.join('&');
    }

    ui.learnLink = learnLink;

    /**
     * Result screen. results: [{ word, result: 'correct' | 'close' | 'wrong' }]. "close" counts as correct.
     * options: { durationMs, extra: Node[] }.
     */
    /** Percent ring: green from 70%, amber from 50%, red below. `label` is the small text under the number. */
    ui.ring = function (percent, label) {
        var tone = percent >= 70 ? 'ok' : percent >= 50 ? 'mid' : 'low';
        var ring = el('div', 'tp-ring tp-ring--' + tone);
        ring.style.setProperty('--p', String(percent));
        ring.setAttribute('role', 'img');
        ring.setAttribute('aria-label', percent + '% ' + (label || 'trả lời đúng'));
        var inner = el('span', 'tp-ring__inner');
        inner.appendChild(el('span', 'tp-ring__value', percent + '%'));
        inner.appendChild(el('span', 'tp-ring__label', label || 'trả lời đúng'));
        ring.appendChild(inner);
        return ring;
    };

    ui.summary = function (session, results, options) {
        var opts = options || {};
        var config = session.config;
        session.finish();
        session.clearFeedback();
        var total = results.length;
        var right = results.filter(function (r) { return r.result !== 'wrong'; }).length;
        var percent = total ? Math.round(100 * right / total) : 0;
        var stars = core.starsFor(right, total);
        var missed = [];
        var seen = {};
        results.forEach(function (r) {
            if (r.result === 'wrong' && !seen[r.word.id]) { seen[r.word.id] = true; missed.push(r.word); }
        });

        var root = el('section', 'tp-summary');
        root.setAttribute('aria-label', 'Kết quả');
        var head = el('div', 'tp-summary__head');
        if (total) { head.appendChild(ui.ring(percent)); }
        var starBox = el('div', 'tp-stars tp-pop');
        starBox.setAttribute('role', 'img');
        starBox.setAttribute('aria-label', stars + ' sao');
        for (var i = 1; i <= 3; i++) {
            starBox.appendChild(icon(i <= stars ? 'fa-solid fa-star' : 'fa-solid fa-star tp-star--off'));
        }
        head.appendChild(starBox);
        head.appendChild(el('h2', 'tp-summary__title', total ? 'Xong phiên rồi!' : 'Chưa có câu nào'));
        root.appendChild(head);

        var kpis = el('div', 'tp-kpis');
        [['Câu hỏi', String(total), 'primary'], ['Đúng', right + '/' + total, 'success'], ['Thời gian', core.formatDuration(opts.durationMs != null ? opts.durationMs : session.elapsed()), 'info']]
            .forEach(function (item) {
                var card = el('div', 'tp-kpi k-soft tp-hue-' + item[2]);
                card.appendChild(el('span', 'tp-kpi__value', item[1]));
                card.appendChild(el('span', 'tp-kpi__label', item[0]));
                kpis.appendChild(card);
            });
        root.appendChild(kpis);
        if (opts.extra) { opts.extra.forEach(function (node) { root.appendChild(node); }); }

        if (missed.length) {
            var section = el('section', 'tp-missed k-panel');
            var missedTitle = el('h3', 'tp-missed__title');
            missedTitle.appendChild(icon('fa-solid fa-circle-xmark'));
            missedTitle.appendChild(document.createTextNode('Từ làm sai'));
            section.appendChild(missedTitle);
            var list = el('ul', 'tp-missed__list');
            missed.forEach(function (w) {
                var item = el('li', 'tp-missed__item');
                var text = el('div', 'tp-missed__text');
                var link = el('a', 'tp-missed__term', w.term);
                link.href = '/Sets/Cards/' + w.id;
                link.lang = 'en';
                text.appendChild(link);
                if (w.phonetic) { text.appendChild(el('span', 'tct-phonetic tp-missed__ipa', w.phonetic)); }
                text.appendChild(el('span', 'tp-missed__def', w.definition));
                item.appendChild(text);
                item.appendChild(ui.speakButton(w.term));
                list.appendChild(item);
            });
            section.appendChild(list);
            root.appendChild(section);
        }

        var actions = el('div', 'tp-summary__actions');
        function add(label, href, cls) {
            var a = el('a', 'btn btn-lg ' + (cls || 'btn-outline-primary'), label);
            a.href = href;
            actions.appendChild(a);
        }
        add('Luyện tiếp', config.againUrl, 'btn-primary');
        if (missed.length) { add('Học các từ sai', learnLink(config, missed.slice(0, 50).map(function (w) { return w.id; }))); }
        root.appendChild(actions);

        var links = el('div', 'tp-summary__links');
        [['Từ hay quên', '/Practice/HardWords'], ['Xong', config.exitUrl]].forEach(function (pair) {
            var a = el('a', 'tp-summary__link', pair[0]);
            a.href = pair[1];
            links.appendChild(a);
        });
        root.appendChild(links);

        session.setCounter('');
        session.render(root);
        var heading = root.querySelector('.tp-summary__title');
        if (heading) { heading.tabIndex = -1; heading.focus(); }
        return { missed: missed, percent: percent, right: right, total: total };
    };

    // ------------------------------------------------------------------ progress writer

    function createWriter(config) {
        var warned = false;
        function post(cardId, isKnown, retry) {
            return T.fetchJson('/Home/UpdateCardProgress', { method: 'POST', body: { cardId: cardId, isKnown: isKnown } }).catch(function (error) {
                if (retry && !error.status) { return post(cardId, isKnown, false); }   // network failure: one retry
                if (!warned) {
                    warned = true;
                    T.toast('Không lưu được tiến độ ôn tập. Kết quả luyện tập vẫn được giữ.', { type: 'danger' });
                }
                return null;
            });
        }
        return {
            enabled: !!config.countsToSrs,
            /** Fire-and-forget; only when "Tính vào ôn tập" is on. Never blocks the session. */
            write: function (cardId, isKnown) {
                if (!config.countsToSrs) { return Promise.resolve(null); }
                return post(cardId, !!isKnown, true);
            }
        };
    }

    // ------------------------------------------------------------------ session shell

    ui.createSession = function (config) {
        var stage = ui.byId('tpStage');
        var progress = ui.byId('tpProgress');
        var fill = ui.byId('tpProgressFill');
        var counter = ui.byId('tpCounter');
        var slot = ui.byId('tpSlot');
        var session = {
            config: config,
            stage: stage,
            slot: slot,
            feedbackHost: ui.byId('tpFeedback'),
            startedAt: Date.now(),
            answered: 0,
            finished: false,
            pendingNext: null,
            store: core.createStore({ local: safeStorage('localStorage'), session: safeStorage('sessionStorage') }),
            writer: createWriter(config),
            toast: T.toast
        };

        session.setProgress = function (done, total) {
            var pct = total ? Math.round(100 * done / total) : 0;
            fill.style.setProperty('--p', String(pct));
            progress.setAttribute('aria-valuemax', String(total));
            progress.setAttribute('aria-valuenow', String(done));
            counter.textContent = done + '/' + total;
        };
        session.setCounter = function (text) { counter.textContent = text; };
        session.setSlot = function (nodes) {
            slot.textContent = '';
            (nodes || []).forEach(function (n) { slot.appendChild(n); });
        };
        session.noteAnswer = function () { session.answered += 1; };
        session.finish = function () { session.finished = true; };
        session.elapsed = function () { return Date.now() - session.startedAt; };
        session.clearFeedback = function () { session.feedbackHost.textContent = ''; session.pendingNext = null; };
        session.render = function (node, animate) {
            stage.textContent = '';
            session.clearFeedback();
            if (animate) { node.classList.add('tp-slide-in'); }
            stage.appendChild(node);
        };
        session.fail = function (message) {
            session.render(ui.stateMessage({ kind: 'error', title: 'Không mở được chế độ này.', message: message || 'Hãy tải lại trang hoặc chọn chế độ khác.', actionLabel: 'Về Luyện tập', actionUrl: config.hubUrl }));
        };

        // Exit: ask first when answers would be lost (Learn saves its own state and never asks).
        var exit = ui.byId('tpExit');
        var modalEl = ui.byId('tpExitModal');
        exit.addEventListener('click', function (event) {
            var dirty = session.answered > 0 && !session.finished && config.mode !== 'learn';
            if (dirty && modalEl && window.bootstrap && window.bootstrap.Modal) {
                event.preventDefault();
                window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
            }
        });
        if (modalEl) {
            modalEl.addEventListener('hidden.bs.modal', function () { exit.focus(); });
        }

        // Enter continues from the feedback panel when focus is not already on a button.
        ui.onKeys(function (event, key) {
            if (key === 'Enter' && session.pendingNext && !(event.target && /^(BUTTON|A|SUMMARY)$/.test(event.target.tagName))) {
                session.pendingNext();
                return true;
            }
            return false;
        });

        return session;
    };

    ui.start = function () {
        var config = ui.readConfig();
        if (!config || config.empty) { return; }
        var session = ui.createSession(config);
        var run = P.modes[config.mode];
        if (typeof run !== 'function') { session.fail('Chế độ này chưa sẵn sàng.'); return; }
        try {
            run(session);
        } catch (error) {
            if (window.console && console.error) { console.error('practice: mode failed', error); }
            session.fail();
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', ui.start);
    } else {
        ui.start();
    }
}());
