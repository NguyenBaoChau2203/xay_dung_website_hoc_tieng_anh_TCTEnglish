'use strict';

/**
 * Behavior regression for AI chat double-submit guard (Plan1 R1 / Plan2 F2).
 * Executes production wwwroot/js/ai-chat.js in a Node harness with fake DOM/fetch.
 * Does not re-implement the guard algorithm in the test.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const AI_CHAT_JS = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/ai-chat.js');
const SEND_URL = '/AI/SendMessage';
const OBS_URL = '/AI/Observability';

function enhanceElement(el) {
  if (el.__aiChatEnhanced) return el;
  el.__aiChatEnhanced = true;

  const originalSetAttribute = el.setAttribute.bind(el);
  el.setAttribute = (name, value) => {
    originalSetAttribute(name, value);
    if (String(name).startsWith('data-')) {
      const dataKey = String(name)
        .slice(5)
        .replace(/-([a-z])/g, (_, c) => c.toUpperCase());
      el.dataset[dataKey] = String(value);
    }
  };

  Object.defineProperty(el, 'parentElement', {
    get() {
      return el.parentNode;
    },
    configurable: true,
  });

  el.closest = (selector) => {
    let node = el;
    while (node) {
      if (matchesSelector(node, selector)) return node;
      node = node.parentNode;
    }
    return null;
  };

  el.focus = () => {
    el._focused = true;
  };
  if (!el.removeChild) el.removeChild = (child) => {
    const index = el.children.indexOf(child);
    if (index < 0) throw new Error('DOM child not found');
    el.children.splice(index, 1); child.parentNode = null; return child;
  };

  if (el.tagName === 'FORM') {
    el.requestSubmit = () => {
      const evt = {
        type: 'submit',
        target: el,
        preventDefault() {
          this.defaultPrevented = true;
        },
        defaultPrevented: false,
      };
      el.dispatchEvent(evt);
    };
  }

  return el;
}

function matchesSelector(el, selector) {
  if (!el || !selector) return false;
  if (selector.startsWith('#')) return el.id === selector.slice(1);
  if (selector.startsWith('.')) {
    return selector
      .slice(1)
      .split('.')
      .filter(Boolean)
      .every((p) => el.classList.contains(p));
  }
  if (selector.startsWith('[') && selector.endsWith(']')) {
    const inner = selector.slice(1, -1);
    const eq = inner.indexOf('=');
    if (eq === -1) {
      return el.getAttribute(inner) != null || Object.prototype.hasOwnProperty.call(el.dataset, toDatasetKey(inner));
    }
    const key = inner.slice(0, eq);
    const raw = inner.slice(eq + 1).replace(/^["']|["']$/g, '');
    if (key.startsWith('data-')) {
      return String(el.dataset[toDatasetKey(key)] ?? el.getAttribute(key)) === raw;
    }
    return el.getAttribute(key) === raw;
  }
  if (/^[a-z0-9-]+$/i.test(selector)) return el.tagName === selector.toUpperCase();
  return false;
}

function toDatasetKey(attrName) {
  const name = attrName.startsWith('data-') ? attrName.slice(5) : attrName;
  return name.replace(/-([a-z])/g, (_, c) => c.toUpperCase());
}

function countRoleBubbles(chatWindow, role) {
  return (chatWindow.children || []).filter(
    (child) => child.getAttribute('data-role') === role
  ).length;
}

function buildChatDom() {
  const ids = [
    'chatForm',
    'messageInput',
    'chatWindow',
    'scrollToBottomBtn',
    'typing',
    'sendBtn',
    'stopBtn',
    'conversationIdInput',
    'conversationMetaValue',
    'conversationMetaStatus',
    'chatStatus',
    'aiMetricRequestCount',
    'aiMetricErrorRate',
    'aiMetricLatency',
    'aiMetricTokenToday',
  ];

  const { document, byId, body } = createDocument(ids);

  // Retag form / input / buttons with realistic element shapes.
  const form = createElement('form', 'chatForm');
  enhanceElement(form);
  form.dataset.state = 'idle';
  byId.chatForm = form;

  const shell = createElement('div', 'chatShell');
  enhanceElement(shell);
  shell.setAttribute('data-ai-chat-shell', 'true');
  shell.dataset.userInitial = 'T';
  shell.dataset.aiAvatar = '/images/ai/tct-ai-mark.svg';

  const quickActions = createElement('div', 'quickActions');
  enhanceElement(quickActions);
  quickActions.setAttribute('data-ai-quick-actions', 'true');
  const quickBtn = createElement('button', 'quickBtn');
  enhanceElement(quickBtn);
  quickBtn.setAttribute('data-ai-quick-action', 'true');
  quickBtn.setAttribute('data-question', 'Quick pending question');
  quickBtn.textContent = 'Quick pending question';
  quickActions.appendChild(quickBtn);
  shell.appendChild(quickActions);

  Object.assign(byId.messageInput, {
    tagName: 'TEXTAREA',
    value: '',
    scrollHeight: 46,
  });
  enhanceElement(byId.messageInput);

  enhanceElement(byId.chatWindow);
  byId.chatWindow.scrollTop = 0;
  byId.chatWindow.scrollHeight = 100;
  byId.chatWindow.clientHeight = 100;

  enhanceElement(byId.scrollToBottomBtn);
  byId.scrollToBottomBtn.classList.add('d-none');

  enhanceElement(byId.typing);
  byId.typing.classList.add('d-none');

  enhanceElement(byId.sendBtn);
  byId.sendBtn.disabled = false;
  byId.sendBtn.textContent = 'Gửi';

  enhanceElement(byId.stopBtn);
  byId.stopBtn.disabled = true;
  byId.stopBtn.classList.add('d-none');

  enhanceElement(byId.conversationIdInput);
  byId.conversationIdInput.value = '';
  byId.conversationIdInput.tagName = 'INPUT';

  [
    'conversationMetaValue',
    'conversationMetaStatus',
    'chatStatus',
    'aiMetricRequestCount',
    'aiMetricErrorRate',
    'aiMetricLatency',
    'aiMetricTokenToday',
  ].forEach((id) => enhanceElement(byId[id]));

  byId.conversationMetaValue.textContent = 'Đoạn chat mới';
  byId.chatStatus.classList.add('d-none');

  const csrf = createElement('input', '');
  enhanceElement(csrf);
  csrf.name = '__RequestVerificationToken';
  csrf.value = 'test-csrf';
  csrf.tagName = 'INPUT';
  form.appendChild(csrf);

  // Mount tree: shell → form + window controls as siblings under shell for closest().
  shell.appendChild(form);
  // Keep id lookups pointing at live nodes; re-parent the remaining controls under form/shell.
  [
    'messageInput',
    'sendBtn',
    'stopBtn',
    'conversationIdInput',
    'conversationMetaValue',
    'conversationMetaStatus',
    'chatStatus',
    'aiMetricRequestCount',
    'aiMetricErrorRate',
    'aiMetricLatency',
    'aiMetricTokenToday',
  ].forEach((id) => {
    if (byId[id].parentNode) {
      byId[id].parentNode.children = byId[id].parentNode.children.filter((c) => c !== byId[id]);
    }
    form.appendChild(byId[id]);
  });

  ['chatWindow', 'scrollToBottomBtn', 'typing'].forEach((id) => {
    if (byId[id].parentNode) {
      byId[id].parentNode.children = byId[id].parentNode.children.filter((c) => c !== byId[id]);
    }
    shell.appendChild(byId[id]);
  });

  // Replace any prior body children that used the same ids.
  body.children = body.children.filter((c) => !ids.includes(c.id) && c.id !== '');
  body.appendChild(shell);

  document.getElementById = (id) => byId[id] || null;
  const originalQuerySelector = document.querySelector.bind(document);
  document.querySelector = (sel) => {
    if (sel === '[data-ai-chat-shell]') return shell;
    if (sel === '[data-ai-quick-actions]') return quickActions;
    if (sel === '.ai-chat-shell') return shell;
    if (sel === 'input[name="__RequestVerificationToken"]') return csrf;
    if (sel.startsWith('#')) return byId[sel.slice(1)] || null;
    return originalQuerySelector(sel);
  };

  form.querySelector = (sel) => {
    if (sel === 'input[name="__RequestVerificationToken"]') return csrf;
    return null;
  };

  const createLiveElement = document.createElement.bind(document);
  document.createElement = (tag) => enhanceElement(createLiveElement(tag));

  return { document, byId, body, form, shell, quickBtn, quickActions };
}

function createPendingTransport() {
  const sendCalls = [];
  const observabilityCalls = [];
  /** @type {Array<{mode: 'success'|'error', resolve: Function}>} */
  const pending = [];

  function enqueueBarrier() {
    let resolve;
    const promise = new Promise((res) => {
      resolve = res;
    });
    const entry = { mode: 'pending', resolve, promise };
    pending.push(entry);
    return entry;
  }

  const fetchImpl = async (url, options = {}) => {
    const u = String(url);
    if (u.includes(OBS_URL) || u.includes('Observability')) {
      observabilityCalls.push({ url: u, options });
      return {
        ok: true,
        status: 200,
        headers: { get: () => 'application/json' },
        json: async () => ({
          requestCount: 0,
          errorRate: 0,
          latencyMs: 0,
          tokensUsedToday: 0,
          tokenBudget: 0,
        }),
      };
    }

    const entry = enqueueBarrier();
    sendCalls.push({
      url: u,
      body: options.body ? JSON.parse(options.body) : null,
      options,
    });

    await entry.promise;

    if (entry.mode === 'error') {
      return {
        ok: false,
        status: 503,
        headers: { get: () => 'text/plain' },
        json: async () => ({ error: 'busy' }),
      };
    }

    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({
        text: 'assistant reply',
        conversationId: 42,
        conversationTitle: 'Assistant reply title',
      }),
    };
  };

  function settleOldest(mode) {
    const entry = pending.find((p) => p.mode === 'pending');
    assert.ok(entry, 'expected a pending send to settle');
    entry.mode = mode;
    entry.resolve();
  }

  return {
    fetchImpl,
    sendCalls,
    observabilityCalls,
    resolveSuccess() {
      settleOldest('success');
    },
    resolveError() {
      settleOldest('error');
    },
  };
}

