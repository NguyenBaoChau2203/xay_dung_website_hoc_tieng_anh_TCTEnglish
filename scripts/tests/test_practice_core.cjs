'use strict';
// node --test scripts/tests/test_practice_core.cjs
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const core = require('../../TCTEnglish/wwwroot/js/practice-core.js');
const rngOf = (seed) => core.mulberry32(seed);

let nextId = 1;
function word(term, definition, extra) {
    return Object.assign({
        id: nextId++, setId: 1, term, definition, phonetic: null, partsOfSpeech: [], topic: null,
        examples: [], verbForms: null, plural: null, status: 'new', wrongCount: 0
    }, extra || {});
}
function words(n, prefix) {
    return Array.from({ length: n }, (_, i) => word((prefix || 'term') + i, 'nghĩa ' + (prefix || 'term') + i));
}

// ---------------------------------------------------------------- typing
test('checkTyping: correct, close, wrong, blank and the 6-letter rule', () => {
    assert.equal(core.checkTyping('Running', 'running'), 'correct');
    assert.equal(core.checkTyping('  running. ', 'running'), 'correct');
    assert.equal(core.checkTyping('it’s', "it's"), 'correct');
    assert.equal(core.checkTyping('runing', 'running'), 'close');
    assert.equal(core.checkTyping('runnning', 'running'), 'close');
    assert.equal(core.checkTyping('studdy', 'study'), 'wrong', 'under 6 letters: no "close"');
    assert.equal(core.checkTyping('stady', 'study'), 'wrong');
    assert.equal(core.checkTyping('xx', 'running'), 'wrong');
    assert.equal(core.checkTyping('', 'running'), 'wrong');
    assert.equal(core.checkTyping('   ', 'running'), 'wrong');
    assert.equal(core.checkTyping('look forward', { term: 'look forward to' }), 'wrong');
    assert.equal(core.checkTyping('look  forward to', { term: 'look forward to' }), 'correct');
});

// ---------------------------------------------------------------- distractors
test('pickDistractors: unique terms/meanings, never the target, prefers same POS then topic', () => {
    const target = word('run', 'chạy', { partsOfSpeech: ['verb'], topic: 'Sport' });
    const pool = [
        word('table', 'cái bàn', { partsOfSpeech: ['noun'] }),
        word('jump', 'nhảy', { partsOfSpeech: ['verb'] }),
        word('swim', 'bơi', { partsOfSpeech: ['verb'], topic: 'Sport' }),
        word('Run', 'chạy', { partsOfSpeech: ['verb'] }),          // same term key as the target
        word('sprint', 'chạy', { partsOfSpeech: ['verb'] }),        // same definition as the target
        word('chair', 'cái ghế', { partsOfSpeech: ['noun'] }),
        word('swim', 'lặn', { partsOfSpeech: ['verb'] }),           // duplicate term of "swim"
        target
    ];
    const picked = core.pickDistractors(target, pool, rngOf(1), 3);
    assert.equal(picked.length, 3);
    assert.ok(!picked.includes(target));
    assert.equal(new Set(picked.map((w) => core.textKey(w.term))).size, 3);
    assert.equal(new Set(picked.map((w) => core.textKey(w.definition))).size, 3);
    assert.equal(picked[0].term, 'swim', 'POS + topic wins');
    assert.equal(picked[0].definition, 'bơi');
    assert.equal(picked[1].term, 'jump', 'same POS next');
    assert.equal(picked[2].partsOfSpeech[0], 'noun');
    assert.ok(!picked.some((w) => core.textKey(w.term) === 'run' || core.textKey(w.definition) === 'chạy'));
});

test('choiceOptions contains the target and is deterministic with a seed', () => {
    const pool = words(10);
    const target = pool[0];
    const a = core.choiceOptions(target, pool, rngOf(7)).map((w) => w.id);
    const b = core.choiceOptions(target, pool, rngOf(7)).map((w) => w.id);
    assert.deepEqual(a, b);
    assert.equal(a.length, 4);
    assert.ok(a.includes(target.id));
});

