'use strict';
// node --test scripts/tests/test_vocabulary_srs.cjs
const test = require('node:test');
const assert = require('node:assert/strict');

const practice = require('../../TCTEnglish/wwwroot/js/practice-core.js');
const { create } = require('../../TCTEnglish/wwwroot/js/vocabulary-core.js');
const core = create(practice);

const rngOf = (seed) => practice.mulberry32(seed);
function mk(id, level, term) {
    return { id, term: term || 'term' + id, definition: 'nghĩa ' + id, phonetic: null, level: level === undefined ? null : level };
}
function words(n, level, start) { return Array.from({ length: n }, (_, i) => mk((start || 1) + i, level)); }
function srs(list, pool) { return core.initSession({ kind: 'review', srs: true, words: list, pool: pool || [], rng: rngOf(5) }); }

// ---------------------------------------------------------------- outcomeFor
test('outcomeFor: known, then good / hard / again by misses', () => {
    assert.equal(core.outcomeFor(0, true), 'known');
    assert.equal(core.outcomeFor(3, true), 'known');
    assert.equal(core.outcomeFor(0, false), 'good');
    assert.equal(core.outcomeFor(1, false), 'hard');
    assert.equal(core.outcomeFor(2, false), 'again');
    assert.equal(core.outcomeFor(7, false), 'again');
    assert.equal(core.outcomeFor(undefined, false), 'good');
    assert.equal(core.outcomeFor(-1, false), 'good');
});

// ---------------------------------------------------------------- stage and dots
test('stageFor / stageLabel follow the memory ladder', () => {
    const table = [[0, 'new', 'Mới học'], [1, 'new', 'Mới học'], [2, 'growing', 'Đang nhớ'], [3, 'growing', 'Đang nhớ'],
        [4, 'strong', 'Nhớ tốt'], [5, 'strong', 'Nhớ tốt'], [6, 'long', 'Thuộc lâu']];
    table.forEach(([level, stage, label]) => {
        assert.equal(core.stageFor(level), stage);
        assert.equal(core.stageLabel(level), label);
    });
    assert.equal(core.stageFor(null), 'none');
    assert.equal(core.stageFor(undefined), 'none');
    assert.equal(core.stageFor(''), 'none');
    assert.equal(core.stageLabel(null), 'Chưa học');
    assert.equal(core.stageFor(99), 'long');
    assert.equal(core.stageFor(-4), 'new');
    assert.equal(core.stageFor('3'), 'growing');
});

test('dotsFor: five dots, filled = min(level, 5), level 6 is five dots in the success hue', () => {
    assert.deepEqual(core.dotsFor(0), { filled: 0, hue: 'warning' });
    assert.deepEqual(core.dotsFor(1), { filled: 1, hue: 'warning' });
    assert.deepEqual(core.dotsFor(2), { filled: 2, hue: 'info' });
    assert.deepEqual(core.dotsFor(3), { filled: 3, hue: 'info' });
    assert.deepEqual(core.dotsFor(4), { filled: 4, hue: 'primary' });
    assert.deepEqual(core.dotsFor(5), { filled: 5, hue: 'primary' });
    assert.deepEqual(core.dotsFor(6), { filled: 5, hue: 'success' });
    assert.deepEqual(core.dotsFor(null), { filled: 0, hue: 'none' });
});

// ---------------------------------------------------------------- question type
test('reviewQuestionType: choose at 0-1, alternate at 2-3, type from 4; long terms stay on choose', () => {
    assert.equal(core.reviewQuestionType(0, 'dog', 0), 'choose');
    assert.equal(core.reviewQuestionType(1, 'dog', 1), 'choose');
    assert.equal(core.reviewQuestionType(2, 'dog', 0), 'choose');
    assert.equal(core.reviewQuestionType(2, 'dog', 1), 'type');
    assert.equal(core.reviewQuestionType(3, 'dog', 2), 'choose');
    assert.equal(core.reviewQuestionType(3, 'dog', 3), 'type');
    assert.equal(core.reviewQuestionType(4, 'dog', 0), 'type');
    assert.equal(core.reviewQuestionType(6, 'dog', 1), 'type');
    assert.equal(core.reviewQuestionType(null, 'dog', 1), 'choose');
    // a phrase and a 25-character term never become typing questions
    assert.equal(core.reviewQuestionType(5, 'see you later', 0), 'choose');
    assert.equal(core.reviewQuestionType(5, 'a'.repeat(25), 0), 'choose');
    assert.equal(core.reviewQuestionType(5, 'a'.repeat(24), 0), 'type');
    assert.equal(core.reviewQuestionType(5, '  dog  ', 0), 'type');
});

