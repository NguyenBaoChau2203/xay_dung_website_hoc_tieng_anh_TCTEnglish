/* TCT English landing page: progressive enhancement only.
   The page is complete without this script; it adds the phone menu and the "hoc thu" demo. */
(function () {
    'use strict';

    // Small DOM builder: text and element nodes only, never an HTML string.
    function h(tag, cls, text, kids) {
        var n = document.createElement(tag);
        if (cls) { n.className = cls; }
        if (text !== undefined && text !== null) { n.textContent = text; }
        (kids || []).forEach(function (k) { n.appendChild(k); });
        return n;
    }

    function ic(cls) {
        var i = h('i', cls);
        i.setAttribute('aria-hidden', 'true');
        return i;
    }

    // ------------------------------------------------------------------ phone menu
    function initNav() {
        var toggle = document.querySelector('[data-lp-nav-toggle]');
        var panel = document.getElementById('lp-nav-links');
        if (!toggle || !panel) { return; }

        function setOpen(open, restoreFocus) {
            panel.classList.toggle('is-open', open);
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            toggle.setAttribute('aria-label', open ? 'Đóng menu' : 'Mở menu');
            if (!open && restoreFocus) { toggle.focus(); }
        }

        toggle.addEventListener('click', function () { setOpen(!panel.classList.contains('is-open'), false); });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && panel.classList.contains('is-open')) { setOpen(false, true); }
        });
        // Following an in-page link closes the panel; the browser then scrolls to the section natively.
        panel.addEventListener('click', function (e) {
            if (e.target.closest && e.target.closest('a') && panel.classList.contains('is-open')) { setOpen(false, false); }
        });
    }

    // ------------------------------------------------------------------ "hoc thu" demo (client only: no network, cookies or storage)
    function initDemo() {
        var root = document.querySelector('[data-lp-demo]');
        var core = window.TctLanding;
        if (!root || !core) { return; }

        var q = function (name) { return root.querySelector('[data-lp-demo-' + name + ']'); };
        var playBox = q('play'), resultBox = q('result'), progress = q('progress'), prompt = q('prompt');
        var word = q('word'), optionList = q('options'), feedback = q('feedback');
        var ctaHref = root.getAttribute('data-lp-cta-href') || '/Account/Register';
        var ctaLabel = root.getAttribute('data-lp-cta-label') || 'Tạo tài khoản miễn phí';
        var note = root.getAttribute('data-lp-result-note') || '';

        var seed = window.crypto && window.crypto.getRandomValues ? window.crypto.getRandomValues(new Uint32Array(1))[0] : Date.now() % 2147483647;
        var state = core.createDemo(core.QUESTIONS, seed);
        var buttons = [];

        root.classList.add('is-live');

        function focusFirst() { if (buttons[0]) { buttons[0].focus(); } }

        function renderQuestion() {
            var c = core.current(state);
            resultBox.hidden = true;
            playBox.hidden = false;
            progress.textContent = 'Học thử · câu ' + c.number + '/' + c.total;
            prompt.textContent = c.prompt;
            word.textContent = c.word;
            feedback.textContent = '';
            feedback.className = 'lp-quiz-feedback';
            optionList.textContent = '';
            buttons = c.options.map(function (o, i) {
                var b = h('button', 'lp-option', null, [h('kbd', '', String(i + 1)), h('span', 'lp-option-label', o.label)]);
                b.type = 'button';
                b.setAttribute('data-option-id', o.id);
                b.addEventListener('click', function () { onAnswer(o.id); });
                optionList.appendChild(h('li', '', null, [b]));
                return b;
            });
        }

        // The right option and the chosen wrong one get an icon and hidden text, so colour is never the only cue.
        function markOptions(r) {
            buttons.forEach(function (b) {
                var id = b.getAttribute('data-option-id');
                var right = id === r.correctId;
                b.setAttribute('aria-disabled', 'true');
                if (!right && id !== r.chosenId) { return; }
                b.classList.add(right ? 'is-correct' : 'is-wrong');
                b.appendChild(ic('fa-solid lp-option-mark ' + (right ? 'fa-circle-check' : 'fa-circle-xmark')));
                b.appendChild(h('span', 'lp-visually-hidden', right ? ' (đáp án đúng)' : ' (bạn đã chọn, chưa đúng)'));
            });
        }

        function showFeedback(r) {
            var line = h('div', 'lp-quiz-feedback-line', null, [ic(r.correct ? 'fa-solid fa-circle-check' : 'fa-solid fa-circle-xmark'), h('strong', '', r.label)]);
            if (r.detail) { line.appendChild(h('span', 'lp-quiz-detail', r.detail)); }
            line.appendChild(h('span', 'lp-quiz-schedule', null, [ic('fa-regular fa-calendar-check'), document.createTextNode(' ' + r.schedule)]));
            var next = h('button', 'lp-btn lp-btn--sm lp-quiz-next', core.current(state).isLast ? 'Xem kết quả' : 'Câu tiếp theo');
            next.type = 'button';
            next.addEventListener('click', function () {
                core.next(state);
                if (state.finished) { renderResult(); } else { renderQuestion(); focusFirst(); }
            });
            feedback.className = 'lp-quiz-feedback ' + (r.correct ? 'is-correct' : 'is-wrong');
            feedback.textContent = '';
            feedback.appendChild(line);
            feedback.appendChild(next);
            next.focus();
        }

        function onAnswer(id) {
            var r = core.answer(state, id);
            if (r.ignored) { return; }
            markOptions(r);
            showFeedback(r);
        }

        function renderResult() {
            var s = core.summary(state);
            var score = h('p', 'lp-quiz-score', 'Bạn đúng ' + s.correct + '/' + s.total + ' câu.');
            var cta = h('a', 'lp-btn', ctaLabel);
            var again = h('button', 'lp-quiz-again', 'Làm lại');
            score.tabIndex = -1;
            cta.href = ctaHref;
            again.type = 'button';
            again.addEventListener('click', function () { state = core.reset(state); renderQuestion(); focusFirst(); });
            playBox.hidden = true;
            resultBox.hidden = false;
            resultBox.textContent = '';
            [score, h('p', 'lp-quiz-note', note), h('div', 'lp-quiz-actions', null, [cta, again])].forEach(function (n) { resultBox.appendChild(n); });
            score.focus();
        }

        // Keys 1-4 answer only while focus is inside the card (the listener is on the card itself).
        root.addEventListener('keydown', function (e) {
            if (e.altKey || e.ctrlKey || e.metaKey || state.finished) { return; }
            var i = core.keyOptionIndex(e.key);
            var c = i < 0 ? null : core.current(state);
            if (!c || c.answered || !c.options[i]) { return; }
            e.preventDefault();
            onAnswer(c.options[i].id);
        });

        renderQuestion();
    }

    function init() {
        initNav();
        initDemo();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
