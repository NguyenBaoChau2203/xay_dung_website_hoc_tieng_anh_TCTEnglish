/*
 * Học (learn): rounds of 7 words. Multiple choice first, then typing; misses are re-asked in the same round.
 * State lives on this device (practice-core createStore); progress is written only when "Tính vào ôn tập" is on.
 */
(function () {
    'use strict';

    var P = window.TctPractice;
    var ui = P.ui;
    var core = P.core;
    var el = ui.el;
    var icon = ui.icon;

    function run(session) {
        var config = session.config;
        var query = config.query || {};
        var userId = config.userId;
        var targetIds = config.targets.map(function (w) { return w.id; });
        var byId = {};
        config.targets.forEach(function (w) { byId[w.id] = w; });
        var pool = core.dedupeById(config.targets.concat(config.pool));
        var limit = query.words ? targetIds.length : (query.n || targetIds.length);
        var singleSet = !!config.setId && !query.words;

        var saved = session.store.loadLearn(userId, config.scopeKey);
        var state = core.restoreLearn(saved, targetIds, targetIds, limit);
        var hadProgress = !!state && (state.roundStars.length > 0 || state.words.some(function (w) { return w.level > 0 || w.misses > 0; }));
        if (!state) { state = core.initLearn(targetIds.slice(0, limit)); }

        var introduced = {};
        var roundRight = 0;
        var roundTotal = 0;
        var roundLabel = el('span', 'tct-badge tct-badge--success');
        var segments = el('span', 'tp-seg');
        segments.setAttribute('role', 'img');
        var segDone = el('span', 'tp-seg__part tp-seg__part--done');
        var segLearning = el('span', 'tp-seg__part tp-seg__part--learning');
        var segNew = el('span', 'tp-seg__part tp-seg__part--new');
        segments.appendChild(segDone);
        segments.appendChild(segLearning);
        segments.appendChild(segNew);

        function save() { session.store.saveLearn(userId, config.scopeKey, state); }

        function wordState(id) {
            for (var i = 0; i < state.words.length; i++) { if (state.words[i].wordId === id) { return state.words[i]; } }
            return { level: 0, misses: 0 };
        }

        function updateProgress() {
            var p = core.progress(state);
            var total = state.words.length;
            segDone.style.setProperty('--n', String(p.mastered));
            segLearning.style.setProperty('--n', String(p.learning));
            segNew.style.setProperty('--n', String(p.notStarted));
            segments.setAttribute('aria-label', 'Đã thuộc ' + p.mastered + '/' + total + ', đang học ' + p.learning);
            session.setProgress(p.mastered, total);
            session.setCounter(p.mastered + '/' + total);
        }

        function mergeStats(percent) {
            if (singleSet) { session.store.mergeStats(config.setId, { learnPercent: percent }); }
        }

        function heading(text) {
            var h = el('h2', 'tp-summary__title', text);
            h.tabIndex = -1;
            return h;
        }

        function stars(count) {
            var box = el('span', 'tp-stars tp-stars--small');
            box.setAttribute('role', 'img');
            box.setAttribute('aria-label', count + ' sao');
            for (var i = 1; i <= 3; i++) { box.appendChild(icon(i <= count ? 'fa-solid fa-star' : 'fa-regular fa-star tp-star--off')); }
            return box;
        }

        // ------------------------------------------------------------- resume
        function showResume() {
            var p = core.progress(state);
            var box = el('section', 'tct-card tp-match-start');
            var h = heading('Tiếp tục học?');
            box.appendChild(h);
            box.appendChild(el('p', 'm-0', 'Đã thuộc ' + p.mastered + '/' + state.words.length));
            var actions = el('div', 'tp-summary__actions');
            var go = el('button', 'btn btn-primary', 'Học tiếp');
            go.type = 'button';
            go.addEventListener('click', function () { if (state.queue.length > 0 && state.round > 0) { askNext(); } else { showStages(); } });
            var restart = el('button', 'btn btn-outline-primary', 'Bắt đầu lại');
            restart.type = 'button';
            restart.addEventListener('click', function () { reset(); showStages(); });
            actions.appendChild(go);
            actions.appendChild(restart);
            box.appendChild(actions);
            session.setSlot([]);
            updateProgress();
            session.render(box);
            go.focus();
        }

        function reset() {
            session.store.clearLearn(userId, config.scopeKey);
            state = core.initLearn(targetIds.slice(0, limit));
            introduced = {};
            updateProgress();
            mergeStats(0);
        }

        // ------------------------------------------------------------- stage list
        function wordChip(id) {
            var w = byId[id];
            var chip = el('li', 'tct-card tp-chip-word');
            var term = el('span', 'tp-chip-word__term tct-break', w ? w.term : '');
            term.lang = 'en';
            chip.appendChild(term);
            var st = wordState(id);
            if (st.level === 0 && st.misses === 0) {
                chip.appendChild(el('span', 'tct-badge tct-badge--warning', 'Mới'));
            } else {
                var d = core.masteryDots(state, id);
                var dots = el('span', 'tp-dots');
                for (var i = 0; i < 3; i++) { dots.appendChild(el('span', 'tp-dot' + (i < d ? ' tp-dot--on' : ''))); }
                dots.setAttribute('aria-hidden', 'true');
                chip.appendChild(dots);
                chip.appendChild(el('span', 'visually-hidden', d + '/3'));
            }
            return chip;
        }

        function showStages() {
            if (core.learnComplete(state)) { showComplete(); return; }
            session.setSlot([]);
            updateProgress();
            var root = el('section', 'tp-stages');
            var title = heading('Các chặng');
            root.appendChild(title);

            var list = el('ol', 'tp-step-list');
            var done = core.roundsDone(state);
            var totalStages = core.stageCount(state);
            for (var i = 0; i < done; i++) {
                var item = el('li', 'tp-step tp-step--done');
                item.appendChild(icon('fa-solid fa-circle-check'));
                item.appendChild(el('span', 'tp-step__name', 'Chặng ' + (i + 1)));
                item.appendChild(stars(state.roundStars[i] || 1));
                list.appendChild(item);
            }
            var current = el('li', 'tp-step tp-step--current');
            current.setAttribute('aria-current', 'step');
            current.appendChild(icon('fa-solid fa-play'));
            current.appendChild(el('span', 'tp-step__name', 'Chặng ' + (done + 1)));
            current.appendChild(el('span', 'tct-badge tct-badge--success', 'Đang học'));
            list.appendChild(current);
            for (var j = done + 1; j < totalStages; j++) {
                var locked = el('li', 'tp-step tp-step--locked');
                locked.appendChild(icon('fa-solid fa-lock'));
                locked.appendChild(el('span', 'tp-step__name', 'Chặng ' + (j + 1)));
                locked.appendChild(el('span', 'visually-hidden', 'Chưa mở'));
                list.appendChild(locked);
            }
            root.appendChild(list);

            var preview = core.previewRound(state, core.LEARN_ROUND_SIZE);
            var card = el('div', 'tct-card tp-stage-card');
            card.appendChild(el('h3', 'tp-missed__title', 'Chặng ' + (done + 1)));
            card.appendChild(el('p', 'tp-stage-card__meta', preview.fresh + ' từ mới · ' + preview.review + ' từ ôn lại'));
            var chips = el('ul', 'tp-chip-words');
            preview.ids.forEach(function (id) { chips.appendChild(wordChip(id)); });
            card.appendChild(chips);
            var start = el('button', 'btn btn-primary btn-lg', 'Bắt đầu chặng ' + (done + 1));
            start.type = 'button';
            start.addEventListener('click', function () {
                core.startRound(state, core.LEARN_ROUND_SIZE);
                roundRight = 0;
                roundTotal = 0;
                save();
                askNext();
            });
            card.appendChild(start);
            root.appendChild(card);

            session.render(root);
            start.focus({ preventScroll: true });
        }

        // ------------------------------------------------------------- round
        function levelBadge(id) {
            var st = wordState(id);
            if (st.level >= 1) { return el('span', 'tct-badge tct-badge--primary align-self-start', 'Ôn lại — gõ từ'); }
            if (st.misses > 0) { return el('span', 'tct-badge tct-badge--danger align-self-start', 'Học lại'); }
            return el('span', 'tct-badge tct-badge--warning align-self-start', 'Từ mới');
        }

        function askNext() {
            session.setSlot([roundLabel, segments]);
            roundLabel.textContent = 'Vòng ' + state.round;
            updateProgress();
            if (state.queue.length === 0) { roundEnd(); return; }
            var id = state.queue[0];
            var word = byId[id];
            if (!word) { core.answer(state, id, true); save(); askNext(); return; }   // word vanished from the scope
            var st = wordState(id);
            if (st.level === 0 && st.misses === 0 && !introduced[id]) {
                showIntro(word);
                return;
            }
            showQuestion(word);
        }

        function showIntro(word) {
            var card = el('section', 'tct-card tp-question');
            card.setAttribute('aria-label', 'Từ mới');
            card.appendChild(el('span', 'tct-badge tct-badge--warning align-self-start', 'Từ mới'));
            card.appendChild(ui.termLine(word));
            card.appendChild(ui.meaningLine(word));
            var ex = word.examples && word.examples[0];
            if (ex) {
                var line = el('p', 'tp-example', ex.text);
                line.lang = 'en';
                card.appendChild(line);
                if (ex.vi) { card.appendChild(el('p', 'tp-example-vi', ex.vi)); }
            }
            var ok = el('button', 'btn btn-primary align-self-end', 'Đã hiểu');
            ok.type = 'button';
            ok.addEventListener('click', function () { introduced[word.id] = true; askNext(); });
            card.appendChild(ok);
            session.render(card, true);
            ok.focus();
        }

        function showQuestion(word) {
            var q = core.questionFor(state, word, pool, Math.random);
            var card = el('section', 'tct-card tp-question');
            card.setAttribute('aria-label', 'Câu hỏi');
            card.appendChild(levelBadge(word.id));
            var typing = null;
            var options = null;
            var speakText = null;

            if (q.kind === 'choice' && q.prompt === 'term') {
                card.appendChild(el('p', 'tp-prompt', 'Nghĩa của từ này là gì?'));
                card.appendChild(ui.termLine(word));
                speakText = word.term;
            } else if (q.kind === 'choice') {
                card.appendChild(el('p', 'tp-prompt', 'Từ nào?'));
                var visual = ui.visual(word);
                if (visual) { card.appendChild(visual); }
                card.appendChild(ui.meaningLine(word));
            } else {
                card.appendChild(el('p', 'tp-prompt', 'Gõ từ tiếng Anh có nghĩa này'));
                var vis = ui.visual(word);
                if (vis) { card.appendChild(vis); }
                card.appendChild(ui.meaningLine(word));
            }

            var answered = false;
            function record(outcome, extra) {
                if (answered) { return; }
                answered = true;
                var correct = outcome !== 'wrong';
                core.answer(state, word.id, correct);
                roundTotal += 1;
                if (correct) { roundRight += 1; }
                session.noteAnswer();
                session.writer.write(word.id, correct);
                save();
                updateProgress();
                ui.feedbackPanel(session, outcome, word, {
                    extra: extra,
                    last: false,
                    onNext: function () { askNext(); }
                });
            }

            if (q.kind === 'typing') {
                typing = ui.typingInput(word, {
                    hints: true,
                    onSubmit: function (text) {
                        var outcome = core.checkTyping(text, word);
                        typing.lock();
                        record(outcome, outcome === 'correct' ? null : ui.diff(text, word.term));
                    },
                    onSkip: function () { typing.lock(); record('wrong', null); }
                });
                card.appendChild(typing.root);
            } else {
                options = ui.options(q, {
                    onPick: function (optionId) {
                        if (answered) { return; }
                        options.reveal(optionId);
                        record(optionId === q.answerId ? 'correct' : 'wrong', null);
                    }
                });
                card.appendChild(options.root);
                card.appendChild(ui.keyHints(['1–' + q.options.length + ' chọn đáp án', 'Enter tiếp tục']));
            }

            session.render(card, true);
            keyContext.options = options;
            keyContext.speakText = speakText;
            keyContext.answered = function () { return answered; };
            if (typing) { typing.focus(); } else { session.stage.focus({ preventScroll: true }); }
        }

        var keyContext = { options: null, speakText: null, answered: function () { return true; } };
        ui.onKeys(function (event, key) {
            if (keyContext.speakText && (key === 'r' || key === 'R')) { ui.speak(keyContext.speakText); return true; }
            if (keyContext.options && !keyContext.answered() && /^[1-9]$/.test(key)) {
                var button = keyContext.options.buttons[Number(key) - 1];
                if (button) { button.click(); return true; }
            }
            return false;
        });

        // ------------------------------------------------------------- round end and completion
        function bar(label, count, total, cls) {
            var row = el('div', 'tp-bar-row');
            row.appendChild(el('span', 'tp-bar-row__label', label));
            var track = el('span', 'tp-bar-row__track');
            var fill = el('span', 'tp-bar-row__fill ' + cls);
            fill.style.setProperty('--p', String(total ? Math.round(100 * count / total) : 0));
            track.appendChild(fill);
            row.appendChild(track);
            row.appendChild(el('span', 'tp-bar-row__count', String(count)));
            return row;
        }

        function roundEnd() {
            var count = core.starsFor(roundRight, roundTotal);
            core.recordRound(state, count);
            var p = core.progress(state);
            var total = state.words.length;
            mergeStats(core.learnPercent(state));
            save();
            updateProgress();

            var root = el('section', 'tp-summary');
            root.setAttribute('aria-label', 'Kết quả vòng học');
            var head = el('div', 'tp-summary__head');
            var starBox = el('div', 'tp-stars tp-pop');
            starBox.setAttribute('role', 'img');
            starBox.setAttribute('aria-label', count + ' sao');
            for (var i = 1; i <= 3; i++) { starBox.appendChild(icon(i <= count ? 'fa-solid fa-star' : 'fa-regular fa-star tp-star--off')); }
            head.appendChild(starBox);
            var title = heading('Hết vòng ' + state.round);
            head.appendChild(title);
            root.appendChild(head);

            var bars = el('div', 'tct-card tp-bars');
            bars.appendChild(bar('Chưa học', p.notStarted, total, 'tp-bar-row__fill--new'));
            bars.appendChild(bar('Đang học', p.learning, total, 'tp-bar-row__fill--learning'));
            bars.appendChild(bar('Đã thuộc', p.mastered, total, 'tp-bar-row__fill--done'));
            root.appendChild(bars);

            var words = el('ul', 'tp-missed__list');
            state.roundWords.forEach(function (id) {
                var w = byId[id];
                if (!w) { return; }
                var mastered = wordState(id).level >= 2;
                var item = el('li', 'tct-card tp-missed__item');
                item.appendChild(icon(mastered ? 'fa-solid fa-circle-check tp-ok' : 'fa-solid fa-hourglass-half tp-learning'));
                var text = el('div', 'tp-missed__text');
                var term = el('span', 'tp-missed__term', w.term);
                term.lang = 'en';
                text.appendChild(term);
                text.appendChild(el('span', 'tp-missed__def', w.definition));
                item.appendChild(text);
                item.appendChild(el('span', 'tct-badge ' + (mastered ? 'tct-badge--success' : 'tct-badge--warning'), mastered ? 'Đã thuộc' : 'Đang học'));
                words.appendChild(item);
            });
            root.appendChild(words);

            var actions = el('div', 'tp-summary__actions');
            var next = el('button', 'btn btn-primary', 'Tiếp tục');
            next.type = 'button';
            next.addEventListener('click', function () { showStages(); });
            actions.appendChild(next);
            root.appendChild(actions);

            session.setSlot([]);
            session.render(root);
            title.focus();
        }

        function showComplete() {
            mergeStats(100);
            session.finish();
            updateProgress();
            var root = el('section', 'tp-summary');
            root.setAttribute('aria-label', 'Hoàn thành');
            var head = el('div', 'tp-summary__head');
            var star = el('div', 'tp-stars tp-pop');
            star.setAttribute('role', 'img');
            star.setAttribute('aria-label', 'Hoàn thành');
            star.appendChild(icon('fa-solid fa-star'));
            head.appendChild(star);
            var title = heading('Bạn đã thuộc hết ' + state.words.length + ' từ!');
            head.appendChild(title);
            root.appendChild(head);
            var actions = el('div', 'tp-summary__actions');
            var again = el('button', 'btn btn-outline-primary', 'Học lại từ đầu');
            again.type = 'button';
            again.addEventListener('click', function () { session.finished = false; reset(); showStages(); });
            actions.appendChild(again);
            [['Làm bài kiểm tra', config.testUrl, 'btn-primary'], ['Xong', config.exitUrl, 'btn-outline-primary']].forEach(function (a) {
                var link = el('a', 'btn ' + a[2], a[0]);
                link.href = a[1];
                actions.appendChild(link);
            });
            root.appendChild(actions);
            session.setSlot([]);
            session.render(root);
            title.focus();
        }

        if (hadProgress) { showResume(); } else { showStages(); }
    }

    P.modes.learn = run;
}());
