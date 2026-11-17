'use strict';
// node --test scripts/tests/test_vocabulary_core.cjs
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const practice = require('../../TCTEnglish/wwwroot/js/practice-core.js');
const modulePath = path.join(__dirname, '../../TCTEnglish/wwwroot/js/vocabulary-core.js');
const { create } = require(modulePath);
const core = create(practice);

const rngOf = (seed) => practice.mulberry32(seed);
function mk(id) { return { id, term: 'term' + id, definition: 'nghĩa ' + id, phonetic: null, partsOfSpeech: [], topic: null }; }
function words(n, start) { return Array.from({ length: n }, (_, i) => mk((start || 1) + i)); }

/** Answers every item correctly until the result; returns the events. */
function runCorrect(state, onItem) {
    const events = [];
    let guard = 0;
    for (let item = core.current(state); item; item = core.current(state)) {
        if (guard++ > 500) { throw new Error('session did not finish'); }
        if (item.phase === 'intro') { core.introNext(state); continue; }
        if (onItem) { onItem(item); }
        events.push(core.answer(state, item.wordId, 'correct'));
    }
    return events;
}

// ---------------------------------------------------------------- rounds
test('planRounds: rounds of 5 and a short tail merged into the previous round', () => {
    const sizes = (n) => core.planRounds(Array.from({ length: n }, (_, i) => i)).map((r) => r.length);
    assert.deepEqual(sizes(0), []);
    assert.deepEqual(sizes(3), [3]);
    assert.deepEqual(sizes(5), [5]);
    assert.deepEqual(sizes(7), [7]);
    assert.deepEqual(sizes(8), [5, 3]);
    assert.deepEqual(sizes(10), [5, 5]);
    assert.deepEqual(sizes(12), [5, 7]);
    assert.deepEqual(core.planRounds([1, 2, 3, 4, 5, 6, 7, 8], 5, 3).flat(), [1, 2, 3, 4, 5, 6, 7, 8]);
});

// ---------------------------------------------------------------- sessions
test('a full learn session without mistakes: intro, choose, type, result', () => {
    const state = core.initSession({ kind: 'learn', words: words(7), rng: rngOf(1) });
    assert.equal(state.rounds.length, 1);
    assert.deepEqual(core.current(state), { phase: 'intro', wordId: 1 });

    const phases = new Set();
    const events = runCorrect(state, (item) => phases.add(item.phase));
    const finished = events.filter((e) => e.finished).map((e) => e.wordId);

    assert.deepEqual([...phases].sort(), ['choose', 'type']);
    assert.equal(core.current(state), null);
    assert.equal(state.phase, 'result');
    assert.deepEqual(finished.slice().sort((a, b) => a - b), [1, 2, 3, 4, 5, 6, 7]);
    assert.equal(new Set(finished).size, 7, 'each word finishes exactly once');
    assert.deepEqual(core.progress(state), { finished: 7, total: 7, percent: 100 });
    for (let id = 1; id <= 7; id++) { assert.equal(state.words[id].grade, 'good'); }

    const s = core.summary(state);
    assert.equal(s.total, 7);
    assert.equal(s.remembered, 7);
    assert.equal(s.needReview, 0);
    assert.equal(s.known, 0);
    assert.equal(s.asked, 14);
    assert.equal(s.right, 14);
    assert.equal(s.accuracyPercent, 100);
});

test('rounds: a 12-word learn session runs two rounds and each starts with an intro', () => {
    const state = core.initSession({ kind: 'learn', words: words(12), rng: rngOf(2) });
    assert.deepEqual(state.rounds.map((r) => r.length), [5, 7]);
    assert.deepEqual(core.roundInfo(state), { number: 1, count: 2, size: 5 });

    let roundChanges = 0;
    let introsRound2 = 0;
    for (let item = core.current(state); item; item = core.current(state)) {
        if (item.phase === 'intro') {
            if (state.roundIndex === 1) { introsRound2 += 1; }
            core.introNext(state);
            continue;
        }
        const e = core.answer(state, item.wordId, 'correct');
        if (e.roundChanged) {
            roundChanges += 1;
            if (state.phase !== 'result') { assert.equal(state.phase, 'intro'); assert.equal(state.introIndex, 0); }
        }
    }
    assert.equal(roundChanges, 2);
    assert.equal(introsRound2, 7);
    assert.equal(core.progress(state).percent, 100);
});