// ---------------------------------------------------------------- cloze
test('makeCloze: inflected forms, verbForms, plural and multi-word terms', () => {
    const run = word('run', 'chạy', { verbForms: { ing: 'running' }, examples: [{ text: 'He is running fast.', vi: 'Anh ấy chạy nhanh.' }] });
    const c1 = core.makeCloze(run);
    assert.deepEqual([c1.before, c1.answer, c1.after, c1.vi], ['He is ', 'running', ' fast.', 'Anh ấy chạy nhanh.']);

    const study = word('study', 'học', { examples: [{ text: 'She studies every day.' }] });
    assert.equal(core.makeCloze(study).answer, 'studies');

    const go = word('go', 'đi', { verbForms: { past: 'went', pastParticiple: 'gone', ing: 'going', thirdPerson: 'goes' }, examples: [{ text: 'They went home early.' }] });
    assert.equal(core.makeCloze(go).answer, 'went');

    const phrase = word('look forward to', 'mong đợi', { examples: [{ text: 'I look forward to meeting you.' }] });
    const c4 = core.makeCloze(phrase);
    assert.equal(c4.answer, 'look forward to');
    assert.equal(c4.before, 'I ');

    const child = word('child', 'đứa trẻ', { plural: 'children', examples: [{ text: 'The children are playing.' }] });
    assert.equal(core.makeCloze(child).answer, 'children');

    const cap = word('Paris', 'Paris', { examples: [{ text: 'We love paris in spring.' }] });
    assert.equal(core.makeCloze(cap).answer, 'paris', 'case-insensitive, keeps the sentence casing');
});

test('makeCloze: null without examples and never matches inside other words', () => {
    assert.equal(core.makeCloze(word('art', 'nghệ thuật')), null);
    assert.equal(core.makeCloze(word('art', 'nghệ thuật', { examples: [{ text: 'We start the party now.' }] })), null);
    assert.equal(core.makeCloze(word('art', 'nghệ thuật', { examples: [{ text: 'Modern art is fun.' }] })).answer, 'art');
    assert.equal(core.makeCloze(word('cat', 'mèo', { examples: [{ text: 'A category of things.' }] })), null);
    assert.equal(core.fitsMode(word('x', 'y'), 'cloze'), false);
    assert.equal(core.fitsMode(word('x', 'y'), 'quiz'), true);
});

// ---------------------------------------------------------------- drills
test('buildQuestions: choice questions, listening labels, typing, and tiny-pool fallback', () => {
    const targets = words(8);
    const quiz = core.buildQuestions('quiz', targets, [], rngOf(3));
    assert.equal(quiz.length, 8);
    quiz.forEach((q) => {
        assert.equal(q.kind, 'choice');
        assert.equal(q.options.length, 4);
        assert.ok(q.options.some((o) => o.id === q.answerId));
        const expected = q.prompt === 'term' ? q.word.definition : q.word.term;
        assert.equal(q.options.find((o) => o.id === q.answerId).label, expected);
    });

    const listening = core.buildQuestions('listening', targets, [], rngOf(3));
    listening.forEach((q) => {
        assert.equal(q.prompt, 'audio');
        assert.equal(q.options.find((o) => o.id === q.answerId).label, q.word.term);
    });

    const typing = core.buildQuestions('typing', targets, [], rngOf(3));
    assert.ok(typing.every((q) => q.kind === 'typing'));

    const lonely = core.buildQuestions('quiz', [word('only', 'duy nhất')], [], rngOf(3));
    assert.equal(lonely.length, 1);
    assert.equal(lonely[0].kind, 'typing', 'fewer than 2 candidates falls back to typing');
});

