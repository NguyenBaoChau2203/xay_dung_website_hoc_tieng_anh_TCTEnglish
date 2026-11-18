'use strict';
// node --test scripts/tests/test_listening_core_redesign.cjs
// Pure helpers added to listening-core.js for the direction-F redesign (word spans, word forms, vocab marks,
// dictation store, level summary, next step, subtitle, dictation verdict).
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const ROOT = path.join(__dirname, '..', '..');
const core = require(path.join(ROOT, 'TCTEnglish', 'wwwroot', 'js', 'listening-core.js'));
const FORMS = JSON.parse(fs.readFileSync(path.join(ROOT, 'TCTEnglish.Tests', 'Fixtures', 'listening-base-forms.json'), 'utf8'));

const joined = (spans) => spans.map((s) => s.v).join('');
const words = (spans) => spans.filter((s) => s.t === 'word').map((s) => s.key);

// ---------------------------------------------------------------- wordSpans
test('wordSpans: words and separators, nothing dropped', () => {
    const text = "Hello, world! Don't stop.";
    const spans = core.wordSpans(text);
    assert.equal(joined(spans), text);
    assert.deepEqual(words(spans), ['hello', 'world', "don't", 'stop']);
    assert.deepEqual(spans.map((s) => s.t), ['word', 'sep', 'word', 'sep', 'word', 'sep', 'word', 'sep']);
    assert.equal(spans[0].v, 'Hello');
    assert.equal(spans[1].v, ', ');
});

test('wordSpans: curly apostrophes stay inside the word, keys use a straight apostrophe', () => {
    const spans = core.wordSpans('Anna’s car isn’t here');
    assert.deepEqual(words(spans), ["anna's", 'car', "isn't", 'here']);
    assert.equal(spans[0].v, 'Anna’s');
    assert.equal(joined(spans), 'Anna’s car isn’t here');
});

test('wordSpans: hyphenated words are one word; a trailing apostrophe is a separator', () => {
    assert.deepEqual(words(core.wordSpans('a well-known fact')), ['a', 'well-known', 'fact']);
    const spans = core.wordSpans("the dogs' toys");
    assert.deepEqual(words(spans), ['the', 'dogs', 'toys']);
    assert.equal(joined(spans), "the dogs' toys");
});

test('wordSpans: numbers and symbols are separators, never clickable words', () => {
    const text = 'I paid 25 dollars at 3pm (about $5).';
    const spans = core.wordSpans(text);
    assert.equal(joined(spans), text);
    assert.deepEqual(words(spans), ['i', 'paid', 'dollars', 'at', 'about']);
    assert.ok(spans.every((s, i) => i === 0 || !(s.t === 'sep' && spans[i - 1].t === 'sep')), 'adjacent separators are merged');
});

test('wordSpans: empty, blank, punctuation-only and non-string input', () => {
    assert.deepEqual(core.wordSpans(''), []);
    assert.deepEqual(core.wordSpans(null), []);
    assert.deepEqual(core.wordSpans(undefined), []);
    assert.deepEqual(core.wordSpans('?!...'), [{ t: 'sep', v: '?!...' }]);
    assert.deepEqual(core.wordSpans('   '), [{ t: 'sep', v: '   ' }]);
});

test('wordSpans: accented and Vietnamese letters are words and are never altered', () => {
    const text = 'Xin chào, café mới!';
    const spans = core.wordSpans(text);
    assert.equal(joined(spans), text);
    assert.deepEqual(words(spans), ['xin', 'chào', 'café', 'mới']);
});

// ---------------------------------------------------------------- baseForms
test('baseForms: the shared fixture (the C# mirror asserts the same file)', () => {
    assert.equal(FORMS.cases.length, 20);
    FORMS.cases.forEach((c) => assert.deepEqual(core.baseForms(c.input), c.forms, c.input));
});

test('baseForms: always starts with the word itself, is unique and has at most six entries', () => {
    ['stopped', 'studied', 'running', 'boxes', 'cities', 'x', '', null, undefined, "o'clock", 'is', "children's"].forEach((w) => {
        const forms = core.baseForms(w);
        assert.ok(forms.length <= 6, String(w));
        assert.equal(new Set(forms).size, forms.length, String(w));
        if (w) assert.equal(forms[0], core.baseForms(w)[0]);
    });
    assert.deepEqual(core.baseForms(''), []);
    assert.deepEqual(core.baseForms('   '), []);
    assert.deepEqual(core.baseForms(null), []);
});

