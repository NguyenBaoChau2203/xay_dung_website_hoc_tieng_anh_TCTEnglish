/*
 * Kiểm tra (test): one scrolling page of mixed questions, submit, score, review, retry the missed words.
 * Questions are built with a seeded rng so a reload rebuilds the same test and restores the session draft.
 */
(function () {
    'use strict';

    var P = window.TctPractice;
    var ui = P.ui;
    var core = P.core;
    var el = ui.el;
    var icon = ui.icon;

    function randomSeed() { return Math.floor(Math.random() * 4294967296) >>> 0; }

    /** Draft slot: the same targets + options always map to the same slot, whatever the seed. */
    function slotId(config, types, ask, count) {
        var pseudo = config.targets.map(function (t) { return { id: String(t.id) }; });
        pseudo.push({ id: config.scopeKey + '|' + types.join(',') + '|' + ask + '|' + count });
        return core.testId(pseudo);
    }

    function questionTitle(q) {
        if (q.kind === 'cloze') { return q.cloze.before + '____' + q.cloze.after; }
        if (q.kind === 'truefalse') { return q.prompt === 'term' ? q.word.term + ' — ' + q.shown : q.word.definition + ' — ' + q.shown; }
        if (q.kind === 'typing') { return q.word.definition; }
        return q.prompt === 'term' ? q.word.term : q.word.definition;
    }

    function run(session) {
        var config = session.config;
        var query = config.query || {};
        var types = core.parseTestTypes(query.types);
        var ask = core.parseTestAsk(query.ask);
        var count = query.n || 10;
        var slot = slotId(config, types, ask, count);
        var userId = config.userId;

        var allWords = core.dedupeById(config.targets.concat(config.pool));
        var draft = session.store.loadDraft(userId, slot);
        var seed = draft && typeof draft.seed === 'number' ? draft.seed : randomSeed();
        var questions = core.buildTest(config.targets, config.pool, { types: types, ask: ask, count: count }, core.mulberry32(seed));
        var current = { questions: questions, answers: {}, slot: slot, seed: seed, persist: true };
        if (draft && draft.answers) { current.answers = restoreAnswers(questions, draft.answers); }

        start(current);

        // ------------------------------------------------------------- helpers
        function restoreAnswers(qs, saved) {
            var out = {};
            qs.forEach(function (q) {
                var a = saved[q.id];
                if (a == null || a === '') { return; }
                if (q.kind === 'choice' || q.kind === 'cloze') {
                    if (q.options.some(function (o) { return o.id === a; })) { out[q.id] = a; }
                } else if (q.kind === 'truefalse') {
                    if (a === 'true' || a === 'false') { out[q.id] = a; }
                } else {
                    out[q.id] = String(a);
                }
            });
            return out;
        }

        function saveDraft(run) {
            if (run.persist) { session.store.saveDraft(userId, run.slot, { seed: run.seed, answers: run.answers }); }
        }

        function answered(run) {
            return run.questions.filter(function (q) { var a = run.answers[q.id]; return a != null && String(a).trim() !== ''; }).length;
        }

        // ------------------------------------------------------------- test page
        function start(run) {
            var total = run.questions.length;
            var root = el('div', 'tp-test');
            var main = el('div', 'tp-test__main');
            var currentIndex = -1;
            var jump = el('nav', 'tp-jump');
            jump.setAttribute('aria-label', 'Danh sách câu hỏi');
            var jumpGrid = el('div', 'tp-jump__grid');
            jump.appendChild(el('p', 'tp-jump__title', 'Câu hỏi'));
            jump.appendChild(jumpGrid);
            var counter = el('span', 'tp-test-bar__count');
            var miniBar = el('span', 'k-progress k-progress--thin tp-test-bar__progress');
            miniBar.setAttribute('aria-hidden', 'true');
            var miniFill = el('span', 'k-progress__fill');
            miniBar.appendChild(miniFill);
            var submit = el('button', 'btn btn-primary', 'Nộp bài');
            submit.type = 'button';
            var bar = el('div', 'tp-test-bar');
            bar.appendChild(counter);
            bar.appendChild(miniBar);
            bar.appendChild(submit);

            var jumpButtons = [];
            var sections = [];

            function refresh() {
                var done = answered(run);
                counter.textContent = 'Đã làm ' + done + '/' + total;
                session.setProgress(done, total);
                miniFill.style.setProperty('--p', (total ? Math.round(100 * done / total) : 0) + '%');
                run.questions.forEach(function (q, i) {
                    var has = run.answers[q.id] != null && String(run.answers[q.id]).trim() !== '';
                    jumpButtons[i].classList.toggle('is-answered', has);
                    jumpButtons[i].setAttribute('aria-label', 'Câu ' + (i + 1) + (has ? ', đã làm' : ', chưa làm'));
                });
            }

            function setCurrent(index) {
                if (index === currentIndex || index < 0 || !jumpButtons[index]) { return; }
                currentIndex = index;
                jumpButtons.forEach(function (b, n) {
                    b.classList.toggle('is-current', n === index);
                    if (n === index) { b.setAttribute('aria-current', 'true'); } else { b.removeAttribute('aria-current'); }
                });
            }

            function setAnswer(q, value) {
                if (value == null || String(value).trim() === '') { delete run.answers[q.id]; } else { run.answers[q.id] = value; }
                session.noteAnswer();
                saveDraft(run);
                refresh();
            }

            run.questions.forEach(function (q, i) {
                var hue = q.kind === 'truefalse' ? 'info' : q.kind === 'typing' ? 'warning' : q.kind === 'cloze' ? 'danger' : 'primary';
                var section = el('section', 'k-panel tp-q tp-hue-' + hue);
                var headId = 'tpQ' + (i + 1) + 'Title';
                section.setAttribute('aria-labelledby', headId);
                section.id = 'tpQ' + (i + 1);
                var head = el('div', 'tp-q__head');
                var title = el('h2', 'tp-q__title', 'Câu ' + (i + 1) + '/' + total);
                title.id = headId;
                title.tabIndex = -1;
                var lead = el('div', 'tp-q__lead');
                var num = el('span', 'tp-q__num', String(i + 1));
                num.setAttribute('aria-hidden', 'true');
                lead.appendChild(num);
                lead.appendChild(title);
                head.appendChild(lead);
                head.appendChild(el('span', 'tct-badge tct-badge--' + hue, core.TEST_TYPE_LABEL[q.kind === 'truefalse' ? 'tf' : q.kind]));
                section.appendChild(head);
                buildBody(section, q, run, setAnswer);
                main.appendChild(section);
                sections.push(section);

                var jb = el('button', 'tp-jump__btn', String(i + 1));
                jb.type = 'button';
                jb.addEventListener('click', function () {
                    setCurrent(i);
                    section.scrollIntoView({ block: 'start', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
                    title.focus({ preventScroll: true });
                });
                jumpButtons.push(jb);
                jumpGrid.appendChild(jb);
            });

            root.appendChild(jump);
            root.appendChild(main);
            if ('IntersectionObserver' in window) {
                var observer = new IntersectionObserver(function (entries) {
                    entries.forEach(function (entry) { if (entry.isIntersecting) { setCurrent(sections.indexOf(entry.target)); } });
                }, { rootMargin: '-25% 0px -65% 0px' });
                sections.forEach(function (s) { observer.observe(s); });
            }
            setCurrent(0);
            var wrapper = el('div', 'tp-test-wrap');
            wrapper.appendChild(root);
            wrapper.appendChild(bar);

            submit.addEventListener('click', function () {
                var left = total - answered(run);
                if (left > 0) { confirmSubmit(left, function () { finish(run); }); } else { finish(run); }
            });

            ui.barTitle('Kiểm tra');
            session.setSlot([el('span', 'tp-bar__meta', config.scopeName + ' · ' + total + ' câu')]);
            session.render(wrapper);
            refresh();
        }

        function buildBody(section, q, run, setAnswer) {
            var saved = run.answers[q.id];
            if (q.kind === 'choice' || q.kind === 'cloze') {
                if (q.kind === 'cloze') {
                    var cl = el('p', 'tp-cloze');
                    cl.lang = 'en';
                    cl.appendChild(document.createTextNode(q.cloze.before));
                    cl.appendChild(el('span', 'tp-cloze__blank'));
                    cl.appendChild(document.createTextNode(q.cloze.after));
                    section.appendChild(cl);
                    if (q.cloze.vi) { section.appendChild(el('p', 'tp-example-vi', q.cloze.vi)); }
                } else if (q.prompt === 'term') {
                    section.appendChild(el('p', 'tp-prompt', 'Nghĩa của từ này là gì?'));
                    section.appendChild(ui.termLine(q.word));
                } else {
                    section.appendChild(el('p', 'tp-prompt', 'Từ nào?'));
                    section.appendChild(ui.meaningLine(q.word));
                }
                var ctl = ui.options(q, {
                    onPick: function (id) {
                        var next = run.answers[q.id] === id ? null : id;   // pick again to clear
                        select(ctl.buttons, next);
                        setAnswer(q, next);
                    }
                });
                select(ctl.buttons, saved || null);
                section.appendChild(ctl.root);
            } else if (q.kind === 'truefalse') {
                var line = el('div', 'tp-tf-line');
                if (q.prompt === 'term') {
                    line.appendChild(ui.termLine(q.word, { visual: false }));
                    line.appendChild(el('p', 'tp-meaning', q.shown));
                    section.appendChild(line);
                    section.appendChild(el('p', 'tp-prompt', 'Nghĩa này đúng không?'));
                } else {
                    line.appendChild(el('p', 'tp-meaning', q.word.definition));
                    var shown = el('p', 'tp-term tct-break', q.shown);
                    shown.lang = 'en';
                    line.appendChild(shown);
                    section.appendChild(line);
                    section.appendChild(el('p', 'tp-prompt', 'Từ này đúng không?'));
                }
                var group = el('div', 'tp-tf-buttons');
                group.setAttribute('role', 'group');
                group.setAttribute('aria-label', 'Chọn đúng hoặc sai');
                var tfButtons = [];
                [['true', 'Đúng'], ['false', 'Sai']].forEach(function (pair) {
                    var b = el('button', 'tp-option tp-tf-btn');
                    b.type = 'button';
                    b.dataset.optionId = pair[0];
                    b.appendChild(icon(pair[0] === 'true' ? 'fa-solid fa-check tp-tf-icon tp-tf-icon--ok' : 'fa-solid fa-xmark tp-tf-icon tp-tf-icon--bad'));
                    b.appendChild(el('span', 'tp-option__label', pair[1]));
                    b.addEventListener('click', function () {
                        var next = run.answers[q.id] === pair[0] ? null : pair[0];
                        select(tfButtons, next);
                        setAnswer(q, next);
                    });
                    tfButtons.push(b);
                    group.appendChild(b);
                });
                select(tfButtons, saved || null);
                section.appendChild(group);
            } else {   // typing
                section.appendChild(el('p', 'tp-prompt', 'Gõ từ tiếng Anh có nghĩa này'));
                section.appendChild(ui.meaningLine(q.word));
                var id = 'tpAns' + q.id.replace(/\W/g, '');
                var label = el('label', 'k-label tp-field-label', 'Gõ từ tiếng Anh');
                label.htmlFor = id;
                var input = el('input', 'form-control tp-typing__input');
                input.id = id;
                input.type = 'text';
                ['autocomplete:off', 'autocapitalize:none', 'autocorrect:off', 'spellcheck:false'].forEach(function (pair) {
                    var p = pair.split(':'); input.setAttribute(p[0], p[1]);
                });
                input.lang = 'en';
                if (saved) { input.value = saved; }
                input.addEventListener('input', function () { setAnswer(q, input.value); });
                section.appendChild(label);
                section.appendChild(input);
            }
        }

        function select(buttons, id) {
            buttons.forEach(function (b) { b.setAttribute('aria-pressed', String(id != null && b.dataset.optionId === id)); });
        }

        function confirmSubmit(left, onConfirm) {
            var modal = el('div', 'modal fade');
            modal.tabIndex = -1;
            modal.setAttribute('aria-labelledby', 'tpSubmitTitle');
            modal.setAttribute('aria-hidden', 'true');
            var dialog = el('div', 'modal-dialog modal-dialog-centered');
            var content = el('div', 'modal-content');
            var header = el('div', 'modal-header');
            var h = el('h2', 'modal-title fs-5', 'Còn ' + left + ' câu chưa làm. Vẫn nộp?');
            h.id = 'tpSubmitTitle';
            header.appendChild(h);
            var close = el('button', 'btn-close');
            close.type = 'button';
            close.setAttribute('data-bs-dismiss', 'modal');
            close.setAttribute('aria-label', 'Đóng');
            header.appendChild(close);
            var footer = el('div', 'modal-footer');
            var keep = el('button', 'btn btn-outline-primary', 'Làm tiếp');
            keep.type = 'button';
            keep.setAttribute('data-bs-dismiss', 'modal');
            var go = el('button', 'btn btn-primary', 'Nộp bài');
            go.type = 'button';
            footer.appendChild(keep);
            footer.appendChild(go);
            content.appendChild(header);
            content.appendChild(footer);
            dialog.appendChild(content);
            modal.appendChild(dialog);
            document.body.appendChild(modal);
            if (!window.bootstrap || !window.bootstrap.Modal) {
                modal.remove();
                if (window.confirm('Còn ' + left + ' câu chưa làm. Vẫn nộp?')) { onConfirm(); }
                return;
            }
            var instance = window.bootstrap.Modal.getOrCreateInstance(modal);
            go.addEventListener('click', function () {
                modal.addEventListener('hidden.bs.modal', function () { modal.remove(); onConfirm(); }, { once: true });
                instance.hide();
            });
            modal.addEventListener('hidden.bs.modal', function () { modal.remove(); }, { once: true });
            instance.show();
        }

        // ------------------------------------------------------------- result
        function finish(run) {
            session.finish();
            session.clearFeedback();
            var graded = core.gradeTest(run.questions, run.answers);
            graded.items.forEach(function (item) {
                if (item.answer !== '') { session.writer.write(item.question.word.id, item.result !== 'wrong'); }
            });
            session.store.clearDraft(userId, run.slot);
            if (config.setId && !query.words && run.persist) { session.store.mergeStats(config.setId, { testPercent: graded.percent }); }
            showResult(run, graded);
        }

        function showResult(run, graded) {
            var root = el('section', 'tp-summary tp-result');
            root.setAttribute('aria-label', 'Kết quả bài kiểm tra');

            var head = el('div', 'tp-summary__head');
            head.appendChild(ui.ring(graded.percent));
            var title = el('h2', 'tp-summary__title', 'Đúng ' + graded.correct + '/' + graded.total);
            title.tabIndex = -1;
            head.appendChild(title);
            head.appendChild(el('p', 'm-0', core.testRemark(graded.percent)));
            root.appendChild(head);

            var missedWords = [];
            var seen = {};
            graded.items.forEach(function (item) {
                if (item.result === 'wrong' && !seen[item.question.word.id]) { seen[item.question.word.id] = true; missedWords.push(item.question.word); }
            });

            var filter = el('button', 'tp-filter k-seg');
            filter.type = 'button';
            filter.setAttribute('aria-pressed', 'false');
            var filterItem = el('span', 'k-seg__item', 'Chỉ câu sai');
            filter.appendChild(filterItem);
            var toolbar = el('div', 'tp-result__toolbar');
            toolbar.appendChild(el('h3', 'tp-missed__title', 'Chi tiết từng câu'));
            toolbar.appendChild(filter);
            root.appendChild(toolbar);

            var list = el('ol', 'tp-result__list');
            graded.items.forEach(function (item, i) {
                var li = el('li', 'tp-result__item');
                li.dataset.result = item.result;
                var q = item.question;
                var label = (i + 1) + '. ' + questionTitle(q);
                if (item.result === 'correct') {
                    var d = el('details', 'k-panel tp-result__card tp-result__card--ok');
                    var sum = el('summary', 'tp-result__summary');
                    sum.appendChild(icon('fa-solid fa-circle-check me-2'));
                    sum.appendChild(document.createTextNode(label));
                    d.appendChild(sum);
                    d.appendChild(el('p', 'tp-result__line', 'Đáp án: ' + core.expectedText(q)));
                    li.appendChild(d);
                } else {
                    var card = el('div', 'k-panel tp-result__card tp-result__card--' + (item.result === 'close' ? 'close' : 'bad'));
                    var heading = el('p', 'tp-result__q');
                    heading.appendChild(icon(item.result === 'close' ? 'fa-solid fa-circle-check me-2' : 'fa-solid fa-circle-xmark me-2'));
                    heading.appendChild(document.createTextNode(label));
                    if (item.result === 'close') { heading.appendChild(el('span', 'tct-badge tct-badge--warning ms-2', 'Gần đúng')); }
                    card.appendChild(heading);
                    var given = core.answerText(q, item.answer);
                    card.appendChild(el('p', 'tp-result__line tp-result__line--bad', 'Bạn trả lời: ' + (given || 'Bỏ trống')));
                    var good = el('p', 'tp-result__line tp-result__line--ok');
                    good.appendChild(document.createTextNode('Đáp án: ' + core.expectedText(q) + ' '));
                    good.appendChild(ui.speakButton(q.word.term));
                    card.appendChild(good);
                    li.appendChild(card);
                }
                list.appendChild(li);
            });
            root.appendChild(list);

            filter.addEventListener('click', function () {
                var on = filter.getAttribute('aria-pressed') !== 'true';
                filter.setAttribute('aria-pressed', String(on));
                filterItem.classList.toggle('is-active', on);
                Array.prototype.forEach.call(list.children, function (li) { li.hidden = on && li.dataset.result === 'correct'; });
            });

            var actions = el('div', 'tp-summary__actions');
            if (missedWords.length) {
                var retry = el('button', 'btn btn-lg btn-primary', 'Làm lại câu sai');
                retry.type = 'button';
                retry.addEventListener('click', function () {
                    var retrySeed = randomSeed();
                    var qs = core.buildTest(missedWords, allWords, { types: types, ask: ask, count: missedWords.length }, core.mulberry32(retrySeed));
                    var next = { questions: qs, answers: {}, slot: core.testId(qs), seed: retrySeed, persist: false };
                    session.finished = false;
                    start(next);
                    window.scrollTo(0, 0);
                });
                actions.appendChild(retry);
                var learn = el('a', 'btn btn-lg btn-outline-primary', 'Học các từ sai');
                learn.href = ui.learnLink(config, missedWords.slice(0, 50).map(function (w) { return w.id; }));
                actions.appendChild(learn);
            } else {
                var fresh = el('a', 'btn btn-lg btn-primary', 'Làm bài mới');
                fresh.href = config.againUrl;
                actions.appendChild(fresh);
            }
            root.appendChild(actions);
            var links = el('div', 'tp-summary__links');
            [missedWords.length ? ['Làm bài mới', config.againUrl] : null, ['Xong', config.exitUrl]].forEach(function (pair) {
                if (!pair) { return; }
                var link = el('a', 'tp-summary__link', pair[0]);
                link.href = pair[1];
                links.appendChild(link);
            });
            root.appendChild(links);

            session.setCounter('');
            session.setProgress(graded.total, graded.total);
            session.render(root);
            title.focus();
            window.scrollTo(0, 0);
        }
    }

    P.modes.test = run;
}());