test('buildQuestions(cloze): drops words without a usable example and builds blank options', () => {
    const withExample = word('run', 'chạy', { examples: [{ text: 'They run daily.' }] });
    const without = word('walk', 'đi bộ');
    const pool = words(6);
    const qs = core.buildQuestions('cloze', [withExample, without], pool, rngOf(2));
    assert.equal(qs.length, 1);
    assert.equal(qs[0].kind, 'cloze');
    assert.equal(qs[0].cloze.answer, 'run');
    assert.equal(qs[0].options.find((o) => o.id === qs[0].answerId).label, 'run');
    assert.ok(qs[0].options.length >= 2);
    assert.equal(core.buildQuestions('cloze', [without], pool, rngOf(2)).length, 0);
});

// ---------------------------------------------------------------- test
function clozeWord(term) {
    return word(term, 'nghĩa ' + term, { examples: [{ text: 'I really like ' + term + ' a lot.' }] });
}

test('buildTest: quotas, cloze redistribution, no repeated words, both true and false items', () => {
    const targets = [];
    for (let i = 0; i < 24; i++) { targets.push(i % 2 === 0 ? clozeWord('alpha' + i) : word('beta' + i, 'nghĩa beta' + i)); }
    const pool = words(20, 'pool');
    const questions = core.buildTest(targets, pool, { types: ['choice', 'tf', 'typing', 'cloze'], ask: 'both', count: 20 }, rngOf(11));
    assert.equal(questions.length, 20);
    const ids = questions.map((q) => q.word.id);
    assert.equal(new Set(ids).size, 20, 'each word appears once');
    const byKind = {};
    questions.forEach((q) => { byKind[q.kind] = (byKind[q.kind] || 0) + 1; });
    assert.equal(byKind.choice, 5);
    assert.equal(byKind.truefalse, 5);
    assert.equal(byKind.typing, 5);
    assert.equal(byKind.cloze, 5);
    const tf = questions.filter((q) => q.kind === 'truefalse');
    assert.ok(tf.some((q) => q.isTrue) && tf.some((q) => !q.isTrue), 'both outcomes occur');
    tf.forEach((q) => {
        const truth = q.prompt === 'term' ? q.word.definition : q.word.term;
        assert.equal(q.shown === truth, q.isTrue);
    });

    // remainder goes to random types: 10 questions over 3 types -> 4/3/3 in some order.
    const odd = core.buildTest(words(30), pool, { types: 'choice,tf,typing', count: 10 }, rngOf(5));
    const counts = Object.values(odd.reduce((a, q) => { a[q.kind] = (a[q.kind] || 0) + 1; return a; }, {})).sort();
    assert.deepEqual(counts, [3, 3, 4]);
});

test('buildTest: an unfilled cloze quota moves to other types; cloze-only falls back; tiny sets use typing', () => {
    const targets = [clozeWord('alpha'), ...words(11)];
    const qs = core.buildTest(targets, words(10, 'p'), { types: ['choice', 'cloze'], count: 10 }, rngOf(4));
    assert.equal(qs.length, 10);
    assert.equal(qs.filter((q) => q.kind === 'cloze').length, 1);
    assert.equal(qs.filter((q) => q.kind === 'choice').length, 9);

    const only = core.buildTest(words(6), words(10, 'p'), { types: ['cloze'], count: 6 }, rngOf(4));
    assert.equal(only.length, 6);
    assert.ok(only.every((q) => q.kind === 'choice'));

    const few = core.buildTest([word('solo', 'một mình')], [], { types: ['choice', 'tf'], count: 10 }, rngOf(4));
    assert.equal(few.length, 1);
    assert.equal(few[0].kind, 'typing');
    assert.equal(core.buildTest([], [], { count: 5 }, rngOf(1)).length, 0);
});

