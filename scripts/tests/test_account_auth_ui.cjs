'use strict';

/**
 * Behavior tests for Account Auth & Recovery UI (AU01-AU04).
 * Executes production TCTEnglish/wwwroot/js/account-auth-ui.js in a Node harness
 * with fake DOM/fetch using scripts/tests/dom_fixture.cjs.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const AUTH_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/account-auth-ui.js');

function setupAuthDom(options = {}) {
  const { document: doc, byId, body } = createDocument([]);

  function dispatchWithBubble(target, evt) {
    evt.target = target;
    let cur = target;
    while (cur) {
      if (cur._listeners && cur._listeners[evt.type]) {
        for (const fn of cur._listeners[evt.type]) fn(evt);
      }
      cur = cur.parentNode;
    }
    if (doc._listeners && doc._listeners[evt.type]) {
      for (const fn of doc._listeners[evt.type]) fn(evt);
    }
  }

  function make(tag, id) {
    const el = createElement(tag, id);
    if (id) byId[id] = el;
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
    el.removeAttribute = (name) => {
      delete el.attributes[name];
    };
    el.dispatchEvent = (evt) => dispatchWithBubble(el, evt);
    el.click = () => {
      dispatchWithBubble(el, { type: 'click', target: el, preventDefault() {} });
    };
    el.closest = (sel) => {
      let cur = el;
      while (cur) {
        if (sel === '[data-password]' && cur.dataset && cur.dataset.password) return cur;
        if (sel === '[data-auth-tab]' && cur.dataset && cur.dataset.authTab) return cur;
        if (sel.startsWith('.') && cur.classList && cur.classList.contains(sel.slice(1))) return cur;
        cur = cur.parentNode;
      }
      return null;
    };
    return el;
  }

  const root = make('div', 'auth-root');
  root.className = 'tct-theme account-auth-ui';
  body.appendChild(root);

  // Extend doc.querySelector to find .account-auth-ui
  const origQuerySelector = doc.querySelector.bind(doc);
  doc.querySelector = (sel) => {
    if (sel === '.account-auth-ui') return root;
    return origQuerySelector(sel);
  };

  // Auth Heading & Description
  const heading = make('h1', 'auth-heading');
  heading.textContent = 'Đăng nhập';
  root.appendChild(heading);

  const desc = make('p', 'auth-description');
  desc.textContent = 'Tiếp tục học với TCT English.';
  root.appendChild(desc);

  // Tabs
  const tablist = make('div', 'auth-tabs');
  tablist.setAttribute('role', 'tablist');

  const tabLogin = make('button', 'tab-login');
  tabLogin.setAttribute('role', 'tab');
  tabLogin.setAttribute('aria-selected', 'true');
  tabLogin.setAttribute('aria-controls', 'form-login');
  tabLogin.tabIndex = 0;
  tablist.appendChild(tabLogin);

  const tabRegister = make('button', 'tab-register');
  tabRegister.setAttribute('role', 'tab');
  tabRegister.setAttribute('aria-selected', 'false');
  tabRegister.setAttribute('aria-controls', 'form-register');
  tabRegister.tabIndex = -1;
  tablist.appendChild(tabRegister);
  root.appendChild(tablist);

  // Alerts
  const authSuccess = make('div', 'auth-success');
  authSuccess.hidden = true;
  root.appendChild(authSuccess);

  const authServerError = make('div', 'auth-server-error');
  authServerError.hidden = true;
  root.appendChild(authServerError);

  const loginError = make('div', 'login-error-msg');
  loginError.hidden = true;
  root.appendChild(loginError);

  const blockedAlert = make('div', 'blocked-alert');
  blockedAlert.hidden = true;
  root.appendChild(blockedAlert);

  // Login Form
  const formLogin = make('form', 'form-login');
  formLogin.action = '/Account/Login';
  formLogin.method = 'POST';
  formLogin.hidden = false;
  formLogin.dataset.homeUrl = '/Home/Index';

  const loginEmail = make('input', 'login-email');
  loginEmail.type = 'email';
  loginEmail.value = options.loginEmail || 'test@example.com';
  formLogin.appendChild(loginEmail);

  const loginPassword = make('input', 'login-password');
  loginPassword.type = 'password';
  loginPassword.value = options.loginPassword || 'Password123';
  formLogin.appendChild(loginPassword);

  const loginToggle = make('button', 'login-password-toggle');
  loginToggle.setAttribute('data-password', 'login-password');
  loginToggle.dataset.password = 'login-password';
  loginToggle.setAttribute('aria-label', 'Hiện mật khẩu đăng nhập');
  loginToggle.setAttribute('aria-pressed', 'false');
  const iconLogin = make('i', '');
  iconLogin.className = 'fa-solid fa-eye';
  loginToggle.appendChild(iconLogin);
  formLogin.appendChild(loginToggle);

  const btnLoginSubmit = make('button', 'btn-login-submit');
  btnLoginSubmit.type = 'submit';
  btnLoginSubmit.textContent = 'Đăng nhập';
  formLogin.appendChild(btnLoginSubmit);
  root.appendChild(formLogin);

  // Register Form
  const formRegister = make('form', 'form-register');
  formRegister.action = '/Account/Register';
  formRegister.method = 'POST';
  formRegister.hidden = true;

  const regEmail = make('input', 'register-email');
  regEmail.type = 'email';
  regEmail.value = options.registerEmail || 'new@example.com';
  formRegister.appendChild(regEmail);

  const regName = make('input', 'register-name');
  regName.type = 'text';
  regName.value = options.registerName || 'New User';
  formRegister.appendChild(regName);

  const regPassword = make('input', 'register-password');
  regPassword.type = 'password';
  regPassword.value = options.registerPassword || 'Secret123';
  formRegister.appendChild(regPassword);

  const regToggle = make('button', 'register-password-toggle');
  regToggle.setAttribute('data-password', 'register-password');
  regToggle.dataset.password = 'register-password';
  regToggle.setAttribute('aria-label', 'Hiện mật khẩu đăng ký');
  regToggle.setAttribute('aria-pressed', 'false');
  const iconReg = make('i', '');
  iconReg.className = 'fa-solid fa-eye';
  regToggle.appendChild(iconReg);
  formRegister.appendChild(regToggle);

  const terms = make('input', 'terms');
  terms.type = 'checkbox';
  terms.checked = options.termsChecked !== false;
  formRegister.appendChild(terms);

  const btnRegSubmit = make('button', 'btn-register-submit');
  btnRegSubmit.type = 'submit';
  btnRegSubmit.textContent = 'Đăng ký';
  formRegister.appendChild(btnRegSubmit);
  root.appendChild(formRegister);

  // Recovery - Forgot Password Form (AU02)
  const formForgot = make('form', 'form-forgot-password');
  formForgot.action = '/Account/ForgotPassword';
  formForgot.method = 'POST';
  const forgotEmail = make('input', 'Email');
  forgotEmail.type = 'email';
  forgotEmail.value = 'forgot@example.com';
  formForgot.appendChild(forgotEmail);
  const btnForgotSubmit = make('button', 'btn-forgot-submit');
  btnForgotSubmit.type = 'submit';
  btnForgotSubmit.textContent = 'Gửi liên kết khôi phục';
  formForgot.appendChild(btnForgotSubmit);
  root.appendChild(formForgot);

  // Recovery - Reset Password Form (AU04)
  const formReset = make('form', 'form-reset-password');
  formReset.action = '/Account/ResetPassword';
  formReset.method = 'POST';
  const resetToken = make('input', 'Token');
  resetToken.type = 'hidden';
  resetToken.value = 'valid-token';
  formReset.appendChild(resetToken);
  const resetEmail = make('input', 'Email');
  resetEmail.type = 'hidden';
  resetEmail.value = 'reset@example.com';
  formReset.appendChild(resetEmail);
  const resetNewPwd = make('input', 'NewPassword');
  resetNewPwd.type = 'password';
  resetNewPwd.value = options.newPassword || 'newpass123';
  formReset.appendChild(resetNewPwd);
  const resetConfirmPwd = make('input', 'ConfirmPassword');
  resetConfirmPwd.type = 'password';
  resetConfirmPwd.value = options.confirmPassword || 'newpass123';
  formReset.appendChild(resetConfirmPwd);
  const resetMismatch = make('p', 'reset-mismatch');
  resetMismatch.hidden = true;
  formReset.appendChild(resetMismatch);
  const btnResetSubmit = make('button', 'btn-reset-submit');
  btnResetSubmit.type = 'submit';
  btnResetSubmit.textContent = 'Xác nhận đặt lại mật khẩu';
  formReset.appendChild(btnResetSubmit);
  root.appendChild(formReset);

  return {
    doc,
    byId,
    root,
    tabLogin,
    tabRegister,
    formLogin,
    formRegister,
    btnLoginSubmit,
    btnRegSubmit,
    terms,
    loginEmail,
    loginPassword,
    loginToggle,
    regToggle,
    heading,
    desc,
    loginError,
    formForgot,
    btnForgotSubmit,
    formReset,
    btnResetSubmit,
    resetNewPwd,
    resetConfirmPwd,
    resetMismatch,
  };
}

function loadAuthScript(dom, contextOverrides = {}) {
  const windowListeners = Object.create(null);
  const windowObj = {
    location: { href: 'http://localhost/Account/Login', reloadCalled: false, reload() { this.reloadCalled = true; } },
    document: dom.doc,
    matchMedia: () => ({ matches: false }),
    history: { back() {} },
    _listeners: windowListeners,
    addEventListener: (type, fn) => {
      if (!windowListeners[type]) windowListeners[type] = [];
      windowListeners[type].push(fn);
    },
    removeEventListener: (type, fn) => {
      if (!windowListeners[type]) return;
      windowListeners[type] = windowListeners[type].filter((f) => f !== fn);
    },
    FormData: function FormData(form) {
      this._form = form;
      this.entries = () => [];
    },
    ...contextOverrides,
  };

  const sandbox = {
    window: windowObj,
    document: dom.doc,
    location: windowObj.location,
    fetch: contextOverrides.fetch || (() => Promise.resolve({ ok: true, json: () => Promise.resolve({ success: true }) })),
    FormData: windowObj.FormData,
    setTimeout: (fn) => fn(),
    console,
  };

  vm.createContext(sandbox);
  const fs = require('node:fs');
  const code = fs.readFileSync(AUTH_JS_PATH, 'utf8');
  vm.runInContext(code, sandbox);

  // Dispatch DOMContentLoaded
  const listeners = dom.doc._listeners['DOMContentLoaded'] || [];
  listeners.forEach((fn) => fn());
  return sandbox;
}

test('Auth native forms remain retryable when another validator cancels submission', async () => {
  for (const [formKey, buttonKey] of [
    ['formRegister', 'btnRegSubmit'],
    ['formForgot', 'btnForgotSubmit'],
    ['formReset', 'btnResetSubmit'],
  ]) {
    const dom = setupAuthDom();
    dom.terms.checked = true;
    dom.resetNewPwd.value = dom.resetConfirmPwd.value = 'fixture-password';
    loadAuthScript(dom);
    const form = dom[formKey];
    form.dispatchEvent({ type: 'submit', defaultPrevented: true, preventDefault() { this.defaultPrevented = true; } });
    assert.equal(dom[buttonKey].disabled, false, formKey + ' was already cancelled');
    assert.notEqual(form._inFlight, true);
    form.addEventListener('submit', (event) => event.preventDefault());
    form.dispatchEvent({ type: 'submit', defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } });
    await Promise.resolve();
    assert.equal(dom[buttonKey].disabled, false, formKey + ' was cancelled later');
    assert.equal(dom[buttonKey].getAttribute('aria-busy'), null);
    assert.equal(form._inFlight, false);
  }
});

test('AJAX login respects a submit event already cancelled by another validator', () => {
  const dom = setupAuthDom();
  let requests = 0;
  loadAuthScript(dom, { fetch: () => { requests++; return new Promise(() => {}); } });
  dom.formLogin.dispatchEvent({ type: 'submit', defaultPrevented: true, preventDefault() { this.defaultPrevented = true; } });
  assert.equal(requests, 0);
  assert.equal(dom.btnLoginSubmit.disabled, false);
});

test('AU01: switchTab updates aria-selected, tabindex, visibility and headings', () => {
  const dom = setupAuthDom();
  loadAuthScript(dom);

  assert.equal(dom.tabLogin.getAttribute('aria-selected'), 'true');
  assert.equal(dom.tabRegister.getAttribute('aria-selected'), 'false');
  assert.equal(dom.formLogin.hidden, false);
  assert.equal(dom.formRegister.hidden, true);

  // Switch to register
  dom.tabRegister.click();
  assert.equal(dom.tabLogin.getAttribute('aria-selected'), 'false');
  assert.equal(dom.tabRegister.getAttribute('aria-selected'), 'true');
  assert.equal(dom.tabRegister.tabIndex, 0);
  assert.equal(dom.tabLogin.tabIndex, -1);
  assert.equal(dom.formLogin.hidden, true);
  assert.equal(dom.formRegister.hidden, false);
  assert.equal(dom.heading.textContent, 'Tạo tài khoản');
  assert.equal(dom.desc.textContent, 'Tạo tài khoản để bắt đầu học.');

  // ArrowLeft switches back to login
  dom.tabRegister.dispatchEvent({ type: 'keydown', key: 'ArrowLeft', preventDefault() {} });
  assert.equal(dom.tabLogin.getAttribute('aria-selected'), 'true');
  assert.equal(dom.formLogin.hidden, false);
  assert.equal(dom.heading.textContent, 'Đăng nhập');
});

test('AU01: password toggle flips input type, aria-label and aria-pressed', () => {
  const dom = setupAuthDom();
  loadAuthScript(dom);

  assert.equal(dom.loginPassword.type, 'password');
  assert.equal(dom.loginToggle.getAttribute('aria-pressed'), 'false');

  dom.loginToggle.click();
  assert.equal(dom.loginPassword.type, 'text');
  assert.equal(dom.loginToggle.getAttribute('aria-pressed'), 'true');
  assert.equal(dom.loginToggle.getAttribute('aria-label'), 'Ẩn mật khẩu đăng nhập');

  dom.loginToggle.click();
  assert.equal(dom.loginPassword.type, 'password');
  assert.equal(dom.loginToggle.getAttribute('aria-pressed'), 'false');
  assert.equal(dom.loginToggle.getAttribute('aria-label'), 'Hiện mật khẩu đăng nhập');
});

test('AU01: handleLogin sends single request during in-flight submission and navigates on success', async () => {
  const dom = setupAuthDom();
  let fetchCount = 0;
  let resolveFetch;
  const fetchPromise = new Promise((resolve) => {
    resolveFetch = resolve;
  });

  const sandbox = loadAuthScript(dom, {
    fetch: () => {
      fetchCount++;
      return fetchPromise;
    },
  });

  // First submit
  dom.formLogin.dispatchEvent({ type: 'submit', target: dom.formLogin, preventDefault() {} });
  assert.equal(fetchCount, 1);
  assert.equal(dom.btnLoginSubmit.disabled, true);
  assert.equal(dom.btnLoginSubmit.getAttribute('aria-busy'), 'true');

  // Second submit attempt while in flight
  dom.formLogin.dispatchEvent({ type: 'submit', target: dom.formLogin, preventDefault() {} });
  assert.equal(fetchCount, 1, 'Duplicate submit while in-flight must be ignored');

  // Resolve fetch with success
  resolveFetch({
    ok: true,
    json: () => Promise.resolve({ success: true }),
  });

  await delay(10);
  assert.equal(sandbox.window.location.href, '/Home/Index');
});

test('AU01: handleLogin safely displays server error message without HTML injection', async () => {
  const dom = setupAuthDom();
  const evilMessage = '<img src=x onerror=alert(1)> Sai mật khẩu';

  loadAuthScript(dom, {
    fetch: () =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve({ success: false, message: evilMessage }),
      }),
  });

  dom.formLogin.dispatchEvent({ type: 'submit', target: dom.formLogin, preventDefault() {} });
  await delay(10);

  assert.equal(dom.btnLoginSubmit.disabled, false);
  assert.equal(dom.loginError.hidden, false);
  assert.equal(dom.loginError.innerHTML.includes('<img'), false);
  assert.match(dom.loginError.textContent, /Sai mật khẩu/);
});

test('AU01: handleRegister blocks submit when terms unchecked and restores on pageshow', () => {
  const dom = setupAuthDom({ termsChecked: false });
  let alertShown = false;
  const sandbox = loadAuthScript(dom, {
    Swal: {
      fire() {
        alertShown = true;
      },
    },
  });

  let submitted = false;
  dom.formRegister.dispatchEvent({
    type: 'submit',
    target: dom.formRegister,
    preventDefault() {
      submitted = false;
    },
  });
  assert.equal(alertShown || dom.btnRegSubmit.disabled === false, true);

  // Check terms, submit should set pending
  dom.terms.checked = true;
  let defaultPrevented = false;
  dom.formRegister.dispatchEvent({
    type: 'submit',
    target: dom.formRegister,
    preventDefault() {
      defaultPrevented = true;
    },
  });
  assert.equal(defaultPrevented, false, 'Native submission must not be prevented');
  assert.equal(dom.btnRegSubmit.disabled, true);
  assert.equal(dom.btnRegSubmit.getAttribute('aria-busy'), 'true');

  // pageshow recovers button
  const pageShowListeners = sandbox.window._listeners?.pageshow || [];
  pageShowListeners.forEach((fn) => fn());
  assert.equal(dom.btnRegSubmit.disabled, false);
});

test('AU04: handleResetSubmit detects password mismatch', () => {
  const dom = setupAuthDom({
    newPassword: 'passWord123',
    confirmPassword: 'differentPassword',
  });
  loadAuthScript(dom);

  let defaultPrevented = false;
  dom.formReset.dispatchEvent({
    type: 'submit',
    target: dom.formReset,
    preventDefault() {
      defaultPrevented = true;
    },
  });

  assert.equal(defaultPrevented, true, 'Mismatch must prevent submission');
  assert.equal(dom.resetMismatch.hidden, false);
  assert.equal(dom.btnResetSubmit.disabled, false);
});

test('AU02: handleForgotSubmit allows first valid submit and blocks concurrent duplicate submit', () => {
  const dom = setupAuthDom();
  const sandbox = loadAuthScript(dom);

  let firstPrevented = false;
  const evt1 = {
    type: 'submit',
    target: dom.formForgot,
    preventDefault() {
      firstPrevented = true;
    },
  };

  dom.formForgot.dispatchEvent(evt1);
  assert.equal(firstPrevented, false, 'First valid Forgot submit must NOT be prevented');
  assert.equal(dom.btnForgotSubmit.disabled, true, 'Button should become disabled');

  // Second submit during flight must be prevented
  let secondPrevented = false;
  const evt2 = {
    type: 'submit',
    target: dom.formForgot,
    preventDefault() {
      secondPrevented = true;
    },
  };
  dom.formForgot.dispatchEvent(evt2);
  assert.equal(secondPrevented, true, 'Second submit during in-flight must be prevented');
});

test('R1: duplicate submission ownership (inline onsubmit + listener) must NOT cancel the first valid submit', () => {
  const dom = setupAuthDom();
  const sandbox = loadAuthScript(dom);

  // Simulate what happens in browser when inline onsubmit="handleForgotSubmit(event)" exists
  // AND the addEventListener('submit', handleForgotSubmit) also runs
  let defaultPrevented = false;
  const evt = {
    type: 'submit',
    target: dom.formForgot,
    preventDefault() {
      defaultPrevented = true;
    },
  };

  // Inline handler executes first
  sandbox.window.handleForgotSubmit(evt);
  // Then the event listener executes on the same event
  dom.formForgot.dispatchEvent(evt);

  assert.equal(defaultPrevented, false, 'First submit must not be canceled by duplicate handler invocation on same event');
});