// ---------------------------------------------------------------- planReview
test('planReview keeps the server order, caps at 20 and records the question types', () => {
    const list = words(30, 4);
    const plan = core.planReview(list);
    assert.equal(plan.queue.length, 20);
    assert.deepEqual(plan.queue, list.slice(0, 20).map((w) => w.id));
    assert.equal(plan.maxExtraTries, 2);
    assert.ok(plan.queue.every((id) => plan.types[String(id)] === 'type'));
    assert.deepEqual(core.planReview([]).queue, []);
    assert.deepEqual(core.planReview(null).queue, []);
});

test('planReview is deterministic and drops duplicate ids', () => {
    const list = words(8, 2).concat([mk(3, 2)]);
    assert.deepEqual(core.planReview(list), core.planReview(list));
    assert.equal(core.planReview(list).queue.length, 8);
    const a = srs(words(9, 1), words(5, 0, 100));
    const b = srs(words(9, 1), words(5, 0, 100));
    assert.deepEqual(a.queue, b.queue);
});

test('a session handed more than 20 words only plays and reports the first 20', () => {
    const state = srs(words(26, 1));
    assert.equal(state.queue.length, 20);
    assert.equal(core.progress(state).total, 20);
    assert.equal(core.summary(state).total, 20);
    assert.equal(core.current(state).phase, 'review');
});

// ---------------------------------------------------------------- the review flow
test('every word is asked once; a clean pass gives good for all of them', () => {
    const state = srs(words(4, 1));
    const events = [];
    for (let item = core.current(state); item; item = core.current(state)) {
        events.push(core.answer(state, item.wordId, 'correct'));
    }
    assert.equal(events.length, 4);
    assert.ok(events.every((e) => e.finished && e.outcome === 'good'));
    assert.equal(state.phase, 'result');
    assert.equal(core.current(state), null);
    assert.equal(core.summary(state).accuracyPercent, 100);
});

test('one miss gives hard; two misses give again; a third miss ends the word as again', () => {
    const state = srs(words(3, 1));
    const order = state.queue.slice();
    const [a, b, c] = order;
    const result = {};
    const play = (id, outcome) => { const e = core.answer(state, id, outcome); if (e.finished) { result[id] = e.outcome; } return e; };

    // a: wrong, then right (hard)
    assert.equal(play(a, 'wrong').finished, false);
    assert.deepEqual(state.queue, [b, c, a]);
    play(b, 'correct');                       // b: good
    play(c, 'wrong');                         // c: miss 1, to the end
    assert.deepEqual(state.queue, [a, c]);
    play(a, 'correct');                       // a: hard
    play(c, 'wrong');                         // c: miss 2, to the end again
    assert.deepEqual(state.queue, [c]);
    assert.equal(play(c, 'wrong').finished, true); // c: miss 3 -> again, no more tries

    assert.deepEqual(result, { [a]: 'hard', [b]: 'good', [c]: 'again' });
    assert.equal(state.phase, 'result');
});

test('two misses followed by a correct answer is again', () => {
    const state = srs(words(1, 1));
    const id = state.queue[0];
    core.answer(state, id, 'wrong');
    core.answer(state, id, 'wrong');
    const last = core.answer(state, id, 'correct');
    assert.equal(last.outcome, 'again');
    assert.equal(last.finished, true);
});

test('"close" counts as correct', () => {
    const state = srs(words(1, 4));
    const e = core.answer(state, state.queue[0], 'close');
    assert.equal(e.outcome, 'good');
});