test('buildTest: ask=term and ask=meaning fix the prompt; same seed rebuilds the same test', () => {
    const t = words(12);
    const a = core.buildTest(t, [], { types: ['choice'], ask: 'term', count: 12 }, rngOf(9));
    assert.ok(a.every((q) => q.prompt === 'term'));
    const b = core.buildTest(t, [], { types: ['choice'], ask: 'meaning', count: 12 }, rngOf(9));
    assert.ok(b.every((q) => q.prompt === 'meaning'));
    const again = core.buildTest(t, [], { types: ['choice'], ask: 'term', count: 12 }, rngOf(9));
    assert.equal(core.testId(a), core.testId(again));
    assert.notEqual(core.testId(a), core.testId(b));
    assert.deepEqual(core.parseTestTypes('cloze,bogus,choice'), ['choice', 'cloze']);
    assert.deepEqual(core.parseTestTypes('bogus'), ['choice', 'tf', 'typing']);
    assert.equal(core.parseTestAsk('nope'), 'both');
});

test('gradeTest: correct, close, wrong and blank answers; text helpers', () => {
    const w1 = word('apple', 'quả táo'); const w2 = word('banana', 'quả chuối'); const w3 = word('orange', 'quả cam'); const w4 = word('grape', 'nho', { phonetic: '/greɪp/' });
    const choice = { id: 'c', kind: 'choice', prompt: 'term', word: w1, options: [{ id: '1', label: 'quả táo' }, { id: '2', label: 'quả lê' }], answerId: '1' };
    const tf = { id: 't', kind: 'truefalse', prompt: 'term', word: w2, shown: 'quả cam', isTrue: false };
    const typing = { id: 'y', kind: 'typing', word: w3 };
    const typing2 = { id: 'z', kind: 'typing', word: w4 };
    const questions = [choice, tf, typing, typing2];

    const result = core.gradeTest(questions, { c: '1', t: 'false', y: 'orenge ', z: '' });
    assert.deepEqual(result.items.map((i) => i.result), ['correct', 'correct', 'close', 'wrong']);
    assert.equal(result.correct, 3);
    assert.equal(result.total, 4);
    assert.equal(result.percent, 75);

    const wrong = core.gradeTest(questions, { c: '2', t: 'true', y: 'zzz' });
    assert.deepEqual(wrong.items.map((i) => i.result), ['wrong', 'wrong', 'wrong', 'wrong']);
    assert.equal(wrong.items[3].answer, '');
    assert.equal(core.gradeTest([], {}).percent, 0);

    assert.equal(core.answerText(choice, '2'), 'quả lê');
    assert.equal(core.answerText(tf, 'true'), 'Đúng');
    assert.equal(core.answerText(tf, 'false'), 'Sai');
    assert.equal(core.answerText(typing, '  orenge '), 'orenge');
    assert.equal(core.expectedText(choice), 'quả táo');
    assert.equal(core.expectedText(tf), 'Sai (banana — quả chuối)');
    assert.equal(core.expectedText(typing2), 'grape · /greɪp/');
    assert.equal(core.testRemark(95), 'Xuất sắc!');
    assert.equal(core.testRemark(75), 'Khá lắm!');
    assert.equal(core.testRemark(10), 'Cần ôn thêm một chút.');
});

