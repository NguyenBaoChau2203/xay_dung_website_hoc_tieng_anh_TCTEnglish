'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const core = require(path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'notification-core.js'));

const item = (id, over = {}) => ({ id, isRead: false, category: 'learning', dayGroup: 'today', ...over });

test('badgeText: nothing for zero or junk, 99+ above 99', () => {
  assert.equal(core.badgeText(0), '');
  assert.equal(core.badgeText(-3), '');
  assert.equal(core.badgeText(NaN), '');
  assert.equal(core.badgeText(undefined), '');
  assert.equal(core.badgeText(1), '1');
  assert.equal(core.badgeText(99), '99');
  assert.equal(core.badgeText(100), '99+');
  assert.equal(core.badgeText('7'), '7');
  assert.equal(core.badgeText(7.9), '7');
});

test('bellLabel carries the unread count for screen readers', () => {
  assert.equal(core.bellLabel(0), 'Thông báo');
  assert.equal(core.bellLabel(1), 'Thông báo, 1 chưa đọc');
  assert.equal(core.bellLabel(12), 'Thông báo, 12 chưa đọc');
  assert.equal(core.bellLabel(250), 'Thông báo, hơn 99 chưa đọc');
});

test('newChipText and summaryText use the page wording', () => {
  assert.equal(core.newChipText(0), '');
  assert.equal(core.newChipText(2), '2 mới');
  assert.equal(core.newChipText(140), '99+ mới');
  assert.equal(core.summaryText(2), 'Bạn có 2 thông báo chưa đọc.');
  assert.equal(core.summaryText(0), 'Bạn đã đọc hết thông báo.');
});

test('markAllMessage says how many were marked', () => {
  assert.equal(core.markAllMessage(3), 'Đã đánh dấu 3 thông báo là đã đọc.');
  assert.equal(core.markAllMessage(0), 'Không còn thông báo chưa đọc.');
  assert.equal(core.FAILURE_MESSAGE, 'Chưa đánh dấu được. Vui lòng thử lại.');
});

test('safeLocalUrl mirrors the server rule: one slash, no scheme, no backslash, no control character', () => {
  for (const ok of ['/Goals', '/Goals#badges', '/Premium?plan=year', '/', '/Billing/Order/TCT-1']) {
    assert.equal(core.safeLocalUrl(ok), ok);
    assert.equal(core.isSafeLocalUrl(ok), true);
  }
  assert.equal(core.safeLocalUrl('  /Goals \t'), '/Goals');
  for (const bad of [null, undefined, 42, '', '   ', 'Goals', './Goals', '//evil.example', '/\\evil.example',
    '\\\\evil.example', '\t//evil.example', 'https://evil.example', 'javascript:alert(1)', 'JaVaScRiPt:alert(1)',
    'data:text/html,x', '/ok\nbreak', '/ok\rreturn', '/ok\ttab', '/ok\u0000nul', '/ok\u007fdel', '/a\\b']) {
    assert.equal(core.safeLocalUrl(bad), null, JSON.stringify(bad));
    assert.equal(core.isSafeLocalUrl(bad), false);
  }
  const long = '/' + 'a'.repeat(600);
  assert.equal(core.safeLocalUrl(long).length, 500);
});

test('dayGroupLabel and groupByDay keep order and drop empty groups', () => {
  assert.equal(core.dayGroupLabel('today'), 'Hôm nay');
  assert.equal(core.dayGroupLabel('yesterday'), 'Hôm qua');
  assert.equal(core.dayGroupLabel('earlier'), 'Trước đó');
  assert.equal(core.dayGroupLabel('anything'), 'Trước đó');

  const groups = core.groupByDay([
    item(1, { dayGroup: 'earlier' }), item(2, { dayGroup: 'today' }), item(3, { dayGroup: 'weird' }),
    item(4, { dayGroup: 'today' }),
  ]);
  assert.deepEqual(groups.map((g) => g.key), ['today', 'earlier']);
  assert.deepEqual(groups[0].items.map((i) => i.id), [2, 4]);
  assert.deepEqual(groups[1].items.map((i) => i.id), [1, 3]);
  assert.deepEqual(core.groupByDay([]), []);
  assert.deepEqual(core.groupByDay(null), []);
});

test('mergeById appends only new ids, keeps order and does not mutate', () => {
  const existing = [item(5), item(4)];
  const merged = core.mergeById(existing, [item(4), item(3), item(5), item(2)]);
  assert.deepEqual(merged.map((i) => i.id), [5, 4, 3, 2]);
  assert.equal(existing.length, 2);
  assert.deepEqual(core.mergeById(null, [item(1), item(1)]).map((i) => i.id), [1]);
  assert.deepEqual(core.mergeById([item(1)], null).map((i) => i.id), [1]);
});

test('cursorQuery encodes the server cursor and ignores a missing one', () => {
  assert.equal(core.cursorQuery(null), '');
  assert.equal(core.cursorQuery({}), '');
  assert.equal(core.cursorQuery({ before: '', beforeId: 3 }), '');
  assert.equal(
    core.cursorQuery({ before: '2026-11-17T09:00:00.1234567Z', beforeId: 42 }),
    'before=2026-11-17T09%3A00%3A00.1234567Z&beforeId=42');
  assert.equal(core.cursorQuery({ before: '2026-11-17T09:00:00Z' }), 'before=2026-11-17T09%3A00%3A00Z');
});

