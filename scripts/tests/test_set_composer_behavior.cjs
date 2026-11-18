'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const core = require(path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'set-composer.js'));

const card = (term, definition, extra = {}) => ({ term, definition, d: {}, ...extra });

const enrichResult = () => ({
  isValidWord: true,
  ipaUs: '/rɪˈzɪliənt/',
  ipaUk: '/rɪˈzɪliənt/',
  cefr: 'C1',
  senses: [
    { pos: ['adj'], meaningVi: 'kiên cường', meaningEn: 'able to recover', examples: [{ text: 'She is resilient.', vi: 'Cô ấy kiên cường.' }] },
    { pos: ['adj', 'noun'], meaningVi: 'đàn hồi', examples: [{ text: 'Rubber is resilient.', vi: 'Cao su đàn hồi.' }] },
  ],
  synonyms: ['tough'],
  antonyms: [],
  collocations: [{ text: 'resilient economy', vi: 'nền kinh tế vững' }],
  wordFamily: [],
  verbForms: null,
  plural: null,
  topicVi: 'Cảm xúc',
  emoji: '💪',
  imageQuery: 'strong tree',
});

test('saveLabel: singular for zero and one, counted for several', () => {
  assert.equal(core.saveLabel(0), 'Lưu từ');
  assert.equal(core.saveLabel(1), 'Lưu từ');
  assert.equal(core.saveLabel(5), 'Lưu 5 từ');
});

test('findInnerDuplicates flags repeats of an earlier card, ignoring case and spaces', () => {
  const cards = [card('Apple', 'a'), card(' pear ', 'b'), card('apple', 'c'), card('', ''), card('PEAR', 'd'), card('', '')];
  assert.deepEqual(core.findInnerDuplicates(cards), [
    { index: 2, firstIndex: 0 },
    { index: 4, firstIndex: 1 },
  ]);
  assert.deepEqual(core.findInnerDuplicates([card('a', ''), card('b', '')]), []);
});

test('applyEnrich fills the card from the first sense and marks it AI generated', () => {
  const next = core.applyEnrich(card('resilient', ''), enrichResult(), [0]);
  assert.equal(next.definition, 'kiên cường');
  assert.equal(next.aiMeaning, true);
  assert.equal(next.d.phonetic, '/rɪˈzɪliənt/');
  assert.deepEqual(next.d.partsOfSpeech, ['adj']);
  assert.equal(next.d.cefr, 'C1');
  assert.equal(next.d.topic, 'Cảm xúc');
  assert.equal(next.d.emoji, '💪');
  assert.equal(next.d.examples.length, 1);
  assert.deepEqual(next.d.synonyms, ['tough']);
  assert.equal(next.imageQuery, 'strong tree');
  assert.equal(next.aiGenerated, true);
});

test('applyEnrich never overwrites a meaning or IPA the learner typed', () => {
  const typed = card('resilient', 'bền bỉ của tôi', { meaningTyped: true, readingTyped: true, d: { phonetic: '/my/' } });
  const next = core.applyEnrich(typed, enrichResult(), [0]);
  assert.equal(next.definition, 'bền bỉ của tôi');
  assert.equal(next.d.phonetic, '/my/');
  assert.equal(next.d.phoneticUk, '/rɪˈzɪliənt/');
  assert.equal(next.d.cefr, 'C1');
  assert.equal(typed.definition, 'bền bỉ của tôi', 'input must not be mutated');
});

test('applyEnrich with several senses joins meanings, unions parts of speech and merges examples', () => {
  const next = core.applyEnrich(card('resilient', ''), enrichResult(), [0, 1]);
  assert.equal(next.definition, 'kiên cường; đàn hồi');
  assert.deepEqual(next.d.partsOfSpeech, ['adj', 'noun']);
  assert.equal(next.d.examples.length, 2);
});

test('applyEnrich replaces an earlier AI meaning when the learner has not typed one', () => {
  const first = core.applyEnrich(card('resilient', ''), enrichResult(), [0]);
  const second = core.applyEnrich(first, enrichResult(), [1]);
  assert.equal(second.definition, 'đàn hồi');
});