// ---------------------------------------------------------------- vocabIndex / markVocab
const ITEMS = [
    { word: 'latte', definition: 'cà phê sữa' },
    { word: 'to go', definition: 'mang đi' },
    { word: 'orders', definition: 'đặt hàng' },
    { word: 'break the ice', definition: 'phá vỡ sự ngượng' },
    { word: 'the ice', definition: 'băng' },
];

test('vocabIndex: a Map of base forms to items, phrases kept longest first', () => {
    const index = core.vocabIndex(ITEMS);
    assert.ok(index instanceof Map);
    assert.equal(index.get('latte').definition, 'cà phê sữa');
    assert.equal(index.get('order').word, 'orders');
    assert.equal(index.has('to'), false, 'a phrase word is not indexed as a single word');
    assert.deepEqual(index.phrases.map((p) => p.words.join(' ')), ['break the ice', 'to go', 'the ice']);
});

test('vocabIndex: tolerates empty, null and malformed items', () => {
    assert.equal(core.vocabIndex([]).size, 0);
    assert.equal(core.vocabIndex(null).size, 0);
    assert.equal(core.vocabIndex([null, {}, { word: '  ' }, { word: 'tea' }]).size, 1);
});

test('markVocab: single words (with inflections) and phrases, longest phrase first, does not mutate', () => {
    const index = core.vocabIndex(ITEMS);
    const spans = core.wordSpans('Can I have a latte to go, please? She ordered tea. He tried to break the ice with a joke.');
    const before = JSON.stringify(spans);
    const marked = core.markVocab(spans, index);
    assert.equal(JSON.stringify(spans), before);
    assert.equal(marked.length, spans.length);
    assert.equal(joined(marked), joined(spans));
    const flagged = marked.filter((s) => s.vocab).map((s) => s.key);
    assert.deepEqual(flagged, ['latte', 'to', 'go', 'ordered', 'break', 'the', 'ice']);
    const toGo = marked.filter((s) => s.vocab && (s.key === 'to' || s.key === 'go'));
    assert.ok(toGo.every((s) => s.vocab.word === 'to go' && s.phrase === toGo[0].phrase));
    const ice = marked.filter((s) => s.key === 'break' || s.key === 'the' && s.vocab || s.key === 'ice');
    assert.ok(ice.every((s) => s.vocab.word === 'break the ice'), 'the longest phrase wins over "the ice"');
    const loneTo = marked.find((s) => s.key === 'to' && !s.vocab);
    assert.ok(loneTo, 'a standalone "to" that is not part of the phrase stays unflagged');
});

test('markVocab: a phrase cannot span a sentence break and an empty index changes nothing', () => {
    const index = core.vocabIndex([{ word: 'to go', definition: 'x' }]);
    const marked = core.markVocab(core.wordSpans('I want to. Go now.'), index);
    assert.equal(marked.some((s) => s.vocab), false);
    const plain = core.markVocab(core.wordSpans('nothing here'), core.vocabIndex([]));
    assert.equal(plain.some((s) => s.vocab), false);
    assert.deepEqual(core.markVocab([], index), []);
});

// ---------------------------------------------------------------- dictationStore
function memoryStorage() {
    const data = {};
    return {
        data,
        getItem: (k) => (k in data ? data[k] : null),
        setItem: (k, v) => { data[k] = String(v); },
        removeItem: (k) => { delete data[k]; },
    };
}

test('dictationStore: versioned key, save, load and clear', () => {
    const storage = memoryStorage();
    const store = core.dictationStore(storage, 42, 107);
    assert.deepEqual(store.load(), {});
    assert.equal(store.save(0, { status: 'correct', score: 1, text: 'Hello there' }), true);
    store.save(2, { status: 'near', score: 0.8, text: 'x' });
    assert.deepEqual(Object.keys(storage.data), ['tct.listening.dictation.v1.42.107']);
    assert.deepEqual(core.dictationStore(storage, 42, 107).load()[0], { status: 'correct', score: 1, text: 'Hello there' });
    assert.equal(Object.keys(store.load()).length, 2);
    store.clear();
    assert.deepEqual(store.load(), {});
    assert.deepEqual(Object.keys(storage.data), []);
});

