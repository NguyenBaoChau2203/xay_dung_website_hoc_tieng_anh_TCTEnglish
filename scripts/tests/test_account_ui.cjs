'use strict';

/**
 * Behavior tests for Account UI (AU05 - AU06).
 * Executes production TCTEnglish/wwwroot/js/account-ui.js in a Node harness
 * with fake DOM using scripts/tests/dom_fixture.cjs.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const ACCOUNT_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/account-ui.js');

function setupAccountDom(options = {}) {
  const { document: doc, byId, body } = createDocument([]);

  function dispatchWithBubble(target, evt) {
    evt.target = target;
    let cur = target;
    while (cur) {
      if (cur._listeners && cur._listeners[evt.type]) {
        for (const fn of cur._listeners[evt.type]) fn.call(cur, evt);
      }
      cur = cur.parentNode;
    }
    if (doc._listeners && doc._listeners[evt.type]) {
      for (const fn of doc._listeners[evt.type]) fn.call(doc, evt);
    }
  }

  function make(tag, id) {
    const el = createElement(tag, id);
    if (id) byId[id] = el;
    el.removeAttribute = (name) => {
      delete el.attributes[name];
    };
    el.replaceChildren = (...nodes) => {
      el.children = [];
      el._textContent = '';
      nodes.forEach((n) => el.appendChild(n));
    };
    Object.defineProperty(el, 'textContent', {
      get() {
        if (el._textContent !== undefined && el.children.length === 0) return el._textContent;
        if (el.children.length > 0) return el.children.map((c) => c.textContent || '').join('');
        return el._textContent || '';
      },
      set(val) {
        el._textContent = String(val);
      },
      configurable: true,
    });
    el.dispatchEvent = (evt) => dispatchWithBubble(el, evt);
    el.contains = (node) => {
      for (let current = node; current; current = current.parentNode) {
        if (current === el) return true;
      }
      return false;
    };
    el.click = () => {
      dispatchWithBubble(el, { type: 'click', target: el, preventDefault() {} });
    };
    el.closest = (sel) => {
      let cur = el;
      while (cur) {
        if (sel.includes('[data-tab]') && cur.dataset && cur.dataset.tab) return cur;
        if (sel.includes('[data-profile-tab]') && cur.dataset && cur.dataset.profileTab) return cur;
        if (sel.includes('[data-section]') && cur.dataset && cur.dataset.section) return cur;
        if (sel.includes('[data-password]') && cur.dataset && cur.dataset.password) return cur;
        if (sel.includes('.toggle-pwd') && cur.classList && cur.classList.contains('toggle-pwd')) return cur;
        if (sel.startsWith('.') && cur.classList && cur.classList.contains(sel.slice(1))) return cur;
        cur = cur.parentNode;
      }
      return null;
    };
    return el;
  }

  let profileForm;
  let deleteForm;

  const root = make('div', 'account-root');
  root.className = 'tct-theme tct-page account-ui';
  body.appendChild(root);

  // Extend doc.querySelector and doc.querySelectorAll
  const origQuerySelector = doc.querySelector.bind(doc);
  const origQuerySelectorAll = doc.querySelectorAll.bind(doc);
  doc.querySelector = (sel) => {
    if (sel === '.account-ui') return root;
    return origQuerySelector(sel);
  };
  doc.querySelectorAll = (sel) => {
    if (sel === '.account-ui form') return [profileForm, deleteForm];
    if (sel === '.account-tab-pane') return [paneProfile, paneSecurity, paneSub];
    if (sel === '.sidebar-nav-item') return [navProfile, navSecurity, navSub];
    return origQuerySelectorAll(sel);
  };

  // Shell elements
  const header = make('header', 'shell-header');
  header.className = 'header';
  body.appendChild(header);

  const footer = make('footer', 'shell-footer');
  footer.className = 'tct-footer';
  body.appendChild(footer);

  // Mobile toggle and overlay
  const mobileToggle = make('button', 'sidebarMobileToggle');
  mobileToggle.setAttribute('aria-expanded', 'false');
  root.appendChild(mobileToggle);

  const overlay = make('div', 'sidebarOverlay');
  overlay.hidden = true;
  root.appendChild(overlay);

  // Account Sidebar
  const sidebar = make('aside', 'accountSidebar');
  sidebar.className = 'account-sidebar preview-account-nav';
  root.appendChild(sidebar);

  const sidebarToggle = make('button', 'accountSidebarToggle');
  sidebarToggle.setAttribute('aria-expanded', 'true');
  const sidebarToggleIcon = make('i', 'accountSidebarToggleIcon');
  sidebarToggle.appendChild(sidebarToggleIcon);
  sidebar.appendChild(sidebarToggle);

  // Nav items
  const navProfile = make('button', 'nav-profile');
  navProfile.className = 'sidebar-nav-item preview-nav-item active';
  navProfile.setAttribute('data-tab', 'profile');
  navProfile.dataset.tab = 'profile';
  sidebar.appendChild(navProfile);

  const navSecurity = make('button', 'nav-security');
  navSecurity.className = 'sidebar-nav-item preview-nav-item';
  navSecurity.setAttribute('data-tab', 'security');
  navSecurity.dataset.tab = 'security';
  sidebar.appendChild(navSecurity);

  const navSub = make('button', 'nav-subscription');
  navSub.className = 'sidebar-nav-item preview-nav-item';
  navSub.setAttribute('data-tab', 'subscription');
  navSub.dataset.tab = 'subscription';
  sidebar.appendChild(navSub);

  // Tab Panes
  const paneProfile = make('div', 'tab-profile');
  paneProfile.className = 'account-tab-pane active';
  paneProfile.hidden = false;
  const h2Profile = make('h2', 'heading-profile');
  h2Profile.textContent = 'Thông tin cá nhân';
  paneProfile.appendChild(h2Profile);
  root.appendChild(paneProfile);

  const paneSecurity = make('div', 'tab-security');
  paneSecurity.className = 'account-tab-pane';
  paneSecurity.hidden = true;
  const h2Security = make('h2', 'heading-security');
  h2Security.textContent = 'Bảo mật & Mật khẩu';
  paneSecurity.appendChild(h2Security);
  root.appendChild(paneSecurity);

  const paneSub = make('div', 'tab-subscription');
  paneSub.className = 'account-tab-pane';
  paneSub.hidden = true;
  const h2Sub = make('h2', 'heading-subscription');
  h2Sub.textContent = 'Gói dịch vụ';
  paneSub.appendChild(h2Sub);
  root.appendChild(paneSub);

  // Profile Avatar elements
  const avatarFallback = make('div', 'avatarFallback');
  avatarFallback.textContent = 'U';
  avatarFallback.hidden = options.hasAvatar === true;
  avatarFallback.style.display = options.hasAvatar === true ? 'none' : '';
  paneProfile.appendChild(avatarFallback);

  const avatarPreview = make('img', 'avatarPreview');
  avatarPreview.hidden = options.hasAvatar !== true;
  avatarPreview.style.display = options.hasAvatar === true ? '' : 'none';
  if (options.hasAvatar) {
    avatarPreview.src = options.avatarUrl || '/uploads/avatars/saved-photo.jpg';
    avatarPreview.setAttribute('src', avatarPreview.src);
  }
  paneProfile.appendChild(avatarPreview);

  const avatarStatus = make('p', 'avatar-status');
  avatarStatus.hidden = true;
  paneProfile.appendChild(avatarStatus);

  const avatarClickTarget = make('button', 'avatarClickTarget');
  paneProfile.appendChild(avatarClickTarget);

  const fileInput = make('input', 'Avatar');
  fileInput.type = 'file';
  paneProfile.appendChild(fileInput);

  profileForm = make('form', 'profileForm');
  profileForm.action = '/Account/Profile';
  profileForm.method = 'POST';

  const fullNameInput = make('input', 'FullName');
  fullNameInput.value = 'Nguyen Van Test';
  profileForm.appendChild(fullNameInput);

  const btnCancel = make('button', 'profile-cancel');
  profileForm.appendChild(btnCancel);

  const btnProfileSave = make('button', 'profile-save');
  btnProfileSave.setAttribute('type', 'submit');
  profileForm.appendChild(btnProfileSave);

  paneProfile.appendChild(profileForm);

  // Delete modal elements
  const modalEl = make('div', 'deleteAccountModal');
  const deleteConfirmInput = make('input', 'deleteConfirmInput');
  modalEl.appendChild(deleteConfirmInput);
  deleteForm = make('form', 'deleteAccountForm');
  deleteForm.action = '/Account/DeleteAccount';
  deleteForm.method = 'POST';
  const deleteAccountBtn = make('button', 'deleteAccountBtn');
  deleteAccountBtn.setAttribute('type', 'submit');
  deleteAccountBtn.disabled = true;
  deleteForm.appendChild(deleteAccountBtn);
  modalEl.appendChild(deleteForm);
  root.appendChild(modalEl);

  return {
    doc,
    byId,
    body,
    root,
    sidebar,
    sidebarToggle,
    sidebarToggleIcon,
    mobileToggle,
    overlay,
    navProfile,
    navSecurity,
    navSub,
    paneProfile,
    paneSecurity,
    paneSub,
    h2Profile,
    h2Security,
    h2Sub,
    avatarFallback,
    avatarPreview,
    avatarStatus,
    avatarClickTarget,
    fileInput,
    fullNameInput,
    btnCancel,
    btnProfileSave,
    modalEl,
    deleteConfirmInput,
    deleteAccountBtn,
  };
}

function loadAccountScript(dom, contextOverrides = {}) {
  const windowListeners = Object.create(null);
  const storage = Object.create(null);
  const searchStr = optionsSearch(contextOverrides.queryTab);
  dom.doc.body = dom.body;
  dom.body.style = dom.body.style || {};
  dom.body.style.removeProperty = dom.body.style.removeProperty || ((p) => delete dom.body.style[p]);

  const windowObj = {
    location: {
      href: 'http://localhost/Account/Profile' + searchStr,
      search: searchStr,
      pathname: '/Account/Profile',
      replaceStateCalled: false,
      replaceState(state, title, url) {
        this.replaceStateCalled = true;
        this.lastUrl = String(url);
      },
    },
    document: dom.doc,
    matchMedia: (query) => {
      if (contextOverrides.mql) return contextOverrides.mql;
      return {
        matches: contextOverrides.isMobile === true,
        addEventListener(name, fn) {
          if (name === 'change') (windowObj._mqlListeners = windowObj._mqlListeners || []).push(fn);
        },
        removeEventListener() {},
      };
    },
    localStorage: {
      getItem: (k) => storage[k] || null,
      setItem: (k, v) => {
        storage[k] = String(v);
      },
      removeItem: (k) => {
        delete storage[k];
      },
    },
    history: {
      replaceState(state, title, url) {
        windowObj.location.replaceState(state, title, url);
      },
    },
    bootstrap: contextOverrides.bootstrap || {
      Offcanvas: {
        getOrCreateInstance: (el) => ({
          show() {
            el.classList.add('show');
            dom.overlay.classList.add('show');
            dom.overlay.hidden = false;
          },
          hide() {
            el.classList.remove('show');
            dom.overlay.classList.remove('show');
            dom.overlay.hidden = true;
          },
          dispose() {},
        }),
      },
    },
    URL,
    URLSearchParams,
    _listeners: windowListeners,
    addEventListener: (type, fn) => {
      if (!windowListeners[type]) windowListeners[type] = [];
      windowListeners[type].push(fn);
    },
    removeEventListener: (type, fn) => {
      if (!windowListeners[type]) return;
      windowListeners[type] = windowListeners[type].filter((f) => f !== fn);
    },
    console,
  };

  const sandbox = {
    window: windowObj,
    document: dom.doc,
    location: windowObj.location,
    localStorage: windowObj.localStorage,
    bootstrap: windowObj.bootstrap,
    URL,
    URLSearchParams,
    FileReader: function FileReader() {
      this.readAsDataURL = () => {
        this.result = 'data:image/png;base64,synthetic';
        if (typeof this.onload === 'function') this.onload({ target: this });
      };
    },
    setTimeout: contextOverrides.setTimeout || ((fn) => fn()),
    console,
  };

  vm.createContext(sandbox);
  const fs = require('node:fs');
  const code = fs.readFileSync(ACCOUNT_JS_PATH, 'utf8');
  vm.runInContext(code, sandbox);

  const listeners = dom.doc._listeners['DOMContentLoaded'] || [];
  listeners.forEach((fn) => fn());
  return sandbox;
}

function optionsSearch(tab) {
  if (!tab) return '';
  return `?tab=${encodeURIComponent(tab)}`;
}

test('AU05: activates tab based on URL query and deep link replaceState', () => {
  const dom = setupAccountDom();
  const sandbox = loadAccountScript(dom, { queryTab: 'security' });

  assert.equal(dom.paneProfile.hidden, true);
  assert.equal(dom.paneSecurity.hidden, false);
  assert.equal(dom.navSecurity.classList.contains('active'), true);
  assert.equal(dom.navProfile.classList.contains('active'), false);

  // Click on subscription tab
  dom.navSub.click();
  assert.equal(dom.paneSub.hidden, false);
  assert.equal(dom.paneSecurity.hidden, true);
  assert.equal(dom.navSub.classList.contains('active'), true);
  assert.equal(sandbox.window.location.replaceStateCalled, true);
});

test('AU05: desktop sidebar collapse stores state in localStorage', () => {
  const dom = setupAccountDom();
  const sandbox = loadAccountScript(dom, { isMobile: false });

  assert.equal(dom.sidebar.classList.contains('sidebar-collapsed'), false);
  dom.sidebarToggle.click();
  assert.equal(dom.sidebar.classList.contains('sidebar-collapsed'), true);
  assert.equal(sandbox.localStorage.getItem('accountSidebarCollapsed'), 'true');

  dom.sidebarToggle.click();
  assert.equal(dom.sidebar.classList.contains('sidebar-collapsed'), false);
  assert.equal(sandbox.localStorage.getItem('accountSidebarCollapsed'), 'false');
});

test('AU05: avatar upload click triggers file input and FileReader preview', () => {
  const dom = setupAccountDom();
  loadAccountScript(dom);

  let clicked = false;
  dom.fileInput.click = () => {
    clicked = true;
  };
  dom.avatarClickTarget.click();
  assert.equal(clicked, true);

  // Simulate file selection
  dom.fileInput.files = [{ type: 'image/png', size: 1024 * 100 }];
  dom.fileInput.dispatchEvent({ type: 'change', target: dom.fileInput });

  assert.equal(dom.avatarPreview.hidden, false);
  assert.equal(dom.avatarFallback.hidden, true);
  assert.equal(dom.avatarStatus.hidden, false);
  assert.match(dom.avatarStatus.textContent, /Ảnh mới chưa lưu/);

  // Cancel restores original state
  dom.btnCancel.click();
  assert.equal(dom.avatarPreview.hidden, true);
  assert.equal(dom.avatarFallback.hidden, false);
});

test('AU06: delete confirm gate only enables button when exact CONFIRM is entered', () => {
  const dom = setupAccountDom();
  loadAccountScript(dom);

  dom.deleteConfirmInput.value = 'confirm';
  dom.deleteConfirmInput.dispatchEvent({ type: 'input', target: dom.deleteConfirmInput });
  assert.equal(dom.deleteAccountBtn.disabled, true, 'Lowercase confirm must not enable button');

  dom.deleteConfirmInput.value = ' CONFIRM ';
  dom.deleteConfirmInput.dispatchEvent({ type: 'input', target: dom.deleteConfirmInput });
  assert.equal(dom.deleteAccountBtn.disabled, false, 'Trimmed CONFIRM must enable button');

  dom.deleteConfirmInput.value = 'OTHER';
  dom.deleteConfirmInput.dispatchEvent({ type: 'input', target: dom.deleteConfirmInput });
  assert.equal(dom.deleteAccountBtn.disabled, true);
});

test('R4: Cancel on profile with existing saved avatar restores original avatar, not initials', () => {
  const dom = setupAccountDom({ hasAvatar: true });
  loadAccountScript(dom);

  // Before edit: avatar is visible, fallback is hidden
  assert.equal(dom.avatarPreview.hidden, false);
  assert.equal(dom.avatarFallback.hidden, true);

  // User selects a new file to preview
  dom.fileInput.files = [{ type: 'image/png', size: 1024 * 50 }];
  dom.fileInput.dispatchEvent({ type: 'change', target: dom.fileInput });

  // Now user clicks Cancel
  dom.btnCancel.click();

  // Saved avatar must STILL be visible! Not hidden!
  assert.equal(dom.avatarPreview.hidden, false, 'Saved avatar must be visible after Cancel');
  assert.equal(dom.avatarFallback.hidden, true, 'Fallback initials must be hidden after Cancel');
  assert.match(dom.avatarPreview.src, /saved-photo\.jpg/);
});

test('R3: pageshow must NOT enable delete button if confirm input is empty', () => {
  const dom = setupAccountDom();
  const sandbox = loadAccountScript(dom);

  // Initially empty input, delete button is disabled
  assert.equal(dom.deleteConfirmInput.value, '');
  assert.equal(dom.deleteAccountBtn.disabled, true);

  // Trigger pageshow event (e.g. BFCache restore)
  const pageShowListeners = sandbox.window._listeners?.pageshow || [];
  pageShowListeners.forEach((fn) => fn());

  // Delete button MUST stay disabled because input is empty!
  assert.equal(dom.deleteAccountBtn.disabled, true, 'Delete button must NOT be enabled by pageshow when input is empty');
});

// Drive Bootstrap's separate show/shown and hide/hidden phases explicitly.
// Bootstrap, rather than the Account script, owns body scroll restoration.
function setupDrawerLifecycle() {
  const dom = setupAccountDom();
  let matches = true;
  const changes = [];
  let instance;
  const bootstrap = { Offcanvas: { getOrCreateInstance() {
    if (instance && !instance.disposed) return instance;
    let shown = false;
    let previousOverflow;
    let previousPadding;
    instance = {
      disposed: false,
      hideCalls: 0,
      show() {
        if (shown) return;
        shown = true;
        dom.sidebar.dispatchEvent({ type: 'show.bs.offcanvas' });
        previousOverflow = dom.body.style.overflow;
        previousPadding = dom.body.style['padding-right'];
        dom.body.style.overflow = 'hidden';
        dom.body.style['padding-right'] = '15px';
        dom.sidebar.setAttribute('role', 'dialog');
        dom.sidebar.setAttribute('aria-modal', 'true');
        dom.sidebar.classList.add('showing');
      },
      finishShow() {
        assert.equal(this.disposed, false, 'Do not dispose before Bootstrap finishes showing');
        dom.sidebar.classList.remove('showing');
        dom.sidebar.classList.add('show');
        dom.sidebar.dispatchEvent({ type: 'shown.bs.offcanvas' });
      },
      hide() {
        if (!shown) return;
        shown = false;
        this.hideCalls++;
        dom.sidebar.classList.add('hiding');
        dom.sidebar.dispatchEvent({ type: 'hide.bs.offcanvas' });
      },
      finishHide() {
        assert.equal(this.disposed, false, 'Do not dispose before Bootstrap finishes hiding');
        dom.sidebar.classList.remove('show', 'hiding');
        dom.sidebar.removeAttribute('role');
        dom.sidebar.removeAttribute('aria-modal');
        dom.body.style.overflow = previousOverflow;
        dom.body.style['padding-right'] = previousPadding;
        dom.sidebar.dispatchEvent({ type: 'hidden.bs.offcanvas' });
      },
      dispose() { this.disposed = true; },
    };
    return instance;
  } } };
  const sandbox = loadAccountScript(dom, {
    bootstrap,
    setTimeout: () => {},
    mql: {
      get matches() { return matches; },
      addEventListener(name, fn) { if (name === 'change') changes.push(fn); },
    },
  });
  return {
    dom, sandbox,
    get instance() { return instance; },
    resize(mobile) { matches = mobile; changes.forEach((fn) => fn({ matches })); },
  };
}

test('R2: resize waits for Bootstrap hidden before disposing and restores desktop navigation', () => {
  const drawer = setupDrawerLifecycle();
  drawer.sandbox.localStorage.setItem('accountSidebarCollapsed', 'true');
  drawer.dom.mobileToggle.click();
  drawer.instance.finishShow();
  drawer.resize(false);
  assert.equal(drawer.instance.hideCalls, 1);
  assert.equal(drawer.instance.disposed, false);
  assert.equal(drawer.dom.body.style.overflow, 'hidden');
  drawer.instance.finishHide();
  assert.equal(drawer.instance.disposed, true);
  assert.equal(drawer.dom.body.style.overflow, undefined);
  assert.equal(drawer.dom.sidebar.getAttribute('aria-modal'), null);
  assert.equal(drawer.dom.sidebar.classList.contains('offcanvas'), false);
  assert.equal(drawer.dom.sidebar.classList.contains('sidebar-collapsed'), true);
});

test('drawer desktop cleanup preserves another modal scroll lock and pre-existing inert state', () => {
  const dom = setupAccountDom();
  dom.body.classList.add('modal-open');
  dom.body.style.overflow = 'hidden';
  dom.body.style['padding-right'] = '17px';
  dom.byId['shell-header'].inert = true;
  const backdrop = createElement('div');
  backdrop.className = 'offcanvas-backdrop';
  dom.body.appendChild(backdrop);
  loadAccountScript(dom, { isMobile: false });
  assert.equal(dom.body.style.overflow, 'hidden');
  assert.equal(dom.body.style['padding-right'], '17px');
  assert.equal(dom.body.classList.contains('modal-open'), true);
  assert.equal(dom.byId['shell-header'].inert, true);
  assert.equal(backdrop.parentNode, dom.body, 'Do not remove another offcanvas backdrop');
});

test('mobile-desktop-mobile during hide retains a usable Bootstrap drawer', () => {
  const drawer = setupDrawerLifecycle();
  drawer.dom.byId['shell-header'].inert = true;
  drawer.dom.mobileToggle.click();
  drawer.instance.finishShow();
  drawer.resize(false);
  drawer.resize(true);
  drawer.instance.finishHide();
  assert.equal(drawer.instance.disposed, false);
  assert.equal(drawer.dom.sidebar.classList.contains('offcanvas'), true);
  assert.equal(drawer.dom.sidebar.inert, true);
  assert.equal(drawer.dom.byId['shell-header'].inert, true);
  drawer.dom.mobileToggle.click();
  drawer.instance.finishShow();
  assert.equal(drawer.dom.sidebar.getAttribute('aria-modal'), 'true');
  assert.equal(drawer.dom.body.style.overflow, 'hidden');
});

test('resize to desktop during opening finishes show before starting hide', () => {
  const drawer = setupDrawerLifecycle();
  drawer.dom.mobileToggle.click();
  drawer.resize(false);
  assert.equal(drawer.instance.hideCalls, 0);
  drawer.instance.finishShow();
  assert.equal(drawer.instance.hideCalls, 1);
  drawer.instance.finishHide();
  assert.equal(drawer.instance.disposed, true);
  assert.equal(drawer.dom.sidebar.classList.contains('show'), false);
});

test('overlay close during opening finishes show before starting hide', () => {
  const drawer = setupDrawerLifecycle();
  drawer.dom.mobileToggle.click();
  drawer.dom.overlay.click();
  assert.equal(drawer.instance.hideCalls, 0);
  drawer.instance.finishShow();
  assert.equal(drawer.instance.hideCalls, 1);
  drawer.instance.finishHide();
  assert.equal(drawer.dom.sidebar.inert, true);
  assert.equal(drawer.dom.overlay.hidden, true);
});

test('Account native submit cancelled by validation remains retryable', async () => {
  const dom = setupAccountDom();
  loadAccountScript(dom);
  const form = dom.btnProfileSave.parentNode;
  const cancelled = { type: 'submit', defaultPrevented: true, preventDefault() { this.defaultPrevented = true; } };
  form.dispatchEvent(cancelled);
  assert.equal(dom.btnProfileSave.disabled, false);
  assert.notEqual(form._inFlight, true);

  const lateCancelled = { type: 'submit', defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  form.addEventListener('submit', (event) => event.preventDefault());
  form.dispatchEvent(lateCancelled);
  await Promise.resolve();
  assert.equal(dom.btnProfileSave.disabled, false);
  assert.equal(dom.btnProfileSave.getAttribute('aria-busy'), null);
  assert.equal(form._inFlight, false);
});

test('Account native submit allows first request and rejects concurrent duplicate', () => {
  const dom = setupAccountDom();
  loadAccountScript(dom);
  const form = dom.btnProfileSave.parentNode;
  const first = { type: 'submit', defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  form.dispatchEvent(first);
  assert.equal(first.defaultPrevented, false);
  assert.equal(dom.btnProfileSave.disabled, true);
  const duplicate = { type: 'submit', defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  form.dispatchEvent(duplicate);
  assert.equal(duplicate.defaultPrevented, true);
});