test('a wrong answer re-queues the word at the end of the same pass', () => {
    const state = core.initSession({ kind: 'review', words: words(4), rng: rngOf(3) });
    assert.equal(state.phase, 'choose');
    const first = core.current(state).wordId;
    const queueBefore = state.queue.slice();

    const e = core.answer(state, first, 'wrong');

    assert.equal(e.ignored, false);
    assert.equal(e.correct, false);
    assert.equal(state.phase, 'choose');
    assert.equal(state.queue.length, queueBefore.length);
    assert.equal(state.queue[state.queue.length - 1], first);
    assert.notEqual(core.current(state).wordId, first);
    assert.equal(state.words[first].misses, 1);
    assert.equal(state.words[first].finished, false);

    // Answer everything else; the missed word is asked again later in the same pass.
    while (state.phase === 'choose') {
        const item = core.current(state);
        core.answer(state, item.wordId, 'correct');
    }
    assert.equal(state.phase, 'type');
    assert.equal(state.asked, 4, 'a retry is the same question, not a new one');
    assert.equal(state.firstTryRight, 3, 'the retry is not a first-try answer');
});

test('"I already know this word" skips practice and grades easy', () => {
    const state = core.initSession({ kind: 'learn', words: words(5), rng: rngOf(4) });
    core.introKnown(state);
    assert.equal(state.words[1].known, true);
    assert.equal(state.words[1].finished, true);
    assert.equal(state.words[1].grade, 'easy');
    for (let i = 0; i < 4; i++) { core.introNext(state); }

    const seen = [];
    runCorrect(state, (item) => seen.push(item.wordId));
    assert.ok(!seen.includes(1), 'the known word is never practised');
    const s = core.summary(state);
    assert.equal(s.known, 1);
    assert.equal(s.remembered, 4);
    assert.equal(s.total, 5);
    assert.equal(core.progress(state).percent, 100);
});

test('when every word of a round is known the round is skipped', () => {
    const state = core.initSession({ kind: 'learn', words: words(10), rng: rngOf(5) });
    for (let i = 0; i < 5; i++) { core.introKnown(state); }
    assert.equal(state.roundIndex, 1);
    assert.equal(state.phase, 'intro');
    for (let i = 0; i < 5; i++) { core.introKnown(state); }
    assert.equal(state.phase, 'result');
    assert.equal(core.summary(state).known, 10);
});

test('review sessions skip the intro and ask meaning -> term', () => {
    const review = core.initSession({ kind: 'review', words: words(5), rng: rngOf(6) });
    assert.equal(core.current(review).phase, 'choose');
    const q = core.questionFor(review, core.current(review).wordId, [], rngOf(6));
    assert.equal(q.kind, 'choice');
    assert.equal(q.prompt, 'meaning');
    assert.equal(q.options.length, 4);
    assert.ok(q.options.every((o) => /^term\d+$/.test(o.label)), 'options are terms');
    assert.equal(q.options.find((o) => o.id === q.answerId).label, q.word.term);

    const relearn = core.initSession({ kind: 'relearn', words: words(5), rng: rngOf(6) });
    const r = core.questionFor(relearn, core.current(relearn).wordId, [], rngOf(6));
    assert.equal(r.prompt, 'term');
    assert.ok(r.options.every((o) => /^nghĩa \d+$/.test(o.label)), 'options are meanings');
});

test('typing-only fallback when there are too few distractors', () => {
    const tiny = core.initSession({ kind: 'relearn', words: words(2), rng: rngOf(7) });
    assert.equal(tiny.phase, 'type', 'pass A is skipped for words with fewer than 2 distractors');
    const q = core.questionFor(tiny, core.current(tiny).wordId, [], rngOf(7));
    assert.equal(q.kind, 'typing');

    const topped = core.initSession({ kind: 'relearn', words: words(2), pool: words(5, 100), rng: rngOf(7) });
    assert.equal(topped.phase, 'choose');
    assert.equal(core.questionFor(topped, core.current(topped).wordId, [], rngOf(7)).kind, 'choice');

    const single = core.initSession({ kind: 'review', words: words(1), rng: rngOf(7) });
    assert.equal(single.phase, 'type');
    runCorrect(single);
    assert.equal(single.phase, 'result');
});

test('"close" counts as a correct answer', () => {
    const state = core.initSession({ kind: 'relearn', words: words(1), rng: rngOf(8) });
    const e = core.answer(state, 1, 'close');
    assert.equal(e.correct, true);
    assert.equal(e.finished, true);
    assert.equal(e.grade, 'easy');
    assert.equal(state.firstTryRight, 1);
    assert.equal(state.phase, 'result');
});

test('grading table for learn, relearn and review', () => {
    const g = (kind, w) => core.gradeFor(kind, w);
    assert.equal(g('learn', { known: true, misses: 0 }), 'easy');
    assert.equal(g('learn', { misses: 0 }), 'good');
    assert.equal(g('learn', { misses: 1 }), 'good');
    assert.equal(g('learn', { misses: 2 }), 'hard');
    assert.equal(g('learn', { misses: 5 }), 'hard');
    for (const kind of ['relearn', 'review']) {
        assert.equal(g(kind, { misses: 0 }), 'easy');
        assert.equal(g(kind, { misses: 1 }), 'good');
        assert.equal(g(kind, { misses: 2 }), 'hard');
    }
});

