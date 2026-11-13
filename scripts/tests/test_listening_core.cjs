'use strict';
// node --test scripts/tests/test_listening_core.cjs
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const FILE = path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'listening-core.js');
const core = require(FILE);
const rngOf = (seed) => core.mulberry32(seed);

// ---------------------------------------------------------------- purity
test('listening-core.js is pure: no document, window or fetch outside comments', () => {
    const code = fs.readFileSync(FILE, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');
    assert.doesNotMatch(code, /\bdocument\b/);
    assert.doesNotMatch(code, /\bwindow\b/);
    assert.doesNotMatch(code, /\bfetch\b/);
    assert.doesNotMatch(code, /\blocalStorage\b/);
});

// ---------------------------------------------------------------- normalize / tokenize / levenshtein
test('normalize: NFC, case, curly apostrophes, punctuation, inner apostrophes and hyphens', () => {
    assert.equal(core.normalize("  Hello,   WORLD!  "), 'hello world');
    assert.equal(core.normalize('Don’t stop'), "don't stop");
    assert.equal(core.normalize("It's a well-known fact."), "it's a well-known fact");
    assert.equal(core.normalize("'quoted' -dash- words"), 'quoted dash words');
    assert.equal(core.normalize('café'), 'café'); // decomposed accent -> NFC
    assert.equal(core.normalize('Xin chào, thế giới!'), 'xin chào thế giới');
    assert.equal(core.normalize(null), '');
    assert.equal(core.normalize('?!...'), '');
});

test('tokenize: words, and an empty array for blank input', () => {
    assert.deepEqual(core.tokenize("Don't stop, well-known!"), ["don't", 'stop', 'well-known']);
    assert.deepEqual(core.tokenize(''), []);
    assert.deepEqual(core.tokenize('   '), []);
    assert.deepEqual(core.tokenize(undefined), []);
});

test('levenshtein: known distances', () => {
    assert.equal(core.levenshtein('kitten', 'sitting'), 3);
    assert.equal(core.levenshtein('', 'abc'), 3);
    assert.equal(core.levenshtein('abc', ''), 3);
    assert.equal(core.levenshtein('same', 'same'), 0);
    assert.equal(core.levenshtein('weather', 'wether'), 1);
});

// ---------------------------------------------------------------- gradeDictation
test('gradeDictation: exact match ignores case and punctuation', () => {
    const r = core.gradeDictation("i really like the weather, today", 'I really like the weather today.');
    assert.equal(r.status, 'correct');
    assert.equal(r.score, 1);
    assert.equal(r.extra, 0);
    assert.deepEqual(r.words.map((w) => w.state), ['ok', 'ok', 'ok', 'ok', 'ok', 'ok']);
    assert.equal(r.words[0].text, 'I'); // display keeps the answer's case
    assert.equal(r.words[5].text, 'today'); // edge punctuation stripped
});

test('gradeDictation: a one-letter slip in a 4+ letter word is "near" and the sentence is near at 5 of 6', () => {
    const r = core.gradeDictation('I really like the wether today', 'I really like the weather today');
    assert.equal(r.words[4].state, 'near');
    assert.equal(r.status, 'near');
    assert.ok(Math.abs(r.score - 5 / 6) < 1e-9);
});

test('gradeDictation: words under 4 letters are never "near"', () => {
    const r = core.gradeDictation('I like the cut now', 'I like the cat now');
    assert.equal(r.words[3].state, 'wrong');
    assert.equal(r.score, 4 / 5);
});

test('gradeDictation: missing word, wrong word and blank input', () => {
    const missing = core.gradeDictation('I like apples', 'I like red apples');
    assert.deepEqual(missing.words.map((w) => w.state), ['ok', 'ok', 'missing', 'ok']);
    assert.equal(missing.status, 'wrong');

    const wrong = core.gradeDictation('I like green apples', 'I like red apples');
    assert.deepEqual(wrong.words.map((w) => w.state), ['ok', 'ok', 'wrong', 'ok']);

    const blank = core.gradeDictation('   ', 'Hello there');
    assert.equal(blank.status, 'wrong');
    assert.equal(blank.score, 0);
    assert.deepEqual(blank.words.map((w) => w.state), ['missing', 'missing']);
});

test('gradeDictation: an extra typed word keeps a perfect score from being "correct"', () => {
    const r = core.gradeDictation('I like the red apples', 'I like red apples');
    assert.equal(r.score, 1);
    assert.equal(r.extra, 1);
    assert.equal(r.status, 'near');
});

test('gradeDictation: a mostly different sentence is wrong; thresholds are 1 and 0.8', () => {
    assert.equal(core.gradeDictation('completely different words here', 'I really like the weather').status, 'wrong');
    assert.equal(core.gradeDictation('one two three four', 'one two three four five').status, 'near'); // 4/5 = 0.8
    assert.equal(core.gradeDictation('one two three', 'one two three four five').status, 'wrong'); // 3/5
});

test('gradeDictation: an empty answer is only correct for empty input', () => {
    assert.equal(core.gradeDictation('', '').status, 'correct');
    assert.equal(core.gradeDictation('hi', '').status, 'wrong');
});

// ---------------------------------------------------------------- pickBlanks / gradeBlank
function joinParts(parts) { return parts.map((p) => p.value).join(''); }

test('pickBlanks: deterministic for a seed, different across seeds, text is preserved', () => {
    const line = 'The teacher explained the difficult lesson to every student yesterday morning.';
    const a = core.pickBlanks(line, rngOf(7));
    const b = core.pickBlanks(line, rngOf(7));
    assert.deepEqual(a, b);
    assert.equal(joinParts(a.parts), line);
    const seen = new Set();
    for (let seed = 1; seed <= 25; seed++) seen.add(JSON.stringify(core.pickBlanks(line, rngOf(seed)).parts));
    assert.ok(seen.size > 1);
});

test('pickBlanks: 1 to 4 blanks, about one per 5 words, never adjacent, only 3+ letter non-stop words', () => {
    const short = 'She bought fresh bread today.'; // 5 words -> 1 blank
    assert.equal(core.pickBlanks(short, rngOf(3)).blanks, 1);
    const long = Array.from({ length: 40 }, (_, i) => 'planet' + String.fromCharCode(97 + (i % 26))).join(' ');
    assert.equal(core.pickBlanks(long, rngOf(3)).blanks, 4); // capped at 4

    const stop = new Set(core.STOP_WORDS);
    for (let seed = 1; seed <= 60; seed++) {
        const line = 'We are going to visit the beautiful old museum near the river and then eat some lovely food.';
        const r = core.pickBlanks(line, rngOf(seed));
        const words = line.match(/[A-Za-z']+/g);
        const idx = [];
        r.parts.forEach((p) => {
            if (p.type !== 'blank') return;
            assert.ok(p.answer.length >= 3, p.answer);
            assert.ok(!stop.has(p.answer.toLowerCase()), p.answer);
            idx.push(words.indexOf(p.answer));
        });
        assert.ok(idx.length >= 1 && idx.length <= 4);
        for (let k = 1; k < idx.length; k++) assert.ok(idx[k] - idx[k - 1] >= 2, 'adjacent blanks at seed ' + seed);
        assert.equal(joinParts(r.parts), line);
    }
});

test('pickBlanks: punctuation stays outside the blank; a one-word line is blanked once', () => {
    const r = core.pickBlanks('Really? Yes, absolutely!', rngOf(1));
    r.parts.filter((p) => p.type === 'blank').forEach((p) => assert.match(p.answer, /^[A-Za-z]+$/));
    assert.equal(joinParts(r.parts), 'Really? Yes, absolutely!');
    const one = core.pickBlanks('Hello', rngOf(1));
    assert.equal(one.blanks, 1);
    assert.deepEqual(one.parts, [{ type: 'blank', value: 'Hello', answer: 'Hello' }]);
});

test('pickBlanks: a line with no eligible word returns no blanks', () => {
    const stopsOnly = core.pickBlanks('It is on the table.'.replace('table', 'it'), rngOf(1));
    assert.equal(stopsOnly.blanks, 0);
    assert.deepEqual(stopsOnly.parts, [{ type: 'text', value: 'It is on the it.' }]);
    assert.equal(core.pickBlanks('', rngOf(1)).blanks, 0);
    assert.equal(core.pickBlanks('Is it?', rngOf(1)).blanks, 0);
    assert.equal(core.pickBlanks('Oh no!', rngOf(1)).blanks, 0); // 2-letter words only
});

test('gradeBlank: correct, near, wrong and blank', () => {
    assert.equal(core.gradeBlank('Weather', 'weather'), 'correct');
    assert.equal(core.gradeBlank(' weather. ', 'weather'), 'correct');
    assert.equal(core.gradeBlank('wether', 'weather'), 'near');
    assert.equal(core.gradeBlank('cut', 'cat'), 'wrong'); // under 4 letters
    assert.equal(core.gradeBlank('window', 'weather'), 'wrong');
    assert.equal(core.gradeBlank('', 'weather'), 'wrong');
    assert.equal(core.gradeBlank("don't", "don't"), 'correct');
});

// ---------------------------------------------------------------- filterLessons / paginate
const LESSONS = [
    { id: 1, title: 'Cuộc sống hàng ngày', topic: 'Daily life', level: 'A1', status: 'new' },
    { id: 2, title: 'Đi du lịch Nhật Bản', topic: 'Travel', level: 'B1', status: 'progress' },
    { id: 3, title: 'Ordering coffee', topic: 'Daily life', level: 'A1', status: 'done' },
    { id: 4, title: 'Job interview tips', topic: 'Work', level: 'B2', status: 'new' }
];

test('filterLessons: level, topic and status, alone and combined; "all" and empty mean no filter', () => {
    assert.deepEqual(core.filterLessons(LESSONS, {}), [1, 2, 3, 4]);
    assert.deepEqual(core.filterLessons(LESSONS, { level: 'all', topic: '', status: 'all', q: '' }), [1, 2, 3, 4]);
    assert.deepEqual(core.filterLessons(LESSONS, { level: 'A1' }), [1, 3]);
    assert.deepEqual(core.filterLessons(LESSONS, { topic: 'Daily life' }), [1, 3]);
    assert.deepEqual(core.filterLessons(LESSONS, { status: 'new' }), [1, 4]);
    assert.deepEqual(core.filterLessons(LESSONS, { level: 'A1', status: 'done' }), [3]);
    assert.deepEqual(core.filterLessons(LESSONS, { level: 'C1' }), []);
    assert.deepEqual(core.filterLessons(null, { level: 'A1' }), []);
});

test('filterLessons: search is case- and accent-insensitive on title and topic (incl. đ)', () => {
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'cuoc song' }), [1]);
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'CUỘC' }), [1]);
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'di du lich' }), [2]);
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'đi du' }), [2]);
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'daily' }), [1, 3]); // matches the topic
    assert.deepEqual(core.filterLessons(LESSONS, { q: '  coffee  ' }), [3]);
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'nothing like this' }), []);
    assert.deepEqual(core.filterLessons(LESSONS, { q: 'daily', level: 'A1', status: 'done' }), [3]);
});

