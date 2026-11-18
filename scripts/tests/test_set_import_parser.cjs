'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const { parseImport } = require(path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'set-import-parser.js'));

const terms = (result) => result.lines.map((l) => [l.term, l.meaning]);

test('splits term and meaning on tab, spaced dashes, bare en/em dashes, colon and equals', () => {
  const text = [
    'apple\tquả táo',
    'borrow - mượn',
    'lend – cho mượn',
    'run—chạy',
    'resilient: kiên cường',
    'cat = con mèo',
  ].join('\n');
  const result = parseImport(text, []);
  assert.deepEqual(terms(result), [
    ['apple', 'quả táo'],
    ['borrow', 'mượn'],
    ['lend', 'cho mượn'],
    ['run', 'chạy'],
    ['resilient', 'kiên cường'],
    ['cat', 'con mèo'],
  ]);
  assert.equal(result.ok, 6);
  assert.equal(result.errors, 0);
});

test('a hyphen inside a word is not a separator, but a spaced hyphen after it is', () => {
  const result = parseImport('well-known - nổi tiếng\nmother-in-law', []);
  assert.deepEqual(terms(result), [
    ['well-known', 'nổi tiếng'],
    ['mother-in-law', ''],
  ]);
});

test('prefers the first separator in priority order (tab before dash before colon)', () => {
  const result = parseImport('note: a - b\tc', []);
  assert.deepEqual(terms(result), [['note: a - b', 'c']]);
});

test('strips list markers, skips blank and comment lines, keeps physical line numbers', () => {
  const result = parseImport('1. apple\n\n- pear\n# ghi chú\n2) fig - quả sung\n* plum', []);
  assert.deepEqual(result.lines.map((l) => l.term), ['apple', 'pear', 'fig', 'plum']);
  assert.deepEqual(result.lines.map((l) => l.lineNo), [1, 3, 5, 6]);
  assert.equal(result.lines[2].meaning, 'quả sung');
});

test('a dash followed by only a Vietnamese phrase is a meaning without a term', () => {
  const result = parseImport('- quả táo\n: nghĩa\n= con mèo', []);
  assert.equal(result.errors, 3);
  assert.ok(result.lines.every((l) => l.status === 'error' && l.message === 'Thiếu từ'));
  assert.equal(result.lines[0].meaning, 'quả táo');
});

test('quoted CSV with commas, doubled quotes and newlines inside cells', () => {
  const csv = [
    'term,phonetic,meaning_vi,example,topic',
    '"hello, world",/həˈləʊ/,"xin chào, thế giới","He said ""hi"".\nThen left.",Giao tiếp hằng ngày',
    'plain,,đơn giản,,',
  ].join('\r\n');
  const result = parseImport(csv, []);
  assert.equal(result.lines.length, 2);
  assert.equal(result.lines[0].term, 'hello, world');
  assert.equal(result.lines[0].meaning, 'xin chào, thế giới');
  assert.equal(result.lines[0].phonetic, '/həˈləʊ/');
  assert.equal(result.lines[0].example, 'He said "hi".\nThen left.');
  assert.equal(result.lines[0].topic, 'Giao tiếp hằng ngày');
  assert.equal(result.lines[1].lineNo, 4);
  assert.equal(result.ok, 2);
});

test('CSV header aliases, semicolon delimiter and BOM are accepted', () => {
  const csv = '﻿Từ;Nghĩa;Phiên âm\nresilient;kiên cường;/rɪˈzɪliənt/';
  const result = parseImport(csv, []);
  assert.deepEqual(terms(result), [['resilient', 'kiên cường']]);
  assert.equal(result.lines[0].phonetic, '/rɪˈzɪliənt/');
});

test('the CSV export format round-trips, including formula-escaped cells', () => {
  const exported = [
    'term,phonetic,meaning_vi,meaning_en,parts_of_speech,topic,example,example_vi,synonyms,antonyms,cefr,note',
    "'=1+1,,\"a, \"\"b\"\"\",,noun,,,,,,A1,",
    'run,/rʌn/,chạy,move fast,verb,Thể thao,I run.,Tôi chạy.,jog,,A1,tip',
  ].join('\r\n');
  const result = parseImport(exported, []);
  assert.deepEqual(terms(result), [['=1+1', 'a, "b"'], ['run', 'chạy']]);
  assert.equal(result.lines[1].example, 'I run.');
  assert.equal(result.lines[1].exampleVi, 'Tôi chạy.');
});

test('marks duplicates with the set and with an earlier line, case-insensitively', () => {
  const result = parseImport('Apple - táo\npear - lê\nPEAR - lê 2\napple\nfig', ['apple']);
  assert.deepEqual(result.lines.map((l) => [l.status, l.message]), [
    ['duplicate', 'Đã có trong bộ'],
    ['ok', ''],
    ['duplicate', 'Trùng dòng phía trên'],
    ['duplicate', 'Đã có trong bộ'],
    ['ok', ''],
  ]);
  assert.equal(result.duplicates, 3);
  assert.equal(result.ok, 2);
});

test('per-line length validation', () => {
  const result = parseImport(`${'a'.repeat(256)} - x\nok - ${'b'.repeat(1001)}\nfine - fine`, []);
  assert.deepEqual(result.lines.map((l) => l.message), ['Từ dài quá 255 ký tự', 'Nghĩa dài quá 1000 ký tự', '']);
  assert.equal(result.errors, 2);
  assert.equal(result.ok, 1);
});

test('more than 100 valid lines is truncated and flagged', () => {
  const text = Array.from({ length: 101 }, (_, i) => `word${i} - nghĩa ${i}`).join('\n');
  const result = parseImport(text, []);
  assert.equal(result.ok, 100);
  assert.equal(result.truncated, true);
  assert.equal(result.lines[100].status, 'error');
  assert.equal(result.lines[100].message, 'Quá 100 từ mỗi lần');
});

test('empty and null input produce an empty result', () => {
  for (const input of ['', '   \n\n', null, undefined]) {
    const result = parseImport(input, []);
    assert.deepEqual([result.lines.length, result.ok, result.errors, result.duplicates, result.truncated], [0, 0, 0, 0, false]);
  }
});