test('the grade given to a finished word follows its misses across both passes', () => {
    const state = core.initSession({ kind: 'learn', words: words(5), rng: rngOf(9) });
    for (let i = 0; i < 5; i++) { core.introNext(state); }
    // word 1: wrong once in each pass (2 misses) -> hard; everything else clean
    let missedChoose = false;
    let missedType = false;
    for (let item = core.current(state); item; item = core.current(state)) {
        if (item.wordId === 1 && item.phase === 'choose' && !missedChoose) { missedChoose = true; core.answer(state, 1, 'wrong'); continue; }
        if (item.wordId === 1 && item.phase === 'type' && !missedType) { missedType = true; core.answer(state, 1, 'wrong'); continue; }
        core.answer(state, item.wordId, 'correct');
    }
    assert.equal(state.words[1].misses, 2);
    assert.equal(state.words[1].grade, 'hard');
    assert.equal(state.words[2].grade, 'good');
    const s = core.summary(state);
    assert.equal(s.needReview, 1);
    assert.equal(s.remembered, 4);
});

test('stray answers are ignored', () => {
    const state = core.initSession({ kind: 'learn', words: words(5), rng: rngOf(10) });
    // during the intro
    assert.equal(core.answer(state, 1, 'correct').ignored, true);
    for (let i = 0; i < 5; i++) { core.introNext(state); }
    const item = core.current(state);
    const other = [1, 2, 3, 4, 5].find((id) => id !== item.wordId);
    const snapshot = JSON.stringify({ q: state.queue, a: state.answered, asked: state.asked, w: state.words });

    assert.equal(core.answer(state, other, 'correct').ignored, true);
    assert.equal(core.answer(state, 999, 'correct').ignored, true);
    assert.equal(core.answer(state, item.wordId, 'maybe').ignored, true);
    assert.equal(core.answer(state, item.wordId, undefined).ignored, true);
    assert.equal(JSON.stringify({ q: state.queue, a: state.answered, asked: state.asked, w: state.words }), snapshot);

    runCorrect(state);
    assert.equal(core.answer(state, 1, 'correct').ignored, true, 'after the result');
    core.introNext(state);
    core.introKnown(state);
    assert.equal(state.phase, 'result');
});

test('summary accuracy counts first-try answers over distinct questions', () => {
    const state = core.initSession({ kind: 'relearn', words: words(2), rng: rngOf(11) });
    assert.equal(core.summary(state).accuracyPercent, 100, '100 when nothing was asked');
    // 2 words, typing only: ask word A wrong, then right; word B right
    const a = core.current(state).wordId;
    const b = a === 1 ? 2 : 1;
    core.answer(state, a, 'wrong');
    runCorrect(state);
    const s = core.summary(state);
    assert.equal(s.asked, 2);
    assert.equal(s.right, 1);
    assert.equal(s.accuracyPercent, 50);
    assert.equal(state.words[a].grade, 'good');
    assert.equal(state.words[b].grade, 'easy');
});

test('unsaved words are reported until marked saved', () => {
    const state = core.initSession({ kind: 'review', words: words(3), pool: words(3, 50), rng: rngOf(12) });
    runCorrect(state);
    assert.equal(core.summary(state).unsaved, 3);
    core.markSaved(state, 1, true);
    core.markSaved(state, 2);
    assert.equal(core.summary(state).unsaved, 1);
});

test('questionFor builds a four-option choice that contains the answer', () => {
    const state = core.initSession({ kind: 'learn', words: words(5), pool: words(6, 200), rng: rngOf(13) });
    for (let i = 0; i < 5; i++) { core.introNext(state); }
    const item = core.current(state);
    const q = core.questionFor(state, item.wordId, [], rngOf(13));
    assert.equal(q.kind, 'choice');
    assert.equal(q.options.length, 4);
    assert.equal(new Set(q.options.map((o) => o.id)).size, 4);
    assert.ok(q.options.some((o) => o.id === String(item.wordId)));
    assert.equal(q.answerId, String(item.wordId));
    assert.equal(core.questionFor(state, 12345, [], rngOf(1)), null);

    const typeState = core.initSession({ kind: 'review', words: words(1), rng: rngOf(1) });
    assert.equal(core.questionFor(typeState, 1, [], rngOf(1)).kind, 'typing');
});