test('paginate: visible slice and hasMore', () => {
    assert.deepEqual(core.paginate([1, 2, 3, 4, 5, 6, 7], 6), { visible: [1, 2, 3, 4, 5, 6], hasMore: true });
    assert.deepEqual(core.paginate([1, 2], 6), { visible: [1, 2], hasMore: false });
    assert.deepEqual(core.paginate([1, 2, 3, 4, 5, 6], 6), { visible: [1, 2, 3, 4, 5, 6], hasMore: false });
    assert.deepEqual(core.paginate([], 6), { visible: [], hasMore: false });
    assert.deepEqual(core.paginate([1, 2], -3), { visible: [], hasMore: true });
    assert.deepEqual(core.paginate(null, 6), { visible: [], hasMore: false });
});

// ---------------------------------------------------------------- lineIndexAt / formatClock
test('lineIndexAt: before, inside, on boundaries, in gaps and after the last line', () => {
    const lines = [{ start: 1, end: 3 }, { start: 4, end: 7 }, { start: 8, end: null }];
    assert.equal(core.lineIndexAt(lines, 0), -1);
    assert.equal(core.lineIndexAt(lines, 1), 0);
    assert.equal(core.lineIndexAt(lines, 2.99), 0);
    assert.equal(core.lineIndexAt(lines, 3), -1); // end is exclusive: a gap
    assert.equal(core.lineIndexAt(lines, 3.5), -1);
    assert.equal(core.lineIndexAt(lines, 4), 1);
    assert.equal(core.lineIndexAt(lines, 6.99), 1);
    assert.equal(core.lineIndexAt(lines, 7), -1);
    assert.equal(core.lineIndexAt(lines, 8), 2);
    assert.equal(core.lineIndexAt(lines, 5000), 2); // last line without end stays active
    assert.equal(core.lineIndexAt([{ start: 0, end: 3 }], 5), -1); // after the last line's end
});

