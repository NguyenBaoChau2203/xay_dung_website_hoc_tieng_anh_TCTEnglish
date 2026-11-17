'use strict';

// Tests for wwwroot/js/billing-core.js: money, saving, summary, countdown, polling schedule and stage handling.

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const core = require(path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'billing-core.js'));
const NBSP = ' ';
const DAY = 24 * 60 * 60 * 1000;

test('money uses dot grouping and a non-breaking space before the sign', () => {
  assert.equal(core.formatMoney(399000), `399.000${NBSP}₫`);
  assert.equal(core.formatMoney(49000), `49.000${NBSP}₫`);
  assert.equal(core.formatMoney(1234567), `1.234.567${NBSP}₫`);
  assert.equal(core.formatMoney(0), `0${NBSP}₫`);
  assert.equal(core.formatMoney(999), `999${NBSP}₫`);
  assert.equal(core.formatMoney(49000.9), `49.000${NBSP}₫`, 'fractions are dropped, like the server');
  assert.equal(core.formatMoney('abc'), `0${NBSP}₫`);
});

test('months and price per month', () => {
  assert.equal(core.monthsOf(30), 1);
  assert.equal(core.monthsOf(365), 12);
  assert.equal(core.monthsOf(7), 1, 'never below one month');
  assert.equal(core.perMonth(399000, 365), 33250);
  assert.equal(core.perMonth(49000, 30), 49000);
});

test('saving percent compares with the cheapest monthly plan', () => {
  assert.equal(core.savingPercent(399000, 365, 49000), 32);
  assert.equal(core.savingPercent(49000, 30, 49000), 0, 'the monthly plan itself never shows a saving');
  assert.equal(core.savingPercent(588000, 365, 49000), 0, 'not positive -> hidden');
  assert.equal(core.savingPercent(700000, 365, 49000), 0);
  assert.equal(core.savingPercent(399000, 365, 0), 0, 'no monthly plan to compare with');
  assert.equal(core.savingPercent(0, 365, 49000), 0);
});

test('dates are shown in Vietnam time, whatever the browser time zone is', () => {
  // 2026-11-17T20:30:00Z is already the 18th in Vietnam (UTC+7).
  const ms = Date.UTC(2026, 10, 17, 20, 30);
  assert.equal(core.formatDate(ms), '18/11/2026');
  assert.equal(core.formatDateTime(ms), '18/11/2026 03:30');
  assert.equal(core.formatDateTime(Date.UTC(2026, 10, 17, 6, 45)), '17/11/2026 13:45');
  assert.equal(core.formatDate(Date.UTC(2027, 0, 1, 0, 0)), '01/01/2027');
});

test('summary for a free buyer starts now', () => {
  const now = Date.UTC(2026, 10, 17, 5, 0);
  const s = core.summary({ planName: 'Premium 1 năm', price: 399000, days: 365, nowMs: now, providerName: 'VNPay' });
  assert.equal(s.plan, 'Premium 1 năm');
  assert.equal(s.validity, '365 ngày, đến 17/11/2027');
  assert.equal(s.total, `399.000${NBSP}₫`);
  assert.equal(s.button, `Thanh toán 399.000${NBSP}₫`);
  assert.match(s.note, /Bạn sẽ được chuyển sang trang của VNPay để hoàn tất\. TCT English không lưu thông tin thẻ của bạn\./);
});

test('summary for a Premium buyer extends from the current end date', () => {
  const now = Date.UTC(2026, 10, 17, 5, 0);
  const endsIn10Days = now + 10 * DAY;
  const s = core.summary({ planName: 'Premium 1 tháng', price: 49000, days: 30, nowMs: now, currentEndMs: endsIn10Days, providerName: 'MoMo' });
  assert.equal(s.validity, '30 ngày, đến 27/12/2026');
  // A lapsed end date counts as "now".
  const lapsed = core.summary({ planName: 'x', price: 49000, days: 30, nowMs: now, currentEndMs: now - DAY });
  assert.equal(lapsed.validity, '30 ngày, đến 17/12/2026');
  assert.equal(lapsed.note, '', 'no provider chosen -> no note');
});

test('countdown formats mm:ss and never goes negative', () => {
  assert.equal(core.formatCountdown(14 * 60 * 1000 + 32 * 1000), '14:32');
  assert.equal(core.formatCountdown(59 * 1000 + 1), '01:00', 'rounds up so it never shows 00:00 early');
  assert.equal(core.formatCountdown(0), '00:00');
  assert.equal(core.formatCountdown(-5000), '00:00');
  assert.equal(core.formatCountdown(15 * 60 * 1000), '15:00');
});

