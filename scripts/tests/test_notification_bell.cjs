'use strict';

// Bell panel behaviour added by the notification redesign (plan 2026-11-17-notifications-complete, T7):
// badge/label/chip state, the answer's unread count instead of a second request, safe navigation, mark-all counts.
// The older failure-path tests stay in test_notification_behavior.cjs.

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const vm = require('node:vm');
const { createDocument, createElement } = require('./dom_fixture.cjs');

const JS_DIR = path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js');
const NOTIFICATIONS_JS = path.join(JS_DIR, 'notifications.js');
const CORE_JS = path.join(JS_DIR, 'notification-core.js');

function boot({ fetchImpl }) {
  const { document, byId } = createDocument([
    'notificationBtn', 'notificationBadge', 'notifPanel', 'notifTitle', 'notifNewChip', 'notifList',
    'notifMarkAllBtn', 'notifLoadMoreBtn', 'notifStatus', 'sidebarNotifBadge',
  ]);
  byId.notificationBtn.setAttribute('aria-expanded', 'false');
  byId.notificationBadge.hidden = true;
  byId.sidebarNotifBadge.hidden = true;
  byId.notifNewChip.hidden = true;
  byId.notifMarkAllBtn.hidden = true;

  const calls = [];
  const wrapped = async (url, options = {}) => {
    calls.push({ url: String(url), options });
    return fetchImpl(String(url), options);
  };
  const window = {
    document, fetch: wrapped, console, setTimeout, clearTimeout, setInterval, clearInterval,
    addEventListener() {}, location: { href: '/start' }, TCT_NOTIFICATIONS_TEST_MODE: true,
  };
  window.window = window;
  const sandbox = { window, document, fetch: wrapped, console, setTimeout, clearTimeout, setInterval, clearInterval };
  Object.assign(sandbox, window);
  vm.runInNewContext(fs.readFileSync(CORE_JS, 'utf8'), sandbox, { filename: CORE_JS });
  sandbox.window.TctNotificationCore = sandbox.TctNotificationCore;
  vm.runInNewContext(fs.readFileSync(NOTIFICATIONS_JS, 'utf8'), sandbox, { filename: NOTIFICATIONS_JS });
  return { api: window.TctNotifications, byId, calls, window };
}

const ok = (body) => ({ ok: true, status: 200, json: async () => body });
const unreadItem = (id, over = {}) => {
  const el = createElement('a');
  el.classList.add('notif-item', 'is-unread');
  const dot = createElement('span');
  dot.classList.add('notif-unread-dot');
  el.appendChild(dot);
  return Object.assign(el, over);
};

test('updateBadge drives the badges, the accessible name, the "n mới" chip and the mark-all button', async () => {
  const { api, byId } = boot({ fetchImpl: async () => ok({ count: 0 }) });
  api.stopPolling();

  api.updateBadge(3);
  assert.equal(byId.notificationBadge.textContent, '3');
  assert.equal(byId.notificationBadge.hidden, false);
  assert.equal(byId.sidebarNotifBadge.textContent, '3');
  assert.equal(byId.sidebarNotifBadge.hidden, false);
  assert.equal(byId.notificationBtn.getAttribute('aria-label'), 'Thông báo, 3 chưa đọc');
  assert.ok(byId.notificationBtn.classList.contains('has-notif'));
  assert.equal(byId.notifNewChip.textContent, '3 mới');
  assert.equal(byId.notifNewChip.hidden, false);
  assert.equal(byId.notifMarkAllBtn.hidden, false);

  api.updateBadge(150);
  assert.equal(byId.notificationBadge.textContent, '99+');
  assert.equal(byId.notificationBtn.getAttribute('aria-label'), 'Thông báo, hơn 99 chưa đọc');

  api.updateBadge(0);
  assert.equal(byId.notificationBadge.textContent, '');
  assert.equal(byId.notificationBadge.hidden, true);
  assert.equal(byId.sidebarNotifBadge.hidden, true);
  assert.equal(byId.notificationBtn.getAttribute('aria-label'), 'Thông báo');
  assert.equal(byId.notificationBtn.classList.contains('has-notif'), false);
  assert.equal(byId.notifNewChip.hidden, true);
  assert.equal(byId.notifMarkAllBtn.hidden, true);
});

test('updateBadge clears an inline display left by older markup so "hidden" decides', async () => {
  const { api, byId } = boot({ fetchImpl: async () => ok({ count: 0 }) });
  api.stopPolling();
  byId.notificationBadge.style.display = 'none';

  api.updateBadge(2);

  assert.equal(byId.notificationBadge.style.display, '');
  assert.equal(byId.notificationBadge.hidden, false);
});