// ---------------------------------------------------------------- learn
test('learn: rounds of 7, misses re-queue and reset, mastered words are never asked', () => {
    const ids = Array.from({ length: 10 }, (_, i) => i + 1);
    const s = core.initLearn(ids);
    assert.equal(core.progress(s).notStarted, 10);
    core.startRound(s);
    assert.equal(s.round, 1);
    assert.deepEqual(s.queue, [1, 2, 3, 4, 5, 6, 7]);
    assert.deepEqual(s.roundWords, s.queue);
    assert.equal(core.roundDone(s), false);

    core.answer(s, 1, false);
    assert.deepEqual(s.queue, [2, 3, 4, 5, 6, 7, 1], 'miss goes to the end');
    assert.equal(core.levelOf(s, 1), 0);
    assert.equal(s.words[0].misses, 1);
    core.answer(s, 2, true); core.answer(s, 3, true);
    assert.equal(core.levelOf(s, 2), 1);
    core.answer(s, 999, true);            // stray ids are ignored
    core.answer(s, 2, true);              // no longer queued -> ignored
    assert.equal(core.levelOf(s, 2), 1);
    [4, 5, 6, 7, 1].forEach((id) => core.answer(s, id, true));
    assert.equal(core.roundDone(s), true);
    assert.deepEqual(core.progress(s), { notStarted: 3, learning: 7, mastered: 0 });

    // round 2: seen words first (7), then new words fill the round; the 3 unseen come after.
    const preview = core.previewRound(s);
    assert.deepEqual(preview.ids, [1, 2, 3, 4, 5, 6, 7]);
    assert.deepEqual([preview.fresh, preview.review], [0, 7]);
    core.startRound(s);
    assert.equal(s.round, 2);
    s.queue.slice().forEach((id) => core.answer(s, id, true));
    assert.equal(core.progress(s).mastered, 7);
    core.startRound(s);
    assert.deepEqual(s.queue, [8, 9, 10], 'mastered words are never asked again');
    assert.equal(core.learnComplete(s), false);
    [8, 9, 10].forEach((id) => core.answer(s, id, true));
    core.startRound(s);
    assert.deepEqual(s.queue, [8, 9, 10], 'level 1 words come back as typing');
    [8, 9, 10].forEach((id) => core.answer(s, id, true));
    assert.equal(core.learnComplete(s), true);
    assert.equal(core.learnPercent(s), 100);
});

test('learn: questionFor, stars, stages, dots', () => {
    const pool = words(8);
    const s = core.initLearn(pool.map((w) => w.id));
    assert.equal(core.questionFor(s, pool[0], pool, rngOf(1)).kind, 'choice');
    core.startRound(s); core.answer(s, pool[0].id, true);
    assert.equal(core.questionFor(s, pool[0], pool, rngOf(1)).kind, 'typing');
    assert.equal(core.questionFor(core.initLearn([5]), word('solo', 'x', { id: 5 }), [], rngOf(1)).kind, 'typing', 'no distractors -> typing');

    assert.equal(core.starsFor(9, 10), 3);
    assert.equal(core.starsFor(7, 10), 2);
    assert.equal(core.starsFor(6, 10), 1);
    assert.equal(core.starsFor(0, 0), 1);

    core.recordRound(s, 3); core.recordRound(s, 2);
    assert.equal(core.roundsDone(s), 2);
    assert.deepEqual(s.roundStars, [3, 2]);
    assert.equal(core.stageCount(s), 2 + 2, '2 done + ceil(8 open / 7)');

    assert.equal(core.masteryDots(s, pool[0].id), 2);
    assert.equal(core.masteryDots(s, pool[1].id), 0);
    core.answer(s, pool[1].id, false);
    assert.equal(core.masteryDots(s, pool[1].id), 1);
    s.words[2].level = 2;
    assert.equal(core.masteryDots(s, pool[2].id), 3);
});

test('restoreLearn: drops removed ids, appends new targets up to the limit, rejects another version', () => {
    const s = core.initLearn([1, 2, 3]);
    core.startRound(s); core.answer(s, 1, true);
    const saved = JSON.parse(JSON.stringify(s));

    const kept = core.restoreLearn(saved, [1, 3], [1, 3, 4, 5], 4);
    assert.deepEqual(kept.words.map((w) => w.wordId), [1, 3, 4, 5]);
    assert.equal(kept.words[0].level, 1);
    assert.ok(!kept.queue.includes(2) && !kept.roundWords.includes(2));
    assert.equal(kept.round, 1);

    const limited = core.restoreLearn(saved, [1, 2, 3], [1, 2, 3, 4, 5], 3);
    assert.equal(limited.words.length, 3);

    assert.equal(core.restoreLearn(Object.assign({}, saved, { version: 2 }), [1], [1], 5), null);
    assert.equal(core.restoreLearn(saved, [99], [], 5), null, 'empty result');
    assert.equal(core.restoreLearn(null, [1], [1], 5), null);
    assert.equal(core.restoreLearn({ version: 1, words: 'x' }, [1], [1], 5), null);
});