test('the same seed gives the same session', () => {
    const order = (seed) => {
        const state = core.initSession({ kind: 'review', words: words(9), pool: words(5, 40), rng: rngOf(seed) });
        const seen = [];
        runCorrect(state, (item) => seen.push(item.phase + item.wordId));
        return seen.join(',');
    };
    assert.equal(order(21), order(21));
    assert.notEqual(order(21), order(22));
});

test('an empty session is already at the result', () => {
    const state = core.initSession({ kind: 'learn', words: [], rng: rngOf(1) });
    assert.equal(state.phase, 'result');
    assert.equal(core.current(state), null);
    assert.deepEqual(core.progress(state), { finished: 0, total: 0, percent: 0 });
});

// ---------------------------------------------------------------- filters
test('filterCourses: level, status and accent-insensitive name search', () => {
    const cards = [
        { name: 'Đồ ăn & Thức uống', level: 'A1', status: 'progress' },
        { name: 'Du lịch & Địa điểm', level: 'B1', status: 'new' },
        { name: 'Nghề nghiệp', level: 'B1', status: 'done' },
        { name: 'Màu sắc', level: null, status: 'new' }
    ];
    assert.equal(core.filterCourses(cards, {}).length, 4);
    assert.equal(core.filterCourses(cards, { level: 'all', status: '' }).length, 4);
    assert.deepEqual(core.filterCourses(cards, { level: 'B1' }).map((c) => c.name), ['Du lịch & Địa điểm', 'Nghề nghiệp']);
    assert.deepEqual(core.filterCourses(cards, { status: 'new' }).map((c) => c.name), ['Du lịch & Địa điểm', 'Màu sắc']);
    assert.deepEqual(core.filterCourses(cards, { q: 'do an' }).map((c) => c.name), ['Đồ ăn & Thức uống']);
    assert.deepEqual(core.filterCourses(cards, { q: 'ĐỒ ĂN' }).map((c) => c.name), ['Đồ ăn & Thức uống']);
    assert.deepEqual(core.filterCourses(cards, { q: 'dia diem' }).map((c) => c.name), ['Du lịch & Địa điểm']);
    assert.deepEqual(core.filterCourses(cards, { q: 'nghe', level: 'B1', status: 'done' }).map((c) => c.name), ['Nghề nghiệp']);
    assert.deepEqual(core.filterCourses(cards, { q: 'zzz' }), []);
});

test('filterWords: search term or meaning, state, lesson and topic', () => {
    const list = [
        { term: 'Dog', definition: 'Con chó', state: 'new', lesson: 1, topic: 'Animals (Động vật)' },
        { term: 'Cat', definition: 'Con mèo', state: 'learning', lesson: 1, topic: 'Animals (Động vật)' },
        { term: 'Red', definition: 'Màu đỏ', state: 'known', lesson: 2, topic: 'Colors (Màu sắc)' }
    ];
    assert.equal(core.filterWords(list, {}).length, 3);
    assert.deepEqual(core.filterWords(list, { q: 'cho' }).map((w) => w.term), ['Dog']);
    assert.deepEqual(core.filterWords(list, { q: 'meo' }).map((w) => w.term), ['Cat']);
    assert.deepEqual(core.filterWords(list, { q: 'do' }).map((w) => w.term), ['Dog', 'Red']);
    assert.deepEqual(core.filterWords(list, { q: 'CAT' }).map((w) => w.term), ['Cat']);
    assert.deepEqual(core.filterWords(list, { state: 'known' }).map((w) => w.term), ['Red']);
    assert.deepEqual(core.filterWords(list, { lesson: 1 }).map((w) => w.term), ['Dog', 'Cat']);
    assert.deepEqual(core.filterWords(list, { lesson: '2' }).map((w) => w.term), ['Red']);
    assert.deepEqual(core.filterWords(list, { topic: 'colors (mau sac)' }).map((w) => w.term), ['Red']);
    assert.deepEqual(core.filterWords(list, { q: 'o', state: 'new', lesson: 1 }).map((w) => w.term), ['Dog']);
});

// ---------------------------------------------------------------- module hygiene
test('the module is pure: no DOM, network or window access outside the UMD wrapper', () => {
    const source = fs.readFileSync(modulePath, 'utf8');
    const start = source.indexOf('}(typeof self');
    const body = source.slice(start);
    assert.ok(start > 0);
    for (const forbidden of [/\bdocument\b/, /\bfetch\b/, /\bwindow\b/, /\binnerHTML\b/, /\blocalStorage\b/, /\bconsole\./]) {
        assert.ok(!forbidden.test(body), 'unexpected ' + forbidden);
    }
    assert.ok(!/\bwindow\b/.test(source), 'the wrapper uses root, not window');
});

test('create() refuses to run without the practice helpers', () => {
    assert.throws(() => create(undefined), /practice helpers/);
});