test('stray answers are ignored in an srs review', () => {
    const state = srs(words(2, 1));
    const [a, b] = state.queue;
    assert.equal(core.answer(state, b, 'correct').ignored, true);
    assert.equal(core.answer(state, a, 'maybe').ignored, true);
    assert.equal(state.queue.length, 2);
});

test('the question follows the word level: choose, typing and the phrase exception', () => {
    const list = [mk(1, 0, 'dog'), mk(2, 5, 'cat'), mk(3, 5, 'see you later'), mk(4, 2, 'horse'), mk(5, 2, 'bird')];
    const state = srs(list, words(6, 0, 100));
    const q = (id) => core.questionFor(state, id, [], rngOf(9));
    assert.equal(q(1).kind, 'choice');
    assert.equal(q(2).kind, 'typing');
    assert.equal(q(3).kind, 'choice');
    assert.equal(q(4).kind, 'typing'); // level 2 at position 3 (odd) alternates to typing
    assert.equal(q(5).kind, 'choice'); // level 2 at position 4 (even) is a choice
});

test('level 2-3 words alternate between choose and type by position', () => {
    const list = words(4, 2);
    const state = srs(list, words(6, 0, 100));
    const kinds = list.map((w) => core.questionFor(state, w.id, [], rngOf(2)).kind);
    assert.deepEqual(kinds, ['choice', 'typing', 'choice', 'typing']);
});

test('a choice question contains the answer and falls back to typing without distractors', () => {
    const state = srs(words(6, 0));
    const id = state.queue[0];
    const q = core.questionFor(state, id, [], rngOf(1));
    assert.equal(q.kind, 'choice');
    assert.ok(q.options.some((o) => o.id === String(id)));
    assert.equal(q.answerId, String(id));
    const tiny = srs(words(2, 0));
    assert.equal(core.questionFor(tiny, tiny.queue[0], [], rngOf(1)).kind, 'typing');
});

// ---------------------------------------------------------------- lessons also get an outcome
test('a learn session gives each word an outcome from its misses; known words are known', () => {
    const state = core.initSession({ kind: 'learn', words: words(5), pool: words(4, undefined, 50), rng: rngOf(3) });
    core.introKnown(state);
    assert.equal(state.words['1'].outcome, 'known');
    while (core.current(state) && core.current(state).phase === 'intro') { core.introNext(state); }
    const events = [];
    for (let item = core.current(state); item; item = core.current(state)) {
        events.push(core.answer(state, item.wordId, 'correct'));
    }
    const finished = events.filter((e) => e.finished);
    assert.equal(finished.length, 4);
    assert.ok(finished.every((e) => e.outcome === 'good'));
});

test('the outcome of a lesson word counts misses across both passes', () => {
    const state = core.initSession({ kind: 'relearn', words: words(1), pool: words(4, undefined, 50), rng: rngOf(4) });
    const id = core.current(state).wordId;
    core.answer(state, id, 'wrong');   // choose pass, miss 1
    core.answer(state, id, 'correct');
    const typing = core.current(state);
    core.answer(state, typing.wordId, 'wrong');   // type pass, miss 2
    const last = core.answer(state, typing.wordId, 'correct');
    assert.equal(last.outcome, 'again');
});

// ---------------------------------------------------------------- recording
test('claimRecord hands out a finished word once; releaseRecord allows one retry', () => {
    const state = srs(words(2, 1));
    const [a, b] = state.queue;
    assert.equal(core.claimRecord(state, a), null, 'an unfinished word cannot be recorded');
    core.answer(state, a, 'correct');
    assert.equal(core.claimRecord(state, a), 'good');
    assert.equal(core.claimRecord(state, a), null, 'never twice');
    core.releaseRecord(state, a);
    assert.equal(core.claimRecord(state, a), 'good', 'after a failed call the word can be sent again');
    core.markRecorded(state, a, { level: 2, nextReviewLabel: 'Ôn lại sau 3 ngày', promoted: true, xpEarned: 5 });
    core.releaseRecord(state, a);
    assert.equal(core.claimRecord(state, a), null, 'a saved word is never reported again');
    assert.equal(core.claimRecord(state, 999), null);
    assert.equal(core.claimRecord(state, b), null);
});

