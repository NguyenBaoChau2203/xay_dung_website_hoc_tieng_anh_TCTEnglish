'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const jsPath = path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'admin-content.js');
const A = require(jsPath);
const ID = 'dQw4w9WgXcQ';

test('parseYoutubeId accepts every known link shape (mirrors AdminInputRules)', () => {
  const accepted = [
    ID,
    '  dQw4w9WgXcQ  ',
    'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
    'https://youtube.com/watch?v=dQw4w9WgXcQ&t=42s',
    'https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ',
    'https://m.youtube.com/watch?v=dQw4w9WgXcQ',
    'https://youtu.be/dQw4w9WgXcQ',
    'https://youtu.be/dQw4w9WgXcQ?si=abc123',
    'https://www.youtube.com/embed/dQw4w9WgXcQ',
    'https://www.youtube.com/embed/dQw4w9WgXcQ?start=10',
    'https://www.youtube.com/shorts/dQw4w9WgXcQ',
    'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ',
    'youtu.be/dQw4w9WgXcQ',
    'www.youtube.com/watch?v=dQw4w9WgXcQ',
  ];
  for (const input of accepted) assert.equal(A.parseYoutubeId(input), ID, input);
});

test('parseYoutubeId rejects everything else, including look-alike hosts', () => {
  const rejected = [
    null, undefined, '', '   ', 'abc', 'dQw4w9WgXc', 'dQw4w9WgXcQQ', 'dQw4w9WgX!Q',
    'https://www.youtube.com/watch?v=short', 'https://www.youtube.com/watch',
    'https://vimeo.com/dQw4w9WgXcQ', 'https://evil.example/watch?v=dQw4w9WgXcQ',
    'https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ', 'javascript:alert(1)',
    'https://www.youtube.com/channel/UC1234567890123456789012',
  ];
  for (const input of rejected) assert.equal(A.parseYoutubeId(input), null, String(input));
});

test('youtubeThumbnailUrl builds the default image only for an id', () => {
  assert.equal(A.youtubeThumbnailUrl(ID), 'https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg');
  assert.equal(A.youtubeThumbnailUrl(''), null);
  assert.equal(A.youtubeThumbnailUrl(null), null);
});

test('isSafeImageUrl follows the server rule', () => {
  for (const ok of [null, '', '   ', 'https://cdn.example.com/a/b.png', '/images/animals/cat.jpg', '/uploads/premium/abc-1.png']) {
    assert.equal(A.isSafeImageUrl(ok), true, String(ok));
  }
  for (const bad of [
    'http://cdn.example.com/a.png', '//cdn.example.com/a.png', 'javascript:alert(1)', 'data:image/png;base64,AAAA',
    'ftp://cdn.example.com/a.png', 'images/a.png', '/\\evil.example/a.png', '/../etc/passwd', '/uploads/a b.png',
    'https://', 'https:///a.png', 'https://exa mple.com/a.png', 'https://cdn.example.com/a.png\n',
    'https://user:pw@cdn.example.com/a.png',
  ]) {
    assert.equal(A.isSafeImageUrl(bad), false, JSON.stringify(bad));
  }
});

test('normalizeDuration normalizes known shapes and rejects the rest', () => {
  const cases = [['4:12', '04:12'], ['04:12', '04:12'], [' 4:05 ', '04:05'], ['0:45', '00:45'], ['75:30', '75:30'],
    ['1:02:03', '1:02:03'], ['01:02:03', '1:02:03'], ['12:00:00', '12:00:00']];
  for (const [input, expected] of cases) assert.deepEqual(A.normalizeDuration(input), { ok: true, value: expected }, input);

  for (const empty of [null, undefined, '', '   ']) assert.deepEqual(A.normalizeDuration(empty), { ok: true, value: null });

  for (const bad of ['4 phút', '4:7', '4:60', '1:60:00', '1:2:03', '12', '-1:30', '1:30:00:00', '00:00', 'a:bc', '1000:00']) {
    assert.deepEqual(A.normalizeDuration(bad), { ok: false, value: null }, bad);
  }
});