function loadAiChat({ document, fetchImpl, sourceText, connection }) {
  const listeners = Object.create(null);
  const windowObj = {
    document,
    fetch: fetchImpl,
    signalR: connection ? { HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' } } : undefined,
    userActivityHubConnection: connection,
    bootstrap: undefined,
    marked: undefined,
    DOMPurify: undefined,
    innerWidth: 1280,
    top: null,
    self: null,
    location: { href: 'http://localhost/AI/Chat', origin: 'http://localhost' },
    history: { state: null, replaceState() {} },
    setTimeout,
    clearTimeout,
    requestAnimationFrame(cb) {
      return setTimeout(cb, 0);
    },
    addEventListener(type, fn) {
      if (!listeners[type]) listeners[type] = [];
      listeners[type].push(fn);
    },
    removeEventListener(type, fn) {
      if (!listeners[type]) return;
      listeners[type] = listeners[type].filter((f) => f !== fn);
    },
    parent: { postMessage() {} },
  };
  windowObj.top = windowObj;
  windowObj.self = windowObj;
  windowObj.window = windowObj;

  const sandbox = {
    window: windowObj,
    signalR: windowObj.signalR,
    document,
    console,
    setTimeout,
    clearTimeout,
    fetch: fetchImpl,
    URL,
    Event: function Event(type, init) {
      return {
        type,
        bubbles: !!(init && init.bubbles),
        cancelable: !!(init && init.cancelable),
        preventDefault() {
          this.defaultPrevented = true;
        },
        defaultPrevented: false,
      };
    },
  };
  sandbox.globalThis = sandbox;
  sandbox.self = windowObj;

  const code = sourceText || fs.readFileSync(AI_CHAT_JS, 'utf8');
  vm.runInNewContext(code, sandbox, { filename: AI_CHAT_JS });
  assert.ok(windowObj.tctAiChat && typeof windowObj.tctAiChat.init === 'function');

  windowObj.tctAiChat.init({
    sendUrl: SEND_URL,
    observabilityUrl: OBS_URL,
    hubUrl: '/classChatHub',
    deleteUrl: '/AI/DeleteConversation',
  });

  return { windowObj };
}

function pressEnter(input, { shiftKey = false, isComposing = false, keyCode = 13 } = {}) {
  input.dispatchEvent({
    type: 'keydown',
    key: 'Enter',
    shiftKey,
    isComposing,
    keyCode,
    preventDefault() {
      this.defaultPrevented = true;
    },
    defaultPrevented: false,
  });
}

function clickQuickAction(btn, eventTarget) {
  const target = eventTarget || btn;
  const root = btn.parentNode;
  root.dispatchEvent({
    type: 'click',
    target,
    preventDefault() {},
  });
}

test('F2: pending send blocks Enter / requestSubmit / quick action; draft preserved; one transport call', async () => {
  const { document, byId, form, quickBtn } = buildChatDom();
  const transport = createPendingTransport();
  loadAiChat({ document, fetchImpl: transport.fetchImpl });

  byId.messageInput.value = 'first message';
  pressEnter(byId.messageInput);
  await delay(20);

  assert.equal(transport.sendCalls.length, 1, 'first Enter should issue one send');
  assert.equal(countRoleBubbles(byId.chatWindow, 'user'), 1);
  assert.equal(byId.messageInput.value, '', 'composer clears after accepted submit');

  byId.messageInput.value = 'second draft must stay';
  pressEnter(byId.messageInput);
  form.requestSubmit();
  clickQuickAction(quickBtn);
  await delay(20);

  assert.equal(transport.sendCalls.length, 1, 'pending guard must block extra transports');
  assert.equal(countRoleBubbles(byId.chatWindow, 'user'), 1, 'no second user bubble while pending');
  assert.equal(byId.messageInput.value, 'second draft must stay');

  transport.resolveSuccess();
  await delay(40);

  assert.equal(countRoleBubbles(byId.chatWindow, 'assistant'), 1);
  assert.equal(byId.messageInput.value, 'second draft must stay', 'success must not clear unrelated draft');

  byId.messageInput.value = 'follow-up after unlock';
  pressEnter(byId.messageInput);
  await delay(20);
  assert.equal(transport.sendCalls.length, 2, 'after unlock a new send is allowed');
  assert.equal(countRoleBubbles(byId.chatWindow, 'user'), 2);
  assert.equal(byId.messageInput.value, '');

  transport.resolveSuccess();
  await delay(40);
});

test('F2: error settle unlocks and allows retry without dropping new draft timing', async () => {
  const { document, byId } = buildChatDom();
  const transport = createPendingTransport();
  loadAiChat({ document, fetchImpl: transport.fetchImpl });

  byId.messageInput.value = 'will fail';
  pressEnter(byId.messageInput);
  await delay(20);
  assert.equal(transport.sendCalls.length, 1);

  byId.messageInput.value = 'typed while pending';
  pressEnter(byId.messageInput);
  await delay(10);
  assert.equal(transport.sendCalls.length, 1);
  assert.equal(byId.messageInput.value, 'typed while pending');

  transport.resolveError();
  await delay(60);

  assert.equal(transport.sendCalls.length, 1, 'error path must not auto-retry');
  assert.ok(
    countRoleBubbles(byId.chatWindow, 'system') >= 1 || byId.chatStatus.classList.contains('d-none') === false,
    'error should surface to the user'
  );

  byId.messageInput.value = 'retry after error';
  pressEnter(byId.messageInput);
  await delay(20);
  assert.equal(transport.sendCalls.length, 2);
  assert.equal(byId.messageInput.value, '');

  transport.resolveSuccess();
  await delay(40);
});

test('F2: Shift+Enter and IME composition do not submit', async () => {
  const { document, byId } = buildChatDom();
  const transport = createPendingTransport();
  loadAiChat({ document, fetchImpl: transport.fetchImpl });

  byId.messageInput.value = 'line one';
  pressEnter(byId.messageInput, { shiftKey: true });
  await delay(10);
  assert.equal(transport.sendCalls.length, 0);
  assert.equal(byId.messageInput.value, 'line one');

  pressEnter(byId.messageInput, { isComposing: true, keyCode: 229 });
  await delay(10);
  assert.equal(transport.sendCalls.length, 0);
  assert.equal(byId.messageInput.value, 'line one');
});

test('Unknown SignalR outcome never dispatches the same intent through HTTP fallback', async () => {
  const { document, byId } = buildChatDom();
  const transport = createPendingTransport();
  let invocations = 0;
  const connection = {
    state: 'Connected', off() {}, on() {}, onreconnecting() {}, onreconnected() {}, onclose() {},
    invoke() { invocations++; return Promise.reject(new Error('connection lost after dispatch')); },
  };
  byId.conversationIdInput.value = '11111111-1111-1111-1111-111111111111';
  loadAiChat({ document, fetchImpl: transport.fetchImpl, connection });
  await delay(20);
  byId.messageInput.value = 'one intended provider call';
  pressEnter(byId.messageInput);
  await delay(50);
  assert.equal(invocations, 1, JSON.stringify({ status: byId.chatStatus.textContent, sends: transport.sendCalls.length }));
  assert.doesNotMatch(byId.chatStatus.textContent, /not a function|not defined/);
  assert.equal(transport.sendCalls.length, 0, 'an unknown dispatched outcome must not call provider again via HTTP');
});

test('F2: mutation without sync submit guard allows double transport (red proof)', async () => {
  const production = fs.readFileSync(AI_CHAT_JS, 'utf8');
  const mutated = production
    .replace(
      /elForm\.addEventListener\('submit',\s*async\s*function\s*\(event\)\s*\{[\s\S]*?if\s*\(isSubmitting\s*\|\|\s*elSendBtn\.disabled\s*\|\|\s*elForm\.dataset\.state\s*===\s*'sending'\)\s*\{\s*return;\s*\}/,
      "elForm.addEventListener('submit', async function (event) {\n            event.preventDefault();\n            /* MUTATION: removed isSubmitting guard for red proof */"
    )
    .replace(
      /elInput\.addEventListener\('keydown',\s*function\s*\(e\)\s*\{[\s\S]*?if\s*\(isSubmitting\s*\|\|\s*elSendBtn\.disabled\s*\|\|\s*elForm\.dataset\.state\s*===\s*'sending'\)\s*\{\s*return;\s*\}/,
      "elInput.addEventListener('keydown', function (e) {\n            if (e.key === 'Enter' && !e.shiftKey) {\n                if (e.isComposing || e.keyCode === 229) {\n                    return;\n                }\n                e.preventDefault();\n                /* MUTATION: removed keydown isSubmitting guard */"
    );

  assert.notEqual(mutated, production, 'in-memory mutation must alter production source');
  assert.match(mutated, /MUTATION: removed isSubmitting guard/);

  const { document, byId, form } = buildChatDom();
  const transport = createPendingTransport();
  loadAiChat({ document, fetchImpl: transport.fetchImpl, sourceText: mutated });

  byId.messageInput.value = 'first';
  pressEnter(byId.messageInput);
  await delay(20);
  assert.equal(transport.sendCalls.length, 1);

  byId.messageInput.value = 'second should leak without guard';
  form.requestSubmit();
  await delay(20);

  assert.ok(
    transport.sendCalls.length >= 2,
    `mutated source should allow double submit; got ${transport.sendCalls.length}`
  );

  transport.resolveSuccess();
  await delay(40);
});
