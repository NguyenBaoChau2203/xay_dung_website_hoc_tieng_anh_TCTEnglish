/*
 * System Vocabulary lessons: pure logic (no DOM, no network).
 * UMD: under Node `module.exports = { create }`; in the browser `TctVocabulary.core` is
 * built from `TctPractice.core`. Practice helpers are injected so Node tests can pass the real file.
 *
 * Session flow (plan 4.3): words are taught in rounds of 5. In a `learn` session each round starts with an
 * intro card per word; then every kind practises the round with pass A (choose) and pass B (type).
 * A wrong answer re-queues the word at the end of the same pass. A word is finished when it passes pass B,
 * and is graded once (plan 4.4). The state is a plain object that the helpers mutate and return.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = { create: factory };
    } else {
        root.TctVocabulary = root.TctVocabulary || {};
        root.TctVocabulary.core = factory(root.TctPractice && root.TctPractice.core);
    }
}(typeof self !== 'undefined' ? self : this, function create(practice) {
    'use strict';

    if (!practice) { throw new Error('vocabulary-core needs the practice helpers (TctPractice.core).'); }

    var ROUND_SIZE = 5;
    var ROUND_MIN_TAIL = 3;
    var KINDS = ['learn', 'relearn', 'review'];

    // ------------------------------------------------------------------ text helpers

    /** Lower-case, accent-insensitive key for searching Vietnamese text. */
    function fold(value) {
        return String(value == null ? '' : value)
            .replace(/[đĐ]/g, 'd')
            .normalize('NFD')
            .replace(/[̀-ͯ]/g, '')
            .toLowerCase()
            .trim()
            .replace(/\s+/g, ' ');
    }

    function isAll(value) { return value == null || value === '' || value === 'all'; }

    // ------------------------------------------------------------------ rounds

    /** Rounds of `size` words; a last round shorter than `minTail` is merged into the previous one. */
    function planRounds(wordIds, size, minTail) {
        var step = Math.max(1, size || ROUND_SIZE);
        var tail = Math.max(1, minTail == null ? ROUND_MIN_TAIL : minTail);
        var ids = Array.prototype.slice.call(wordIds || []);
        var rounds = [];
        for (var i = 0; i < ids.length; i += step) { rounds.push(ids.slice(i, i + step)); }
        if (rounds.length > 1 && rounds[rounds.length - 1].length < tail) {
            var last = rounds.pop();
            rounds[rounds.length - 1] = rounds[rounds.length - 1].concat(last);
        }
        return rounds;
    }

    // ------------------------------------------------------------------ grading (plan 4.4)

    /** masteryLevel sent to the unchanged record endpoint: "easy" | "good" | "hard". */
    function gradeFor(kind, wordState) {
        var w = wordState || {};
        var misses = w.misses || 0;
        if (kind === 'learn') {
            if (w.known) { return 'easy'; }
            return misses <= 1 ? 'good' : 'hard';
        }
        if (misses === 0) { return 'easy'; }
        return misses === 1 ? 'good' : 'hard';
    }

    // ------------------------------------------------------------------ session state

    function newWordState() {
        return { misses: 0, known: false, finished: false, grade: null, saved: false, tried: { choose: false, type: false } };
    }

    function key(id) { return String(id); }

    /** Whether a word has at least 2 distractors available for pass A. Uses its own rng so the session rng is untouched. */
    function canChoose(state, id) {
        var cached = state.choosable[key(id)];
        if (cached === undefined) {
            cached = practice.pickDistractors(state.byId[key(id)], state.candidates, practice.mulberry32(7), 3).length >= 2;
            state.choosable[key(id)] = cached;
        }
        return cached;
    }

    function remainingOfRound(state) {
        return (state.rounds[state.roundIndex] || []).filter(function (id) {
            var w = state.words[key(id)];
            return !w.known && !w.finished;
        });
    }

    function startRound(state) {
        if (state.roundIndex >= state.rounds.length) {
            state.phase = 'result';
            state.queue = [];
            return state;
        }
        if (state.kind === 'learn') {
            state.phase = 'intro';
            state.introIndex = 0;
            state.queue = [];
            return state;
        }
        return beginChoose(state);
    }

    function advanceRound(state) {
        state.roundIndex += 1;
        return startRound(state);
    }

    function beginChoose(state) {
        var remaining = remainingOfRound(state);
        if (remaining.length === 0) { return advanceRound(state); }
        var queue = practice.shuffle(remaining, state.rng).filter(function (id) { return canChoose(state, id); });
        if (queue.length === 0) { return beginType(state); }
        state.phase = 'choose';
        state.queue = queue;
        return state;
    }

    function beginType(state) {
        var remaining = remainingOfRound(state);
        if (remaining.length === 0) { return advanceRound(state); }
        state.phase = 'type';
        state.queue = practice.shuffle(remaining, state.rng);
        return state;
    }

    /**
     * opts: { kind: "learn" | "relearn" | "review", words: [{ id, term, definition, ... }], pool?: [...], rng? }
     * `pool` holds other cards used only as distractors.
     */
    function initSession(opts) {
        var o = opts || {};
        var words = practice.dedupeById(o.words || []);
        var state = {
            kind: KINDS.indexOf(o.kind) >= 0 ? o.kind : 'learn',
            rng: o.rng || Math.random,
            rounds: planRounds(words.map(function (w) { return w.id; })),
            roundIndex: 0,
            phase: 'result',
            queue: [],
            introIndex: 0,
            words: {},
            byId: {},
            choosable: {},
            candidates: practice.dedupeById(words.concat(o.pool || [])),
            answered: 0,
            asked: 0,
            firstTryRight: 0
        };
        words.forEach(function (w) {
            state.byId[key(w.id)] = w;
            state.words[key(w.id)] = newWordState();
        });
        return startRound(state);
    }

    /** The item to show now: { phase, wordId }, or null at the result. */
    function current(state) {
        if (!state || state.phase === 'result') { return null; }
        if (state.phase === 'intro') {
            var round = state.rounds[state.roundIndex] || [];
            return state.introIndex < round.length ? { phase: 'intro', wordId: round[state.introIndex] } : null;
        }
        return state.queue.length ? { phase: state.phase, wordId: state.queue[0] } : null;
    }

    function endIntro(state) {
        return beginChoose(state);
    }

    /** Moves to the next intro card; after the last one, practice starts. */
    function introNext(state) {
        if (!state || state.phase !== 'intro') { return state; }
        state.introIndex += 1;
        if (state.introIndex >= (state.rounds[state.roundIndex] || []).length) { return endIntro(state); }
        return state;
    }

    /** "I already know this word": finished at once with grade easy and skipped in practice. */
    function introKnown(state) {
        var item = current(state);
        if (!item || item.phase !== 'intro') { return state; }
        var w = state.words[key(item.wordId)];
        w.known = true;
        w.finished = true;
        w.grade = 'easy';
        return introNext(state);
    }

    /**
     * Applies one answer to the item on screen. `outcome`: "correct" | "close" | "wrong" ("close" counts as correct).
     * Answers for another word, during the intro or at the result are ignored.
     * Returns { ignored, correct, finished, grade, phaseChanged, roundChanged }.
     */
    function answer(state, wordId, outcome) {
        var event = { ignored: true, wordId: wordId, correct: false, finished: false, grade: null, phaseChanged: false, roundChanged: false };
        var item = current(state);
        if (!item || item.phase === 'intro' || key(item.wordId) !== key(wordId)) { return event; }
        if (outcome !== 'correct' && outcome !== 'close' && outcome !== 'wrong') { return event; }

        event.ignored = false;
        var pass = item.phase;
        var w = state.words[key(item.wordId)];
        var first = !w.tried[pass];
        state.answered += 1;
        if (first) {
            w.tried[pass] = true;
            state.asked += 1;
        }

        state.queue.shift();
        if (outcome === 'wrong') {
            w.misses += 1;
            state.queue.push(item.wordId);
            return event;
        }

        event.correct = true;
        if (first) { state.firstTryRight += 1; }
        if (pass === 'type') {
            w.finished = true;
            w.grade = gradeFor(state.kind, w);
            event.finished = true;
            event.grade = w.grade;
        }
        if (state.queue.length === 0) {
            if (pass === 'choose') {
                beginType(state);
                event.phaseChanged = true;
            } else {
                advanceRound(state);
                event.roundChanged = true;
            }
        }
        return event;
    }

    /**
     * The question for a word. Pass A: a 4-option choice (prompt "term" shows the term and asks for the meaning;
     * prompt "meaning" shows the meaning and asks for the term; review sessions use "meaning").
     * Pass B, or fewer than 2 distractors: a typing question. Shapes match practice-core (`kind`, `prompt`, `word`, `options`, `answerId`).
     */
    function questionFor(state, wordId, pool, rng) {
        var word = state.byId[key(wordId)];
        if (!word) { return null; }
        var typing = { id: word.id + ':typing', kind: 'typing', word: word };
        if (state.phase !== 'choose') { return typing; }
        var candidates = practice.dedupeById(state.candidates.concat(pool || []));
        var options = practice.choiceOptions(word, candidates, rng || state.rng);
        if (options.length < 3) { return typing; }
        var prompt = state.kind === 'review' ? 'meaning' : 'term';
        return {
            id: word.id + ':choice:' + prompt,
            kind: 'choice',
            prompt: prompt,
            word: word,
            options: options.map(function (o) { return { id: String(o.id), label: prompt === 'term' ? o.definition : o.term }; }),
            answerId: String(word.id)
        };
    }

    /** Remembers that the record call for a word succeeded (or not). */
    function markSaved(state, wordId, saved) {
        var w = state && state.words[key(wordId)];
        if (w) { w.saved = saved !== false; }
        return state;
    }

    function progress(state) {
        var ids = Object.keys(state.words);
        var finished = ids.filter(function (id) { return state.words[id].finished; }).length;
        return { finished: finished, total: ids.length, percent: ids.length ? Math.round(100 * finished / ids.length) : 0 };
    }

    /** Position in the round list, for the "Tiếp theo: n từ nữa" card. Null at the result. */
    function roundInfo(state) {
        if (!state || state.phase === 'result') { return null; }
        return { number: state.roundIndex + 1, count: state.rounds.length, size: (state.rounds[state.roundIndex] || []).length };
    }

    function summary(state) {
        var ids = Object.keys(state.words);
        var known = 0;
        var remembered = 0;
        var needReview = 0;
        var unsaved = 0;
        ids.forEach(function (id) {
            var w = state.words[id];
            if (!w.finished) { return; }
            if (w.known) { known += 1; }
            else if (w.grade === 'hard') { needReview += 1; }
            else { remembered += 1; }
            if (!w.saved) { unsaved += 1; }
        });
        return {
            total: ids.length,
            remembered: remembered,
            needReview: needReview,
            known: known,
            asked: state.asked,
            right: state.firstTryRight,
            accuracyPercent: state.asked ? Math.round(100 * state.firstTryRight / state.asked) : 100,
            unsaved: unsaved
        };
    }

    // ------------------------------------------------------------------ filters

    /** Course cards: [{ level, status, name }]. level/status "all" or blank means no filter; q matches the name. */
    function filterCourses(cards, filters) {
        var f = filters || {};
        var q = fold(f.q);
        return (cards || []).filter(function (card) {
            if (!isAll(f.level) && String(card.level || '') !== String(f.level)) { return false; }
            if (!isAll(f.status) && String(card.status || '') !== String(f.status)) { return false; }
            return !q || fold(card.name).indexOf(q) >= 0;
        });
    }

    /** Course words: [{ term, definition, state, lesson, topic }]. q matches the term or the meaning. */
    function filterWords(words, filters) {
        var f = filters || {};
        var q = fold(f.q);
        var topic = isAll(f.topic) ? '' : fold(f.topic);
        return (words || []).filter(function (word) {
            if (!isAll(f.state) && String(word.state || '') !== String(f.state)) { return false; }
            if (!isAll(f.lesson) && Number(word.lesson) !== Number(f.lesson)) { return false; }
            if (topic && fold(word.topic) !== topic) { return false; }
            return !q || fold(word.term).indexOf(q) >= 0 || fold(word.definition).indexOf(q) >= 0;
        });
    }

    return {
        ROUND_SIZE: ROUND_SIZE,
        ROUND_MIN_TAIL: ROUND_MIN_TAIL,
        fold: fold,
        planRounds: planRounds,
        initSession: initSession,
        current: current,
        introNext: introNext,
        introKnown: introKnown,
        answer: answer,
        questionFor: questionFor,
        gradeFor: gradeFor,
        markSaved: markSaved,
        progress: progress,
        roundInfo: roundInfo,
        summary: summary,
        filterCourses: filterCourses,
        filterWords: filterWords
    };
}));