test('mark read uses the unread count in the answer and sends no second request before navigating', async () => {
  const { api, byId, calls, window } = boot({
    fetchImpl: async (url) => {
      if (url.includes('/mark-read')) return ok({ success: true, unreadCount: 4 });
      return ok({ count: 5 });
    },
  });
  api.stopPolling();
  api.updateBadge(5);
  calls.length = 0;

  const n = { id: 11, isRead: false, url: '/Goals#badges', relatedUrl: '/Goals#badges' };
  const el = unreadItem(11);
  const done = await api.handleItemClick(n, el);

  assert.equal(done, true);
  assert.equal(n.isRead, true);
  assert.equal(el.classList.contains('is-unread'), false);
  assert.equal(byId.notificationBadge.textContent, '4', 'badge follows the answer');
  assert.equal(calls.filter((c) => c.url.includes('unread-count')).length, 0, 'no extra unread-count request (F19)');
  assert.equal(calls.filter((c) => c.url.includes('/mark-read')).length, 1);
  assert.equal(calls[0].options.method, 'POST');
  assert.equal(calls[0].options.headers.RequestVerificationToken, 'test-csrf');
  assert.equal(window.location.href, '/Goals#badges');
});

test('a link that is not a local path is never followed', async () => {
  const { api, window } = boot({
    fetchImpl: async (url) => (url.includes('/mark-read') ? ok({ success: true, unreadCount: 0 }) : ok({ count: 1 })),
  });
  api.stopPolling();

  for (const bad of ['//evil.example/x', 'https://evil.example', 'javascript:alert(1)', '/ok\\bad']) {
    window.location.href = '/start';
    const n = { id: 21, isRead: false, url: bad, relatedUrl: bad };
    await api.handleItemClick(n, unreadItem(21));
    assert.equal(window.location.href, '/start', 'must not navigate to ' + bad);
    assert.equal(n.isRead, true, 'still marked read');
  }
});

test('an item that is already read just navigates, without a request', async () => {
  const { api, calls, window } = boot({ fetchImpl: async () => ok({ count: 0 }) });
  api.stopPolling();
  calls.length = 0;

  const n = { id: 31, isRead: true, url: '/Premium', relatedUrl: '/Premium' };
  await api.handleItemClick(n, unreadItem(31));

  assert.equal(window.location.href, '/Premium');
  assert.equal(calls.length, 0);
});

test('mark all clears the loaded items, takes the count from the answer and announces how many', async () => {
  const { api, byId } = boot({
    fetchImpl: async (url) => {
      if (url.includes('mark-all-read')) return ok({ success: true, updated: 3, unreadCount: 0 });
      return ok({ count: 3 });
    },
  });
  api.stopPolling();
  api.updateBadge(3);
  const a = unreadItem(1);
  const b = unreadItem(2);
  byId.notifList.appendChild(a);
  byId.notifList.appendChild(b);

  await api.handleMarkAllClick();

  assert.equal(a.classList.contains('is-unread'), false);
  assert.equal(b.querySelector('.notif-unread-dot'), null);
  assert.equal(byId.notificationBadge.hidden, true);
  assert.equal(byId.notifMarkAllBtn.disabled, false, 'button is usable again');
  assert.equal(byId.notifMarkAllBtn.hidden, true, 'and hidden while nothing is unread');
  assert.equal(byId.notifStatus.textContent, 'Đã đánh dấu 3 thông báo là đã đọc.');
});

test('a failed mark all announces the failure and keeps the badge', async () => {
  const { api, byId } = boot({
    fetchImpl: async (url) => (url.includes('mark-all-read') ? { ok: false, status: 500, json: async () => ({}) } : ok({ count: 2 })),
  });
  api.stopPolling();
  api.updateBadge(2);

  await api.handleMarkAllClick();

  assert.equal(byId.notifMarkAllBtn.disabled, false);
  assert.equal(byId.notificationBadge.textContent, '2');
  assert.equal(byId.notifStatus.textContent, 'Chưa đánh dấu được. Vui lòng thử lại.');
});

test('a failed mark read restores the card, the badge and announces the failure', async () => {
  const { api, byId } = boot({
    fetchImpl: async (url) => (url.includes('/mark-read') ? { ok: false, status: 500, json: async () => ({}) } : ok({ count: 2 })),
  });
  api.stopPolling();
  api.updateBadge(2);
  const n = { id: 41, isRead: false, url: null, relatedUrl: null };
  const el = unreadItem(41);

  const done = await api.handleItemClick(n, el);

  assert.equal(done, false);
  assert.equal(n.isRead, false);
  assert.ok(el.classList.contains('is-unread'));
  assert.equal(byId.notificationBadge.textContent, '2');
  assert.equal(byId.notifStatus.textContent, 'Chưa đánh dấu được. Vui lòng thử lại.');
});

test('polling: a good answer updates the badge, a failed one keeps it', async () => {
  let fail = false;
  const { api, byId } = boot({
    fetchImpl: async () => (fail ? { ok: false, status: 500, json: async () => ({}) } : ok({ count: 7 })),
  });
  await new Promise((resolve) => setTimeout(resolve, 20));   // the boot-time poll finishes first
  api.stopPolling();
  assert.equal(byId.notificationBadge.textContent, '7');

  fail = true;
  await api.fetchUnreadCount();
  api.stopPolling();
  assert.equal(byId.notificationBadge.textContent, '7', 'a failed poll does not clear the badge');
});

test('the bell script builds nodes with createElement only (no innerHTML)', () => {
  const source = fs.readFileSync(NOTIFICATIONS_JS, 'utf8');
  assert.doesNotMatch(source.replace(/\/\*[\s\S]*?\*\//g, ''), /\.innerHTML\s*=|insertAdjacentHTML|outerHTML/);
});