test('mergeBatchResults removes inserted cards and marks skipped and invalid ones in place', () => {
  const cards = [card('apple', 'a', { uid: 1 }), card('Pear', 'b', { uid: 2 }), card('fig', 'c', { uid: 3 }), card('plum', 'd', { uid: 4 })];
  const merged = core.mergeBatchResults(cards, {
    inserted: [{ id: 10, term: 'apple' }, { id: 11, term: 'plum' }],
    skipped: ['pear'],
    invalid: ['FIG'],
  });
  assert.deepEqual(merged.insertedItems.map((i) => i.id), [10, 11]);
  assert.deepEqual(merged.remaining.map((c) => [c.term, c.saveError]), [
    ['Pear', 'Đã có trong bộ này'],
    ['fig', 'Dữ liệu chưa hợp lệ'],
  ]);
});

test('mergeBatchResults handles two cards with the same term: only as many as were inserted are removed', () => {
  const cards = [card('a', '1', { uid: 1 }), card('A', '2', { uid: 2 })];
  const merged = core.mergeBatchResults(cards, { inserted: [{ id: 5, term: 'a' }], skipped: ['A'], invalid: [] });
  assert.equal(merged.insertedItems.length, 1);
  assert.equal(merged.remaining.length, 1);
  assert.equal(merged.remaining[0].saveError, 'Đã có trong bộ này');
});

test('detailSummary lists AI, examples, first part of speech, topic and image', () => {
  const full = card('x', 'y', {
    aiGenerated: true,
    d: { examples: [{}, {}, {}], partsOfSpeech: ['adj', 'noun'], topic: 'Cảm xúc', imageUrl: '/images/cards/a.jpg' },
  });
  assert.equal(core.detailSummary(full), 'Đã điền bằng AI · 3 ví dụ · Tính từ · Cảm xúc · có ảnh');
  assert.equal(core.detailSummary(card('x', 'y')), '');
  assert.equal(core.detailSummary(card('x', 'y', { d: { topic: 'Khác' } })), 'Khác');
});

test('saveStatus reports ready and incomplete cards, or enrich progress while it runs', () => {
  const cards = [card('a', 'b'), card('c', ''), card('', '')];
  assert.equal(core.saveStatus(cards, null), '1 thẻ sẵn sàng · 1 thẻ thiếu nghĩa');
  assert.equal(core.saveStatus([card('a', 'b')], null), '1 thẻ sẵn sàng');
  assert.equal(core.saveStatus(cards, { done: 3, total: 10 }), 'Đang điền 3/10…');
  assert.equal(core.saveStatus(cards, { done: 10, total: 10 }), '1 thẻ sẵn sàng · 1 thẻ thiếu nghĩa');
});

test('isBlank and isReady', () => {
  assert.equal(core.isBlank(card('', '')), true);
  assert.equal(core.isBlank(card(' ', '  ')), true);
  assert.equal(core.isBlank(card('', '', { d: { imageUrl: '/x.png' } })), false);
  assert.equal(core.isReady(card('a', 'b')), true);
  assert.equal(core.isReady(card('a', ' ')), false);
});

test('toInputDto trims, drops empty rows and only keeps an image source with an image', () => {
  const dto = core.toInputDto(card(' run ', ' chạy ', {
    aiGenerated: true,
    d: {
      examples: [{ text: 'I run.', vi: '' }, { text: ' ', vi: 'x' }],
      imageSource: { provider: 'pixabay' },
      verbForms: { past: '', pastParticiple: '', ing: '', thirdPerson: '' },
      partsOfSpeech: ['verb'],
    },
  }));
  assert.equal(dto.term, 'run');
  assert.equal(dto.definition, 'chạy');
  assert.equal(dto.examples.length, 1);
  assert.equal(dto.imageSource, null);
  assert.equal(dto.verbForms, null);
  assert.equal(dto.aiGenerated, true);
  assert.deepEqual(dto.partsOfSpeech, ['verb']);
});

test('chunk splits into groups of the requested size', () => {
  assert.deepEqual(core.chunk([1, 2, 3, 4, 5], 2), [[1, 2], [3, 4], [5]]);
  assert.deepEqual(core.chunk([], 30), []);
  assert.equal(core.chunk(Array.from({ length: 100 }, (_, i) => i), 30).length, 4);
});
