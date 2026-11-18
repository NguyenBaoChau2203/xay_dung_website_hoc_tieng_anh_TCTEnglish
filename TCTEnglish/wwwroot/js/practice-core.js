/*
 * Practice modes: pure logic (no DOM). UMD: module.exports under Node, window.TctPractice.core in the browser.
 * Every random step takes an injectable rng (default Math.random). Strings shown to users are Vietnamese literals.
 * Mutating helpers (Learn state) change the state they receive and also return it.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.TctPractice = root.TctPractice || {};
        root.TctPractice.core = factory();
    }
}(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    // ------------------------------------------------------------------ registry

    var MODE_INFO = {
        learn: { label: 'Học', hint: 'Học theo vòng tới khi thuộc hết bộ từ.' },
        test: { label: 'Kiểm tra', hint: 'Làm bài trộn nhiều dạng câu, chấm điểm cuối bài.' },
        match: { label: 'Ghép thẻ', hint: 'Ghép từ với nghĩa nhanh nhất có thể.' },
        flashcard: { label: 'Lật thẻ', hint: 'Lật qua lật lại, tự đánh dấu thuộc hay chưa.' },
        quiz: { label: 'Trắc nghiệm', hint: 'Chọn nghĩa đúng của từ, hoặc từ đúng với nghĩa.' },
        typing: { label: 'Gõ từ', hint: 'Nhìn nghĩa, gõ lại từ.' },
        listening: { label: 'Nghe', hint: 'Nghe đọc rồi chọn từ đúng.' },
        cloze: { label: 'Điền từ', hint: 'Chọn từ còn thiếu trong câu ví dụ.' }
    };
    var STUDY_GAMES = ['learn', 'test', 'match', 'flashcard'];
    var SKILLS = ['quiz', 'typing', 'listening', 'cloze'];

    var COUNTS = {
        flashcard: [20, 50, 0],
        match: [6, 12, 18],
        learn: [10, 20, 50],
        test: [10, 20, 30],
        quiz: [10, 20, 30],
        typing: [10, 20, 30],
        listening: [10, 20, 30],
        cloze: [10, 20, 30]
    };
    var COUNT_UNIT = {
        flashcard: { legend: 'Số thẻ', unit: 'thẻ' },
        match: { legend: 'Số cặp', unit: 'cặp' },
        learn: { legend: 'Số từ', unit: 'từ' },
        'default': { legend: 'Số câu', unit: 'câu' }
    };

    function countsFor(mode) { return (COUNTS[mode] || COUNTS.quiz).slice(); }
    function countUnit(mode) { return COUNT_UNIT[mode] || COUNT_UNIT['default']; }

    var TEST_TYPES = ['choice', 'tf', 'typing', 'cloze'];
    var DEFAULT_TEST_TYPES = ['choice', 'tf', 'typing'];
    var TEST_TYPE_LABEL = { choice: 'Trắc nghiệm', tf: 'Đúng / Sai', typing: 'Gõ từ', cloze: 'Điền từ' };
    var TEST_ASK = ['both', 'term', 'meaning'];
    var TEST_ASK_LABEL = { both: 'Trộn', term: 'Từ', meaning: 'Nghĩa' };

    // ------------------------------------------------------------------ helpers

    /** Seeded generator for tests and for rebuilding the same test after a reload. */
    function mulberry32(seed) {
        var a = seed >>> 0;
        return function () {
            a = (a + 0x6D2B79F5) >>> 0;
            var t = a;
            t = Math.imul(t ^ (t >>> 15), t | 1);
            t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
            return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
        };
    }

    function shuffle(list, rng) {
        var r = rng || Math.random;
        var out = Array.prototype.slice.call(list || []);
        for (var i = out.length - 1; i > 0; i--) {
            var j = Math.floor(r() * (i + 1));
            var tmp = out[i]; out[i] = out[j]; out[j] = tmp;
        }
        return out;
    }

    function textKey(s) {
        return String(s == null ? '' : s).normalize('NFC').trim().toLowerCase().replace(/\s+/g, ' ');
    }

    function levenshtein(a, b) {
        var x = Array.from(String(a)); var y = Array.from(String(b));
        if (x.length === 0) { return y.length; }
        if (y.length === 0) { return x.length; }
        var prev = []; var i; var j;
        for (j = 0; j <= y.length; j++) { prev[j] = j; }
        for (i = 1; i <= x.length; i++) {
            var cur = [i];
            for (j = 1; j <= y.length; j++) {
                var cost = x[i - 1] === y[j - 1] ? 0 : 1;
                cur[j] = Math.min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + cost);
            }
            prev = cur;
        }
        return prev[y.length];
    }

    function cleanTyping(s) {
        return String(s == null ? '' : s).normalize('NFC').toLowerCase()
            .replace(/[’`]/g, "'")
            .trim()
            .replace(/[.!?,;:]+$/, '')
            .replace(/\s+/g, ' ')
            .trim();
    }

    /** "correct" | "close" | "wrong". Close = one typo in a word of at least 6 letters. */
    function checkTyping(input, word) {
        var term = typeof word === 'string' ? word : (word && word.term);
        var typed = cleanTyping(input);
        var expected = cleanTyping(term);
        if (!typed) { return 'wrong'; }
        if (typed === expected) { return 'correct'; }
        var letters = Array.from(expected.replace(/\s+/g, '')).length;
        if (letters >= 6 && levenshtein(typed, expected) <= 1) { return 'close'; }
        return 'wrong';
    }

    function sharesPos(a, b) {
        var x = a.partsOfSpeech || []; var y = b.partsOfSpeech || [];
        for (var i = 0; i < x.length; i++) { if (y.indexOf(x[i]) >= 0) { return true; } }
        return false;
    }

    function dedupeById(list) {
        var seen = {}; var out = [];
        (list || []).forEach(function (w) {
            if (w && !seen[w.id]) { seen[w.id] = true; out.push(w); }
        });
        return out;
    }

    /** Up to `count` words from the pool that look like the target (same part of speech +2, same topic +1), all distinct in term and meaning. */
    function pickDistractors(target, pool, rng, count) {
        var wanted = count == null ? 3 : count;
        var shuffled = shuffle(pool || [], rng).filter(function (w) { return w && w.id !== target.id; });
        var scored = shuffled.map(function (w, i) {
            var score = 0;
            if (sharesPos(target, w)) { score += 2; }
            if (target.topic && w.topic && textKey(target.topic) === textKey(w.topic)) { score += 1; }
            return { w: w, score: score, i: i };
        });
        scored.sort(function (a, b) { return b.score - a.score || a.i - b.i; });
        var usedTerms = {}; var usedDefs = {};
        usedTerms[textKey(target.term)] = true;
        usedDefs[textKey(target.definition)] = true;
        var out = [];
        for (var k = 0; k < scored.length && out.length < wanted; k++) {
            var w = scored[k].w;
            var t = textKey(w.term); var d = textKey(w.definition);
            if (usedTerms[t] || usedDefs[d]) { continue; }
            usedTerms[t] = true; usedDefs[d] = true;
            out.push(w);
        }
        return out;
    }

    function choiceOptions(target, pool, rng) {
        return shuffle([target].concat(pickDistractors(target, pool, rng, 3)), rng);
    }

    function englishForms(word) {
        var forms = [];
        function add(f) { if (f && String(f).trim()) { forms.push(String(f).trim()); } }
        var term = String(word.term || '').trim();
        add(term);
        var vf = word.verbForms || {};
        add(vf.past); add(vf.pastParticiple); add(vf.ing); add(vf.thirdPerson);
        add(word.plural);
        if (term && !/\s/.test(term)) {
            var base = term.replace(/e$/i, '');
            add(term + 's'); add(term + 'es'); add(term + 'd'); add(term + 'ed');
            add(base + 'ing'); add(term + 'ing');
            if (/y$/i.test(term)) { add(term.slice(0, -1) + 'ies'); }
        }
        var seen = {}; var out = [];
        forms.forEach(function (f) { var k = f.toLowerCase(); if (!seen[k]) { seen[k] = true; out.push(f); } });
        out.sort(function (a, b) { return b.length - a.length; });
        return out;
    }

    function escapeRegex(s) { return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }

    /** First example sentence that contains the word or one of its forms -> { before, answer, after, vi } or null. */
    function makeCloze(word) {
        var examples = word.examples || [];
        var forms = englishForms(word);
        for (var i = 0; i < examples.length; i++) {
            var text = String(examples[i] && examples[i].text || '');
            if (!text) { continue; }
            for (var j = 0; j < forms.length; j++) {
                var pattern = escapeRegex(forms[j]).replace(/\s+/g, '\\s+');
                var re = new RegExp("(^|[^\\p{L}'])(" + pattern + ")(?![\\p{L}])", 'iu');
                var m = re.exec(text);
                if (m) {
                    var start = m.index + m[1].length;
                    return {
                        before: text.slice(0, start),
                        answer: m[2],
                        after: text.slice(start + m[2].length),
                        vi: examples[i].vi || null
                    };
                }
            }
        }
        return null;
    }

    function fitsMode(word, mode) {
        return mode === 'cloze' ? makeCloze(word) !== null : true;
    }

    // ------------------------------------------------------------------ drills

    function optionFor(word, label) { return { id: String(word.id), label: label }; }

    function typingQuestion(word) { return { id: word.id + ':typing', kind: 'typing', word: word }; }

    function choiceQuestion(word, prompt, pool, rng, kindId) {
        var options = choiceOptions(word, pool, rng);
        if (options.length < 2) { return typingQuestion(word); }
        return {
            id: word.id + ':' + (kindId || 'choice') + ':' + prompt,
            kind: 'choice',
            prompt: prompt,
            word: word,
            options: options.map(function (o) { return optionFor(o, prompt === 'term' ? o.definition : o.term); }),
            answerId: String(word.id)
        };
    }

    function clozeQuestion(word, cloze, pool, rng, kindId) {
        var answerKey = textKey(cloze.answer);
        var candidates = (pool || []).filter(function (w) { return textKey(w.term) !== answerKey; });
        var distractors = pickDistractors(word, candidates, rng, 3);
        if (distractors.length < 1) { return typingQuestion(word); }
        var options = shuffle([{ id: String(word.id), label: cloze.answer }].concat(distractors.map(function (w) { return optionFor(w, w.term); })), rng);
        return { id: word.id + ':' + (kindId || 'cloze'), kind: 'cloze', word: word, cloze: cloze, options: options, answerId: String(word.id) };
    }

    /** Drill questions for quiz / typing / listening / cloze. Targets are shuffled; unusable words (cloze without an example) are dropped. */
    function buildQuestions(mode, targets, pool, rng) {
        var r = rng || Math.random;
        var all = dedupeById((targets || []).concat(pool || []));
        var words = shuffle(dedupeById(targets || []).filter(function (w) { return fitsMode(w, mode); }), r);
        return words.map(function (word) {
            if (mode === 'typing') { return typingQuestion(word); }
            if (mode === 'listening') { return choiceQuestion(word, 'audio', all, r); }
            if (mode === 'cloze') { return clozeQuestion(word, makeCloze(word), all, r); }
            return choiceQuestion(word, r() < 0.5 ? 'term' : 'meaning', all, r);
        });
    }

    // ------------------------------------------------------------------ test

    function parseTestTypes(raw) {
        var list = Array.isArray(raw) ? raw : String(raw == null ? '' : raw).split(',');
        var picked = list.map(function (x) { return String(x).trim().toLowerCase(); });
        var ordered = TEST_TYPES.filter(function (t) { return picked.indexOf(t) >= 0; });
        return ordered.length ? ordered : DEFAULT_TEST_TYPES.slice();
    }

    function parseTestAsk(raw) {
        var v = String(raw == null ? '' : raw).trim().toLowerCase();
        return TEST_ASK.indexOf(v) >= 0 ? v : 'both';
    }

    function pickPrompt(ask, rng) {
        if (ask === 'term') { return 'term'; }
        if (ask === 'meaning') { return 'meaning'; }
        return rng() < 0.5 ? 'term' : 'meaning';
    }

    function testQuestion(type, word, all, ask, rng) {
        if (type === 'choice') {
            return choiceQuestion(word, pickPrompt(ask, rng), all, rng);
        }
        if (type === 'tf') {
            var other = pickDistractors(word, all, rng, 1)[0];
            if (!other) { return typingQuestion(word); }
            var isTrue = rng() < 0.5;
            var prompt = pickPrompt(ask, rng);
            var shown = prompt === 'term'
                ? (isTrue ? word.definition : other.definition)
                : (isTrue ? word.term : other.term);
            return { id: word.id + ':tf:' + prompt, kind: 'truefalse', prompt: prompt, word: word, shown: shown, isTrue: isTrue };
        }
        if (type === 'cloze') {
            var cloze = makeCloze(word);
            return cloze ? clozeQuestion(word, cloze, all, rng) : typingQuestion(word);
        }
        return typingQuestion(word);
    }

    /**
     * Mixed test. `targets` may hold more words than `count` (the server sends 2n when cloze is selected).
     * Quotas: floor(total / types) each, remainder to random types; cloze words are taken first and an unfilled
     * cloze quota is handed to the other types.
     */
    function buildTest(targets, pool, options, rng) {
        var r = rng || Math.random;
        var opts = options || {};
        var words = shuffle(dedupeById(targets || []), r);
        var total = Math.min(opts.count == null ? words.length : opts.count, words.length);
        var all = dedupeById((targets || []).concat(pool || []));
        var ask = parseTestAsk(opts.ask);
        var chosen = parseTestTypes(opts.types);
        if (all.length < 2) {
            chosen = chosen.filter(function (t) { return t !== 'choice' && t !== 'tf'; });
            if (chosen.length === 0) { chosen = ['typing']; }
        }
        if (total === 0) { return []; }

        var quota = {};
        var per = Math.floor(total / chosen.length);
        chosen.forEach(function (t) { quota[t] = per; });
        shuffle(chosen, r).slice(0, total - per * chosen.length).forEach(function (t) { quota[t] += 1; });

        var assignments = [];   // { word, type }
        var used = {};
        if (quota.cloze) {
            var fits = words.filter(function (w) { return makeCloze(w) !== null; }).slice(0, quota.cloze);
            fits.forEach(function (w) { assignments.push({ word: w, type: 'cloze' }); used[w.id] = true; });
            var unfilled = quota.cloze - fits.length;
            quota.cloze = 0;
            if (unfilled > 0) {
                var others = chosen.filter(function (t) { return t !== 'cloze'; });
                if (others.length === 0) { others = [all.length >= 2 ? 'choice' : 'typing']; }
                for (var u = 0; u < unfilled; u++) {
                    var t = others[u % others.length];
                    quota[t] = (quota[t] || 0) + 1;
                }
            }
        }

        var order = chosen.filter(function (t) { return t !== 'cloze'; });
        Object.keys(quota).forEach(function (t) { if (order.indexOf(t) < 0 && quota[t] > 0) { order.push(t); } });
        var remaining = words.filter(function (w) { return !used[w.id]; }).slice(0, total - assignments.length);
        var cursor = 0;
        order.forEach(function (t) {
            for (var n = 0; n < (quota[t] || 0) && cursor < remaining.length; n++) {
                assignments.push({ word: remaining[cursor++], type: t });
            }
        });
        while (cursor < remaining.length) { assignments.push({ word: remaining[cursor++], type: 'typing' }); }

        return shuffle(assignments.map(function (a) { return testQuestion(a.type, a.word, all, ask, r); }), r);
    }

    function optionLabel(q, id) {
        var found = (q.options || []).filter(function (o) { return o.id === id; })[0];
        return found ? found.label : '';
    }

    function answerText(q, answer) {
        if (q.kind === 'truefalse') {
            if (answer === 'true') { return 'Đúng'; }
            if (answer === 'false') { return 'Sai'; }
            return '';
        }
        if (q.kind === 'typing') { return String(answer == null ? '' : answer).trim(); }
        return optionLabel(q, answer);
    }

    function expectedText(q) {
        if (q.kind === 'truefalse') {
            if (q.isTrue) { return 'Đúng'; }
            return 'Sai (' + q.word.term + ' — ' + q.word.definition + ')';
        }
        if (q.kind === 'typing') { return q.word.term + (q.word.phonetic ? ' · ' + q.word.phonetic : ''); }
        if (q.kind === 'cloze') { return q.cloze.answer; }
        return optionLabel(q, q.answerId);
    }

    /** answers: { [questionId]: optionId | "true" | "false" | typed text }. "close" typing counts as correct. */
    function gradeTest(questions, answers) {
        var given = answers || {};
        var correct = 0;
        var items = questions.map(function (q) {
            var answer = given[q.id];
            var blank = answer == null || String(answer).trim() === '';
            var result = 'wrong';
            if (!blank) {
                if (q.kind === 'typing') { result = checkTyping(answer, q.word); }
                else if (q.kind === 'truefalse') { result = (String(answer) === 'true') === q.isTrue ? 'correct' : 'wrong'; }
                else { result = String(answer) === q.answerId ? 'correct' : 'wrong'; }
            }
            if (result !== 'wrong') { correct++; }
            return { question: q, answer: blank ? '' : answer, result: result };
        });
        return {
            correct: correct,
            total: questions.length,
            percent: questions.length ? Math.round(100 * correct / questions.length) : 0,
            items: items
        };
    }

    function testRemark(percent) {
        if (percent >= 90) { return 'Xuất sắc!'; }
        if (percent >= 70) { return 'Khá lắm!'; }
        return 'Cần ôn thêm một chút.';
    }

    /** Stable id for a built test (hash of its question ids), used to key the session draft. */
    function testId(questions) {
        var h = 5381;
        var s = questions.map(function (q) { return q.id; }).join('|');
        for (var i = 0; i < s.length; i++) { h = ((h << 5) + h + s.charCodeAt(i)) | 0; }
        return (h >>> 0).toString(36);
    }

    // ------------------------------------------------------------------ learn

    var LEARN_ROUND_SIZE = 7;
    var LEARN_VERSION = 1;

    function initLearn(ids) {
        return {
            version: LEARN_VERSION,
            words: (ids || []).map(function (id) { return { wordId: id, level: 0, misses: 0 }; }),
            round: 0,
            queue: [],
            roundWords: [],
            roundStars: []
        };
    }

    function learnWord(state, id) {
        for (var i = 0; i < state.words.length; i++) { if (state.words[i].wordId === id) { return state.words[i]; } }
        return null;
    }

    function levelOf(state, id) {
        var w = learnWord(state, id);
        return w ? w.level : 0;
    }

    function isSeen(w) { return w.level > 0 || w.misses > 0; }

    function selectRound(state, size) {
        var open = state.words.filter(function (w) { return w.level < 2; });
        var seen = open.filter(isSeen);
        var fresh = open.filter(function (w) { return !isSeen(w); });
        return seen.concat(fresh).slice(0, size || LEARN_ROUND_SIZE);
    }

    /** Takes up to `size` unmastered words (seen first, then new, in study order) and queues them. */
    function startRound(state, size) {
        var picked = selectRound(state, size).map(function (w) { return w.wordId; });
        state.round += 1;
        state.queue = picked.slice();
        state.roundWords = picked.slice();
        return state;
    }

    function previewRound(state, size) {
        var picked = selectRound(state, size);
        var fresh = picked.filter(function (w) { return !isSeen(w); }).length;
        return { ids: picked.map(function (w) { return w.wordId; }), fresh: fresh, review: picked.length - fresh };
    }

    function questionFor(state, word, pool, rng) {
        var r = rng || Math.random;
        if (levelOf(state, word.id) >= 1) { return typingQuestion(word); }
        return choiceQuestion(word, r() < 0.5 ? 'term' : 'meaning', dedupeById(pool || []), r);
    }

    /** Correct: level +1 (max 2). Wrong: level 0, misses +1, re-queued at the end. Ids not in the queue are ignored. */
    function answer(state, wordId, correct) {
        var at = state.queue.indexOf(wordId);
        if (at < 0) { return state; }
        var w = learnWord(state, wordId);
        state.queue.splice(at, 1);
        if (!w) { return state; }
        if (correct) {
            w.level = Math.min(2, w.level + 1);
        } else {
            w.level = 0;
            w.misses += 1;
            state.queue.push(wordId);
        }
        return state;
    }

    function roundDone(state) { return state.round > 0 && state.queue.length === 0; }

    function learnComplete(state) {
        return state.words.length > 0 && state.words.every(function (w) { return w.level >= 2; });
    }

    function learnProgress(state) {
        var p = { notStarted: 0, learning: 0, mastered: 0 };
        state.words.forEach(function (w) {
            if (w.level >= 2) { p.mastered++; }
            else if (w.level === 1 || w.misses > 0) { p.learning++; }
            else { p.notStarted++; }
        });
        return p;
    }

    function learnPercent(state) {
        return state.words.length ? Math.round(100 * learnProgress(state).mastered / state.words.length) : 0;
    }

    function starsFor(right, total) {
        if (!total) { return 1; }
        var pct = 100 * right / total;
        if (pct >= 90) { return 3; }
        if (pct >= 70) { return 2; }
        return 1;
    }

    function recordRound(state, stars) {
        state.roundStars.push(stars);
        return state;
    }

    function roundsDone(state) { return state.roundStars.length; }

    function stageCount(state) {
        if (learnComplete(state)) { return roundsDone(state); }
        var open = state.words.filter(function (w) { return w.level < 2; }).length;
        return roundsDone(state) + Math.ceil(open / LEARN_ROUND_SIZE);
    }

    function masteryDots(state, id) {
        var w = learnWord(state, id);
        if (!w) { return 0; }
        if (w.level >= 2) { return 3; }
        if (w.level === 1) { return 2; }
        return w.misses > 0 ? 1 : 0;
    }

    /** Re-validate a saved state against the words available now. Returns null for another version or an empty result. */
    function restoreLearn(saved, availableIds, targetIds, limit) {
        if (!saved || typeof saved !== 'object' || saved.version !== LEARN_VERSION || !Array.isArray(saved.words)) { return null; }
        var available = {};
        (availableIds || []).forEach(function (id) { available[id] = true; });
        var words = saved.words.filter(function (w) { return w && available[w.wordId]; })
            .map(function (w) { return { wordId: w.wordId, level: Math.max(0, Math.min(2, w.level | 0)), misses: Math.max(0, w.misses | 0) }; });
        var have = {};
        words.forEach(function (w) { have[w.wordId] = true; });
        var cap = limit == null ? Infinity : limit;
        (targetIds || []).forEach(function (id) {
            if (words.length < cap && !have[id]) { words.push({ wordId: id, level: 0, misses: 0 }); have[id] = true; }
        });
        if (words.length === 0) { return null; }
        function alive(id) { return !!have[id]; }
        return {
            version: LEARN_VERSION,
            words: words,
            round: saved.round | 0,
            queue: (saved.queue || []).filter(alive),
            roundWords: (saved.roundWords || []).filter(alive),
            roundStars: (saved.roundStars || []).filter(function (s) { return s >= 1 && s <= 3; })
        };
    }

    // ------------------------------------------------------------------ match

    var MATCH_PAIRS_PER_BOARD = 6;
    var MATCH_PENALTY_MS = 1000;

    /** Boards of up to 6 pairs. A board never holds two words with the same term or meaning; a board with fewer than 2 pairs is dropped. */
    function buildMatchBoards(words, pairs, rng) {
        var r = rng || Math.random;
        var picked = shuffle(dedupeById(words || []), r).slice(0, pairs == null ? undefined : pairs);
        var boards = [];
        picked.forEach(function (w) {
            var tk = textKey(w.term); var dk = textKey(w.definition);
            for (var i = 0; i < boards.length; i++) {
                var b = boards[i];
                if (b.words.length < MATCH_PAIRS_PER_BOARD && !b.terms[tk] && !b.defs[dk]) {
                    b.words.push(w); b.terms[tk] = true; b.defs[dk] = true;
                    return;
                }
            }
            var fresh = { words: [w], terms: {}, defs: {} };
            fresh.terms[tk] = true; fresh.defs[dk] = true;
            boards.push(fresh);
        });
        return boards.filter(function (b) { return b.words.length >= 2; }).map(function (b, index) {
            var tiles = [];
            b.words.forEach(function (w) {
                tiles.push({ id: w.id + ':t', pairId: w.id, side: 'term', label: w.term });
                tiles.push({ id: w.id + ':m', pairId: w.id, side: 'meaning', label: w.definition });
            });
            return { index: index, pairIds: b.words.map(function (w) { return w.id; }), tiles: shuffle(tiles, r) };
        });
    }

    function isPair(a, b) {
        return !!a && !!b && a.pairId === b.pairId && a.side !== b.side;
    }

    /** "12,3 giây" or "1 phút 05,2 giây" (tenths, comma decimal). */
    function formatMs(ms) {
        var tenths = Math.max(0, Math.round((ms || 0) / 100));
        if (tenths < 600) { return (tenths / 10).toFixed(1).replace('.', ',') + ' giây'; }
        var minutes = Math.floor(tenths / 600);
        var rest = tenths - minutes * 600;
        var secs = (rest / 10).toFixed(1).replace('.', ',');
        return minutes + ' phút ' + (secs.length < 4 ? '0' + secs : secs) + ' giây';
    }

    /** mm:ss for session summaries. */
    function formatDuration(ms) {
        var total = Math.max(0, Math.round((ms || 0) / 1000));
        var m = Math.floor(total / 60); var s = total % 60;
        return (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;
    }

    // ------------------------------------------------------------------ typing aids

    /** Case-insensitive LCS diff: letters kept in both strings are ok, the rest are flagged. */
    function diffChars(typed, answer) {
        var a = Array.from(String(typed == null ? '' : typed));
        var b = Array.from(String(answer == null ? '' : answer));
        var al = a.map(function (c) { return c.toLowerCase(); });
        var bl = b.map(function (c) { return c.toLowerCase(); });
        var rows = a.length + 1; var cols = b.length + 1; var i; var j;
        var dp = [];
        for (i = 0; i < rows; i++) { dp.push(new Array(cols).fill(0)); }
        for (i = a.length - 1; i >= 0; i--) {
            for (j = b.length - 1; j >= 0; j--) {
                dp[i][j] = al[i] === bl[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1]);
            }
        }
        var okA = new Array(a.length).fill(false); var okB = new Array(b.length).fill(false);
        i = 0; j = 0;
        while (i < a.length && j < b.length) {
            if (al[i] === bl[j]) { okA[i] = true; okB[j] = true; i++; j++; }
            else if (dp[i + 1][j] >= dp[i][j + 1]) { i++; }
            else { j++; }
        }
        return {
            typed: a.map(function (ch, k) { return { ch: ch, ok: okA[k] }; }),
            answer: b.map(function (ch, k) { return { ch: ch, ok: okB[k] }; })
        };
    }

    /** Reveals the first `shown` non-space characters, masks the rest with "_", keeps spaces. */
    function hintMask(answer, shown) {
        var left = Math.max(0, shown | 0);
        return Array.from(String(answer == null ? '' : answer)).map(function (ch) {
            if (/\s/.test(ch)) { return ch; }
            if (left > 0) { left--; return ch; }
            return '_';
        }).join('');
    }

    function maxHints(answer) {
        var letters = Array.from(String(answer == null ? '' : answer).replace(/\s+/g, '')).length;
        return Math.max(0, letters - 1);
    }

    // ------------------------------------------------------------------ suggestion

    function isNum(v) { return typeof v === 'number' && isFinite(v); }

    /** Which study game to suggest next. */
    function suggestMode(stats) {
        var s = stats || {};
        if (!isNum(s.learnPercent) || s.learnPercent < 100) { return 'learn'; }
        if (!isNum(s.testPercent)) { return 'test'; }
        if (s.testPercent < 80) { return 'learn'; }
        return 'match';
    }

    // ------------------------------------------------------------------ device storage

    function keys() {
        return {
            learn: function (userId, scopeKey) { return 'tct:learn:' + userId + ':' + scopeKey; },
            matchBest: function (userId, scopeKey, pairs) { return 'tct:match-best:' + userId + ':' + scopeKey + ':' + pairs; },
            testDraft: function (userId, id) { return 'tct:test-draft:' + userId + ':' + id; },
            setStats: function (setId) { return 'tct:set-stats:' + setId; },
            setup: function (userId) { return 'tct:practice-setup:' + userId; }
        };
    }

    function readJson(store, key) {
        try {
            var raw = store && store.getItem(key);
            if (raw == null) { return null; }
            return JSON.parse(raw);
        } catch (e) {
            if (typeof console !== 'undefined' && console.warn) { console.warn('practice: ignoring unreadable storage entry', key); }
            return null;
        }
    }

    function writeJson(store, key, value) {
        try { store.setItem(key, JSON.stringify(value)); return true; } catch (e) { return false; }
    }

    function removeKey(store, key) {
        try { store.removeItem(key); } catch (e) { /* storage unavailable */ }
    }

    /**
     * Device-local state. `storages` = { local, session } (Storage-like); both optional, every access is guarded so a
     * throwing or missing storage just means "nothing saved".
     */
    function createStore(storages, clock) {
        var local = storages && storages.local;
        var session = storages && storages.session;
        var k = keys();
        var now = clock || function () { return Date.now(); };

        function mergeStats(setId, patch) {
            if (!setId) { return false; }
            var current = readJson(local, k.setStats(setId));
            var stats = current && typeof current === 'object' && !Array.isArray(current) ? current : {};
            if (isNum(patch.learnPercent)) { stats.learnPercent = patch.learnPercent; stats.learnAt = now(); }
            if (isNum(patch.testPercent)) { stats.testPercent = patch.testPercent; stats.testAt = now(); }
            if (isNum(patch.matchMs) && (patch.matchPairs == null || patch.matchPairs >= MATCH_PAIRS_PER_BOARD)) {
                if (!isNum(stats.matchMs) || patch.matchMs < stats.matchMs) { stats.matchMs = patch.matchMs; stats.matchAt = now(); }
            }
            return writeJson(local, k.setStats(setId), stats);
        }

        return {
            keys: k,
            readStats: function (setId) {
                var v = readJson(local, k.setStats(setId));
                return v && typeof v === 'object' && !Array.isArray(v) ? v : {};
            },
            mergeStats: mergeStats,
            loadLearn: function (userId, scopeKey) { return readJson(local, k.learn(userId, scopeKey)); },
            saveLearn: function (userId, scopeKey, state) { return writeJson(local, k.learn(userId, scopeKey), state); },
            clearLearn: function (userId, scopeKey) { removeKey(local, k.learn(userId, scopeKey)); },
            getMatchBest: function (userId, scopeKey, pairs) {
                var v = readJson(local, k.matchBest(userId, scopeKey, pairs));
                return v && isNum(v.ms) ? v : null;
            },
            /** Stores `ms` when it beats the saved best. Returns true for a new record. */
            setMatchBest: function (userId, scopeKey, pairs, ms) {
                var key = k.matchBest(userId, scopeKey, pairs);
                var best = readJson(local, key);
                if (best && isNum(best.ms) && best.ms <= ms) { return false; }
                writeJson(local, key, { ms: ms, at: now() });
                return true;
            },
            loadDraft: function (userId, id) { return readJson(session, k.testDraft(userId, id)); },
            saveDraft: function (userId, id, draft) { return writeJson(session, k.testDraft(userId, id), draft); },
            clearDraft: function (userId, id) { removeKey(session, k.testDraft(userId, id)); },
            loadSetup: function (userId) { return readJson(local, k.setup(userId)); },
            saveSetup: function (userId, setup) { return writeJson(local, k.setup(userId), setup); }
        };
    }

    return {
        MODE_INFO: MODE_INFO, STUDY_GAMES: STUDY_GAMES, SKILLS: SKILLS, COUNTS: COUNTS, COUNT_UNIT: COUNT_UNIT,
        TEST_TYPES: TEST_TYPES, DEFAULT_TEST_TYPES: DEFAULT_TEST_TYPES, TEST_TYPE_LABEL: TEST_TYPE_LABEL, TEST_ASK: TEST_ASK, TEST_ASK_LABEL: TEST_ASK_LABEL,
        LEARN_ROUND_SIZE: LEARN_ROUND_SIZE, MATCH_PAIRS_PER_BOARD: MATCH_PAIRS_PER_BOARD, MATCH_PENALTY_MS: MATCH_PENALTY_MS,
        countsFor: countsFor, countUnit: countUnit,
        mulberry32: mulberry32, shuffle: shuffle, textKey: textKey, levenshtein: levenshtein, dedupeById: dedupeById,
        checkTyping: checkTyping, pickDistractors: pickDistractors, choiceOptions: choiceOptions,
        englishForms: englishForms, makeCloze: makeCloze, fitsMode: fitsMode,
        buildQuestions: buildQuestions,
        parseTestTypes: parseTestTypes, parseTestAsk: parseTestAsk, buildTest: buildTest, gradeTest: gradeTest,
        answerText: answerText, expectedText: expectedText, testRemark: testRemark, testId: testId,
        initLearn: initLearn, startRound: startRound, levelOf: levelOf, questionFor: questionFor, answer: answer,
        roundDone: roundDone, learnComplete: learnComplete, progress: learnProgress, learnPercent: learnPercent,
        starsFor: starsFor, recordRound: recordRound, roundsDone: roundsDone, stageCount: stageCount,
        previewRound: previewRound, masteryDots: masteryDots, restoreLearn: restoreLearn,
        buildMatchBoards: buildMatchBoards, isPair: isPair, formatMs: formatMs, formatDuration: formatDuration,
        diffChars: diffChars, hintMask: hintMask, maxHints: maxHints,
        suggestMode: suggestMode,
        createStore: createStore
    };
}));