test('remaining time follows the server clock offset', () => {
  const expires = 1_000_000;
  assert.equal(core.remainingMs(expires, 900_000, 0), 100_000);
  // The browser clock is 30 s ahead of the server: the server says 130 s are left, not 100 s.
  assert.equal(core.remainingMs(expires, 900_000, -30_000), 130_000);
  assert.equal(core.remainingMs(expires, 2_000_000, 0), 0);
});

test('polling schedule: 3 s waiting, 5 s confirming, 15 s after a minute, stop at ten minutes', () => {
  assert.equal(core.nextDelay('waiting', 0), 3000);
  assert.equal(core.nextDelay('waiting', 59_999), 3000);
  assert.equal(core.nextDelay('confirming', 0), 5000);
  assert.equal(core.nextDelay('confirming', 59_999), 5000);
  assert.equal(core.nextDelay('waiting', 60_000), 15000);
  assert.equal(core.nextDelay('confirming', 120_000), 15000);
  assert.equal(core.nextDelay('waiting', 10 * 60 * 1000 - 1), 15000);
  assert.equal(core.nextDelay('waiting', 10 * 60 * 1000), -1);
  assert.equal(core.nextDelay('paid', 0), -1);
  assert.equal(core.nextDelay('not_completed', 0), -1);
  assert.equal(core.nextDelay('anything-else', 0), -1);
});

test('the refresh is posted every 15 s for the first three minutes, only while confirming', () => {
  assert.equal(core.refreshDue('confirming', 0, null), true);
  assert.equal(core.refreshDue('confirming', 10_000, 10_000), false);
  assert.equal(core.refreshDue('confirming', 20_000, 15_000), true);
  assert.equal(core.refreshDue('confirming', 3 * 60 * 1000, 15_000), true);
  assert.equal(core.refreshDue('confirming', 3 * 60 * 1000 + 1, 99_000), false);
  assert.equal(core.refreshDue('waiting', 0, null), false);
  assert.equal(core.refreshDue('paid', 0, null), false);
});

test('reacting to a status answer', () => {
  assert.deepEqual(core.reactTo({ stage: 'paid', paid: true }, 'waiting'), { action: 'reload', stage: 'paid' });
  assert.deepEqual(core.reactTo({ paid: true }, 'confirming'), { action: 'reload', stage: 'paid' });
  assert.deepEqual(core.reactTo({ stage: 'not_completed' }, 'waiting'), { action: 'reload', stage: 'not_completed' });
  assert.deepEqual(core.reactTo({ stage: 'confirming' }, 'waiting'), { action: 'reload', stage: 'confirming' });
  assert.deepEqual(core.reactTo({ stage: 'waiting' }, 'waiting'), { action: 'continue', stage: 'waiting' });
  assert.deepEqual(core.reactTo({ stage: 'confirming' }, 'confirming'), { action: 'continue', stage: 'confirming' });
  assert.deepEqual(core.reactTo(null, 'waiting'), { action: 'continue', stage: 'waiting' });
  assert.deepEqual(core.reactTo('nope', 'waiting'), { action: 'continue', stage: 'waiting' });
});

test('a 401 or a redirect to the login page means the session ended', () => {
  assert.equal(core.isSessionEnded({ status: 401 }), true);
  assert.equal(core.isSessionEnded({ status: 0, type: 'opaqueredirect' }), true);
  assert.equal(core.isSessionEnded({ status: 200, redirected: true, url: 'http://localhost/Account/Login?ReturnUrl=%2FBilling' }), true);
  assert.equal(core.isSessionEnded({ status: 200, redirected: true, url: 'http://localhost/Billing/OrderStatus?x=1' }), false);
  assert.equal(core.isSessionEnded({ status: 200, redirected: false, url: 'http://localhost/Billing/OrderStatus' }), false);
  assert.equal(core.isSessionEnded({ status: 500 }), false, 'a server error is a transient failure, not a sign-out');
  assert.equal(core.isSessionEnded(null), false);
});

test('the login address comes back to the order page and stays local', () => {
  const url = core.loginUrlFor('TCT-20261117064512-9F3A2C1B');
  assert.equal(url, '/Account/Login?returnUrl=' + encodeURIComponent('/Billing/Order/TCT-20261117064512-9F3A2C1B'));
  assert.ok(url.startsWith('/Account/Login?'));
  const odd = core.loginUrlFor('../../evil?x=1&y=2');
  assert.ok(!odd.includes('&y=2'), 'the code is encoded, never added as a parameter');
});