test('emptyKind chooses the right empty state', () => {
  assert.equal(core.emptyKind('all', null, 3), null);
  assert.equal(core.emptyKind('all', null, 0), 'none');
  assert.equal(core.emptyKind('unread', null, 0), 'unread');
  assert.equal(core.emptyKind('unread', 'badge', 0), 'unread');
  assert.equal(core.emptyKind('all', 'badge', 0), 'category');
  assert.equal(core.emptyKind('unread', null, 2), null);
});

test('markRead lowers the unread total once and never below zero', () => {
  const state = { items: [item(1), item(2, { isRead: true })], unread: 5 };
  const once = core.markRead(state, 1);
  assert.equal(once.items[0].isRead, true);
  assert.equal(once.unread, 4);
  assert.equal(state.items[0].isRead, false, 'input is not mutated');
  assert.equal(state.unread, 5);

  assert.equal(core.markRead(once, 1).unread, 4, 'marking twice changes nothing');
  assert.equal(core.markRead(state, 2).unread, 5, 'an already read item changes nothing');
  assert.equal(core.markRead(state, 99).unread, 5, 'an item that is not loaded changes nothing');
  assert.equal(core.markRead({ items: [item(1)], unread: 0 }, 1).unread, 0);
});

test('restoreUnread undoes a failed markRead', () => {
  const state = { items: [item(1)], unread: 1 };
  const read = core.markRead(state, 1);
  const back = core.restoreUnread(read, 1);
  assert.equal(back.items[0].isRead, false);
  assert.equal(back.unread, 1);
  assert.equal(core.restoreUnread(state, 1).unread, 1, 'an unread item stays as it is');
});

test('applyMarkAll marks the loaded items and takes the unread total from the server', () => {
  const state = {
    items: [item(1), item(2, { category: 'badge' }), item(3, { isRead: true })],
    unread: 7,
  };
  const all = core.applyMarkAll(state, { updated: 7, unreadCount: 0 });
  assert.ok(all.items.every((i) => i.isRead));
  assert.equal(all.unread, 0);

  const badge = core.applyMarkAll(state, { updated: 1, unreadCount: 6 }, 'badge');
  assert.deepEqual(badge.items.map((i) => i.isRead), [false, true, true]);
  assert.equal(badge.unread, 6);

  assert.equal(core.applyMarkAll(state, null).unread, 0, 'no answer body: everything was marked');
  assert.equal(core.applyMarkAll(state, {}, 'badge').unread, 7, 'no answer body for a category: keep the total');
  assert.equal(state.unread, 7, 'input is not mutated');
});

test('nextTabIndex wraps with the arrow keys and jumps with Home and End', () => {
  assert.equal(core.nextTabIndex(0, 'ArrowRight', 2), 1);
  assert.equal(core.nextTabIndex(1, 'ArrowRight', 2), 0);
  assert.equal(core.nextTabIndex(0, 'ArrowLeft', 2), 1);
  assert.equal(core.nextTabIndex(1, 'ArrowLeft', 2), 0);
  assert.equal(core.nextTabIndex(1, 'Home', 3), 0);
  assert.equal(core.nextTabIndex(0, 'End', 3), 2);
  assert.equal(core.nextTabIndex(1, 'a', 3), 1);
  assert.equal(core.nextTabIndex(0, 'ArrowRight', 0), 0);
});

test('nearBottom asks for the next slice at the end of the list', () => {
  assert.equal(core.nearBottom({ scrollTop: 0, clientHeight: 400, scrollHeight: 1000 }), false);
  assert.equal(core.nearBottom({ scrollTop: 560, clientHeight: 400, scrollHeight: 1000 }), true);
  assert.equal(core.nearBottom({ scrollTop: 552, clientHeight: 400, scrollHeight: 1000 }), true);
  assert.equal(core.nearBottom({ scrollTop: 100, clientHeight: 400, scrollHeight: 400 }), true, 'short list');
  assert.equal(core.nearBottom({ scrollTop: 500, clientHeight: 400, scrollHeight: 1000 }, 200), true);
});

test('nextPollDelay: base after success, doubling with a five-minute cap after failures', () => {
  assert.equal(core.nextPollDelay(240000, true), 60000);
  assert.equal(core.nextPollDelay(60000, false), 120000);
  assert.equal(core.nextPollDelay(120000, false), 240000);
  assert.equal(core.nextPollDelay(240000, false), 300000);
  assert.equal(core.nextPollDelay(300000, false), 300000);
  assert.equal(core.nextPollDelay(0, false), 120000);
});

test('constants match the plan', () => {
  assert.equal(core.MARK_READ_NAVIGATE_TIMEOUT_MS, 1500);
  assert.equal(core.POLL_BASE_MS, 60000);
  assert.equal(core.POLL_MAX_MS, 300000);
});
