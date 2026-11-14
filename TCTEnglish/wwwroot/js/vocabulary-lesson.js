/*
 * System Vocabulary lesson and review screen: 1 Làm quen -> 2 Luyện tập -> 3 Kết quả (a review has no step 1).
 * The session logic lives in vocabulary-core.js; the shell, option buttons, typing input and feedback panel come from
 * practice-ui.js. Progress is saved through POST /Vocabulary/Review/Record (the server owns the memory level and the
 * date), once per finished word; the answer drives the "schedule row" in the feedback panel and the result list.
 * Rendering uses createElement / textContent only; the config is a JSON script tag (#tvLesson).
 */
(function () {
    'use strict';

    var T = typeof TctSets !== 'undefined' ? TctSets : null;
    var P = typeof TctPractice !== 'undefined' ? TctPractice : null;
    var V = typeof TctVocabulary !== 'undefined' ? TctVocabulary.core : null;
    var configNode = document.getElementById('tvLesson');
    var cfg = null;
    try { cfg = JSON.parse(configNode ? configNode.textContent : ''); } catch (e) { cfg = null; }
    if (!T || !P || !P.ui || !V || !cfg || cfg.emptyReason || !cfg.words || cfg.words.length === 0) { return; }

    var ui = P.ui;
    var practice = P.core;
    var el = ui.el;
    var icon = ui.icon;

    var session = ui.createSession({
        mode: 'vocabulary',
        countsToSrs: false,
        setId: cfg.setId,
        hubUrl: cfg.courseUrl,
        exitUrl: cfg.courseUrl
    });
    var isReview = cfg.kind === 'review';
    var state = V.initSession({ kind: cfg.kind, words: cfg.words, pool: cfg.pool || [], rng: Math.random, srs: isReview });
    var startedAt = Date.now();
    var current = null;   // what the keyboard handler may act on: { kind, speakText, next, options, answered }

    // ------------------------------------------------------------------ saving (one record call per finished word)

    var pendingSaves = [];
    var UNCHANGED = 'Lịch ôn giữ nguyên';

    function post(cardId, outcome) {
        return T.fetchJson('/Vocabulary/Review/Record', { method: 'POST', body: { cardId: cardId, outcome: outcome } });
    }

    /**
     * Sends the outcome of a finished word once (claimRecord never hands it out twice). One immediate retry;
     * then onDone(null, error) lets the caller offer "Thử lại"; the result screen retries unsaved words once more.
     */
    function recordWord(wordId, onDone) {
        var outcome = V.claimRecord(state, wordId);
        if (!outcome) { return Promise.resolve(null); }
        var promise = post(wordId, outcome)
            .catch(function () { return post(wordId, outcome); })
            .then(function (result) {
                V.markRecorded(state, wordId, result);
                if (onDone) { onDone(result, null); }
                return result;
            })
            .catch(function (error) {
                V.releaseRecord(state, wordId);
                if (onDone) { onDone(null, error); }
                return null;
            });
        pendingSaves.push(promise);
        return promise;
    }

    function flush() {
        return Promise.all(pendingSaves).then(function () {
            return Promise.all(V.unsaved(state).map(function (id) { return recordWord(id); }));
        }).then(function () {
            if (V.unsaved(state).length > 0) {
                T.toast('Không lưu được tiến độ của một số từ. Hãy kiểm tra mạng rồi học lại các từ đó.', { type: 'danger' });
            }
        });
    }

    // ------------------------------------------------------------------ shell helpers

    var pills = Array.prototype.slice.call(document.querySelectorAll('#tvPhases .tv-phase'));

    function setPhase(phase) {
        var at = -1;
        pills.forEach(function (pill, index) { if (pill.getAttribute('data-phase') === phase) { at = index; } });
        pills.forEach(function (pill, index) {
            pill.classList.toggle('is-current', index === at);
            pill.classList.toggle('is-done', index < at);
            if (index === at) { pill.setAttribute('aria-current', 'step'); } else { pill.removeAttribute('aria-current'); }
            pill.textContent = '';
            if (index < at) {
                pill.appendChild(icon('fa-solid fa-check'));
                pill.appendChild(document.createTextNode(pill.getAttribute('data-label')));
                pill.appendChild(el('span', 'visually-hidden', ' (đã xong)'));
            } else {
                pill.textContent = pill.getAttribute('data-num') + ' · ' + pill.getAttribute('data-label');
            }
        });
    }

    function updateProgress() {
        var p = V.progress(state);
        session.setProgress(p.finished, p.total);
    }

    function kbd(text, extraClass) {
        var node = el('kbd', 'k-kbd' + (extraClass ? ' ' + extraClass : ''), text);
        node.setAttribute('aria-hidden', 'true');
        return node;
    }

    function escapeRegex(text) { return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }

    /** The example sentence with the studied word in bold primary; a sentence without the exact word stays plain. */
    function sentence(text, term) {
        var p = el('p', 'tv-example__en');
        p.lang = 'en';
        var match = null;
        try {
            match = new RegExp("(^|[^\\p{L}'])(" + escapeRegex(term).replace(/\s+/g, '\\s+') + ")(?![\\p{L}])", 'iu').exec(text);
        } catch (e) { match = null; }
        if (!match) {
            p.textContent = text;
            return p;
        }
        var start = match.index + match[1].length;
        p.appendChild(document.createTextNode(text.slice(0, start)));
        p.appendChild(el('strong', 'tv-hl', match[2]));
        p.appendChild(document.createTextNode(text.slice(start + match[2].length)));
        return p;
    }

    /** The word's picture: its emoji, or a letter tile. No remote images (plan P10). Decorative. */
    function picture(word, extraClass) {
        var box = el('span', 'tv-pic k-art-box' + (extraClass ? ' ' + extraClass : ''));
        box.setAttribute('aria-hidden', 'true');
        if (word.emoji) {
            box.appendChild(el('span', 'tv-pic__emoji', word.emoji));
        } else {
            box.classList.add('tv-pic--tile');
            box.appendChild(el('span', 'tv-pic__letter k-display', String(word.term || '?').charAt(0).toUpperCase()));
        }
        return box;
    }

    /** Five client-rendered dots (decorative), the same markup as the _LevelDots partial. */
    function dots(level) {
        var d = V.dotsFor(level);
        var stage = V.stageFor(level);
        var wrap = el('span', 'tv-dots tv-dots--' + stage);
        wrap.setAttribute('aria-hidden', 'true');
        for (var i = 1; i <= 5; i++) { wrap.appendChild(el('span', 'tv-dot' + (i <= d.filled ? ' is-on' : ''))); }
        return wrap;
    }

    function mmss(ms) {
        var s = Math.max(0, Math.round(ms / 1000));
        return Math.floor(s / 60) + ':' + ('0' + (s % 60)).slice(-2);
    }

    // ------------------------------------------------------------------ the schedule row (feedback panel)

    /**
     * A row under the answer card that tells when the word comes back. It is created empty and filled when the
     * server answers; the feedback host is a live region, so the text is announced without moving focus.
     */
    function scheduleRow(wordId, outcome) {
        var row = el('div', 'tv-sched');
        function title(result) {
            if (result.nextReviewLabel === UNCHANGED) { return UNCHANGED; }
            if (outcome === 'again') { return 'Từ này sẽ quay lại sớm'; }
            if (outcome === 'hard') { return 'Giữ nguyên bậc nhớ'; }
            return result.promoted ? 'Nhớ tốt lên một bậc' : 'Giữ nguyên bậc nhớ';
        }
        function fill(result) {
            row.textContent = '';
            row.classList.remove('is-error');
            row.appendChild(icon('fa-solid fa-calendar-check tv-sched__icon'));
            var text = el('span', 'tv-sched__title', title(result));
            row.appendChild(text);
            if (result.nextReviewLabel !== UNCHANGED) {
                row.appendChild(dots(result.level));
                row.appendChild(el('span', 'tv-sched__when' + (outcome === 'again' ? ' tv-sched__when--soon' : ''), result.nextReviewLabel));
            }
        }
        function fail() {
            row.textContent = '';
            row.classList.add('is-error');
            row.appendChild(icon('fa-solid fa-triangle-exclamation tv-sched__icon'));
            row.appendChild(el('span', 'tv-sched__title', 'Chưa lưu được tiến độ'));
            var retry = el('button', 'btn btn-sm btn-outline-primary tv-sched__retry', 'Thử lại');
            retry.type = 'button';
            retry.addEventListener('click', function () {
                retry.disabled = true;
                recordWord(wordId, done);
            });
            row.appendChild(retry);
        }
        function done(result, error) { if (result) { fill(result); } else if (error) { fail(); } }
        return { node: row, done: done };
    }

    // ------------------------------------------------------------------ flow

    function render() {
        var item = V.current(state);
        updateProgress();
        if (!item) { showResult(); return; }
        if (item.phase === 'intro') { showIntro(item.wordId); return; }
        showQuestion(item);
    }

    // ---- step 1: Làm quen

    function showIntro(wordId) {
        var word = state.byId[wordId];
        setPhase('intro');
        var wrap = el('div', 'tv-intro');

        var card = el('section', 'tv-wordcard k-panel');
        card.setAttribute('aria-label', 'Từ mới: ' + word.term);
        var learnedBefore = word.level !== null && word.level !== undefined;
        card.appendChild(el('span', 'tct-badge tct-badge--warning tv-wordcard__badge', learnedBefore ? (word.stageLabel || 'Đang nhớ') : 'Từ mới'));

        // One dot per word of this round; the current one is filled.
        var round = state.rounds[state.roundIndex] || [];
        var roundDots = el('span', 'tv-rdots');
        roundDots.setAttribute('aria-hidden', 'true');
        round.forEach(function (id, index) { roundDots.appendChild(el('span', 'tv-rdot' + (index === state.introIndex ? ' is-current' : ''))); });
        card.appendChild(roundDots);

        card.appendChild(picture(word));
        var term = el('p', 'tv-wordcard__term k-display tct-break', word.term);
        term.lang = 'en';
        card.appendChild(term);

        var ipa = el('div', 'tv-wordcard__ipa');
        if (word.phonetic) { ipa.appendChild(el('span', 'tct-phonetic', word.phonetic)); }
        ipa.appendChild(ui.speakButton(word.term));
        card.appendChild(ipa);

        card.appendChild(el('p', 'tv-wordcard__meaning tct-break', word.definition));

        if (word.example) {
            var example = el('div', 'tv-example tv-wordcard__example');
            example.appendChild(sentence(word.example, word.term));
            if (word.exampleVi) { example.appendChild(el('p', 'tv-example__vi', word.exampleVi)); }
            card.appendChild(example);
        }
        wrap.appendChild(card);

        var actions = el('div', 'tv-intro-actions');
        var next = el('button', 'btn btn-primary btn-lg tv-intro-next');
        next.type = 'button';
        next.appendChild(document.createTextNode('Tiếp tục'));
        next.appendChild(kbd('Enter', 'tv-desktop-only'));
        var known = el('button', 'btn k-btn--soft btn-lg tv-intro-known');
        known.type = 'button';
        known.appendChild(icon('fa-solid fa-check-double me-2'));
        known.appendChild(document.createTextNode('Tôi đã biết từ này'));
        actions.appendChild(next);
        actions.appendChild(known);
        wrap.appendChild(actions);

        var hints = ui.keyHints(['R nghe lại', 'Enter từ tiếp theo']);
        hints.classList.add('tv-desktop-only');
        wrap.appendChild(hints);

        function go() {
            V.introNext(state);
            render();
        }
        next.addEventListener('click', go);
        known.addEventListener('click', function () {
            V.introKnown(state);
            recordWord(wordId);
            render();
        });

        current = { kind: 'intro', speakText: word.term, next: go };
        session.render(wrap, true);
        next.focus({ preventScroll: false });
    }

    // ---- round change

    function showRoundBreak() {
        var info = V.roundInfo(state);
        var box = el('section', 'tv-roundbreak k-panel');
        box.setAttribute('aria-label', 'Chuyển sang nhóm từ tiếp theo');
        box.appendChild(icon('fa-solid fa-layer-group tv-roundbreak__icon'));
        var title = el('h2', 'tv-roundbreak__title', 'Tiếp theo: ' + info.size + ' từ nữa');
        box.appendChild(title);
        var go = el('button', 'btn btn-primary btn-lg', 'Tiếp tục');
        go.type = 'button';
        go.addEventListener('click', render);
        box.appendChild(go);
        current = { kind: 'break', next: render };
        setPhase(V.current(state) && V.current(state).phase === 'intro' ? 'intro' : 'practice');
        session.render(box, true);
        go.focus();
    }

    // ---- step 2: Luyện tập (or the review queue)

    /** "Từ mới", or the word's stage and how many times it has been reviewed (its level). */
    function memoryChip(word) {
        var chip = el('p', 'tv-memchip');
        if (word.level === null || word.level === undefined) {
            chip.appendChild(el('span', 'tv-memchip__text', 'Từ mới'));
            return chip;
        }
        chip.appendChild(dots(word.level));
        chip.appendChild(el('span', 'tv-memchip__text', (word.stageLabel || V.stageLabel(word.level)) + ' · lần ôn thứ ' + Math.max(1, word.level)));
        return chip;
    }

    function showQuestion(item) {
        var word = state.byId[item.wordId];
        var q = V.questionFor(state, item.wordId, null, Math.random);
        setPhase('practice');

        var card = el('section', 'tp-question');
        card.setAttribute('aria-label', (isReview ? 'Ôn từ ' : 'Luyện từ ') + (V.progress(state).finished + 1) + ' trên ' + V.progress(state).total);
        current = { kind: 'question', speakText: null, options: null, answered: false, typing: null };

        card.appendChild(memoryChip(word));

        if (q.kind === 'choice' && q.prompt === 'term') {
            card.appendChild(el('p', 'tp-prompt', 'Nghĩa của từ này là gì?'));
            card.appendChild(ui.termLine(word, { visual: false }));
            current.speakText = word.term;
        } else if (q.kind === 'choice') {
            card.appendChild(el('p', 'tp-prompt', 'Từ nào có nghĩa này?'));
            card.appendChild(ui.meaningLine(word));
        } else {
            card.appendChild(el('p', 'tp-prompt', 'Gõ từ tiếng Anh'));
            card.appendChild(ui.meaningLine(word));
        }

        function after(outcome, extra) {
            current.answered = true;
            var event = V.answer(state, item.wordId, outcome);
            session.noteAnswer();
            var panelExtra = extra;
            if (event.finished) {
                // The schedule row sits under the answer card once the word's outcome is final.
                var row = scheduleRow(item.wordId, event.outcome);
                var holder = el('div', 'tv-feedback-extra');
                if (extra) { holder.appendChild(extra); }
                holder.appendChild(row.node);
                panelExtra = holder;
                recordWord(item.wordId, row.done);
            }
            updateProgress();
            ui.feedbackPanel(session, outcome, word, {
                last: state.phase === 'result',
                extra: panelExtra,
                onNext: function () {
                    if (event.roundChanged && state.phase !== 'result') { showRoundBreak(); } else { render(); }
                }
            });
        }

        if (q.kind === 'typing') {
            var typing = ui.typingInput(word, {
                hints: true,
                onSubmit: function (text) {
                    var outcome = practice.checkTyping(text, word);
                    typing.lock();
                    after(outcome, outcome === 'correct' ? null : ui.diff(text, word.term));
                },
                onSkip: function () {
                    typing.lock();
                    after('wrong', null);
                }
            });
            card.appendChild(typing.root);
            current.typing = typing;
        } else {
            var controls = ui.options(q, {
                onPick: function (optionId) {
                    if (current.answered) { return; }
                    controls.reveal(optionId);
                    after(optionId === q.answerId ? 'correct' : 'wrong', null);
                }
            });
            current.options = controls;
            card.appendChild(controls.root);
            var hints = ['1–' + q.options.length + ' chọn đáp án'];
            if (current.speakText) { hints.push('R nghe lại'); }
            hints.push('Enter tiếp tục');
            var hintRow = ui.keyHints(hints);
            hintRow.classList.add('tv-desktop-only');
            card.appendChild(hintRow);
        }

        session.render(card, true);
        if (current.typing) { current.typing.focus(); } else { session.stage.focus({ preventScroll: true }); }
    }

    // ---- step 3: Kết quả

    function showResult() {
        setPhase('result');
        updateProgress();
        session.finish();
        current = null;
        var wait = el('p', 'tv-saving', 'Đang lưu tiến độ…');
        wait.setAttribute('role', 'status');
        session.render(wait);
        flush().then(renderResult);
    }

    function renderResult() {
        var s = V.summary(state);
        var kind = cfg.kind;
        var root = el('section', 'tv-result');
        root.setAttribute('aria-label', 'Kết quả');

        var head = el('div', 'tv-result__head');
        head.appendChild(ui.ring(s.accuracyPercent, 'trả lời đúng'));
        var title = el('h2', 'tv-result__title',
            kind === 'learn' ? 'Xong bài ' + cfg.lessonNumber + ' rồi!'
                : kind === 'relearn' ? 'Ôn xong bài ' + cfg.lessonNumber + '!'
                    : 'Ôn xong ' + s.total + ' từ!');
        title.tabIndex = -1;
        head.appendChild(title);
        var line = kind === 'learn'
            ? 'Bạn vừa học ' + s.total + ' từ mới. Hệ thống đã xếp lịch ôn cho từng từ.'
            : kind === 'relearn'
                ? 'Bạn vừa ôn ' + s.total + ' từ. Lịch ôn mới đã được xếp cho từng từ.'
                : 'Lịch ôn mới đã được xếp cho từng từ.';
        head.appendChild(el('p', 'tv-result__line', line));
        root.appendChild(head);

        var kpis = el('div', 'tv-kpis');
        var tiles = [[String(s.total), kind === 'learn' ? 'từ mới' : 'từ đã ôn', 'primary']];
        if (s.asked > 0) { tiles.push([s.right + '/' + s.asked, 'câu đúng', 'success']); }
        tiles.push([mmss(Date.now() - startedAt), 'thời gian', 'info']);
        if (s.xp > 0) { tiles.push(['+' + s.xp, 'XP', 'accent']); }
        tiles.forEach(function (tile) {
            var box = el('div', 'tv-kpi k-soft tv-hue-' + tile[2]);
            box.appendChild(el('span', 'tv-kpi__value', tile[0]));
            box.appendChild(el('span', 'tv-kpi__label', tile[1]));
            kpis.appendChild(box);
        });
        root.appendChild(kpis);

        var list = el('ul', 'tv-resultlist k-panel tv-list');
        list.setAttribute('aria-label', 'Các từ trong phiên');
        var byWord = {};
        s.words.forEach(function (w) { byWord[w.id] = w; });
        state.rounds.reduce(function (all, r) { return all.concat(r); }, []).forEach(function (id) {
            var word = state.byId[String(id)];
            var info = byWord[id] || {};
            var row = el('li', 'tv-resultrow');
            row.appendChild(picture(word, 'tv-pic--mini'));
            var term = el('span', 'tv-resultrow__term', word.term);
            term.lang = 'en';
            row.appendChild(term);
            row.appendChild(el('span', 'tv-resultrow__def tct-break', word.definition));
            row.appendChild(info.saved ? dots(info.level) : el('span', 'tv-resultrow__dots'));
            var label = info.saved ? info.label : 'Chưa lưu được';
            var soon = info.saved && /10 phút/.test(info.label || '');
            row.appendChild(el('span', 'tv-resultrow__label' + (soon || !info.saved ? ' tv-resultrow__label--soon' : ''), label));
            list.appendChild(row);
        });
        root.appendChild(list);

        var actions = el('div', 'tv-result__actions');
        function link(label, href, cls, iconClass) {
            var a = el('a', 'btn btn-lg ' + cls);
            a.href = href;
            if (iconClass) { a.appendChild(icon(iconClass + ' me-2')); }
            a.appendChild(document.createTextNode(label));
            actions.appendChild(a);
        }
        var back = isReview ? ['Về trang Từ vựng', cfg.homeUrl] : ['Về khóa học', cfg.courseUrl];
        var more = Number(cfg.remainingDue) || 0;
        if (more > 0) {
            link('Ôn ' + more + ' từ đến hạn', cfg.reviewUrl || '/Vocabulary/Review', 'btn-primary', 'fa-solid fa-rotate');
            link(back[0], back[1], 'btn-outline-primary');
        } else if (cfg.nextLessonUrl) {
            link('Học bài tiếp theo', cfg.nextLessonUrl, 'btn-primary', 'fa-solid fa-play');
            link(back[0], back[1], 'btn-outline-primary');
        } else {
            link(back[0], back[1], 'btn-primary');
        }
        root.appendChild(actions);

        if (kind !== 'review' && cfg.relearnUrl) {
            var again = el('p', 'tv-result__again');
            var a = el('a', 'tv-result__again-link', 'Học lại bài này');
            a.href = cfg.relearnUrl;
            again.appendChild(a);
            root.appendChild(again);
        }

        session.setCounter('');
        session.render(root);
        title.focus();
    }

    // ------------------------------------------------------------------ keyboard

    ui.onKeys(function (event, key) {
        if (!current) { return false; }
        if ((key === 'r' || key === 'R') && current.speakText) {
            ui.speak(current.speakText);
            return true;
        }
        if (current.kind === 'intro' && key === 'Enter' && !(event.target && /^(BUTTON|A|SUMMARY)$/.test(event.target.tagName))) {
            current.next();
            return true;
        }
        if (current.options && !current.answered) {
            var index = /^[1-9]$/.test(key) ? Number(key) - 1 : /^[a-dA-D]$/.test(key) ? key.toUpperCase().charCodeAt(0) - 65 : -1;
            var button = index >= 0 ? current.options.buttons[index] : null;
            if (button) { button.click(); return true; }
        }
        return false;
    });

    render();
}());
