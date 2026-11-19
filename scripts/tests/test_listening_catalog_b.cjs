'use strict';
// node --test scripts/tests/test_listening_catalog_b.cjs
// Listening catalogue direction B: level bands, the hero hint and the five-card shelf slice (pure core helpers).
const test = require('node:test');
const assert = require('node:assert/strict');
const core = require('../../TCTEnglish/wwwroot/js/listening-core.js');

test('bandOf maps every CEFR level to the band the pills show', () => {
  assert.equal(core.bandOf('A1'), 'A1–A2');
  assert.equal(core.bandOf('A2'), 'A1–A2');
  assert.equal(core.bandOf('B1'), 'B1');
  assert.equal(core.bandOf('B2'), 'B2');
  assert.equal(core.bandOf('C1'), 'C1–C2');
  assert.equal(core.bandOf('C2'), 'C1–C2');
  assert.equal(core.bandOf('c2'), 'C1–C2', 'case does not matter');
  assert.equal(core.bandOf('Z9'), '');
  assert.equal(core.bandOf(null), '');
  assert.equal(core.bandOf(''), '');
});

test('BANDS lists the four bands in order with their levels', () => {
  assert.deepEqual(core.BANDS.map((b) => b.key), ['A1–A2', 'B1', 'B2', 'C1–C2']);
  assert.deepEqual(core.BANDS.map((b) => b.levels), [['A1', 'A2'], ['B1'], ['B2'], ['C1', 'C2']]);
  assert.equal(core.levelOfBand('A1–A2'), 'A1', 'the URL keeps the first level of the band');
  assert.equal(core.levelOfBand('C1–C2'), 'C1');
  assert.equal(core.levelOfBand('all'), 'all');
  assert.equal(core.levelOfBand('nope'), 'all');
});

test('bandsWithLessons hides empty bands and sums the counts of the levels in a band', () => {
  const levels = [
    { key: 'A1', lessonCount: 0 }, { key: 'A2', lessonCount: 1 }, { key: 'B1', lessonCount: 10 },
    { key: 'B2', lessonCount: 0 }, { key: 'C1', lessonCount: 3 }, { key: 'C2', lessonCount: 2 }
  ];
  const bands = core.bandsWithLessons(levels);
  assert.deepEqual(bands.map((b) => b.key), ['A1–A2', 'B1', 'C1–C2']);
  assert.deepEqual(bands.map((b) => b.lessonCount), [1, 10, 5]);
  assert.deepEqual(core.bandsWithLessons([]), []);
  assert.deepEqual(core.bandsWithLessons(null), []);
  assert.deepEqual(core.bandsWithLessons([{ key: 'A1', lessonCount: 0 }]), []);
});

test('filterLessons accepts a band and still accepts a single level', () => {
  const lessons = [
    { id: 1, title: 'a', topic: 'x', level: 'A1', status: 'new' },
    { id: 2, title: 'b', topic: 'x', level: 'A2', status: 'new' },
    { id: 3, title: 'c', topic: 'x', level: 'B1', status: 'new' },
    { id: 4, title: 'd', topic: 'x', level: 'C2', status: 'done' }
  ];
  assert.deepEqual(core.filterLessons(lessons, { band: 'A1–A2' }), [1, 2]);
  assert.deepEqual(core.filterLessons(lessons, { band: 'C1–C2' }), [4]);
  assert.deepEqual(core.filterLessons(lessons, { band: 'all' }), [1, 2, 3, 4]);
  assert.deepEqual(core.filterLessons(lessons, { band: 'B1', status: 'done' }), []);
  assert.deepEqual(core.filterLessons(lessons, { level: 'A2' }), [2], 'the level filter is unchanged');
});

test('heroHint names the unfinished steps that have content, joined with "và"', () => {
  const all = { transcript: 3, quiz: 2, vocab: 4 };
  assert.equal(core.heroHint({ transcript: true }, all), 'Còn quiz và từ vựng');
  assert.equal(core.heroHint({ transcript: true, vocab: true }, all), 'Còn quiz');
  assert.equal(core.heroHint({ transcript: true, quiz: true }, all), 'Còn từ vựng');
  assert.equal(core.heroHint({}, all), 'Còn nghe hết lời thoại, quiz và từ vựng');
  assert.equal(core.heroHint({ quiz: true, vocab: true }, all), 'Còn nghe hết lời thoại');
  // only steps that exist in the lesson are named
  assert.equal(core.heroHint({ transcript: true }, { transcript: 3, quiz: 0, vocab: 4 }), 'Còn từ vựng');
  assert.equal(core.heroHint({}, { transcript: 2, quiz: 0, vocab: 0 }), 'Còn nghe hết lời thoại');
  // nothing left, or nothing to do
  assert.equal(core.heroHint({ transcript: true, quiz: true, vocab: true }, all), '');
  assert.equal(core.heroHint({}, { transcript: 0, quiz: 0, vocab: 0 }), '');
  assert.equal(core.heroHint(null, null), '');
});

test('shelfSlice keeps the first n ids and reports how many are left', () => {
  const ids = [1, 2, 3, 4, 5, 6, 7];
  assert.deepEqual(core.shelfSlice(ids, 5), { visible: [1, 2, 3, 4, 5], rest: 2 });
  assert.deepEqual(core.shelfSlice([1, 2], 5), { visible: [1, 2], rest: 0 });
  assert.deepEqual(core.shelfSlice([], 5), { visible: [], rest: 0 });
  assert.deepEqual(core.shelfSlice(null, 5), { visible: [], rest: 0 });
  assert.deepEqual(core.shelfSlice(ids, 0), { visible: [], rest: 7 });
  assert.deepEqual(core.shelfSlice(ids, 'x'), { visible: [], rest: 7 });
});

test('a level in the query selects its band (the URL keeps working for every level)', () => {
  const state = core.parseFilterQuery('?level=C2', []);
  assert.equal(state.level, 'C2');
  assert.equal(core.bandOf(state.level), 'C1–C2');
  assert.equal(core.buildFilterQuery({ level: core.levelOfBand('C1–C2'), topic: 'all', status: 'all', q: '' }), '?level=C1');
});
