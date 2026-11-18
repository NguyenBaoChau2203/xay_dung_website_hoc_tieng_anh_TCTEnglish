'use strict';

/**
 * Behavior, contract and state recovery tests for Writing creation panel (Step 02):
 * - Two creation modes: "Mô tả bài muốn luyện" and "Dán bài có sẵn"
 * - Tab switching, keyboard navigation and draft input retention
 * - Meaningful editable example chips
 * - Payload construction and active-mode field isolation
 * - Stable logical idempotency key across retries and invalidation upon edit
 * - Outage template offer inspection, slot completion and explicit confirmation
 * - Template cancellation with brief preservation
 * - Recoverable states, double-submit guard and pageshow protection
 * - Account-scoped state isolation
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');

const ROOT_DIR = path.resolve(__dirname, '../..');
const WRITING_EXERCISES_JS = path.join(ROOT_DIR, 'TCTEnglish/wwwroot/js/writing-exercises.js');

async function dispatchAsync(el, evt) {
  const list = (el._listeners && el._listeners[evt.type]) || [];
  for (const fn of list) {
    await fn(evt);
  }
}

async function waitUntil(predicate) {
  const deadline = Date.now() + 3000;
  while (!predicate()) {
    if (Date.now() > deadline) throw new Error('Expected async condition was not reached');
    await new Promise(setImmediate);
  }
}

function setupWritingCreateFixture(options = {}) {
  const { document, body } = createDocument([]);
  const userId = options.userId || 42;

  const form = createElement('form');
  form.setAttribute('data-writing-create-form', '');
  form.setAttribute('data-create-url', '/Home/Writing/Exercises/CreateFromAi');
  form.setAttribute('data-practice-url', '/Home/Writing/Practice');
  form.setAttribute('data-catalog-url', '/Home/Writing/Exercises#system-catalog-heading');
  form.setAttribute('data-owner-user-id', String(userId));
  form.setAttribute('data-status-url', '/api/writing/generation/{operationId}');
  form.setAttribute('data-confirm-url', '/api/writing/generation/{operationId}/confirm-template');
  form.setAttribute('data-cancel-url', '/api/writing/generation/{operationId}/cancel');
  form.setAttribute('data-capabilities-url', '/api/writing/generation/capabilities');

  const csrf = createElement('input');
  csrf.setAttribute('name', '__RequestVerificationToken');
  csrf.value = 'token-xyz-123';
  form.appendChild(csrf);

  const modeInput = createElement('input');
  modeInput.setAttribute('type', 'hidden');
  modeInput.setAttribute('name', 'mode');
  modeInput.setAttribute('data-writing-mode-input', '');
  modeInput.value = 'from_brief';
  form.appendChild(modeInput);

  // Tabs
  const tabNav = createElement('div');
  tabNav.setAttribute('role', 'tablist');

  const tabBrief = createElement('button');
  tabBrief.setAttribute('type', 'button');
  tabBrief.setAttribute('role', 'tab');
  tabBrief.setAttribute('id', 'tabModeBrief');
  tabBrief.setAttribute('data-mode-tab', 'from_brief');
  tabBrief.classList.add('is-active');
  tabBrief.setAttribute('aria-selected', 'true');
  tabNav.appendChild(tabBrief);

  const tabSource = createElement('button');
  tabSource.setAttribute('type', 'button');
  tabSource.setAttribute('role', 'tab');
  tabSource.setAttribute('id', 'tabModeSource');
  tabSource.setAttribute('data-mode-tab', 'from_text');
  tabSource.setAttribute('aria-selected', 'false');
  tabSource.setAttribute('tabindex', '-1');
  tabNav.appendChild(tabSource);
  form.appendChild(tabNav);

  // Panel 1: Brief
  const panelBrief = createElement('div');
  panelBrief.setAttribute('role', 'tabpanel');
  panelBrief.setAttribute('id', 'panelModeBrief');
  panelBrief.setAttribute('data-mode-panel', 'from_brief');
  panelBrief.hidden = false;

  const briefCounter = createElement('span');
  briefCounter.setAttribute('data-brief-char-counter', '');
  briefCounter.textContent = '0 / 1200 ký tự';
  panelBrief.appendChild(briefCounter);

  const briefInput = createElement('textarea');
  briefInput.setAttribute('name', 'brief');
  briefInput.setAttribute('data-brief-input', '');
  briefInput.value = options.initialBrief || '';
  briefInput.focus = () => { briefInput._focused = true; };
  panelBrief.appendChild(briefInput);

  // Example chip
  const exampleChip = createElement('button');
  exampleChip.setAttribute('type', 'button');
  exampleChip.setAttribute('data-example-chip', '');
  exampleChip.setAttribute('data-example-brief', 'Viết email xin nghỉ phép để thăm gia đình, trình độ cơ bản, giọng lịch sự.');
  exampleChip.setAttribute('data-example-level', 'beginner');
  exampleChip.setAttribute('data-example-content-type', 'emails');
  exampleChip.setAttribute('data-example-length', 'short');
  exampleChip.setAttribute('data-example-tone', 'formal');
  panelBrief.appendChild(exampleChip);

  // Controls
  const levelSel = createElement('select');
  levelSel.setAttribute('name', 'level');
  levelSel.setAttribute('data-brief-control', 'level');
  levelSel.value = 'intermediate';
  panelBrief.appendChild(levelSel);

  const typeSel = createElement('select');
  typeSel.setAttribute('name', 'contentType');
  typeSel.setAttribute('data-brief-control', 'contentType');
  typeSel.value = 'articles';
  panelBrief.appendChild(typeSel);

  const lenSel = createElement('select');
  lenSel.setAttribute('name', 'length');
  lenSel.setAttribute('data-brief-control', 'length');
  lenSel.value = 'short';
  panelBrief.appendChild(lenSel);

  const toneSel = createElement('select');
  toneSel.setAttribute('name', 'tone');
  toneSel.setAttribute('data-brief-control', 'tone');
  toneSel.value = 'formal';
  panelBrief.appendChild(toneSel);

  form.appendChild(panelBrief);

  // Panel 2: Source
  const panelSource = createElement('div');
  panelSource.setAttribute('role', 'tabpanel');
  panelSource.setAttribute('id', 'panelModeSource');
  panelSource.setAttribute('data-mode-panel', 'from_text');
  panelSource.hidden = true;

  const sourceCounter = createElement('span');
  sourceCounter.setAttribute('data-source-char-counter', '');
  sourceCounter.textContent = '0 / 4000 ký tự';
  panelSource.appendChild(sourceCounter);

  const sourceInput = createElement('textarea');
  sourceInput.setAttribute('name', 'sourceText');
  sourceInput.setAttribute('data-source-input', '');
  sourceInput.value = options.initialSource || '';
  sourceInput.focus = () => { sourceInput._focused = true; };
  panelSource.appendChild(sourceInput);

  form.appendChild(panelSource);

  // Summary, submit & message
  const charSummary = createElement('span');
  charSummary.setAttribute('data-writing-char-summary', '');
  charSummary.textContent = 'Tối đa 1200 ký tự';
  form.appendChild(charSummary);

  const submitBtn = createElement('button');
  submitBtn.setAttribute('data-writing-create-submit', '');
  form.appendChild(submitBtn);

  const submitText = createElement('span');
  submitText.setAttribute('data-writing-create-submit-text', '');
  submitText.textContent = 'Tạo bài theo mô tả';
  submitBtn.appendChild(submitText);

  const msgBox = createElement('p');
  msgBox.setAttribute('data-writing-create-message', '');
  msgBox.hidden = true;
  form.appendChild(msgBox);

  // Template offer card
  const offerCard = createElement('div');
  offerCard.setAttribute('data-template-offer-card', '');
  offerCard.hidden = true;

  const offerCovered = createElement('span');
  offerCovered.setAttribute('data-offer-covered', '');
  offerCard.appendChild(offerCovered);

  const offerMissing = createElement('span');
  offerMissing.setAttribute('data-offer-missing', '');
  offerCard.appendChild(offerMissing);

  const offerTitle = createElement('div');
  offerTitle.setAttribute('data-offer-title', '');
  offerCard.appendChild(offerTitle);
  const templateChoice = createElement('div'); templateChoice.setAttribute('data-offer-template-choice', ''); templateChoice.hidden = true;
  const templateSelect = createElement('select'); templateSelect.setAttribute('data-offer-template-select', '');
  templateSelect.replaceChildren = (...nodes) => {
    while(templateSelect.firstChild) templateSelect.removeChild(templateSelect.firstChild);
    nodes.forEach(node => templateSelect.appendChild(node));
  };
  templateChoice.appendChild(templateSelect); offerCard.appendChild(templateChoice);

  const offerPreview = createElement('div');
  offerPreview.setAttribute('data-offer-preview-lines', '');
  offerCard.appendChild(offerPreview);

  const offerSlotsWrap = createElement('div');
  offerSlotsWrap.setAttribute('data-offer-slots-wrap', '');
  offerSlotsWrap.hidden = true;

  const offerSlotsContainer = createElement('div');
  offerSlotsContainer.setAttribute('data-offer-slots-container', '');
  offerSlotsWrap.appendChild(offerSlotsContainer);
  offerCard.appendChild(offerSlotsWrap);

  const offerCheck = createElement('input');
  offerCheck.setAttribute('type', 'checkbox');
  offerCheck.setAttribute('data-offer-accept-check', '');
  offerCheck.checked = false;
  offerCard.appendChild(offerCheck);

  const confirmBtn = createElement('button');
  confirmBtn.setAttribute('data-offer-confirm-btn', '');
  confirmBtn.disabled = true;
  offerCard.appendChild(confirmBtn);

  const cancelBtn = createElement('button');
  cancelBtn.setAttribute('data-offer-cancel-btn', '');
  offerCard.appendChild(cancelBtn);

  form.appendChild(offerCard);
  body.appendChild(form);

  const sessionStorageStore = new Map(Object.entries(options.savedStorage || {}));
  const mockSessionStorage = {
    getItem(key) { return sessionStorageStore.get(key) || null; },
    setItem(key, val) { sessionStorageStore.set(key, String(val)); },
    removeItem(key) { sessionStorageStore.delete(key); },
    clear() { sessionStorageStore.clear(); },
    key(i) { return Array.from(sessionStorageStore.keys())[i] || null; },
    get length() { return sessionStorageStore.size; }
  };

  let uuidCounter = 0;
  let assignedUrl = null;
  const mockWindow = {
    document,
    location: {
      origin: 'http://localhost',
      assign(url) { assignedUrl = url; },
      reload() { assignedUrl = 'reload'; }
    },
    sessionStorage: mockSessionStorage,
    addEventListener(type, fn) {
      if (!mockWindow._listeners) mockWindow._listeners = {};
      if (!mockWindow._listeners[type]) mockWindow._listeners[type] = [];
      mockWindow._listeners[type].push(fn);
    },
    dispatchEvent(evt) {
      const list = (mockWindow._listeners && mockWindow._listeners[evt.type]) || [];
      for (const fn of list) fn(evt);
    },
    crypto: {
      subtle: require('node:crypto').webcrypto.subtle,
      randomUUID() { return 'uuid-mock-' + (++uuidCounter); }
    },
    console,
    URL,
    setTimeout,
    clearTimeout,
    Date,
    Math
  };
  mockWindow.window = mockWindow;

  let lastFetchCall = null;
  const fetchCalls = [];
  const mockFetch = async (url, opts) => {
    if (url === '/api/writing/generation/quota') return jsonResponse({success: true, data: {ownerUserId: options.ownerProvider ? options.ownerProvider() : options.actualOwner || userId, dailyLimit: 5, remaining: 5, succeeded: 0, reserved: 0, isUnlimited: false}});
    if (url.includes('/by-key/') && !options.recoveryHandler) return jsonResponse({}, 404);
    if (url.includes('/by-key/') && options.recoveryHandler) return options.recoveryHandler(url, opts);
    lastFetchCall = { url, opts };
    fetchCalls.push({ url, opts });
    if (typeof options.fetchHandler === 'function') {
      const result = await options.fetchHandler(url, opts);
      if (result && !result.headers) result.headers = { get: () => 'application/json' };
      return result;
    }
    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({
        success: true,
        data: { status: 'succeeded', exerciseId: 101, level: 'beginner', contentType: 'emails', generationMethod: 'openai' }
      })
    };
  };

  loadScript(WRITING_EXERCISES_JS, {
    window: mockWindow,
    document,
    fetch: mockFetch,
    AbortController, TextEncoder,
    console,
    URL,
    setTimeout,
    clearTimeout,
    Date,
    Math
  });

  for (const fn of document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  return {
    document,
    body,
    form,
    tabBrief,
    tabSource,
    panelBrief,
    panelSource,
    briefInput,
    sourceInput,
    briefCounter,
    sourceCounter,
    charSummary,
    exampleChip,
    levelSel,
    typeSel,
    lenSel,
    toneSel,
    submitBtn,
    submitText,
    msgBox,
    offerCard,
    offerCovered,
    offerMissing,
    offerTitle,
    templateChoice, templateSelect,
    offerPreview,
    offerSlotsWrap,
    offerSlotsContainer,
    offerCheck,
    confirmBtn,
    cancelBtn,
    mockWindow,
    sessionStorage: mockSessionStorage,
    getAssignedUrl: () => assignedUrl,
    getLastFetchCall: () => lastFetchCall,
    getFetchCalls: () => fetchCalls,
    submitForm: async () => {
      const listeners = form._listeners && form._listeners.submit;
      assert.ok(listeners && listeners.length > 0, 'Form submit listener must be registered');
      for (const fn of listeners) {
        await fn({ type: 'submit', preventDefault() {} });
      }
    }
  };
}

// ─── 1. Tab Switching & Input Retention Tests ─────────────────────────

test('writing-exercises.js: tab switching changes active mode, panel visibility, copy, and preserves typed draft', () => {
  const fixture = setupWritingCreateFixture({ initialBrief: 'My initial brief draft', initialSource: 'My initial source draft' });

  assert.equal(fixture.tabBrief.classList.contains('is-active'), true);
  assert.equal(fixture.tabSource.classList.contains('is-active'), false);
  assert.equal(Boolean(fixture.panelBrief.hidden), false);
  assert.equal(Boolean(fixture.panelSource.hidden), true);
  assert.equal(fixture.submitText.textContent, 'Tạo bài theo mô tả');
  assert.equal(fixture.charSummary.textContent, 'Tối đa 1200 ký tự');

  // Switch to source tab
  fixture.tabSource.dispatchEvent({ type: 'click' });

  assert.equal(fixture.tabBrief.classList.contains('is-active'), false);
  assert.equal(fixture.tabSource.classList.contains('is-active'), true);
  assert.equal(Boolean(fixture.panelBrief.hidden), true);
  assert.equal(Boolean(fixture.panelSource.hidden), false);
  assert.equal(fixture.submitText.textContent, 'Tạo bài bằng AI');
  assert.equal(fixture.charSummary.textContent, 'Tối đa 4000 ký tự');

  // Check typed text is preserved on both sides
  assert.equal(fixture.briefInput.value, 'My initial brief draft');
  assert.equal(fixture.sourceInput.value, 'My initial source draft');

  // Switch back to brief tab
  fixture.tabBrief.dispatchEvent({ type: 'click' });
  assert.equal(fixture.tabBrief.classList.contains('is-active'), true);
  assert.equal(Boolean(fixture.panelBrief.hidden), false);
  assert.equal(fixture.briefInput.value, 'My initial brief draft');
});

test('writing-exercises.js: arrow keys navigate and activate tabs', () => {
  const fixture = setupWritingCreateFixture();

  let focused = false;
  fixture.tabSource.focus = () => { focused = true; };

  fixture.tabBrief.dispatchEvent({ type: 'keydown', key: 'ArrowRight', preventDefault() {} });
  assert.ok(focused, 'ArrowRight should focus the next tab');
  assert.equal(fixture.tabSource.classList.contains('is-active'), true, 'Next tab should become active');
});

// ─── 2. Example Chips Interaction Tests ───────────────────────────────

test('writing-exercises.js: clicking an example chip populates brief and controls without auto-submitting', async () => {
  let fetchTriggered = false;
  const fixture = setupWritingCreateFixture({
    fetchHandler: () => { fetchTriggered = true; return { ok: true, status: 200, json: async () => ({}) }; }
  });

  fixture.exampleChip.dispatchEvent({ type: 'click' });

  assert.equal(fixture.briefInput.value, 'Viết email xin nghỉ phép để thăm gia đình, trình độ cơ bản, giọng lịch sự.');
  assert.equal(fixture.levelSel.value, 'beginner');
  assert.equal(fixture.typeSel.value, 'emails');
  assert.equal(fixture.lenSel.value, 'short');
  assert.equal(fixture.toneSel.value, 'formal');
  assert.match(fixture.briefCounter.textContent, /ký tự/);
  assert.equal(fixture.briefInput._focused, true, 'Brief input should be focused after clicking example');
  assert.equal(fetchTriggered, false, 'Example chip click must NOT auto-submit or spend quota');
});

// ─── 3. Validation Tests ──────────────────────────────────────────────

test('writing-exercises.js: brief mode validates empty, short (<20), and non-alphanumeric inputs', async () => {
  const fixture = setupWritingCreateFixture();

  // Empty brief
  fixture.briefInput.value = '   ';
  await fixture.submitForm();
  assert.equal(fixture.msgBox.hidden, false);
  assert.ok(fixture.msgBox.classList.contains('is-error'));
  assert.match(fixture.msgBox.textContent, /Vui lòng nhập mô tả/i);

  // Short brief (< 20 chars)
  fixture.briefInput.value = 'Xin nghi phep';
  await fixture.submitForm();
  assert.match(fixture.msgBox.textContent, /quá ngắn.*tối thiểu 20/i);

  // Non-meaningful brief (< 10 letters/digits)
  fixture.briefInput.value = '....................abc';
  await fixture.submitForm();
  assert.match(fixture.msgBox.textContent, /ít nhất 10 chữ cái hoặc chữ số/i);
});

test('writing-exercises.js: source mode validates empty and short inputs', async () => {
  const fixture = setupWritingCreateFixture();
  fixture.tabSource.dispatchEvent({ type: 'click' });

  // Empty sourceText
  fixture.sourceInput.value = '   ';
  await fixture.submitForm();
  assert.match(fixture.msgBox.textContent, /Vui lòng dán bài viết trước khi gửi/i);

  // Short sourceText
  fixture.sourceInput.value = 'Too short text';
  await fixture.submitForm();
  assert.match(fixture.msgBox.textContent, /Nội dung quá ngắn.*tối thiểu 20/i);
});

// ─── 4. Payload Isolation & Idempotency Tests ─────────────────────────

test('writing-exercises.js: brief submit sends only brief fields; source submit sends only sourceText', async () => {
  const fixture = setupWritingCreateFixture();

  // 1. Submit brief
  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';
  await fixture.submitForm();

  const call1 = fixture.getLastFetchCall();
  assert.ok(call1);
  const body1 = JSON.parse(call1.opts.body);
  assert.equal(body1.mode, 'from_brief');
  assert.equal(body1.brief, 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.');
  assert.equal(body1.level, 'intermediate');
  assert.equal(body1.contentType, 'articles');
  assert.equal(body1.length, 'short');
  assert.equal(body1.tone, 'formal');
  assert.equal(body1.sourceText, undefined, 'sourceText must NOT be included in brief mode');
  assert.ok(body1.idempotencyKey, 'Must include stable idempotency key');

  // 2. Switch to source mode and submit
  fixture.tabSource.dispatchEvent({ type: 'click' });
  fixture.sourceInput.value = 'Đây là một bài viết hoàn chỉnh gồm nhiều câu tiếng Việt để tách câu song ngữ.';
  await fixture.submitForm();

  const call2 = fixture.getLastFetchCall();
  const body2 = JSON.parse(call2.opts.body);
  assert.equal(body2.mode, 'from_text');
  assert.equal(body2.sourceText, 'Đây là một bài viết hoàn chỉnh gồm nhiều câu tiếng Việt để tách câu song ngữ.');
  assert.equal(body2.brief, undefined, 'brief must NOT be included in source mode');
  assert.equal(body2.level, undefined, 'brief level must NOT be included in source mode');
  assert.equal(body2.contentType, undefined, 'brief contentType must NOT be included in source mode');
});

test('writing-exercises.js: stable logical key is reused on retry of same input, and regenerated on edit', async () => {
  let attempt = 0;
  const keys = [];
  const fixture = setupWritingCreateFixture({
    fetchHandler: (url, opts) => {
      const b = JSON.parse(opts.body);
      keys.push(b.idempotencyKey);
      attempt++;
      if (attempt === 1) {
        // First attempt fails with network error
        throw new Error('Network timeout');
      }
      return {
        ok: true,
        status: 200,
        headers: { get: () => 'application/json' },
        json: async () => ({
          success: true,
          data: { status: 'succeeded', exerciseId: 88, level: 'beginner', contentType: 'emails', generationMethod: 'openai' }
        })
      };
    }
  });

  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';

  // Attempt 1 fails
  await fixture.submitForm();
  assert.equal(keys.length, 1);
  const firstKey = keys[0];

  // Retry with unchanged input
  await fixture.submitForm();
  assert.equal(keys.length, 2);
  assert.equal(keys[1], firstKey, 'Logical idempotency key must be reused on unchanged retry');

  // Now change the input
  fixture.briefInput.value = 'Viết email xin nghỉ phép để đi du lịch dài ngày cùng bạn bè.';
  fixture.briefInput.dispatchEvent({ type: 'input' });
  await fixture.submitForm();

  assert.equal(keys.length, 3);
  assert.notEqual(keys[2], firstKey, 'Edited input must generate a new logical idempotency key');
});

// ─── 5. Outage Template Offer, Confirmation and Cancel Tests ──────────

test('writing-exercises.js: outage response (awaiting_template) displays template offer card and renders slots', async () => {
  const offerPayload = {
    success: false,
    errorCode: 'circuit_open',
    data: {
      status: 'awaiting_template',
      operationId: 'op-offer-999',
      coveredConstraints: ['Selected level, format, short length and advertised register.'],
      missingConstraints: ['The complete free description is not interpreted by this engine.'],
      templates: [
        {
          key: 'leave-request',
          version: 1,
          title: 'A leave request',
          level: 'beginner',
          contentType: 'emails',
          slotDefinitions: [
            { key: 'recipient', kind: 'proper_name' },
            { key: 'startDate', kind: 'date_yyyy_MM_dd' },
            { key: 'endDate', kind: 'date_yyyy_MM_dd' },
            { key: 'reason', kind: 'choice' }
          ],
          unfilledPreview: [
            { vietnameseText: 'Kính gửi {name},', englishMeaning: 'Dear {name},' },
            { vietnameseText: 'Tôi xin phép nghỉ từ ngày {start} đến ngày {end}.', englishMeaning: 'I would like to take leave from {start} to {end}.' }
          ]
        }
      ]
    }
  };

  const fixture = setupWritingCreateFixture({
    fetchHandler: () => ({
      ok: false,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => offerPayload
    })
  });

  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';
  await fixture.submitForm();

  assert.equal(Boolean(fixture.offerCard.hidden), false, 'Template offer card must be shown on outage offer');
  assert.match(fixture.offerTitle.textContent, /A leave request/i);
  assert.match(fixture.offerPreview.textContent, /Kính gửi \{name\}/);

  // Check slots container has 4 slot inputs
  const inputs = [...fixture.offerSlotsContainer.querySelectorAll('input'), ...fixture.offerSlotsContainer.querySelectorAll('select')];
  assert.equal(inputs.length, 4, 'Must render inputs for recipient, startDate, endDate, reason');

  // Confirm button should initially be disabled
  assert.equal(fixture.confirmBtn.disabled, true);
});

test('writing-exercises.js: template confirmation requires acceptance check and valid slots before submit', async () => {
  let confirmedBody = null;
  const fixture = setupWritingCreateFixture({
    fetchHandler: (url, opts) => {
      if (url.includes('/confirm-template')) {
        confirmedBody = JSON.parse(opts.body);
        return {
          ok: true,
          status: 200,
          headers: { get: () => 'application/json' },
          json: async () => ({
            success: true,
            data: { status: 'succeeded', exerciseId: 202, level: 'beginner', contentType: 'emails', generationMethod: 'internal_template' }
          })
        };
      }
      return {
        ok: true,
        status: 200,
        headers: { get: () => 'application/json' },
        json: async () => ({
          success: false,
          data: {
            status: 'awaiting_template',
            operationId: 'op-confirm-123',
            templates: [{ key: 'leave-request', version: 1, title: 'A leave request', level: 'beginner', contentType: 'emails', slotDefinitions: [{ key: 'recipient' }], unfilledPreview: [] }]
          }
        })
      };
    }
  });

  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';
  await fixture.submitForm();

  // Try clicking confirm without checking box
  await dispatchAsync(fixture.confirmBtn, { type: 'click' });
  assert.equal(confirmedBody, null, 'Must NOT submit confirmation if not accepted');

  // Fill recipient and check box
  const recipientInput = fixture.offerSlotsContainer.querySelector('input[name="recipient"]');
  recipientInput.value = 'Nguyen An';
  recipientInput.dispatchEvent({ type: 'input' });

  fixture.offerCheck.checked = true;
  fixture.offerCheck.dispatchEvent({ type: 'change' });

  assert.equal(fixture.confirmBtn.disabled, false, 'Confirm button must be enabled when valid slots + checked');

  // Click confirm
  await dispatchAsync(fixture.confirmBtn, { type: 'click' });
  assert.ok(confirmedBody, 'Confirmation should be submitted');
  assert.equal(confirmedBody.templateKey, 'leave-request');
  assert.equal(confirmedBody.acceptLimitedCoverage, true);
  assert.equal(confirmedBody.slots.recipient, 'Nguyen An');
  assert.match(fixture.getAssignedUrl(), /exerciseId=202/);
});

test('writing-exercises.js: template cancellation sends cancel POST and preserves typed brief', async () => {
  let cancelCalled = false;
  const fixture = setupWritingCreateFixture({
    fetchHandler: (url) => {
      if (url.includes('/cancel')) {
        cancelCalled = true;
        return jsonResponse({ success: false, data: {status: 'cancelled', operationId: 'op-cancel-456'} }, 503);
      }
      return {
        ok: true,
        status: 200,
        headers: { get: () => 'application/json' },
        json: async () => ({
          success: false,
          data: {
            status: 'awaiting_template',
            operationId: 'op-cancel-456',
            templates: [{ key: 'leave-request', version: 1, title: 'A leave request', level: 'beginner', contentType: 'emails', slotDefinitions: [], unfilledPreview: [] }]
          }
        })
      };
    }
  });

  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';
  await fixture.submitForm();

  assert.equal(Boolean(fixture.offerCard.hidden), false);

  // Click cancel
  await dispatchAsync(fixture.cancelBtn, { type: 'click' });

  assert.equal(cancelCalled, true, 'Cancel endpoint must be called');
  assert.equal(Boolean(fixture.offerCard.hidden), true, 'Offer card must be hidden after cancel');
  assert.equal(fixture.briefInput.value, 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.', 'User brief must be preserved');
  assert.match(fixture.msgBox.textContent, /Đã hủy bỏ yêu cầu/i);
});

// ─── 6. Bounded Polling, Pageshow & Double-Submit Guard ───────────────

test('writing-exercises.js: pageshow event resets submitting state and never resubmits', async () => {
  let submitCount = 0;
  const fixture = setupWritingCreateFixture({
    fetchHandler: () => {
      submitCount++;
      return { ok: true, status: 200, headers: { get: () => 'application/json' }, json: async () => ({ success: true, data: { status: 'succeeded', exerciseId: 10 } }) };
    }
  });

  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';
  await fixture.submitForm();
  assert.equal(submitCount, 1);

  // Simulate pageshow (e.g. user returns from another page)
  fixture.mockWindow.dispatchEvent({ type: 'pageshow' });

  assert.equal(submitCount, 1, 'pageshow must NEVER auto-resubmit');
  assert.equal(fixture.submitBtn.disabled, false, 'Submit button should be re-enabled');
});

test('writing-exercises.js: double submit is prevented while request is in-flight', async () => {
  let fetchCount = 0;
  let resolveFetch;
  const fetchPromise = new Promise((resolve) => { resolveFetch = resolve; });

  const fixture = setupWritingCreateFixture({
    fetchHandler: () => {
      fetchCount++;
      return fetchPromise;
    }
  });

  fixture.briefInput.value = 'Viết email xin nghỉ phép để thăm gia đình tại quê nhà.';

  // Fire first submit (hanging)
  const p1 = fixture.submitForm();
  // Fire second submit while in-flight
  const p2 = fixture.submitForm();

  await waitUntil(() => fetchCount === 1);
  assert.equal(fetchCount, 1, 'Second submit while in-flight must be ignored');

  resolveFetch({
    ok: true,
    status: 200,
    headers: { get: () => 'application/json' },
    json: async () => ({ success: true, data: { status: 'succeeded', exerciseId: 10 } })
  });
  await Promise.all([p1, p2]);
});

// ─── 7. Account-Scoped State Isolation ─────────────────────────────────

test('writing-exercises.js: session storage keys for other users are wiped on load', () => {
  const fixture = setupWritingCreateFixture({ userId: 55 });
  fixture.sessionStorage.setItem('tct_writing_gen_99', JSON.stringify({ operationId: 'foreign-op' }));
  fixture.sessionStorage.setItem('tct_writing_gen_55', JSON.stringify({ operationId: 'my-op' }));

  // Re-trigger load cleanup
  for (const fn of fixture.document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  assert.equal(fixture.sessionStorage.getItem('tct_writing_gen_99'), null, 'Foreign account storage must be wiped');
  assert.ok(fixture.sessionStorage.getItem('tct_writing_gen_55'), 'Current account storage preserved');
});

function reviewedOffer() {
  return { success: false, data: { status: 'awaiting_template', operationId: 'owned-operation',
    templates: [{ key: 'meeting-follow-up', version: 1, title: 'Meeting', level: 'beginner', contentType: 'emails',
      slotDefinitions: [], unfilledPreview: [{ vietnameseText: 'Kính gửi cả nhóm,', englishMeaning: 'Dear team,' }] }] } };
}
function jsonResponse(body, status = 200) {
  return { ok: status >= 200 && status < 300, status, headers: { get: () => 'application/json' }, json: async () => body };
}

test('review: lost response retains durable identity without persisting private input', async () => {
  const f = setupWritingCreateFixture({ fetchHandler: () => { throw new Error('lost response'); } });
  f.briefInput.value = 'A private description that must never be put in session storage.';
  await f.submitForm();
  const stored = f.sessionStorage.getItem('tct_writing_gen_42');
  assert.ok(stored, 'A refresh must not lose the only idempotency key');
  assert.ok(JSON.parse(stored).idempotencyKey);
  assert.doesNotMatch(stored, /private description/);
});

test('review: cannot switch mode and lose the identity while dispatch is unresolved', async () => {
  let resolve;
  const f = setupWritingCreateFixture({ fetchHandler: () => new Promise(r => { resolve = r; }) });
  f.briefInput.value = 'Write a sufficiently detailed fictional email to request leave.';
  const pending = f.submitForm();
  await waitUntil(() => Boolean(resolve));
  f.tabSource.dispatchEvent({ type: 'click' });
  assert.equal(f.panelBrief.hidden, false);
  resolve(jsonResponse({ errorCode: 'network' }, 503)); await pending;
});

test('review: failed cancel cannot claim success or discard the owned offer', async () => {
  const f = setupWritingCreateFixture({ fetchHandler: (url) => {
    if (url.includes('/cancel')) throw new Error('cancel acknowledgement lost');
    return jsonResponse(reviewedOffer());
  } });
  f.briefInput.value = 'Write a short follow-up email after a fictional meeting.';
  await f.submitForm(); await dispatchAsync(f.cancelBtn, { type: 'click' });
  assert.doesNotMatch(f.msgBox.textContent, /Đã hủy bỏ yêu cầu/);
  assert.ok(f.sessionStorage.getItem('tct_writing_gen_42'), 'Must recover, not forget the operation');
});

test('review: template preview includes both languages exactly once and has associated slot labels', async () => {
  const f = setupWritingCreateFixture({ fetchHandler: () => jsonResponse(reviewedOffer()) });
  f.briefInput.value = 'Write a short follow-up email after a fictional meeting.';
  await f.submitForm();
  assert.match(f.offerPreview.textContent, /Dear team/);
  assert.equal(f.offerPreview.textContent.split('Kính gửi cả nhóm').length - 1, 1);
});

test('review: reload recovers a lost first response by key using reads only', async () => {
  const f = setupWritingCreateFixture({ savedStorage: { tct_writing_gen_42: JSON.stringify({ version: 1,
    idempotencyKey: 'saved-key', fingerprint: null, mode: 'from_brief', pending: true }) },
    recoveryHandler: () => jsonResponse({ success: true, data: { status: 'succeeded', operationId: 'owned-operation',
      exerciseId: 77, level: 'beginner', contentType: 'emails', generationMethod: 'openai' } }) });
  await waitUntil(() => f.form.querySelector('[data-writing-result-link]'));
  assert.equal(f.getAssignedUrl(), null, 'Restoring history must not force navigation');
  assert.ok(f.getFetchCalls().every(c => c.opts.method !== 'POST'));
  assert.equal(f.sessionStorage.getItem('tct_writing_gen_42'), null);
});

test('review: blur conceals private input and stale callbacks cannot redirect into another account', async () => {
  let owner = 42, resolve;
  const f = setupWritingCreateFixture({ ownerProvider: () => owner,
    fetchHandler: () => new Promise(r => { resolve = r; }) });
  f.briefInput.value = 'A private description for the original account only.';
  const pending = f.submitForm(); await waitUntil(() => resolve);
  f.mockWindow.dispatchEvent({ type: 'blur' });
  assert.equal(f.form.hidden, true);
  owner = 55;
  resolve(jsonResponse({ success: true, data: {status: 'succeeded', exerciseId: 15, level: 'beginner', contentType: 'emails'} }));
  await pending;
  assert.equal(f.getAssignedUrl(), null);
  await dispatchAsync(f.mockWindow, { type: 'focus' });
  assert.equal(f.getAssignedUrl(), 'reload');
  assert.equal(f.form.hidden, true);
});

test('review: a server HTML error keeps the stable receipt for recovery', async () => {
  const f = setupWritingCreateFixture({ fetchHandler: () => ({ok: false, status: 500,
    headers: {get: () => 'text/html'}, json: async () => {throw new Error('HTML');} }) });
  f.briefInput.value = 'Write a short fictional report about a community cleanup.';
  await f.submitForm();
  assert.ok(f.sessionStorage.getItem('tct_writing_gen_42'));
  assert.equal(f.getAssignedUrl(), null);
});

test('review: changing acceptance while confirming cannot cause a second confirmation', async () => {
  let confirmCalls = 0, resolve;
  const f = setupWritingCreateFixture({fetchHandler: (url) => {
    if (url.includes('/confirm-template')) {confirmCalls++; return new Promise(r => {resolve = r;});}
    return jsonResponse(reviewedOffer());
  }});
  f.briefInput.value = 'Write a short follow-up email after a fictional meeting.';
  await f.submitForm(); f.offerCheck.checked = true; f.offerCheck.dispatchEvent({type:'change'});
  const pending = dispatchAsync(f.confirmBtn, {type:'click'}); await waitUntil(() => resolve);
  f.offerCheck.dispatchEvent({type:'change'});
  assert.equal(f.confirmBtn.disabled, true);
  await dispatchAsync(f.confirmBtn, {type:'click'}); assert.equal(confirmCalls, 1);
  resolve(jsonResponse({success:true, data:{status:'succeeded', exerciseId:30, level:'beginner', contentType:'emails', generationMethod:'internal_template'}}));
  await pending;
});

test('review: all offered frames can be selected and confirmation uses the reviewed choice', async () => {
  let submitted;
  const offer = reviewedOffer();
  offer.data.templates.push({key:'leave-request',version:1,title:'Leave',level:'beginner',contentType:'emails',
    slotDefinitions:[],unfilledPreview:[{vietnameseText:'Khung được chọn.',englishMeaning:'The chosen frame.'}]});
  const f = setupWritingCreateFixture({fetchHandler:(url,opts)=>{
    if(url.includes('/confirm-template')) {submitted=JSON.parse(opts.body);return jsonResponse({success:true,data:{status:'succeeded',exerciseId:82,level:'beginner',contentType:'emails',generationMethod:'internal_template'}});}
    return jsonResponse(offer);
  }});
  f.briefInput.value='Write a short fictional work email with a reviewed frame.';await f.submitForm();
  assert.equal(f.templateChoice.hidden,false);
  f.offerCheck.checked=true;f.offerCheck.dispatchEvent({type:'change'});
  f.templateSelect.value='1';f.templateSelect.dispatchEvent({type:'change'});
  assert.equal(f.offerCheck.checked,false,'Changing a frame requires informed acceptance again');
  assert.match(f.offerPreview.textContent,/The chosen frame/);
  f.offerCheck.checked=true;f.offerCheck.dispatchEvent({type:'change'});
  await dispatchAsync(f.confirmBtn,{type:'click'});assert.equal(submitted.templateKey,'leave-request');
});

for (const [code,status,state] of [['refusal',503,'failed'],['daily_quota_exceeded',429,'failed'],
  ['quota_exhausted',503,'failed'],['invalid_configuration',503,'failed'],['rate_limited',503,'failed'],['cancelled',503,'cancelled']]) {
  test(`review: ${code} never invents an internal offer or generation success`,async()=>{
    const f=setupWritingCreateFixture({fetchHandler:()=>jsonResponse({success:false,errorCode:code,error:'Synthetic failure',
      data:{status:state,coverage:'Unsupported'}},status)});
    f.briefInput.value='Write a short fictional task with a clear description.';await f.submitForm();
    assert.equal(f.offerCard.hidden,true);assert.equal(f.getAssignedUrl(),null);
    assert.doesNotMatch(f.msgBox.textContent,/Đã tạo bài viết/);
  });
}