test('dictationStore: guests and lessons never share a key', () => {
    const storage = memoryStorage();
    core.dictationStore(storage, null, 1).save(0, { status: 'correct', score: 1 });
    core.dictationStore(storage, 7, 1).save(0, { status: 'wrong', score: 0 });
    core.dictationStore(storage, null, 2).save(0, { status: 'near', score: 0.9 });
    assert.deepEqual(Object.keys(storage.data).sort(), ['tct.listening.dictation.v1.7.1', 'tct.listening.dictation.v1.guest.1', 'tct.listening.dictation.v1.guest.2']);
});

test('dictationStore: missing, throwing or corrupt storage never throws', () => {
    assert.deepEqual(core.dictationStore(null, 1, 1).load(), {});
    assert.equal(core.dictationStore(undefined, 1, 1).save(0, { status: 'correct', score: 1 }), false);
    const throwing = { getItem() { throw new Error('denied'); }, setItem() { throw new Error('denied'); }, removeItem() { throw new Error('denied'); } };
    const store = core.dictationStore(throwing, 1, 1);
    assert.deepEqual(store.load(), {});
    assert.equal(store.save(0, { status: 'correct', score: 1 }), false);
    assert.doesNotThrow(() => store.clear());
    const storage = memoryStorage();
    storage.data['tct.listening.dictation.v1.1.1'] = '{not json';
    assert.deepEqual(core.dictationStore(storage, 1, 1).load(), {});
    storage.data['tct.listening.dictation.v1.1.1'] = JSON.stringify({ v: 2, results: { 0: { status: 'correct', score: 1 } } });
    assert.deepEqual(core.dictationStore(storage, 1, 1).load(), {}, 'an unknown version is ignored');
    storage.data['tct.listening.dictation.v1.1.1'] = JSON.stringify({ v: 1, results: { 0: { status: 'hacked', score: 9 }, x: { status: 'correct', score: 1 }, 1: { status: 'correct', score: 1 } } });
    assert.deepEqual(Object.keys(core.dictationStore(storage, 1, 1).load()), ['1'], 'invalid entries are dropped');
});

test('dictationStore.summary: done, correct, accuracy and skipped, limited to the lesson length', () => {
    const storage = memoryStorage();
    const store = core.dictationStore(storage, 1, 1);
    assert.deepEqual(store.summary(14), { done: 0, correct: 0, skipped: 0, accuracy: 0 });
    store.save(0, { status: 'correct', score: 1 });
    store.save(1, { status: 'near', score: 0.8 });
    store.save(2, { status: 'wrong', score: 0.2 });
    store.save(3, { status: 'skipped', score: 0 });
    store.save(30, { status: 'correct', score: 1 });
    assert.deepEqual(store.summary(14), { done: 3, correct: 1, skipped: 1, accuracy: 67 });
    assert.deepEqual(store.summary(2), { done: 2, correct: 1, skipped: 0, accuracy: 90 });
    assert.deepEqual(store.summary(0), { done: 0, correct: 0, skipped: 0, accuracy: 0 });
});

// ---------------------------------------------------------------- dictationVerdict
test('dictationVerdict: correct only when exact, near from 70%, never 100% unless exact', () => {
    assert.deepEqual(core.dictationVerdict(core.gradeDictation('hello world', 'Hello world.')), { tone: 'correct', percent: 100 });
    const near = core.dictationVerdict(core.gradeDictation('can I have a medium latte too go please', 'Can I have a medium latte to go, please?'));
    assert.equal(near.tone, 'near');
    assert.ok(near.percent >= 70 && near.percent < 100);
    assert.deepEqual(core.dictationVerdict(core.gradeDictation('', 'Hello world')), { tone: 'wrong', percent: 0 });
    const extra = core.dictationVerdict(core.gradeDictation('hello big world', 'hello world'));
    assert.equal(extra.tone, 'near');
    assert.equal(extra.percent, 99);
    assert.deepEqual(core.dictationVerdict(null), { tone: 'wrong', percent: 0 });
    assert.equal(core.dictationVerdict({ status: 'wrong', score: 0.69, extra: 0 }).tone, 'wrong');
    assert.equal(core.dictationVerdict({ status: 'near', score: 0.7, extra: 0 }).tone, 'near');
});

