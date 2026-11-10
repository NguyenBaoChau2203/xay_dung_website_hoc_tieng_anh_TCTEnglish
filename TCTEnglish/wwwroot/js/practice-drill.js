/*
 * Skill drills: quiz (Trắc nghiệm), typing (Gõ từ), listening (Nghe), cloze (Điền từ).
 * One question at a time, feedback panel after each answer, shared summary at the end.
 */
(function () {
    'use strict';

    var P = window.TctPractice;
    var ui = P.ui;
    var core = P.core;
    var el = ui.el;

    function clozeLine(q, filled) {
        var p = el('p', 'tp-cloze');
        p.lang = 'en';
        p.appendChild(document.createTextNode(q.cloze.before));
        var blank = el('span', 'tp-cloze__blank' + (filled ? ' tp-cloze__blank--filled' : ''), filled ? q.cloze.answer : '');
        if (!filled) { blank.setAttribute('aria-label', 'chỗ trống'); }
        p.appendChild(blank);
        p.appendChild(document.createTextNode(q.cloze.after));
        return p;
    }

    function run(session) {
        var config = session.config;
        var questions = core.buildQuestions(config.mode, config.targets, config.pool, Math.random);

        if (questions.length === 0) {
            session.render(ui.stateMessage({
                title: 'Các từ này chưa có câu ví dụ để điền.',
                message: 'Thêm câu ví dụ trong trang từ, hoặc chọn chế độ khác.',
                actionLabel: 'Chọn chế độ khác',
                actionUrl: config.hubUrl
            }));
            session.setCounter('');
            return;
        }

        var index = 0;
        var results = [];
        var current = null;   // { q, options, answered, speakText }
        session.setProgress(0, questions.length);

        function record(q, outcome, extra) {
            results.push({ word: q.word, result: outcome });
            session.noteAnswer();
            session.writer.write(q.word.id, outcome !== 'wrong');
            session.setProgress(results.length, questions.length);
            current.answered = true;
            ui.feedbackPanel(session, outcome, q.word, {
                last: index === questions.length - 1,
                extra: extra,
                onNext: function () { index += 1; showQuestion(); }
            });
        }

        function showQuestion() {
            if (index >= questions.length) {
                ui.summary(session, results);
                current = null;
                return;
            }
            var q = questions[index];
            var card = el('section', 'tp-question');
            card.setAttribute('aria-label', 'Câu ' + (index + 1) + ' trên ' + questions.length);
            current = { q: q, options: null, answered: false, speakText: null };

            if (q.kind === 'choice' && q.prompt === 'term') {
                card.appendChild(el('p', 'tp-prompt', 'Nghĩa của từ này là gì?'));
                card.appendChild(ui.termLine(q.word));
                current.speakText = q.word.term;
            } else if (q.kind === 'choice' && q.prompt === 'meaning') {
                card.appendChild(el('p', 'tp-prompt', 'Từ nào?'));
                var visual = ui.visual(q.word);
                if (visual) { card.appendChild(visual); }
                card.appendChild(ui.meaningLine(q.word));
            } else if (q.kind === 'choice') {   // audio
                card.appendChild(el('p', 'tp-prompt', 'Bạn nghe thấy từ nào?'));
                var listen = el('div', 'tp-listen');
                var play = el('button', 'tp-listen__play');
                play.type = 'button';
                play.setAttribute('aria-label', 'Nghe lại');
                play.appendChild(ui.icon('fa-solid fa-volume-high'));
                play.addEventListener('click', function () { ui.speak(q.word.term); });
                var slow = el('button', 'btn btn-lg k-btn--soft tp-listen__slow', 'Nghe chậm');
                slow.type = 'button';
                slow.addEventListener('click', function () { ui.speak(q.word.term, 0.6); });
                listen.appendChild(play);
                listen.appendChild(slow);
                if (!window.speechSynthesis) {
                    card.appendChild(el('p', 'tp-meaning-en', 'Trình duyệt này không đọc được âm thanh. Hãy chọn chế độ khác.'));
                }
                card.appendChild(listen);
                current.speakText = q.word.term;
            } else if (q.kind === 'cloze') {
                card.appendChild(el('p', 'tp-prompt', 'Chọn từ còn thiếu'));
                current.cloze = clozeLine(q, false);
                card.appendChild(current.cloze);
                if (q.cloze.vi) { card.appendChild(el('p', 'tp-example-vi', q.cloze.vi)); }
            } else {   // typing
                card.appendChild(el('p', 'tp-prompt', 'Gõ từ tiếng Anh có nghĩa này'));
                var vis = ui.visual(q.word);
                if (vis) { card.appendChild(vis); }
                card.appendChild(ui.meaningLine(q.word));
            }

            if (q.kind === 'typing') {
                var typing = ui.typingInput(q.word, {
                    hints: true,
                    onSubmit: function (text) {
                        var outcome = core.checkTyping(text, q.word);
                        typing.lock();
                        record(q, outcome, outcome === 'correct' ? null : ui.diff(text, q.word.term));
                    },
                    onSkip: function () {
                        typing.lock();
                        record(q, 'wrong', null);
                    }
                });
                card.appendChild(typing.root);
                current.typing = typing;
            } else {
                var ctl = ui.options(q, {
                    onPick: function (optionId) {
                        if (current.answered) { return; }
                        var correct = optionId === q.answerId;
                        ctl.reveal(optionId);
                        if (q.kind === 'cloze') {
                            var filled = clozeLine(q, true);
                            current.cloze.replaceWith(filled);
                            current.cloze = filled;
                        }
                        record(q, correct ? 'correct' : 'wrong', null);
                    }
                });
                current.options = ctl;
                card.appendChild(ctl.root);
                var hint = ['1–' + q.options.length + ' chọn đáp án'];
                if (current.speakText) { hint.push('R nghe lại'); }
                hint.push('Enter tiếp tục');
                card.appendChild(ui.keyHints(hint));
            }

            session.render(card, true);
            if (current.typing) {
                current.typing.focus();
            } else {
                session.stage.focus({ preventScroll: true });
            }
            if (q.kind === 'choice' && q.prompt === 'audio') { ui.speak(q.word.term); }   // auto-plays once, when the question first appears
        }

        ui.onKeys(function (event, key) {
            if (!current || !current.q) { return false; }
            if ((key === 'r' || key === 'R') && current.speakText) {
                ui.speak(current.speakText);
                return true;
            }
            if (!current.answered && current.options && /^[1-9]$/.test(key)) {
                var button = current.options.buttons[Number(key) - 1];
                if (button) { button.click(); return true; }
            }
            return false;
        });

        showQuestion();
    }

    ['quiz', 'typing', 'listening', 'cloze'].forEach(function (mode) { P.modes[mode] = run; });
}());
