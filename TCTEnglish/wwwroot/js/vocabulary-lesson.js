/*
 * System Vocabulary lesson screen: 1 Làm quen -> 2 Luyện tập -> 3 Kết quả.
 * The session logic lives in vocabulary-core.js; the shell, option buttons, typing input and feedback panel come from
 * practice-ui.js. Progress is saved only through the existing POST /api/LearningApi/record, once per finished word.
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
    var state = V.initSession({ kind: cfg.kind, words: cfg.words, pool: cfg.pool || [], rng: Math.random });
    var startedAt = Date.now();
    var current = null;   // what the keyboard handler may act on: { kind, speakText, next, options, answered }

    // ------------------------------------------------------------------ saving (one record call per finished word)

    var saves = { pending: [], failed: [], xp: 0, warned: false };

    function post(cardId, grade) {
        return T.fetchJson('/api/LearningApi/record', {
            method: 'POST',
            body: { cardId: cardId, masteryLevel: grade, timestamp: new Date().toISOString() }
        });
    }

    function saved(cardId, result) {
        V.markSaved(state, cardId, true);
        saves.xp += (result && Number(result.xpEarned)) || 0;
    }

    /** Fire and forget: one immediate retry, then the word waits in the retry list until the result screen. */
    function save(cardId, grade) {
        var promise = post(cardId, grade)
            .catch(function () { return post(cardId, grade); })
            .then(function (result) { saved(cardId, result); })
            .catch(function () { saves.failed.push({ id: cardId, grade: grade }); });
        saves.pending.push(promise);
        return promise;
    }

    function flush() {
        return Promise.all(saves.pending).then(function () {
            var retry = saves.failed.splice(0);
            return Promise.all(retry.map(function (item) {
                return post(item.id, item.grade)
                    .then(function (result) { saved(item.id, result); })
                    .catch(function () { saves.failed.push(item); });
            }));
        }).then(function () {
            if (saves.failed.length > 0 && !saves.warned) {
                saves.warned = true;
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

    /** The word's picture, or a letter tile when there is none or it fails to load. */
    function picture(word) {
        var box = el('span', 'tv-pic k-art-box');
        box.setAttribute('aria-hidden', 'true');
        function tile() {
            box.textContent = '';
            box.classList.add('tv-pic--tile');
            box.appendChild(el('span', 'tv-pic__letter k-display', String(word.term || '?').charAt(0).toUpperCase()));
        }
        if (word.imageUrl) {
            var img = document.createElement('img');
            img.alt = '';
            img.loading = 'lazy';
            img.decoding = 'async';
            img.addEventListener('error', tile);
            img.src = word.imageUrl;
            box.appendChild(img);
        } else {
            tile();
        }
        return box;
    }

    function mmss(ms) {
        var s = Math.max(0, Math.round(ms / 1000));
        return Math.floor(s / 60) + ':' + ('0' + (s % 60)).slice(-2);
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
        card.appendChild(el('span', 'tct-badge tct-badge--warning tv-wordcard__badge', 'Từ mới'));
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
        var known = el('button', 'btn btn-outline-primary btn-lg tv-intro-known');
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
            save(wordId, 'easy');
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

    // ---- step 2: Luyện tập

    function showQuestion(item) {
        var word = state.byId[item.wordId];
        var q = V.questionFor(state, item.wordId, null, Math.random);
        setPhase('practice');

        var card = el('section', 'tp-question');
        card.setAttribute('aria-label', 'Luyện từ ' + (V.progress(state).finished + 1) + ' trên ' + cfg.words.length);
        current = { kind: 'question', speakText: null, options: null, answered: false, typing: null };

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
            if (event.finished) { save(item.wordId, event.grade); }
            updateProgress();
            ui.feedbackPanel(session, outcome, word, {
                last: state.phase === 'result',
                extra: extra,
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

    function badgeFor(word) {
        var s = state.words[word.id];
        if (!s.saved) { return { cls: 'tct-badge--danger', icon: 'fa-solid fa-cloud-arrow-up', text: 'Chưa lưu' }; }
        if (s.known) { return { cls: 'tct-badge--info', icon: 'fa-solid fa-check-double', text: 'Đã biết' }; }
        if (s.grade === 'hard') { return { cls: 'tct-badge--warning', icon: 'fa-solid fa-rotate', text: 'Cần ôn lại' }; }
        return { cls: 'tct-badge--success', icon: 'fa-solid fa-check', text: 'Đã nhớ' };
    }

    function renderResult() {
        var s = V.summary(state);
        var kind = cfg.kind;
        var root = el('section', 'tv-result');
        root.setAttribute('aria-label', 'Kết quả');

        var head = el('div', 'tv-result__head');
        head.appendChild(ui.ring(s.accuracyPercent, 'trả lời đúng'));
        var title = el('h2', 'tv-result__title',
            kind === 'learn' ? 'Xong bài ' + cfg.lessonNumber + ' rồi!' : kind === 'relearn' ? 'Ôn xong bài ' + cfg.lessonNumber + '!' : 'Ôn tập xong!');
        title.tabIndex = -1;
        head.appendChild(title);
        var line = kind === 'learn'
            ? 'Bạn vừa học ' + s.total + ' từ mới.' + (s.needReview > 0 ? ' ' + s.needReview + ' từ sẽ được nhắc ôn lại sớm.' : '')
            : 'Bạn vừa ôn ' + s.total + ' từ.';
        head.appendChild(el('p', 'tv-result__line', line));
        root.appendChild(head);

        var kpis = el('div', 'tv-kpis');
        var tiles = [[String(s.total), kind === 'learn' ? 'từ mới' : 'từ đã ôn', 'primary']];
        if (s.asked > 0) { tiles.push([s.right + '/' + s.asked, 'câu đúng', 'success']); }
        tiles.push([mmss(Date.now() - startedAt), 'thời gian', 'info']);
        if (saves.xp > 0) { tiles.push(['+' + saves.xp + ' XP', 'điểm kinh nghiệm', 'accent']); }
        tiles.forEach(function (tile) {
            var box = el('div', 'tv-kpi k-soft tv-hue-' + tile[2]);
            box.appendChild(el('span', 'tv-kpi__value', tile[0]));
            box.appendChild(el('span', 'tv-kpi__label', tile[1]));
            kpis.appendChild(box);
        });
        root.appendChild(kpis);

        var list = el('ul', 'tv-resultlist k-panel tv-list');
        list.setAttribute('aria-label', 'Các từ trong bài');
        cfg.words.forEach(function (word) {
            var row = el('li', 'tv-resultrow');
            var term = el('span', 'tv-resultrow__term', word.term);
            term.lang = 'en';
            row.appendChild(term);
            row.appendChild(el('span', 'tct-phonetic tv-resultrow__ipa', word.phonetic || ''));
            row.appendChild(el('span', 'tv-resultrow__def tct-break', word.definition));
            var badge = badgeFor(word);
            var chip = el('span', 'tct-badge tv-resultrow__badge ' + badge.cls);
            chip.appendChild(icon(badge.icon));
            chip.appendChild(document.createTextNode(badge.text));
            row.appendChild(chip);
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
        var courseIsHome = cfg.courseUrl === cfg.homeUrl;
        if (cfg.nextLessonUrl) {
            link('Học bài ' + cfg.nextLessonNumber, cfg.nextLessonUrl, 'btn-primary', 'fa-solid fa-play');
            link('Về khóa học', cfg.courseUrl, 'btn-outline-primary');
        } else if (kind === 'review') {
            link('Về trang Từ vựng', cfg.homeUrl, 'btn-primary');
            if (!courseIsHome) { link('Về khóa học', cfg.courseUrl, 'btn-outline-primary'); }
        } else {
            link('Về khóa học', cfg.courseUrl, 'btn-primary');
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