// ---------------------------------------------------------------- match
test('buildMatchBoards: each pair once, no duplicate term/meaning on a board, deterministic', () => {
    const ws = words(12);
    const boards = core.buildMatchBoards(ws, 12, rngOf(21));
    assert.equal(boards.length, 2);
    const seen = new Set();
    boards.forEach((b) => {
        assert.equal(b.pairIds.length, 6);
        assert.equal(b.tiles.length, 12);
        b.tiles.forEach((t) => { assert.ok(t.id === t.pairId + ':t' || t.id === t.pairId + ':m'); });
        b.pairIds.forEach((id) => { assert.ok(!seen.has(id)); seen.add(id); });
        const labels = b.tiles.map((t) => core.textKey(t.label));
        assert.equal(new Set(labels).size, labels.length);
    });
    assert.equal(seen.size, 12);
    assert.deepEqual(core.buildMatchBoards(ws, 12, rngOf(21)).map((b) => b.tiles.map((t) => t.id)),
        boards.map((b) => b.tiles.map((t) => t.id)));

    const a = word('same', 'một'); const b = word('Same', 'hai'); const c = word('x', 'một');
    const clash = core.buildMatchBoards([a, b, c, ...words(3)], 6, rngOf(2));
    clash.forEach((board) => {
        const terms = board.tiles.filter((t) => t.side === 'term').map((t) => core.textKey(t.label));
        const defs = board.tiles.filter((t) => t.side === 'meaning').map((t) => core.textKey(t.label));
        assert.equal(new Set(terms).size, terms.length);
        assert.equal(new Set(defs).size, defs.length);
    });
});

test('buildMatchBoards: a last single-pair board is dropped; isPair; formatMs', () => {
    const boards = core.buildMatchBoards(words(7), 7, rngOf(1));
    assert.equal(boards.length, 1);
    assert.equal(boards[0].pairIds.length, 6);
    assert.equal(core.buildMatchBoards(words(1), 6, rngOf(1)).length, 0);

    const t = { pairId: 1, side: 'term' }; const m = { pairId: 1, side: 'meaning' };
    assert.equal(core.isPair(t, m), true);
    assert.equal(core.isPair(t, t), false);
    assert.equal(core.isPair(t, { pairId: 2, side: 'meaning' }), false);
    assert.equal(core.isPair(t, null), false);

    assert.equal(core.formatMs(12345), '12,3 giây');
    assert.equal(core.formatMs(0), '0,0 giây');
    assert.equal(core.formatMs(59949), '59,9 giây');
    assert.equal(core.formatMs(65200), '1 phút 05,2 giây');
    assert.equal(core.formatMs(125000), '2 phút 05,0 giây');
    assert.equal(core.formatDuration(65000), '01:05');
});

// ---------------------------------------------------------------- typing aids
test('typing aids: diffChars, hintMask, maxHints', () => {
    const d = core.diffChars('Colour', 'color');
    assert.equal(d.typed.map((c) => c.ch).join(''), 'Colour');
    assert.deepEqual(d.typed.filter((c) => !c.ok).map((c) => c.ch), ['u']);
    assert.deepEqual(d.answer.filter((c) => !c.ok).map((c) => c.ch), []);
    const e = core.diffChars('cat', 'cart');
    assert.deepEqual(e.answer.filter((c) => !c.ok).map((c) => c.ch), ['r']);
    assert.deepEqual(core.diffChars('', 'abc').answer.map((c) => c.ok), [false, false, false]);

    assert.equal(core.hintMask('look forward to', 0), '____ _______ __');
    assert.equal(core.hintMask('look forward to', 3), 'loo_ _______ __');
    assert.equal(core.hintMask('look forward to', 6), 'look fo_____ __');
    assert.equal(core.hintMask('ab', 99), 'ab');
    assert.equal(core.maxHints('look forward to'), 12);
    assert.equal(core.maxHints('a'), 0);
    assert.equal(core.maxHints(''), 0);
});