test('lineIndexAt: a missing end uses the next start; empty and invalid input', () => {
    const lines = [{ start: 0 }, { start: 5 }, { start: 9, end: 12 }];
    assert.equal(core.lineIndexAt(lines, 4.9), 0);
    assert.equal(core.lineIndexAt(lines, 5), 1);
    assert.equal(core.lineIndexAt(lines, 11), 2);
    assert.equal(core.lineIndexAt(lines, 12), -1);
    assert.equal(core.lineIndexAt([], 3), -1);
    assert.equal(core.lineIndexAt(null, 3), -1);
    assert.equal(core.lineIndexAt(lines, NaN), -1);
});

test('lineIndexAt: binary search over a long transcript', () => {
    const lines = Array.from({ length: 2000 }, (_, i) => ({ start: i * 3, end: i * 3 + 2 }));
    assert.equal(core.lineIndexAt(lines, 0), 0);
    assert.equal(core.lineIndexAt(lines, 1500 * 3 + 1), 1500);
    assert.equal(core.lineIndexAt(lines, 1999 * 3 + 2), -1);
    assert.equal(core.lineIndexAt(lines, 1999 * 3 + 1.5), 1999);
});

test('formatClock: m:ss and h:mm:ss', () => {
    assert.equal(core.formatClock(0), '0:00');
    assert.equal(core.formatClock(5.9), '0:05');
    assert.equal(core.formatClock(65), '1:05');
    assert.equal(core.formatClock(599), '9:59');
    assert.equal(core.formatClock(3600), '1:00:00');
    assert.equal(core.formatClock(3725), '1:02:05');
    assert.equal(core.formatClock(-4), '0:00');
    assert.equal(core.formatClock('x'), '0:00');
});