test('isCefrLevel accepts only the six upper-case levels', () => {
  for (const level of ['A1', 'A2', 'B1', 'B2', 'C1', 'C2']) assert.equal(A.isCefrLevel(level), true);
  for (const level of ['b2', 'D1', '', null, undefined]) assert.equal(A.isCefrLevel(level), false);
});

test('counterState and counterText describe the limit', () => {
  assert.equal(A.counterText(12, 200), '12/200');
  assert.equal(A.counterState(0, 200), 'ok');
  assert.equal(A.counterState(169, 200), 'ok');
  assert.equal(A.counterState(170, 200), 'near');
  assert.equal(A.counterState(200, 200), 'near');
  assert.equal(A.counterState(201, 200), 'over');
  assert.equal(A.counterState(5, 0), 'ok');
});

test('confirmNameMatches needs the exact name', () => {
  assert.equal(A.confirmNameMatches('Bộ A', 'Bộ A'), true);
  assert.equal(A.confirmNameMatches('  Bộ A  ', 'Bộ A'), true);
  assert.equal(A.confirmNameMatches('bộ a', 'Bộ A'), false);
  assert.equal(A.confirmNameMatches('Bộ', 'Bộ A'), false);
  assert.equal(A.confirmNameMatches('', ''), false);
  assert.equal(A.confirmNameMatches(null, 'Bộ A'), false);
});

test('reindexName and reindexId renumber row fields', () => {
  assert.equal(A.reindexName('Lines[3].Text', 0), 'Lines[0].Text');
  assert.equal(A.reindexName('Questions[12].OptionA', 4), 'Questions[4].OptionA');
  assert.equal(A.reindexName('Title', 2), 'Title');
  assert.equal(A.reindexId('line-3-text', 0), 'line-0-text');
  assert.equal(A.reindexId('quiz-12', 1), 'quiz-1');
  assert.equal(A.reindexId('title', 1), 'title');
});

test('moveItem returns a new array and ignores impossible moves', () => {
  const list = ['a', 'b', 'c', 'd'];
  assert.deepEqual(A.moveItem(list, 0, 2), ['b', 'c', 'a', 'd']);
  assert.deepEqual(A.moveItem(list, 3, 0), ['d', 'a', 'b', 'c']);
  assert.deepEqual(A.moveItem(list, 1, 1), list);
  assert.deepEqual(A.moveItem(list, -1, 2), list);
  assert.deepEqual(A.moveItem(list, 1, 9), list);
  assert.deepEqual(list, ['a', 'b', 'c', 'd']);
});

test('snapshotFields and isDirty track real edits only', () => {
  const fields = (extra = {}) => [
    { name: '__RequestVerificationToken', type: 'hidden', value: 'abc' },
    { name: 'Title', type: 'text', value: extra.title ?? 'Bài 1' },
    { name: 'Level', type: 'select-one', value: 'A2' },
    { name: 'IsPublished', type: 'checkbox', value: 'true', checked: extra.published ?? false },
    { name: 'Correct', type: 'radio', value: 'A', checked: true },
    { name: 'Correct', type: 'radio', value: 'B', checked: false },
    { name: 'Upload', type: 'file', value: 'x' },
    { name: 'Disabled', type: 'text', value: 'x', disabled: true },
    { name: 'Go', type: 'submit', value: 'Lưu' },
  ];
  const initial = A.snapshotFields(fields());
  assert.deepEqual(Object.keys(initial).sort(), ['Correct', 'Level', 'Title']);
  assert.equal(A.isDirty(initial, A.snapshotFields(fields())), false);
  assert.equal(A.isDirty(initial, A.snapshotFields(fields({ title: 'Bài 2' }))), true);
  assert.equal(A.isDirty(initial, A.snapshotFields(fields({ published: true }))), true);
  // The antiforgery token never counts as an edit.
  const other = fields();
  other[0].value = 'zzz';
  assert.equal(A.isDirty(initial, A.snapshotFields(other)), false);
});

test('the file never writes data with innerHTML or eval', () => {
  const source = fs.readFileSync(jsPath, 'utf8');
  assert.equal(/innerHTML|outerHTML|insertAdjacentHTML|document\.write|eval\(|new Function/.test(source), false);
});