// ---------------------------------------------------------------- levelSummary
test('levelSummary: totals, integer percentages and the complete/disabled flags', () => {
    const summary = core.levelSummary([
        { key: 'A1', lessonCount: 10, completedCount: 10 },
        { key: 'A2', lessonCount: 12, completedCount: 6 },
        { key: 'B1', lessonCount: 3, completedCount: 1 },
        { key: 'C1', lessonCount: 0, completedCount: 0 },
    ]);
    assert.equal(summary.total, 25);
    assert.equal(summary.completed, 17);
    assert.equal(summary.percent, 68);
    assert.deepEqual(summary.levels.map((l) => [l.key, l.percent, l.complete, l.disabled]), [
        ['A1', 100, true, false], ['A2', 50, false, false], ['B1', 33, false, false], ['C1', 0, false, true],
    ]);
});

test('levelSummary: zero lessons, guests and bad input', () => {
    assert.deepEqual(core.levelSummary([]), { total: 0, completed: 0, percent: 0, levels: [] });
    assert.deepEqual(core.levelSummary(null), { total: 0, completed: 0, percent: 0, levels: [] });
    const guest = core.levelSummary([{ key: 'A1', lessonCount: 4 }]);
    assert.equal(guest.completed, 0);
    assert.equal(guest.levels[0].percent, 0);
    const clamped = core.levelSummary([{ key: 'A1', lessonCount: 2, completedCount: 9 }]);
    assert.equal(clamped.levels[0].percent, 100);
    assert.equal(clamped.completed, 2);
    const allDone = core.levelSummary([{ key: 'A1', lessonCount: 2, completedCount: 2 }, { key: 'A2', lessonCount: 1, completedCount: 1 }]);
    assert.equal(allDone.percent, 100);
});

// ---------------------------------------------------------------- nextStepHash
test('nextStepHash: the first unfinished step with content, else the transcript', () => {
    const counts = { transcript: 8, quiz: 5, vocab: 4 };
    assert.equal(core.nextStepHash({ transcript: false, quiz: false, vocab: false }, counts), '#transcript');
    assert.equal(core.nextStepHash({ transcript: true, quiz: false, vocab: false }, counts), '#quiz');
    assert.equal(core.nextStepHash({ transcript: true, quiz: true, vocab: false }, counts), '#vocab');
    assert.equal(core.nextStepHash({ transcript: true, quiz: true, vocab: true }, counts), '#transcript');
    assert.equal(core.nextStepHash({ transcript: true, quiz: false, vocab: false }, { transcript: 8, quiz: 0, vocab: 4 }), '#vocab');
    assert.equal(core.nextStepHash({ transcript: true }, { transcript: 8, quiz: 0, vocab: 0 }), '#transcript');
    assert.equal(core.nextStepHash(null, null), '#transcript');
    assert.equal(core.nextStepHash(undefined, { transcript: 0, quiz: 3, vocab: 0 }), '#quiz');
});

// ---------------------------------------------------------------- activeSubtitle
const LINES = [
    { start: 2, end: 6, text: 'Hello there', vi: 'Xin chào' },
    { start: 8, end: 12, text: 'How are you?', vi: null },
    { start: 14, end: 18, text: 'Fine, thanks.', vi: 'Khỏe, cảm ơn.' },
];

test('activeSubtitle: off hides, en shows English only, both adds the Vietnamese when there is one', () => {
    assert.equal(core.activeSubtitle(LINES, 3, 'off'), null);
    assert.deepEqual(core.activeSubtitle(LINES, 3, 'en'), { en: 'Hello there', vi: null });
    assert.deepEqual(core.activeSubtitle(LINES, 3, 'both'), { en: 'Hello there', vi: 'Xin chào' });
    assert.deepEqual(core.activeSubtitle(LINES, 9, 'both'), { en: 'How are you?', vi: null });
    assert.deepEqual(core.activeSubtitle(LINES, 14, 'both'), { en: 'Fine, thanks.', vi: 'Khỏe, cảm ơn.' });
});

test('activeSubtitle: nothing before, between or after lines, and for bad input', () => {
    assert.equal(core.activeSubtitle(LINES, 0, 'both'), null);
    assert.equal(core.activeSubtitle(LINES, 7, 'both'), null);
    assert.equal(core.activeSubtitle(LINES, 99, 'both'), null);
    assert.equal(core.activeSubtitle([], 3, 'both'), null);
    assert.equal(core.activeSubtitle(null, 3, 'both'), null);
    assert.equal(core.activeSubtitle(LINES, NaN, 'both'), null);
    assert.equal(core.activeSubtitle(LINES, 3, 'nonsense'), null);
    assert.equal(core.activeSubtitle(LINES, 3), null);
});