// ---------------------------------------------------------------- suggestion
test('suggestMode table', () => {
    assert.equal(core.suggestMode({}), 'learn');
    assert.equal(core.suggestMode({ learnPercent: null }), 'learn');
    assert.equal(core.suggestMode({ learnPercent: 60, testPercent: 100 }), 'learn');
    assert.equal(core.suggestMode({ learnPercent: 100 }), 'test');
    assert.equal(core.suggestMode({ learnPercent: 100, testPercent: 79 }), 'learn');
    assert.equal(core.suggestMode({ learnPercent: 100, testPercent: 80 }), 'match');
    assert.equal(core.suggestMode(null), 'learn');
});

// ---------------------------------------------------------------- counts
test('countsFor matches the plan table and the C# table', () => {
    const expected = {
        flashcard: [20, 50, 0], match: [6, 12, 18], learn: [10, 20, 50],
        test: [10, 20, 30], quiz: [10, 20, 30], typing: [10, 20, 30], listening: [10, 20, 30], cloze: [10, 20, 30]
    };
    Object.keys(expected).forEach((mode) => assert.deepEqual(core.countsFor(mode), expected[mode], mode));
    assert.deepEqual(core.countUnit('match'), { legend: 'Số cặp', unit: 'cặp' });
    assert.deepEqual(core.countUnit('flashcard'), { legend: 'Số thẻ', unit: 'thẻ' });
    assert.deepEqual(core.countUnit('learn'), { legend: 'Số từ', unit: 'từ' });
    assert.deepEqual(core.countUnit('typing'), { legend: 'Số câu', unit: 'câu' });
    assert.deepEqual(core.STUDY_GAMES, ['learn', 'test', 'match', 'flashcard']);
    assert.deepEqual(core.SKILLS, ['quiz', 'typing', 'listening', 'cloze']);

    // Parse the C# switch so the two tables cannot drift.
    const cs = fs.readFileSync(path.join(__dirname, '..', '..', 'TCTEnglish', 'Services', 'Sets', 'PracticeModes.cs'), 'utf8');
    const body = cs.slice(cs.indexOf('CountsFor(string mode)'));
    const csTable = {};
    for (const m of body.matchAll(/(Flashcard|Match|Learn)\s*=>\s*new\[\]\s*\{([^}]*)\}/g)) {
        csTable[m[1].toLowerCase()] = m[2].split(',').map((x) => Number(x.trim()));
    }
    const fallback = /_\s*=>\s*new\[\]\s*\{([^}]*)\}/.exec(body);
    assert.ok(fallback, 'C# default counts found');
    ['test', 'quiz', 'typing', 'listening', 'cloze'].forEach((mode) => { csTable[mode] = fallback[1].split(',').map((x) => Number(x.trim())); });
    assert.deepEqual(csTable, expected);

    Object.keys(core.MODE_INFO).forEach((key) => {
        assert.ok(cs.includes('"' + core.MODE_INFO[key].label + '"'), 'label in C#: ' + key);
        assert.ok(cs.includes('"' + core.MODE_INFO[key].hint + '"'), 'hint in C#: ' + key);
    });
});

// ---------------------------------------------------------------- storage
function fakeStorage() {
    const data = new Map();
    return {
        getItem: (k) => (data.has(k) ? data.get(k) : null),
        setItem: (k, v) => { data.set(k, String(v)); },
        removeItem: (k) => { data.delete(k); },
        _data: data
    };
}
const throwing = { getItem() { throw new Error('denied'); }, setItem() { throw new Error('denied'); }, removeItem() { throw new Error('denied'); } };