test('unsaved lists finished words whose call did not succeed', () => {
    const state = srs(words(3, 1));
    const [a, b, c] = state.queue;
    core.answer(state, a, 'correct');
    core.answer(state, b, 'correct');
    assert.deepEqual(core.unsaved(state), [a, b]);
    core.markRecorded(state, a, { level: 2 });
    assert.deepEqual(core.unsaved(state), [b]);
    core.answer(state, c, 'correct');
    assert.deepEqual(core.unsaved(state), [b, c]);
    core.markRecorded(state, b, { level: 2 });
    core.markRecorded(state, c, { level: 2 });
    assert.deepEqual(core.unsaved(state), []);
});

test('markRecorded keeps the server answer; summary reports level, label, promotion and the XP total', () => {
    const state = srs(words(3, 1));
    const [a, b, c] = state.queue;
    [a, b, c].forEach((id) => core.answer(state, id, 'correct'));
    core.markRecorded(state, a, { level: 2, stage: 'growing', nextReviewLabel: 'Ôn lại sau 3 ngày', promoted: true, xpEarned: 5 });
    core.markRecorded(state, b, { level: 0, nextReviewLabel: 'Ôn lại sau 10 phút', demoted: true, xpEarned: 2 });
    const s = core.summary(state);
    assert.equal(s.xp, 7);
    const row = (id) => s.words.find((w) => w.id === id);
    assert.deepEqual(
        { level: row(a).level, label: row(a).label, promoted: row(a).promoted, stage: row(a).stage, saved: row(a).saved },
        { level: 2, label: 'Ôn lại sau 3 ngày', promoted: true, stage: 'growing', saved: true });
    assert.equal(row(b).stage, 'new');
    assert.equal(row(c).saved, false);
    assert.equal(row(c).label, '');
    assert.equal(s.unsaved, 1);
    assert.equal(s.total, 3);
});

test('summary keeps its earlier fields for a lesson session', () => {
    const state = core.initSession({ kind: 'learn', words: words(2), pool: words(4, undefined, 50), rng: rngOf(8) });
    const s = core.summary(state);
    ['total', 'remembered', 'needReview', 'known', 'asked', 'right', 'accuracyPercent', 'unsaved'].forEach((k) => assert.ok(k in s, k));
    assert.equal(s.xp, 0);
    assert.equal(s.words.length, 2);
});

test('an empty srs review is already at the result', () => {
    const state = srs([]);
    assert.equal(state.phase, 'result');
    assert.equal(core.current(state), null);
    assert.equal(core.summary(state).total, 0);
});

test('the default review (without srs) keeps the two-pass behaviour', () => {
    const state = core.initSession({ kind: 'review', words: words(5), pool: words(4, undefined, 50), rng: rngOf(6) });
    assert.equal(state.srs, false);
    assert.equal(core.current(state).phase, 'choose');
});

// ---------------------------------------------------------------- course word filters (S6)
test('filterWords: "due" keeps due words, "learned" keeps everything but new words, exact states still work', () => {
    const list = [
        { term: 'a', definition: 'x', state: 'new', due: false },
        { term: 'b', definition: 'y', state: 'learning', due: true },
        { term: 'c', definition: 'z', state: 'known', due: false },
        { term: 'd', definition: 'w', state: 'known', due: true }
    ];
    const terms = (state) => core.filterWords(list, { state }).map((w) => w.term).join('');
    assert.equal(terms('due'), 'bd');
    assert.equal(terms('learned'), 'bcd');
    assert.equal(terms('new'), 'a');
    assert.equal(terms('known'), 'cd');
    assert.equal(terms(''), 'abcd');
    assert.equal(terms('all'), 'abcd');
    assert.equal(core.filterWords(list, { state: 'due', q: 'D' }).map((w) => w.term).join(''), 'd');
});
