'use strict';

// Tests for wwwroot/js/landing-core.js: the pure logic of the landing "hoc thu" demo.
// Also guards the no-JS markup in landing.cshtml against drifting from the first question.

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const ROOT = path.join(__dirname, '..', '..', 'TCTEnglish');
const core = require(path.join(ROOT, 'wwwroot', 'js', 'landing-core.js'));

const optionLabels = (state, i = state.index) => state.questions[i].options.map((o) => o.label);
const correctOptionId = (state, i = state.index) => state.questions[i].correctId;
const wrongOptionId = (state, i = state.index) => state.questions[i].options.find((o) => o.id !== state.questions[i].correctId).id;

test('the three fixed questions have one correct option and four distinct options each', () => {
  assert.equal(core.QUESTIONS.length, 3);
  core.QUESTIONS.forEach((q) => {
    assert.equal(q.options.length, 4, q.word);
    assert.equal(new Set(q.options.map((o) => o.label)).size, 4, `${q.word}: labels must be distinct`);
    assert.equal(new Set(q.options.map((o) => o.id)).size, 4, `${q.word}: ids must be distinct`);
    assert.equal(q.options.filter((o) => o.id === q.correctId).length, 1, `${q.word}: exactly one correct option`);
  });
  assert.deepEqual(core.QUESTIONS.map((q) => q.word), ['đàm phán', 'deadline', 'cộng tác']);
  assert.deepEqual(core.QUESTIONS.map((q) => q.options.find((o) => o.id === q.correctId).label), ['negotiate', 'hạn chót', 'collaborate']);
});

test('the same seed always gives the same option order; question order never changes', () => {
  const a = core.createDemo(core.QUESTIONS, 12345);
  const b = core.createDemo(core.QUESTIONS, 12345);
  assert.deepEqual(a.questions.map((q) => q.options.map((o) => o.id)), b.questions.map((q) => q.options.map((o) => o.id)));
  assert.deepEqual(a.questions.map((q) => q.word), ['đàm phán', 'deadline', 'cộng tác']);
});

test('different seeds can give different option orders, and each order keeps all four options', () => {
  const orders = new Set();
  for (let seed = 1; seed <= 40; seed++) {
    const s = core.createDemo(core.QUESTIONS, seed);
    orders.add(s.questions[0].options.map((o) => o.id).join(','));
    s.questions.forEach((q, i) => assert.deepEqual([...q.options.map((o) => o.id)].sort(), [...core.QUESTIONS[i].options.map((o) => o.id)].sort()));
  }
  assert.ok(orders.size > 5, `expected several different orders, got ${orders.size}`);
});

test('createDemo does not mutate the shared question list', () => {
  const before = JSON.stringify(core.QUESTIONS);
  core.createDemo(core.QUESTIONS, 99);
  assert.equal(JSON.stringify(core.QUESTIONS), before);
});

test('current() describes the question, its number and whether it is answered', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  const c = core.current(s);
  assert.equal(c.number, 1);
  assert.equal(c.total, 3);
  assert.equal(c.word, 'đàm phán');
  assert.equal(c.prompt, 'Từ nào có nghĩa này?');
  assert.equal(c.answered, false);
  assert.equal(c.options.length, 4);
  assert.equal(c.isLast, false);
});

test('a correct answer reports success, the 1-day review and locks the question', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  const r = core.answer(s, correctOptionId(s));
  assert.equal(r.correct, true);
  assert.equal(r.correctId, correctOptionId(s));
  assert.equal(r.label, 'Đúng rồi!');
  assert.equal(r.detail, '');
  assert.equal(r.schedule, 'Ôn lại sau 1 ngày');
  assert.equal(core.current(s).answered, true);
  assert.equal(core.current(s).chosenId, correctOptionId(s));
});

test('a wrong answer reports the right answer and the 10-minute review', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  const r = core.answer(s, wrongOptionId(s));
  assert.equal(r.correct, false);
  assert.equal(r.label, 'Chưa đúng');
  assert.equal(r.detail, 'Đáp án: negotiate');
  assert.equal(r.schedule, 'Ôn lại sau 10 phút');
  assert.equal(r.correctId, correctOptionId(s));
});

test('a second answer to the same question is ignored', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  core.answer(s, wrongOptionId(s));
  const again = core.answer(s, correctOptionId(s));
  assert.equal(again.ignored, true);
  assert.equal(core.summary(s).correct, 0);
});

test('an unknown option id is ignored and leaves the question open', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  const r = core.answer(s, 'does-not-exist');
  assert.equal(r.ignored, true);
  assert.equal(core.current(s).answered, false);
});

test('next() needs an answer first, then moves on; the last next() finishes', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  assert.equal(core.next(s).moved, false, 'cannot skip an unanswered question');
  core.answer(s, correctOptionId(s));
  assert.equal(core.next(s).moved, true);
  assert.equal(core.current(s).number, 2);
  core.answer(s, correctOptionId(s));
  core.next(s);
  assert.equal(core.current(s).isLast, true);
  core.answer(s, wrongOptionId(s));
  const end = core.next(s);
  assert.equal(end.finished, true);
  assert.equal(s.finished, true);
});

test('next() past the end stays finished and does not move the index', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  for (let i = 0; i < 3; i++) { core.answer(s, correctOptionId(s)); core.next(s); }
  const idx = s.index;
  assert.equal(core.next(s).finished, true);
  assert.equal(s.index, idx);
  assert.equal(core.answer(s, 'x').ignored, true);
});

test('summary counts only the correct answers', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  core.answer(s, correctOptionId(s)); core.next(s);
  core.answer(s, wrongOptionId(s)); core.next(s);
  core.answer(s, correctOptionId(s)); core.next(s);
  assert.deepEqual(core.summary(s), { correct: 2, total: 3 });
});

test('reset() starts again from question one with nothing answered', () => {
  const s = core.createDemo(core.QUESTIONS, 7);
  for (let i = 0; i < 3; i++) { core.answer(s, correctOptionId(s)); core.next(s); }
  const fresh = core.reset(s);
  assert.equal(fresh.finished, false);
  assert.equal(fresh.index, 0);
  assert.deepEqual(core.summary(fresh), { correct: 0, total: 3 });
  assert.equal(core.current(fresh).answered, false);
});

test('keyOptionIndex maps keys 1-4 to option positions and ignores everything else', () => {
  assert.equal(core.keyOptionIndex('1'), 0);
  assert.equal(core.keyOptionIndex('4'), 3);
  ['0', '5', 'a', 'Enter', '', undefined, '12'].forEach((k) => assert.equal(core.keyOptionIndex(k), -1, String(k)));
});

test('landing.cshtml renders question 1 as plain markup for visitors without JavaScript', () => {
  const view = fs.readFileSync(path.join(ROOT, 'Views', 'Home', 'landing.cshtml'), 'utf8');
  const q1 = core.QUESTIONS[0];
  assert.ok(view.includes(q1.word), 'first word is in the markup');
  q1.options.forEach((o) => assert.ok(view.includes(o.label), `option "${o.label}" is in the markup`));
});