test('storage: keys, set-stats merge keeps quizPercent, lower match time wins, best time and draft round trip', () => {
    const local = fakeStorage(); const session = fakeStorage();
    const store = core.createStore({ local, session }, () => 1000);
    assert.equal(store.keys.learn(5, 'set:9'), 'tct:learn:5:set:9');
    assert.equal(store.keys.matchBest(5, 'mine', 6), 'tct:match-best:5:mine:6');
    assert.equal(store.keys.testDraft(5, 'abc'), 'tct:test-draft:5:abc');
    assert.equal(store.keys.setStats(9), 'tct:set-stats:9');
    assert.equal(store.keys.setup(5), 'tct:practice-setup:5');

    local.setItem('tct:set-stats:9', JSON.stringify({ quizPercent: 80, matchMs: 20000 }));
    store.mergeStats(9, { learnPercent: 60 });
    store.mergeStats(9, { testPercent: 90 });
    store.mergeStats(9, { matchMs: 25000, matchPairs: 6 });   // slower: ignored
    store.mergeStats(9, { matchMs: 15000, matchPairs: 6 });   // faster: wins
    store.mergeStats(9, { matchMs: 1000, matchPairs: 4 });    // under 6 pairs: ignored
    assert.deepEqual(store.readStats(9), { quizPercent: 80, matchMs: 15000, matchAt: 1000, learnPercent: 60, learnAt: 1000, testPercent: 90, testAt: 1000 });

    assert.equal(store.setMatchBest(5, 'mine', 6, 30000), true);
    assert.equal(store.setMatchBest(5, 'mine', 6, 31000), false);
    assert.equal(store.setMatchBest(5, 'mine', 6, 29000), true);
    assert.deepEqual(store.getMatchBest(5, 'mine', 6), { ms: 29000, at: 1000 });
    assert.equal(store.getMatchBest(5, 'mine', 12), null);

    const state = core.initLearn([1, 2]);
    store.saveLearn(5, 'set:9', state);
    assert.deepEqual(store.loadLearn(5, 'set:9'), state);
    store.clearLearn(5, 'set:9');
    assert.equal(store.loadLearn(5, 'set:9'), null);

    store.saveDraft(5, 'abc', { seed: 3, answers: { a: '1' } });
    assert.deepEqual(store.loadDraft(5, 'abc'), { seed: 3, answers: { a: '1' } });
    assert.equal(session._data.size, 1);
    store.clearDraft(5, 'abc');
    assert.equal(store.loadDraft(5, 'abc'), null);

    store.saveSetup(5, { mode: 'test', n: 20 });
    assert.deepEqual(store.loadSetup(5), { mode: 'test', n: 20 });
});

test('storage: corrupt, throwing and missing storages never throw', () => {
    const corrupt = fakeStorage();
    corrupt.setItem('tct:set-stats:1', '{not json');
    corrupt.setItem('tct:learn:1:mine', '[1,');
    const original = console.warn; console.warn = () => {};
    try {
        const a = core.createStore({ local: corrupt, session: corrupt });
        assert.deepEqual(a.readStats(1), {});
        assert.equal(a.loadLearn(1, 'mine'), null);
        assert.equal(a.mergeStats(1, { learnPercent: 10 }), true, 'overwrites the unreadable entry');

        const b = core.createStore({ local: throwing, session: throwing });
        assert.deepEqual(b.readStats(1), {});
        assert.equal(b.loadLearn(1, 'mine'), null);
        assert.equal(b.saveLearn(1, 'mine', {}), false);
        assert.equal(b.mergeStats(1, { testPercent: 1 }), false);
        assert.equal(b.setMatchBest(1, 'mine', 6, 100), true);
        b.clearLearn(1, 'mine'); b.clearDraft(1, 'x');

        const c = core.createStore({});
        assert.equal(c.loadSetup(1), null);
        assert.equal(c.saveDraft(1, 'x', {}), false);
    } finally { console.warn = original; }
});

test('practice-core.js is pure: no DOM or storage globals referenced', () => {
    const src = fs.readFileSync(path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'practice-core.js'), 'utf8');
    const code = src.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');
    for (const banned of ['document.', 'window.', 'localStorage', 'sessionStorage', 'navigator.', 'fetch(', 'XMLHttpRequest']) {
        assert.ok(!code.includes(banned), 'must not reference ' + banned);
    }
});
