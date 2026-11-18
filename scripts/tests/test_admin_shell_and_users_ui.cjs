'use strict';

/**
 * Behavior tests for Admin Shell (AS01) and User Management (AD02) interactive logic.
 * Executes production TCTEnglish/wwwroot/js/admin-shell.js and admin-user-management.js
 * in a Node harness with fake DOM using scripts/tests/dom_fixture.cjs.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement } = require('./dom_fixture.cjs');

const ADMIN_SHELL_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/admin-shell.js');
const ADMIN_USERS_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/admin-user-management.js');

function setupAdminShellDom(options = {}) {
  const { document: doc, byId, body } = createDocument([]);

  function make(tag, id, classes = '') {
    const el = createElement(tag, id);
    if (id) byId[id] = el;
    if (classes) {
      classes.split(' ').filter(Boolean).forEach((c) => el.classList.add(c));
    }
    el.removeAttribute = (name) => {
      delete el.attributes[name];
    };
    el.focus = () => {
      doc._activeElement = el;
    };
    return el;
  }

  const toggleBtn = make('button', 'sidebarToggle', 'admin-header-toggle');
  toggleBtn.setAttribute('aria-expanded', 'false');
  doc.body.appendChild(toggleBtn);

  const backdrop = make('div', 'adminSidebarBackdrop', 'admin-backdrop');
  doc.body.appendChild(backdrop);

  const sidebar = make('aside', 'adminSidebar', 'admin-sidebar');
  const closeBtn = make('button', 'sidebarCloseBtn');
  sidebar.appendChild(closeBtn);

  const firstLink = make('a', 'firstNavLink', 'admin-nav-link');
  firstLink.href = '/Admin/Dashboard';
  sidebar.appendChild(firstLink);

  doc.body.appendChild(sidebar);

  return { doc, byId, body: doc.body, toggleBtn, backdrop, sidebar, closeBtn, firstLink };
}

function loadAdminShellScript(dom, isMobile = true) {
  const windowListeners = Object.create(null);
  const mqlListeners = [];

  const mql = {
    matches: isMobile,
    addEventListener: (type, fn) => {
      mqlListeners.push(fn);
    },
    addListener: (fn) => {
      mqlListeners.push(fn);
    },
    setMatches: (val) => {
      mql.matches = val;
      mqlListeners.forEach((fn) => fn());
    },
  };

  const windowObj = {
    document: dom.doc,
    matchMedia: () => mql,
    _listeners: windowListeners,
    addEventListener: (type, fn) => {
      if (!windowListeners[type]) windowListeners[type] = [];
      windowListeners[type].push(fn);
    },
    removeEventListener: (type, fn) => {
      if (!windowListeners[type]) return;
      windowListeners[type] = windowListeners[type].filter((f) => f !== fn);
    },
    dispatchEvent: (evt) => {
      const list = windowListeners[evt.type] || [];
      list.forEach((fn) => fn(evt));
    },
    console,
  };

  const sandbox = {
    window: windowObj,
    document: dom.doc,
    console,
  };

  vm.createContext(sandbox);
  const fs = require('node:fs');
  const code = fs.readFileSync(ADMIN_SHELL_JS_PATH, 'utf8');
  vm.runInContext(code, sandbox);

  return { sandbox, mql, windowObj };
}

test('AS01: sidebar toggle opens drawer, sets aria-expanded, backdrop and scroll-lock', () => {
  const dom = setupAdminShellDom();
  const { mql } = loadAdminShellScript(dom, true);

  assert.equal(dom.sidebar.classList.contains('show'), false);
  assert.equal(dom.toggleBtn.getAttribute('aria-expanded'), 'false');
  assert.equal(dom.sidebar.inert, true, 'Sidebar must be inert when closed on mobile');

  // Click toggle button to open
  dom.toggleBtn.dispatchEvent({ type: 'click', target: dom.toggleBtn, stopPropagation() {} });

  assert.equal(dom.sidebar.classList.contains('show'), true, 'Sidebar should have show class');
  assert.equal(dom.toggleBtn.getAttribute('aria-expanded'), 'true');
  assert.equal(dom.sidebar.inert, false, 'Sidebar must NOT be inert when open');
  assert.equal(dom.backdrop.classList.contains('show'), true, 'Backdrop should have show class');
  assert.equal(dom.body.classList.contains('admin-drawer-open'), true, 'Body must have scroll-lock class');
});

test('AS01: close button, backdrop click and Escape key dismiss drawer and restore focus', () => {
  const dom = setupAdminShellDom();
  const { sandbox } = loadAdminShellScript(dom, true);

  // Open drawer
  dom.toggleBtn.dispatchEvent({ type: 'click', target: dom.toggleBtn, stopPropagation() {} });
  assert.equal(dom.sidebar.classList.contains('show'), true);

  // 1. ESC key dismisses drawer
  const docListeners = dom.doc._listeners['keydown'] || [];
  docListeners.forEach((fn) => fn({ key: 'Escape', preventDefault() {} }));

  assert.equal(dom.sidebar.classList.contains('show'), false, 'ESC should close drawer');
  assert.equal(dom.toggleBtn.getAttribute('aria-expanded'), 'false');
  assert.equal(dom.body.classList.contains('admin-drawer-open'), false, 'Body scroll-lock must be removed');
  assert.equal(dom.doc._activeElement, dom.toggleBtn, 'Focus should return to toggle button');

  // 2. Open again and test backdrop click
  dom.toggleBtn.dispatchEvent({ type: 'click', target: dom.toggleBtn, stopPropagation() {} });
  assert.equal(dom.sidebar.classList.contains('show'), true);

  dom.backdrop.dispatchEvent({ type: 'click', target: dom.backdrop });
  assert.equal(dom.sidebar.classList.contains('show'), false, 'Backdrop click should close drawer');
  assert.equal(dom.body.classList.contains('admin-drawer-open'), false);

  // 3. Open again and test close button
  dom.toggleBtn.dispatchEvent({ type: 'click', target: dom.toggleBtn, stopPropagation() {} });
  assert.equal(dom.sidebar.classList.contains('show'), true);

  dom.closeBtn.dispatchEvent({ type: 'click', target: dom.closeBtn, stopPropagation() {} });
  assert.equal(dom.sidebar.classList.contains('show'), false, 'Close button should close drawer');
});

test('AS01: resize across 991.98px breakpoint resets drawer and restores desktop access', () => {
  const dom = setupAdminShellDom();
  const { mql } = loadAdminShellScript(dom, true);

  // Open drawer in mobile
  dom.toggleBtn.dispatchEvent({ type: 'click', target: dom.toggleBtn, stopPropagation() {} });
  assert.equal(dom.sidebar.classList.contains('show'), true);

  // Switch to desktop breakpoint (>992px)
  mql.setMatches(false);

  assert.equal(dom.sidebar.classList.contains('show'), false, 'Sidebar show class must be removed on desktop');
  assert.equal(dom.sidebar.inert, false, 'Sidebar must be accessible on desktop');
  assert.equal(dom.backdrop.classList.contains('show'), false, 'Backdrop must be removed on desktop');
  assert.equal(dom.body.classList.contains('admin-drawer-open'), false, 'Body scroll-lock must be removed on desktop');
});

function setupUserManagementDom(options = {}) {
  const { document: doc, byId } = createDocument([]);

  function make(tag, id, classes = '') {
    const el = createElement(tag, id);
    if (id) byId[id] = el;
    if (classes) {
      classes.split(' ').filter(Boolean).forEach((c) => el.classList.add(c));
    }
    el.removeAttribute = (name) => {
      delete el.attributes[name];
    };
    // Sync textContent from innerHTML if innerHTML is set
    let _rawInner = '';
    Object.defineProperty(el, 'innerHTML', {
      get() { return _rawInner; },
      set(val) {
        _rawInner = String(val);
        el.textContent = _rawInner.replace(/<[^>]*>/g, '');
      },
      configurable: true,
    });
    return el;
  }

  const page = make('div', 'userManagementPage');
  doc.body.appendChild(page);

  // Anti-forgery
  const afForm = make('form', 'userManagementAntiForgeryForm');
  const token = make('input', null);
  token.name = '__RequestVerificationToken';
  token.value = 'test-csrf-token';
  afForm.appendChild(token);
  doc.body.appendChild(afForm);

  // Connection badge
  const connBadge = make('div', 'presenceConnectionBadge');
  page.appendChild(connBadge);

  // KPI counters
  const totalUsersCount = make('h2', 'totalUsersCount');
  totalUsersCount.textContent = '100';
  page.appendChild(totalUsersCount);

  const onlineUsersCount = make('h2', 'onlineUsersCount');
  onlineUsersCount.textContent = '25';
  page.appendChild(onlineUsersCount);

  const offlineUsersCount = make('h2', 'offlineUsersCount');
  offlineUsersCount.textContent = '70';
  page.appendChild(offlineUsersCount);

  const blockedUsersCount = make('h2', 'blockedUsersCount');
  blockedUsersCount.textContent = '5';
  page.appendChild(blockedUsersCount);

  const filteredUsersCount = make('span', 'filteredUsersCount');
  filteredUsersCount.textContent = '100';
  page.appendChild(filteredUsersCount);

  // Filter Form
  const filterForm = make('form', 'userFilterForm');
  filterForm.method = 'get';
  const inputQ = make('input', 'filterSearchInput');
  inputQ.name = 'q';
  inputQ.value = '';
  filterForm.appendChild(inputQ);
  const selectRole = make('select', 'filterRoleSelect');
  selectRole.name = 'role';
  filterForm.appendChild(selectRole);
  const selectStatus = make('select', 'filterStatusSelect');
  selectStatus.name = 'status';
  selectStatus.value = options.activeStatusFilter || '';
  filterForm.appendChild(selectStatus);
  page.appendChild(filterForm);

  // Table
  const table = make('table', 'usersTable');
  const tbody = make('tbody');
  table.appendChild(tbody);
  page.appendChild(table);

  // Row for User #42
  const row42 = make('tr', 'user-row-42');
  row42.dataset.userId = '42';
  row42.dataset.status = '0'; // Offline

  const name42 = make('div', 'user-full-name-42');
  name42.textContent = 'Test User 42';
  row42.appendChild(name42);

  const email42 = make('div', 'user-email-42');
  email42.textContent = 'user42@example.local';
  row42.appendChild(email42);

  const roleBadge42 = make('span', 'role-badge-42');
  roleBadge42.textContent = 'Standard';
  row42.appendChild(roleBadge42);

  const statusBadge42 = make('span', 'status-badge-42');
  statusBadge42.dataset.status = '0';
  statusBadge42.textContent = 'Offline';
  row42.appendChild(statusBadge42);

  const btnUnlock42 = make('button', null, 'btn-unlock');
  btnUnlock42.dataset.userId = '42';
  btnUnlock42.disabled = true;
  row42.appendChild(btnUnlock42);

  const btnEdit42 = make('button', null, 'btn-edit');
  btnEdit42.dataset.userId = '42';
  row42.appendChild(btnEdit42);

  tbody.appendChild(row42);

  // Modals (stubbed elements)
  const editModalEl = make('div', 'editUserModal');
  const editUserId = make('input', 'editUserId');
  const editFullName = make('input', 'editFullName');
  const editEmail = make('input', 'editEmail');
  const editRole = make('select', 'editRole');
  const editStatusLabel = make('input', 'editStatusLabel');
  const editErrorAlert = make('div', 'editErrorAlert');
  editModalEl.appendChild(editUserId);
  editModalEl.appendChild(editFullName);
  editModalEl.appendChild(editEmail);
  editModalEl.appendChild(editRole);
  editModalEl.appendChild(editStatusLabel);
  editModalEl.appendChild(editErrorAlert);
  doc.body.appendChild(editModalEl);

  const blockModalEl = make('div', 'blockUserModal');
  const blockUserId = make('input', 'blockUserId');
  const blockUserName = make('span', 'blockUserName');
  const blockDuration = make('select', 'blockDuration');
  const blockReason = make('textarea', 'blockReason');
  const blockErrorAlert = make('div', 'blockErrorAlert');
  blockModalEl.appendChild(blockUserId);
  blockModalEl.appendChild(blockUserName);
  blockModalEl.appendChild(blockDuration);
  blockModalEl.appendChild(blockReason);
  blockModalEl.appendChild(blockErrorAlert);
  doc.body.appendChild(blockModalEl);

  return {
    doc,
    byId,
    page,
    connBadge,
    totalUsersCount,
    onlineUsersCount,
    offlineUsersCount,
    blockedUsersCount,
    filteredUsersCount,
    row42,
    name42,
    email42,
    roleBadge42,
    statusBadge42,
    btnUnlock42,
    tbody,
  };
}

function loadUserManagementScript(dom, contextOverrides = {}) {
  const windowListeners = Object.create(null);
  const hubCallbacks = Object.create(null);

  const fakeHubConnection = {
    on: (evt, cb) => {
      hubCallbacks[evt] = cb;
    },
    off: (evt) => {
      delete hubCallbacks[evt];
    },
  };

  const windowObj = {
    document: dom.doc,
    URLSearchParams: global.URLSearchParams,
    _listeners: windowListeners,
    addEventListener: (type, fn) => {
      if (!windowListeners[type]) windowListeners[type] = [];
      windowListeners[type].push(fn);
    },
    removeEventListener: (type, fn) => {
      if (!windowListeners[type]) return;
      windowListeners[type] = windowListeners[type].filter((f) => f !== fn);
    },
    dispatchEvent: (evt) => {
      const list = windowListeners[evt.type] || [];
      list.forEach((fn) => fn(evt));
    },
    userActivityHubConnection: fakeHubConnection,
    userActivityHubReady: Promise.resolve(fakeHubConnection),
    setTimeout: (fn) => fn(),
    clearTimeout: () => {},
    bootstrap: {
      Modal: function () {
        this.show = () => {};
        this.hide = () => {};
      },
    },
    fetch: contextOverrides.fetch || (() => Promise.resolve({
      ok: true,
      json: () => Promise.resolve({
        statusUpdates: [],
        summary: { totalUsers: 100, onlineUsers: 25, offlineUsers: 70, blockedUsers: 5 },
        totalFilteredCount: 100,
      }),
    })),
    console,
    ...contextOverrides,
  };

  const sandbox = {
    window: windowObj,
    document: dom.doc,
    URLSearchParams: global.URLSearchParams,
    bootstrap: windowObj.bootstrap,
    fetch: windowObj.fetch,
    console,
  };

  vm.createContext(sandbox);
  const fs = require('node:fs');
  const code = fs.readFileSync(ADMIN_USERS_JS_PATH, 'utf8');
  vm.runInContext(code, sandbox);

  return { sandbox, windowObj, hubCallbacks };
}

test('AD02: presence hub events update connection badge and trigger snapshot refresh', async () => {
  const dom = setupUserManagementDom();
  const { windowObj } = loadUserManagementScript(dom);

  // Wait for initial ready promise
  await Promise.resolve();

  // Test connection events
  windowObj.dispatchEvent({ type: 'user-activity:connected' });
  assert.equal(dom.connBadge.textContent.includes('Trực tuyến'), true);

  windowObj.dispatchEvent({ type: 'user-activity:reconnecting' });
  assert.equal(dom.connBadge.textContent.includes('Đang kết nối lại'), true);

  windowObj.dispatchEvent({ type: 'user-activity:closed' });
  assert.equal(dom.connBadge.textContent.includes('Ngoại tuyến'), true);
});

test('AD02: UserStatusChanged event updates user row badge, status dataset and counters', async () => {
  const dom = setupUserManagementDom();
  const { hubCallbacks } = loadUserManagementScript(dom);

  await Promise.resolve();

  assert.equal(dom.statusBadge42.dataset.status, '0');
  assert.equal(dom.onlineUsersCount.textContent, '25');
  assert.equal(dom.offlineUsersCount.textContent, '70');

  // SignalR message that User #42 came Online (status = 1)
  const statusUpdate = {
    userId: 42,
    previousStatus: 0,
    status: 1,
    statusLabel: 'Online',
    statusBadgeClass: 'badge bg-success',
    statusIconClass: 'bi-check-circle-fill',
    canUnlock: false,
  };

  hubCallbacks['UserStatusChanged'](statusUpdate);

  assert.equal(dom.statusBadge42.dataset.status, '1');
  assert.equal(dom.row42.dataset.status, '1');
  assert.equal(dom.statusBadge42.textContent.includes('Online'), true);
  assert.equal(dom.onlineUsersCount.textContent, '26', 'Online count should increment');
  assert.equal(dom.offlineUsersCount.textContent, '69', 'Offline count should decrement');
});