// ---------------------------------------------------------------- requiredSteps / isComplete
test('requiredSteps: only sections that have content, in page order', () => {
    assert.deepEqual(core.requiredSteps({ transcript: 3, quiz: 2, vocab: 4 }), ['transcript', 'vocab', 'quiz']);
    assert.deepEqual(core.requiredSteps({ transcript: 3, quiz: 0, vocab: 0 }), ['transcript']);
    assert.deepEqual(core.requiredSteps({ transcript: 3, quiz: 5, vocab: 0 }), ['transcript', 'quiz']);
    assert.deepEqual(core.requiredSteps({ transcript: 0, quiz: 0, vocab: 2 }), ['vocab']);
    assert.deepEqual(core.requiredSteps({ transcript: 0, quiz: 0, vocab: 0 }), []);
    assert.deepEqual(core.requiredSteps(null), []);
});

test('isComplete: every content combination mirrors the server rule', () => {
    const none = { transcript: false, quiz: false, vocab: false };
    const all = { transcript: true, quiz: true, vocab: true };
    const full = core.requiredSteps({ transcript: 1, quiz: 1, vocab: 1 });
    assert.equal(core.isComplete(none, full), false);
    assert.equal(core.isComplete({ transcript: true, quiz: false, vocab: false }, full), false);
    assert.equal(core.isComplete({ transcript: true, quiz: true, vocab: false }, full), false);
    assert.equal(core.isComplete(all, full), true);

    const transcriptOnly = core.requiredSteps({ transcript: 2, quiz: 0, vocab: 0 });
    assert.equal(core.isComplete({ transcript: true, quiz: false, vocab: false }, transcriptOnly), true);
    assert.equal(core.isComplete(none, transcriptOnly), false);

    const transcriptQuiz = core.requiredSteps({ transcript: 2, quiz: 3, vocab: 0 });
    assert.equal(core.isComplete({ transcript: true, quiz: true, vocab: false }, transcriptQuiz), true);
    assert.equal(core.isComplete({ transcript: true, quiz: false, vocab: true }, transcriptQuiz), false);

    const vocabOnly = core.requiredSteps({ transcript: 0, quiz: 0, vocab: 2 });
    assert.equal(core.isComplete({ transcript: false, quiz: false, vocab: true }, vocabOnly), true);

    assert.equal(core.isComplete(all, []), false); // a lesson with no content never completes
    assert.equal(core.isComplete(null, ['transcript']), false);
});

