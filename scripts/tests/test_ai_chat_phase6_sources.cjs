'use strict';

/**
 * Phase 6 client regressions: encoded sources, stream acceptance, and retry.
 * Executes production wwwroot/js/ai-chat.js. Does not re-implement the UI rules.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const AI_CHAT_JS = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/ai-chat.js');
const SEND_URL = '/AI/Chat/Send';
const OBS_URL = '/AI/Observability';
const HOSTILE = '<img src=x onerror=alert(1)>';

function enhanceElement(el) {
  if (!el || el.__phase6) return el;
  el.__phase6 = true;
  const originalSetAttribute = el.setAttribute.bind(el);
  el.setAttribute = (name, value) => {
    originalSetAttribute(name, value);
    if (String(name).startsWith('data-')) {
      const dataKey = String(name).slice(5).replace(/-([a-z])/g, (_, c) => c.toUpperCase());
      el.dataset[dataKey] = String(value);
    }
  };
  Object.defineProperty(el, 'parentElement', {
    get() { return el.parentNode; },
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
  el.focus = () => { el._focused = true; };
  if (el.tagName === 'FORM') {
    el.requestSubmit = () => {
      el.dispatchEvent({
        type: 'submit',
        target: el,
        preventDefault() { this.defaultPrevented = true; },
        defaultPrevented: false,
      });
    };
  }
  return el;
}

function matchesSelector(el, selector) {
  if (!el || !selector) return false;
  if (selector.startsWith('#')) return el.id === selector.slice(1);
  if (selector.startsWith('.')) {
    return selector.slice(1).split('.').filter(Boolean).every((part) => el.classList.contains(part));
  }
  if (selector.startsWith('[') && selector.endsWith(']')) {
    const inner = selector.slice(1, -1);
    const eq = inner.indexOf('=');
    if (eq === -1) return el.getAttribute(inner) != null;
    return el.getAttribute(inner.slice(0, eq)) === inner.slice(eq + 1).replace(/^["']|["']$/g, '');
  }
  return el.tagName === String(selector).toUpperCase();
}

function buildChatDom() {
  const ids = [
    'chatForm', 'messageInput', 'chatWindow', 'scrollToBottomBtn', 'typing', 'sendBtn', 'stopBtn',
    'conversationIdInput', 'conversationMetaValue', 'conversationMetaStatus', 'chatStatus',
    'aiMetricRequestCount', 'aiMetricErrorRate', 'aiMetricLatency', 'aiMetricTokenToday', 'chatRetryBtn',
  ];
  const { document, byId, body } = createDocument(ids);
  const form = createElement('form', 'chatForm');
  enhanceElement(form);
  form.dataset.state = 'idle';
  byId.chatForm = form;

  const shell = createElement('div', 'chatShell');
  enhanceElement(shell);
  shell.setAttribute('data-ai-chat-shell', 'true');
  shell.dataset.userInitial = 'T';
  shell.dataset.aiAvatar = '/images/ai/tct-ai-launcher.png';

  const statusText = createElement('span', 'chatStatusText');
  enhanceElement(statusText);
  statusText.setAttribute('data-ai-status-text', 'true');
  byId.chatStatusText = statusText;
  byId.chatStatus.appendChild(statusText);
  enhanceElement(byId.chatStatus);
  byId.chatStatus.classList.add('d-none');

  enhanceElement(byId.chatRetryBtn);
  byId.chatRetryBtn.classList.add('d-none');
  byId.chatRetryBtn.textContent = 'Thử lại';

  Object.assign(byId.messageInput, { tagName: 'TEXTAREA', value: '', scrollHeight: 46 });
  enhanceElement(byId.messageInput);
  enhanceElement(byId.chatWindow);
  byId.chatWindow.scrollTop = 0;
  byId.chatWindow.scrollHeight = 100;
  byId.chatWindow.clientHeight = 100;
  byId.chatWindow.scrollTo = () => {};
  ['scrollToBottomBtn', 'typing', 'sendBtn', 'stopBtn', 'conversationIdInput'].forEach((id) => enhanceElement(byId[id]));
  byId.typing.setAttribute('data-role', 'assistant');
  byId.typing.classList.add('d-none');
  byId.stopBtn.classList.add('d-none');
  byId.stopBtn.disabled = true;
  byId.conversationIdInput.value = '';
  byId.conversationMetaValue.textContent = 'Đoạn chat mới';

  const csrf = createElement('input', '');
  enhanceElement(csrf);
  csrf.name = '__RequestVerificationToken';
  csrf.value = 'test-csrf';
  form.appendChild(csrf);
  shell.appendChild(form);
  ['messageInput', 'sendBtn', 'stopBtn', 'conversationIdInput', 'chatStatus', 'chatRetryBtn',
    'aiMetricRequestCount', 'aiMetricErrorRate', 'aiMetricLatency', 'aiMetricTokenToday',
    'conversationMetaValue', 'conversationMetaStatus'].forEach((id) => {
    if (byId[id].parentNode) {
      byId[id].parentNode.children = byId[id].parentNode.children.filter((child) => child !== byId[id]);
    }
    form.appendChild(byId[id]);
  });
  ['chatWindow', 'scrollToBottomBtn', 'typing'].forEach((id) => {
    if (byId[id].parentNode) {
      byId[id].parentNode.children = byId[id].parentNode.children.filter((child) => child !== byId[id]);
    }
    shell.appendChild(byId[id]);
  });
  body.children = [shell];
  document.getElementById = (id) => byId[id] || null;
  document.querySelector = (sel) => {
    if (sel === '[data-ai-chat-shell]' || sel === '.ai-chat-shell') return shell;
    if (sel === 'input[name="__RequestVerificationToken"]') return csrf;
    if (sel.startsWith('#')) return byId[sel.slice(1)] || null;
    return null;
  };
  document.createElement = (tag) => enhanceElement(createElement(tag));
  form.querySelector = (sel) => (sel === 'input[name="__RequestVerificationToken"]' ? csrf : null);
  return { document, byId, form };
}

function createHub() {
  const handlers = Object.create(null);
  const invokes = [];
  return {
    state: 'Connected',
    invokes,
    off(name) { delete handlers[name]; },
    on(name, fn) { handlers[name] = fn; },
    onreconnecting() {},
    onreconnected() {},
    onclose() {},
    invoke(method, ...args) {
      invokes.push({ method, args });
      return Promise.resolve();
    },
    emit(name, payload) {
      if (handlers[name]) handlers[name](payload);
    },
  };
}

function loadAiChat({ document, fetchImpl, connection }) {
  const signalR = { HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' } };
  const windowObj = {
    document,
    fetch: fetchImpl,
    signalR: connection ? signalR : undefined,
    userActivityHubConnection: connection,
    innerWidth: 1280,
    location: { href: 'http://localhost/AI/Chat', origin: 'http://localhost' },
    history: { state: null, replaceState() {} },
    setTimeout,
    clearTimeout,
    requestAnimationFrame(cb) { return setTimeout(cb, 0); },
    addEventListener() {},
    removeEventListener() {},
    parent: { postMessage() {} },
  };
  windowObj.top = windowObj;
  windowObj.self = windowObj;
  windowObj.window = windowObj;
  const sandbox = {
    window: windowObj,
    signalR,
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
        preventDefault() { this.defaultPrevented = true; },
        defaultPrevented: false,
      };
    },
  };
  sandbox.globalThis = sandbox;
  vm.runInNewContext(fs.readFileSync(AI_CHAT_JS, 'utf8'), sandbox, { filename: AI_CHAT_JS });
  windowObj.tctAiChat.init({
    sendUrl: SEND_URL,
    observabilityUrl: OBS_URL,
    hubUrl: '/classChatHub',
    deleteUrl: '/AI/DeleteConversation',
  });
  return windowObj;
}

function observabilityResponse() {
  return {
    ok: true,
    status: 200,
    headers: { get: () => 'application/json' },
    json: async () => ({ requestCount: 0, errorRate: 0, latencyMs: 0, tokensUsedToday: 0, tokenBudget: 0 }),
  };
}

function pressEnter(input) {
  input.dispatchEvent({
    type: 'keydown',
    key: 'Enter',
    shiftKey: false,
    isComposing: false,
    keyCode: 13,
    preventDefault() { this.defaultPrevented = true; },
    defaultPrevented: false,
  });
}

function walk(node, visit) {
  visit(node);
  for (const child of node.children || []) walk(child, visit);
}

function assistantRows(chatWindow) {
  return (chatWindow.children || []).filter((child) => child.getAttribute && child.getAttribute('data-role') === 'assistant');
}

function visibleAssistantText(chatWindow) {
  const parts = [];
  walk(chatWindow, (node) => {
    if (node.classList && node.classList.contains('js-assistant-markdown') && !node.closest('.d-none')) {
      parts.push(node.textContent || '');
    }
  });
  return parts.join('');
}

function sourceNodes(chatWindow) {
  const nodes = [];
  walk(chatWindow, (node) => {
    if (node.classList && node.classList.contains('ai-chat-sources')) nodes.push(node);
  });
  return nodes;
}

test('older messages encode hostile titles and drop unsafe routes', async () => {
  const { document, body } = createDocument([]);
  const chatWindow = createElement('div', 'chatWindow');
  const loadRow = createElement('div', '');
  loadRow.className = 'ai-chat-load-older-row';
  const button = createElement('button', '');
  button.setAttribute('data-ai-load-older', 'messages');
  button.setAttribute('data-ai-load-older-url', '/AI/Chat/Messages');
  button.setAttribute('data-conversation-id', 'c1');
  button.setAttribute('data-ai-message-cursor-time', '2026-01-01T00:04:00.000Z');
  button.setAttribute('data-ai-message-cursor-id', 'm4');
  loadRow.appendChild(button);
  chatWindow.appendChild(loadRow);
  Object.defineProperty(chatWindow, 'scrollHeight', { get: () => 200, configurable: true });
  chatWindow.scrollTop = 0;
  body.appendChild(chatWindow);
  const shell = createElement('div', '');
  shell.setAttribute('data-ai-avatar', '/x.png');

  const enhanceTree = (root) => {
    const stack = [root];
    while (stack.length) {
      const el = stack.pop();
      enhanceElement(el);
      if (!el.insertBefore) {
        el.insertBefore = (node, reference) => {
          if (node && node.nodeType === 11) {
            for (const child of [...(node.children || [])]) el.insertBefore(child, reference);
            return node;
          }
          if (node.parentNode && node.parentNode.children) {
            node.parentNode.children = node.parentNode.children.filter((child) => child !== node);
          }
          node.parentNode = el;
          if (!reference) el.children.push(node);
          else {
            const index = el.children.indexOf(reference);
            if (index < 0) el.children.push(node);
            else el.children.splice(index, 0, node);
          }
          return node;
        };
      }
      for (const child of el.children || []) stack.push(child);
    }
  };
  enhanceTree(chatWindow);
  enhanceTree(shell);
  document.createDocumentFragment = () => {
    const fragment = createElement('fragment', '');
    fragment.nodeType = 11;
    enhanceElement(fragment);
    return fragment;
  };
  document.createElement = (tag) => enhanceElement(createElement(tag));
  const windowObj = {
    document,
    location: { origin: 'http://localhost' },
    URL,
    fetch: async () => ({
      ok: true,
      async json() {
        return {
          hasMore: false,
          items: [
            {
              id: 'm0',
              role: 'assistant',
              content: 'older answer',
              createdAtUtc: '2026-01-01T00:00:00.000Z',
              grounding: {
                outcome: 'insufficient_evidence',
                status: 'current',
                sources: [
                  { sourceKey: 'card:1', title: HOSTILE, route: 'javascript:alert(1)' },
                  { sourceKey: 'card:2', title: 'Remote', route: 'https://evil.example/phish' },
                  { sourceKey: 'card:3', title: 'Admin', route: '/Admin/Index' },
                  { sourceKey: 'card:4', title: '', route: '/Home/Study/301', freshness: 'changed' },
                ],
              },
            },
            {
              id: 'm1',
              role: 'assistant',
              content: 'provider down',
              createdAtUtc: '2026-01-01T00:01:00.000Z',
              grounding: { outcome: 'provider_unavailable', status: 'current', sources: [] },
            },
          ],
        };
      },
    }),
  };
  windowObj.window = windowObj;
  vm.runInNewContext(fs.readFileSync(AI_CHAT_JS, 'utf8'), {
    window: windowObj, document, console, setTimeout, clearTimeout, URL, fetch: windowObj.fetch,
  });
  await windowObj.tctAiChat.loadOlderMessages(button, chatWindow, shell);

  const sources = sourceNodes(chatWindow);
  assert.equal(sources.length, 2);
  const links = [];
  const titles = [];
  walk(sources[0], (node) => {
    if (node.tagName === 'A') links.push(node.href);
    if (node.tagName === 'IMG') throw new Error('hostile title became an image element');
    if (typeof node.innerHTML === 'string' && node.innerHTML.includes(HOSTILE)) {
      throw new Error('hostile title was assigned as HTML');
    }
    if (node.textContent) titles.push(node.textContent);
  });
  assert.deepEqual(links, ['/Home/Study/301']);
  assert.ok(titles.some((title) => title.includes(HOSTILE)));
  assert.ok(titles.some((title) => title === 'Nguồn đã lưu'));
  assert.ok(titles.some((title) => title.includes('Chưa đủ căn cứ')));
  assert.ok(titles.some((title) => title.includes('Nguồn đã thay đổi sau khi trả lời')));
  assert.equal(sources[0].textContent.includes('card:1'), false);
  const states = [];
  walk(chatWindow, (node) => {
    if (node.classList && node.classList.contains('ai-chat-state')) states.push(node.textContent);
  });
  assert.ok(states.includes('Chưa đủ căn cứ'));
  assert.ok(states.includes('Trợ lý tạm thời không trả lời được'));
});

test('stream buffers out-of-order chunks and renders sources only after completion', async () => {
  const { document, byId } = buildChatDom();
  const hub = createHub();
  const sends = [];
  const fetchImpl = async (url) => {
    if (String(url).includes('Observability')) return observabilityResponse();
    sends.push(url);
    return observabilityResponse();
  };
  loadAiChat({ document, fetchImpl, connection: hub });
  await delay(20);
  byId.conversationIdInput.value = '11111111-1111-1111-1111-111111111111';
  byId.messageInput.value = 'stream this answer';
  pressEnter(byId.messageInput);
  await delay(30);

  assert.equal(sends.length, 0);
  assert.equal(hub.invokes.filter((call) => call.method === 'StartAiStream').length, 1);
  const row = assistantRows(byId.chatWindow).at(-1);
  assert.ok(row.classList.contains('d-none'));
  assert.equal(visibleAssistantText(byId.chatWindow), '');

  const streamId = 'stream-a';
  hub.emit('AiStreamChunk', { streamId, conversationId: byId.conversationIdInput.value, chunkIndex: 1, chunk: 'SECOND' });
  hub.emit('AiStreamStarted', { streamId, conversationId: byId.conversationIdInput.value });
  hub.emit('AiStreamChunk', { streamId, conversationId: byId.conversationIdInput.value, chunkIndex: 0, chunk: 'FIRST' });
  hub.emit('AiStreamChunk', { streamId, conversationId: byId.conversationIdInput.value, chunkIndex: 0, chunk: 'DUPLICATE' });
  hub.emit('AiStreamChunk', { streamId: 'foreign', conversationId: byId.conversationIdInput.value, chunkIndex: 2, chunk: 'FOREIGN' });
  assert.equal(visibleAssistantText(byId.chatWindow), '');
  assert.equal(sourceNodes(byId.chatWindow).length, 0);

  hub.emit('AiStreamCompleted', {
    streamId,
    conversationId: byId.conversationIdInput.value,
    grounding: {
      outcome: 'answered',
      status: 'current',
      stateLabel: 'Đã trả lời từ nguồn được phép xem',
      sources: [{ sourceKey: 'card:401', title: 'Forecast', route: '/Home/Study/301', freshness: 'current' }],
    },
  });
  await delay(20);
  assert.equal(visibleAssistantText(byId.chatWindow), 'FIRSTSECOND');
  assert.equal(row.getAttribute('data-accepted'), 'true');
  assert.equal(row.classList.contains('d-none'), false);
  const links = [];
  walk(sourceNodes(byId.chatWindow)[0], (node) => {
    if (node.tagName === 'A') links.push(node.href);
  });
  assert.deepEqual(links, ['/Home/Study/301']);
  const rendered = [];
  walk(sourceNodes(byId.chatWindow)[0], (node) => {
    if (node.textContent) rendered.push(node.textContent);
  });
  assert.ok(rendered.includes('Đã trả lời từ nguồn được phép xem'));
  assert.equal(rendered.includes('card:401'), false);
});

test('Stop drops an unaccepted stream and ignores a late completion', async () => {
  const { document, byId } = buildChatDom();
  const hub = createHub();
  loadAiChat({
    document,
    connection: hub,
    fetchImpl: async (url) => (String(url).includes('Observability') ? observabilityResponse() : observabilityResponse()),
  });
  await delay(20);
  byId.conversationIdInput.value = '22222222-2222-2222-2222-222222222222';
  byId.messageInput.value = 'stop me';
  pressEnter(byId.messageInput);
  await delay(30);
  const streamId = 'stream-stop';
  hub.emit('AiStreamStarted', { streamId, conversationId: byId.conversationIdInput.value });
  hub.emit('AiStreamChunk', { streamId, conversationId: byId.conversationIdInput.value, chunkIndex: 0, chunk: 'PARTIAL FACT' });
  assert.equal(visibleAssistantText(byId.chatWindow), '');
  byId.stopBtn.click();
  await delay(20);
  hub.emit('AiStreamCompleted', {
    streamId,
    conversationId: byId.conversationIdInput.value,
    grounding: {
      outcome: 'answered',
      status: 'current',
      sources: [{ sourceKey: 'card:9', title: 'Stale source', route: '/Home/Study/301' }],
    },
  });
  await delay(20);
  assert.equal(visibleAssistantText(byId.chatWindow), '');
  assert.equal(sourceNodes(byId.chatWindow).length, 0);
  assert.equal(byId.chatWindow.textContent.includes('PARTIAL FACT'), false);
  assert.equal(byId.chatWindow.textContent.includes('Stale source'), false);
  assert.equal(byId.messageInput.value, 'stop me');
  assert.ok(hub.invokes.some((call) => call.method === 'StopAiStream' && call.args[0] === streamId));
});

test('Stop before the stream id arrives cancels it on late admission and ignores completion', async () => {
  const { document, byId } = buildChatDom();
  const hub = createHub();
  loadAiChat({
    document,
    connection: hub,
    fetchImpl: async () => observabilityResponse(),
  });
  await delay(20);
  byId.conversationIdInput.value = '44444444-4444-4444-4444-444444444444';
  byId.messageInput.value = 'stop early';
  pressEnter(byId.messageInput);
  await delay(30);
  assert.equal(byId.stopBtn.classList.contains('d-none'), false);
  byId.stopBtn.click();
  await delay(20);
  hub.emit('AiStreamStarted', { streamId: 'late', conversationId: byId.conversationIdInput.value });
  hub.emit('AiStreamChunk', { streamId: 'late', conversationId: byId.conversationIdInput.value, chunkIndex: 0, chunk: 'LATE FACT' });
  hub.emit('AiStreamCompleted', {
    streamId: 'late',
    conversationId: byId.conversationIdInput.value,
    grounding: {
      outcome: 'answered',
      status: 'current',
      sources: [{ sourceKey: 'card:8', title: 'Late source', route: '/Home/Study/301' }],
    },
  });
  await delay(20);
  assert.equal(byId.chatWindow.textContent.includes('LATE FACT'), false);
  assert.equal(byId.chatWindow.textContent.includes('Late source'), false);
  assert.equal(sourceNodes(byId.chatWindow).length, 0);
  assert.equal(byId.messageInput.value, 'stop early');
  assert.ok(hub.invokes.some((call) => call.method === 'StopAiStream' && call.args[0] === 'late'));
});

test('Stop and retry waits for old admission to settle and rejects replayed old Started', async () => {
  const { document, byId } = buildChatDom();
  const hub = createHub();
  const invoke = hub.invoke.bind(hub);
  let settleOld;
  let startCount = 0;
  hub.invoke = (method, ...args) => {
    invoke(method, ...args);
    if (method === 'StartAiStream' && ++startCount === 1) {
      return new Promise(resolve => { settleOld = resolve; });
    }
    return Promise.resolve();
  };
  let ajax = 0;
  loadAiChat({ document, connection: hub, fetchImpl: async url => {
    if (!String(url).includes('Observability')) ajax++;
    return observabilityResponse();
  } });
  await delay(20);
  const conversationId = '55555555-5555-5555-5555-555555555555';
  byId.conversationIdInput.value = conversationId;
  byId.messageInput.value = 'same draft';
  pressEnter(byId.messageInput);
  await delay(20);
  byId.stopBtn.click();
  await delay(20);
  pressEnter(byId.messageInput);
  await delay(20);
  assert.equal(startCount, 1, 'no second stream while old admission can still arrive');
  assert.equal(ajax, 0, 'no fallback can duplicate the stopped turn');
  assert.equal(byId.messageInput.value, 'same draft');
  hub.emit('AiStreamStarted', { streamId: 'old-late', conversationId });
  hub.emit('AiStreamChunk', { streamId: 'old-late', conversationId, chunkIndex: 0, chunk: 'OLD FACT' });
  hub.emit('AiStreamCompleted', { streamId: 'old-late', conversationId });
  assert.equal(visibleAssistantText(byId.chatWindow), '');
  assert.ok(hub.invokes.some(call => call.method === 'StopAiStream' && call.args[0] === 'old-late'));
  settleOld();
  await delay(20);
  pressEnter(byId.messageInput);
  await delay(20);
  assert.equal(startCount, 2);
  hub.emit('AiStreamStarted', { streamId: 'new', conversationId });
  hub.emit('AiStreamStarted', { streamId: 'old-late', conversationId });
  hub.emit('AiStreamChunk', { streamId: 'old-late', conversationId, chunkIndex: 0, chunk: 'OLD FACT' });
  hub.emit('AiStreamCompleted', { streamId: 'old-late', conversationId });
  assert.equal(visibleAssistantText(byId.chatWindow), '');
  hub.emit('AiStreamChunk', { streamId: 'new', conversationId, chunkIndex: 0, chunk: 'NEW FACT' });
  hub.emit('AiStreamCompleted', { streamId: 'new', conversationId });
  await delay(20);
  assert.equal(visibleAssistantText(byId.chatWindow), 'NEW FACT');
});

test('429 restores the draft and retry does not duplicate the user bubble', async () => {
  const { document, byId } = buildChatDom();
  const sends = [];
  let fail = true;
  const fetchImpl = async (url, options = {}) => {
    if (String(url).includes('Observability')) return observabilityResponse();
    sends.push(JSON.parse(options.body));
    if (fail) {
      return {
        ok: false,
        status: 429,
        headers: { get: () => 'text/plain' },
        json: async () => ({}),
      };
    }
    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({ text: 'accepted later', conversationId: '33333333-3333-3333-3333-333333333333' }),
    };
  };
  loadAiChat({ document, fetchImpl });
  await delay(20);
  byId.messageInput.value = 'same question';
  pressEnter(byId.messageInput);
  await delay(40);
  assert.equal(sends.length, 1);
  assert.equal(byId.messageInput.value, 'same question');
  assert.equal(byId.chatStatusText.textContent, 'Bạn đang gửi quá nhanh. Vui lòng chờ một chút rồi thử lại.');
  assert.equal(byId.chatRetryBtn.classList.contains('d-none'), false);
  const userBubbles = () => (byId.chatWindow.children || []).filter((child) => child.getAttribute('data-role') === 'user');
  assert.equal(userBubbles().length, 1);
  fail = false;
  byId.chatRetryBtn.click();
  await delay(40);
  assert.equal(sends.length, 2);
  assert.equal(sends[1].message, 'same question');
  assert.equal(userBubbles().length, 1);
  assert.equal(byId.messageInput.value, '');
});

test('network failure keeps one draft and does not send twice', async () => {
  const { document, byId } = buildChatDom();
  let calls = 0;
  const fetchImpl = async (url) => {
    if (String(url).includes('Observability')) return observabilityResponse();
    calls += 1;
    throw new TypeError('Failed to fetch');
  };
  loadAiChat({ document, fetchImpl });
  await delay(20);
  byId.messageInput.value = 'offline draft';
  pressEnter(byId.messageInput);
  pressEnter(byId.messageInput);
  await delay(40);
  assert.equal(calls, 1);
  assert.equal(byId.messageInput.value, 'offline draft');
  assert.equal(byId.chatStatusText.textContent, 'Mạng bị gián đoạn. Bản nháp vẫn còn để bạn gửi lại.');
});
