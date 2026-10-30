'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const vm = require('node:vm');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const NOTIFICATIONS_JS = path.join(
  __dirname,
  '..',
  '..',
  'TCTEnglish',
  'wwwroot',
  'js',
  'notifications.js'
);
const NOTIFICATION_INDEX = path.join(
  __dirname,
  '..',
  '..',
  'TCTEnglish',
  'Views',
  'Notification',
  'Index.cshtml'
);

function extractInlineNotificationScript(cshtml) {
  const match = cshtml.match(/@section Scripts \{[\s\S]*?<script>([\s\S]*?)<\/script>/);
  assert.ok(match, 'Notification Index must contain an inline script section');
  return match[1];
}

function loadNotificationsModule({ markReadResponse, markAllResponse }) {
  const { document, byId } = createDocument([
    'notificationBtn',
    'notificationBadge',
    'notifPanel',
    'notifList',
    'notifMarkAllBtn',
    'notifLoadMoreBtn',
    'notifSkeletonTpl',
    'sidebarNotifBadge',
  ]);

  byId.notificationBtn.setAttribute('aria-expanded', 'false');
  byId.notificationBadge.style.display = 'none';
  byId.notifMarkAllBtn.disabled = false;
  byId.notifMarkAllBtn.textContent = 'Đọc tất cả';

  const fetchCalls = [];
  const fetchImpl = async (url, options = {}) => {
    const u = String(url);
    fetchCalls.push({ url: u, options });

    if (u.includes('/api/notifications/unread-count')) {
      return { ok: true, json: async () => ({ count: 1 }) };
    }
    if (u.includes('/api/notifications/mark-all-read')) {
      const res = markAllResponse || { ok: false, status: 500 };
      return {
        ok: res.ok,
        status: res.status || (res.ok ? 200 : 500),
        json: async () => (res.body != null ? res.body : { success: res.ok }),
      };
    }
    if (u.includes('/mark-read')) {
      const res = markReadResponse || { ok: false, status: 500 };
      return {
        ok: res.ok,
        status: res.status || (res.ok ? 200 : 500),
        json: async () => (res.body != null ? res.body : { success: res.ok }),
      };
    }
    if (u.includes('/api/notifications?')) {
      return {
        ok: true,
        json: async () => ({
          success: true,
          data: {
            items: [
              {
                id: 7,
                type: 'GoalCompleted',
                title: 'Goal',
                message: 'Done',
                isRead: false,
                relatedUrl: null,
                timeAgo: '1 phút trước',
              },
            ],
            unreadCount: 1,
            hasMore: false,
          },
        }),
      };
    }
    return { ok: true, json: async () => ({}) };
  };

  const window = {
    document,
    fetch: fetchImpl,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    addEventListener() {},
    location: { href: '' },
    TCT_NOTIFICATIONS_TEST_MODE: true,
  };
  window.window = window;

  const sandbox = {
    window,
    document,
    fetch: fetchImpl,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
  };
  Object.assign(sandbox, window);

  const code = fs.readFileSync(NOTIFICATIONS_JS, 'utf8');
  vm.runInNewContext(code, sandbox, { filename: NOTIFICATIONS_JS });

  return {
    api: window.TctNotifications,
    document,
    byId,
    fetchCalls,
    window,
  };
}

test('mark-read HTTP failure keeps unread state and does not claim read', async () => {
  const { api, byId } = loadNotificationsModule({
    markReadResponse: { ok: false, status: 500 },
  });
  assert.ok(api, 'notifications test hooks must be exposed');

  const n = {
    id: 7,
    isRead: false,
    relatedUrl: null,
    title: 'Goal',
    message: 'Done',
    type: 'GoalCompleted',
    timeAgo: '1m',
  };
  const elItem = createElement('div');
  elItem.classList.add('notif-item', 'is-unread');
  const dot = createElement('div');
  dot.classList.add('notif-unread-dot');
  elItem.appendChild(dot);
  byId.notifList.appendChild(elItem);

  await api.handleItemClick(n, elItem);

  assert.equal(n.isRead, false);
  assert.ok(elItem.classList.contains('is-unread'));
  assert.ok(elItem.querySelector('.notif-unread-dot'));
});

test('mark-read network failure keeps unread and skips relatedUrl navigation claim', async () => {
  const { document, byId } = createDocument([
    'notificationBtn',
    'notificationBadge',
    'notifPanel',
    'notifList',
    'notifMarkAllBtn',
    'notifLoadMoreBtn',
  ]);
  byId.notificationBtn.setAttribute('aria-expanded', 'false');

  const fetchImpl = async (url) => {
    if (String(url).includes('/unread-count')) {
      return { ok: true, json: async () => ({ count: 1 }) };
    }
    if (String(url).includes('/mark-read')) {
      throw new Error('network down');
    }
    return { ok: true, json: async () => ({ success: true, data: { items: [], unreadCount: 1, hasMore: false } }) };
  };

  const window = {
    document,
    fetch: fetchImpl,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    addEventListener() {},
    location: { href: '/Goals' },
    TCT_NOTIFICATIONS_TEST_MODE: true,
  };
  window.window = window;
  const sandbox = { window, document, fetch: fetchImpl, console, setTimeout, clearTimeout, setInterval, clearInterval };
  Object.assign(sandbox, window);
  vm.runInNewContext(fs.readFileSync(NOTIFICATIONS_JS, 'utf8'), sandbox, { filename: NOTIFICATIONS_JS });

  const api = window.TctNotifications;
  const n = { id: 9, isRead: false, relatedUrl: '/Goals/Other' };
  const elItem = createElement('div');
  elItem.classList.add('notif-item', 'is-unread');
  elItem.appendChild(Object.assign(createElement('div'), { className: 'notif-unread-dot' }));
  // recreate classList after className assign
  elItem.classList.add('is-unread');
  const dot = createElement('div');
  dot.classList.add('notif-unread-dot');
  elItem.appendChild(dot);

  const hrefBefore = window.location.href;
  await api.handleItemClick(n, elItem);

  assert.equal(n.isRead, false);
  assert.ok(elItem.classList.contains('is-unread'));
  assert.equal(window.location.href, hrefBefore, 'must not navigate as if read succeeded');
});

test('mark-all HTTP failure restores retryable button and keeps unread items', async () => {
  const { api, byId } = loadNotificationsModule({
    markAllResponse: { ok: false, status: 400 },
  });

  const item = createElement('div');
  item.classList.add('notif-item', 'is-unread');
  const dot = createElement('div');
  dot.classList.add('notif-unread-dot');
  item.appendChild(dot);
  byId.notifList.appendChild(item);

  await api.handleMarkAllClick();

  assert.equal(byId.notifMarkAllBtn.disabled, false, 'button must be retryable after HTTP error');
  assert.ok(item.classList.contains('is-unread'));
});

test('mark-all network failure restores disabled button', async () => {
  const { document, byId } = createDocument([
    'notificationBtn',
    'notificationBadge',
    'notifPanel',
    'notifList',
    'notifMarkAllBtn',
    'notifLoadMoreBtn',
  ]);
  byId.notificationBtn.setAttribute('aria-expanded', 'false');
  byId.notifMarkAllBtn.disabled = false;

  const fetchImpl = async (url) => {
    if (String(url).includes('/unread-count')) {
      return { ok: true, json: async () => ({ count: 2 }) };
    }
    if (String(url).includes('/mark-all-read')) {
      throw new Error('offline');
    }
    return { ok: true, json: async () => ({}) };
  };

  const window = {
    document,
    fetch: fetchImpl,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    addEventListener() {},
    location: { href: '' },
    TCT_NOTIFICATIONS_TEST_MODE: true,
  };
  window.window = window;
  const sandbox = { window, document, fetch: fetchImpl, console, setTimeout, clearTimeout, setInterval, clearInterval };
  Object.assign(sandbox, window);
  vm.runInNewContext(fs.readFileSync(NOTIFICATIONS_JS, 'utf8'), sandbox, { filename: NOTIFICATIONS_JS });

  await window.TctNotifications.handleMarkAllClick();
  assert.equal(byId.notifMarkAllBtn.disabled, false);
});

test('Notification Index inline script must check mark-read success before clearing unread', () => {
  const cshtml = fs.readFileSync(NOTIFICATION_INDEX, 'utf8');
  const script = extractInlineNotificationScript(cshtml);

  // markAsRead must throw/reject when !res.ok (not fire-and-forget).
  assert.match(script, /async function markAsRead[\s\S]*if\s*\(\s*!res\.ok\s*\)/);
  // Rollback unread when mark-read fails.
  assert.match(script, /restoreUnreadPageItem/);
  // Mark-all must re-enable the button on HTTP failure (not only catch).
  assert.match(script, /if\s*\(\s*!res\.ok\s*\)\s*\{[\s\S]*elMarkAll\.disabled\s*=\s*false/);
});

test('successful mark-read may clear unread', async () => {
  const { api } = loadNotificationsModule({
    markReadResponse: { ok: true, status: 200, body: { success: true } },
  });

  const n = { id: 3, isRead: false, relatedUrl: null };
  const elItem = createElement('div');
  elItem.classList.add('notif-item', 'is-unread');
  const dot = createElement('div');
  dot.classList.add('notif-unread-dot');
  elItem.appendChild(dot);

  await api.handleItemClick(n, elItem);
  assert.equal(n.isRead, true);
  assert.equal(elItem.classList.contains('is-unread'), false);
});