// ---------------------------------------------------------------- URL state
test('parseFilterQuery: known values pass, unknown fall back to "all"', () => {
    assert.deepEqual(core.parseFilterQuery('?level=B1&topic=Daily%20life&status=done&q=coffee'),
        { level: 'B1', topic: 'Daily life', status: 'done', q: 'coffee' });
    assert.deepEqual(core.parseFilterQuery('level=b2'), { level: 'B2', topic: 'all', status: 'all', q: '' });
    assert.deepEqual(core.parseFilterQuery('?level=Z9&status=nope&topic='), { level: 'all', topic: 'all', status: 'all', q: '' });
    assert.deepEqual(core.parseFilterQuery(''), { level: 'all', topic: 'all', status: 'all', q: '' });
    assert.deepEqual(core.parseFilterQuery(null), { level: 'all', topic: 'all', status: 'all', q: '' });
    assert.equal(core.parseFilterQuery('?topic=Secret', ['Daily life']).topic, 'all');
    assert.equal(core.parseFilterQuery('?topic=Daily%20life', ['Daily life']).topic, 'Daily life');
    assert.equal(core.parseFilterQuery('?q=' + 'x'.repeat(200)).q.length, 80);
});

test('buildFilterQuery: writes only non-default values and round-trips', () => {
    assert.equal(core.buildFilterQuery({ level: 'all', topic: 'all', status: 'all', q: '' }), '');
    assert.equal(core.buildFilterQuery({}), '');
    assert.equal(core.buildFilterQuery({ level: 'B1', topic: 'all', status: 'done', q: '' }), '?level=B1&status=done');
    const state = { level: 'A2', topic: 'Daily life', status: 'progress', q: 'cà phê' };
    assert.deepEqual(core.parseFilterQuery(core.buildFilterQuery(state)), state);
});

test('mulberry32: same seed, same sequence; values in [0, 1)', () => {
    const a = rngOf(42), b = rngOf(42);
    for (let i = 0; i < 20; i++) {
        const x = a();
        assert.equal(x, b());
        assert.ok(x >= 0 && x < 1);
    }
});
