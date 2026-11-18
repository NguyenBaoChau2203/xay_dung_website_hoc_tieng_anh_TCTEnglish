'use strict';

/**
 * Production writing.js draft helpers must namespace by server-verified owner.
 * Shared localStorage across two mounts simulates A logout → B login same profile.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');

const WRITING_JS = path.join(
  __dirname,
  '..',
  '..',
  'TCTEnglish',
  'wwwroot',
  'js',
  'writing.js'
);

const PRACTICE_IDS = [
  'writingPracticeData',
  'progressFill',
  'progressText',
  'exerciseStatusLabel',
  'exerciseAttemptCount',
  'selectionNote',
  'selectedSentenceCounter',
  'selectedSentenceText',
  'answerInput',
  'feedbackBanner',
  'feedbackIcon',
  'feedbackTitle',
  'feedbackText',
  'feedbackSourceNote',
  'sessionExpiredPrompt',
  'btnReload',
  'feedbackSentenceMeta',
  'feedbackSourceText',
  'feedbackSubmissionText',
  'feedbackReviewText',
  'feedbackReferenceText',
  'feedbackHintText',
  'btnPrevious',
  'btnNext',
  'btnHint',
  'btnSubmit',
  'btnSubmitText',
  'draftIndicator',
];

function createMemoryStorage() {
  const map = new Map();
  return {
    getItem(key) {
      return map.has(String(key)) ? map.get(String(key)) : null;
    },
    setItem(key, value) {
      map.set(String(key), String(value));
    },
    removeItem(key) {
      map.delete(String(key));
    },
    clear() {
      map.clear();
    },
    key(index) {
      return [...map.keys()][index] ?? null;
    },
    get length() {
      return map.size;
    },
    keys() {
      return [...map.keys()];
    },
  };
}

function mountPractice({ ownerUserId, exerciseId, sentenceId, storage, sentences, fetch }) {
  const { document, byId, body } = createDocument(PRACTICE_IDS);

  const root = createElement('div');
  root.setAttribute('data-writing-practice', '');
  if (ownerUserId != null && ownerUserId !== '') {
    root.setAttribute('data-owner-user-id', String(ownerUserId));
  }
  root.setAttribute('data-exercise-id', String(exerciseId));
  root.setAttribute('data-resume-sentence-id', String(sentenceId));
  root.setAttribute('data-hint-url', '/Home/WritingHint');
  root.setAttribute('data-evaluate-url', '/Home/EvaluateWritingSentence');

  const csrf = createElement('input');
  csrf.name = '__RequestVerificationToken';
  csrf.value = 'test-csrf';
  csrf.setAttribute('name', '__RequestVerificationToken');
  root.appendChild(csrf);

  const sentenceItem = createElement('button');
  sentenceItem.setAttribute('data-sentence-item', '');
  sentenceItem.setAttribute('data-sentence-id', String(sentenceId));
  body.appendChild(sentenceItem);
  const sentenceItems = [sentenceItem];
  for (const sentence of sentences || []) {
    if (String(sentence.id) === String(sentenceId)) continue;
    const item = createElement('button');
    item.setAttribute('data-sentence-item', '');
    item.setAttribute('data-sentence-id', String(sentence.id));
    const text = createElement('span');
    text.setAttribute('data-sentence-text', '');
    item.appendChild(text);
    body.appendChild(item);
    sentenceItems.push(item);
  }

  // Move generated id nodes under the practice root where the script expects them.
  for (const id of PRACTICE_IDS) {
    root.appendChild(byId[id]);
  }
  body.appendChild(root);

  const payload = sentences || [
    {
      id: Number(sentenceId),
      number: 1,
      vietnameseText: 'Xin chào',
      placeholder: 'Type here',
      breakAfter: false,
      attemptCount: 0,
      lastSubmittedAnswer: '',
      acceptedAnswer: '',
      hasAccepted: false,
      lastEvaluationPassed: null,
      isCompleted: false,
      acceptedText: '',
      lastEvaluation: null,
    },
  ];
  byId.writingPracticeData.textContent = JSON.stringify(payload);
  byId.answerInput.tagName = 'TEXTAREA';
  byId.exerciseStatusLabel.textContent = 'Chưa bắt đầu';
  byId.exerciseAttemptCount.textContent = '0 lượt gửi';
  byId.answerInput.focus = () => {};
  byId.sessionExpiredPrompt.hidden = true;

  const timers = [];
  const windowObj = {
    document,
    localStorage: storage,
    location: { reload() {} },
    console,
    setTimeout(fn, ms) {
      const id = { fn, ms };
      timers.push(id);
      return id;
    },
    clearTimeout(id) {
      const idx = timers.indexOf(id);
      if (idx >= 0) timers.splice(idx, 1);
    },
  };
  windowObj.window = windowObj;

  const sandbox = {
    window: windowObj,
    document,
    localStorage: storage,
    console,
    setTimeout: windowObj.setTimeout,
    clearTimeout: windowObj.clearTimeout,
    fetch,
  };

  loadScript(WRITING_JS, sandbox);
  const listeners = document._listeners && document._listeners.DOMContentLoaded;
  assert.ok(listeners && listeners.length > 0, 'writing.js must register DOMContentLoaded');
  for (const fn of listeners) {
    fn({ type: 'DOMContentLoaded' });
  }

  return { document, byId, root, storage, timers, sentenceItem, sentenceItems };
}

function typeDraft(byId, text) {
  byId.answerInput.value = text;
  const listeners = byId.answerInput._listeners && byId.answerInput._listeners.input;
  assert.ok(listeners && listeners.length > 0, 'input listener must be registered');
  for (const fn of listeners) {
    fn({ type: 'input', target: byId.answerInput });
  }
}

test('A draft is not restored for B on the same exercise/sentence storage', () => {
  const storage = createMemoryStorage();
  const exerciseId = 42;
  const sentenceId = 7;

  const userA = mountPractice({
    ownerUserId: 11,
    exerciseId,
    sentenceId,
    storage,
  });
  typeDraft(userA.byId, 'Draft belonging to user A');
  assert.match(
    storage.keys().join(','),
    /11/,
    'owned draft key must include owner id after save'
  );

  const userB = mountPractice({
    ownerUserId: 22,
    exerciseId,
    sentenceId,
    storage,
  });

  assert.notEqual(
    userB.byId.answerInput.value,
    'Draft belonging to user A',
    'user B must not see user A draft'
  );
  assert.equal(userB.byId.answerInput.value, '');
});

function submitShortcut(mounted) {
  mounted.byId.answerInput.dispatchEvent({ type: 'keydown', ctrlKey: true, key: 'Enter', preventDefault() {} });
}

function pendingAssessment() {
  let resolve;
  let calls = 0;
  const promise = new Promise(r => { resolve = r; });
  return {
    fetch() { calls++; return promise; },
    get calls() { return calls; },
    accept() { resolve({ ok: true, status: 200, json: async () => ({ success: true, data: {
      passed: true, canAutoAdvance: true, assessmentStatus: 'Accepted', sentenceId: 7
    } }) }); }
  };
}

test('a saved response updates the exercise header from server totals, including other-tab completions', async () => {
  const mounted = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7,
    storage: createMemoryStorage(),
    sentences: [7, 8].map((id, index) => ({ id, number: index + 1, vietnameseText: 'Câu ' + id, hasAccepted: false })),
    fetch: async () => ({ ok: true, status: 200, json: async () => ({ success: true, data: {
      passed: true, canAutoAdvance: true, assessmentStatus: 'Accepted', sentenceId: 7,
      sentenceAttemptCount: 3, exerciseAttemptCount: 11, completedSentenceCount: 2,
      exerciseStatusKey: 'completed', exerciseStatusLabel: 'Hoàn thành'
    } }) }) });
  typeDraft(mounted.byId, 'Hello!');
  submitShortcut(mounted);
  await new Promise(setImmediate);
  assert.equal(mounted.byId.exerciseStatusLabel.textContent, 'Hoàn thành');
  assert.equal(mounted.byId.exerciseAttemptCount.textContent, '11 lượt gửi');
  assert.equal(mounted.byId.progressText.textContent, '2/2 câu đã xong');
});

test('a persisted revision updates attempt/status labels without inventing accepted progress', async () => {
  const mounted = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7,
    storage: createMemoryStorage(),
    fetch: async () => ({ ok: true, status: 200, json: async () => ({ success: true, data: {
      passed: false, canAutoAdvance: false, assessmentStatus: 'NeedsRevision', sentenceId: 7,
      sentenceAttemptCount: 1, exerciseAttemptCount: 4, completedSentenceCount: 0,
      exerciseStatusKey: 'in-progress', exerciseStatusLabel: 'Đang luyện'
    } }) }) });
  typeDraft(mounted.byId, 'An incorrect answer.');
  submitShortcut(mounted);
  await new Promise(setImmediate);
  assert.equal(mounted.byId.exerciseStatusLabel.textContent, 'Đang luyện');
  assert.equal(mounted.byId.exerciseAttemptCount.textContent, '4 lượt gửi');
  assert.equal(mounted.byId.progressText.textContent, '0/1 câu đã xong');
});

test('429 and offline failures keep the exercise header and draft unchanged and allow retry', async () => {
  for (const offline of [false, true]) {
    const storage = createMemoryStorage();
    const mounted = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7, storage,
      fetch: async () => { if (offline) throw new Error('offline');
        return { ok: false, status: 429, headers: { get: () => '1' } }; } });
    typeDraft(mounted.byId, 'My unsent draft');
    submitShortcut(mounted);
    await new Promise(setImmediate);
    assert.equal(mounted.byId.exerciseStatusLabel.textContent, 'Chưa bắt đầu');
    assert.equal(mounted.byId.exerciseAttemptCount.textContent, '0 lượt gửi');
    assert.equal(mounted.byId.progressText.textContent, '0/1 câu đã xong');
    assert.equal(mounted.byId.btnSubmit.disabled, false);
    assert.equal(storage.getItem('wp_draft_u11_ex42_7'), 'My unsent draft');
  }
});

test('Ctrl+Enter while a submit is pending sends only one request', async () => {
  const pending = pendingAssessment();
  const mounted = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7,
    storage: createMemoryStorage(), fetch: pending.fetch });
  typeDraft(mounted.byId, 'Hello!');
  submitShortcut(mounted);
  submitShortcut(mounted);
  assert.equal(pending.calls, 1);
  pending.accept();
  await new Promise(setImmediate);
});

test('accepting an older submission preserves newer typing and its stored draft', async () => {
  const pending = pendingAssessment();
  const storage = createMemoryStorage();
  const mounted = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7,
    storage, fetch: pending.fetch });
  typeDraft(mounted.byId, 'Hello!');
  submitShortcut(mounted);
  typeDraft(mounted.byId, 'My newer draft');
  pending.accept();
  await new Promise(setImmediate);
  assert.equal(mounted.byId.answerInput.value, 'My newer draft');
  assert.equal(storage.getItem('wp_draft_u11_ex42_7'), 'My newer draft');
});

test('an older tab accepting a sentence cannot erase another tab draft', async () => {
  const pending = pendingAssessment();
  const storage = createMemoryStorage();
  const oldTab = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7,
    storage, fetch: pending.fetch });
  typeDraft(oldTab.byId, 'Hello!');
  submitShortcut(oldTab);
  const newTab = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7, storage });
  typeDraft(newTab.byId, 'Draft from another tab');
  pending.accept();
  await new Promise(setImmediate);
  assert.equal(storage.getItem('wp_draft_u11_ex42_7'), 'Draft from another tab');
});

test('a pending response does not navigate away from a newly selected sentence', async () => {
  const pending = pendingAssessment();
  const mounted = mountPractice({ ownerUserId: 11, exerciseId: 42, sentenceId: 7,
    storage: createMemoryStorage(), fetch: pending.fetch,
    sentences: [7, 8, 9].map((id, index) => ({ id, number: index + 1,
      vietnameseText: 'Câu ' + id, hasAccepted: false })) });
  typeDraft(mounted.byId, 'Hello!');
  submitShortcut(mounted);
  mounted.sentenceItems[1].dispatchEvent({ type: 'click' });
  typeDraft(mounted.byId, 'Newly selected sentence draft');
  pending.accept();
  await new Promise(setImmediate);
  assert.equal(mounted.byId.selectedSentenceText.textContent, 'Câu 8');
  assert.equal(mounted.byId.answerInput.value, 'Newly selected sentence draft');
  assert.equal(mounted.byId.progressText.textContent, '1/3 câu đã xong');
});

test('legacy unowned draft key is ignored for current accounts', () => {
  const storage = createMemoryStorage();
  storage.setItem('wp_draft_ex42_7', 'Legacy unowned draft');

  const userA = mountPractice({
    ownerUserId: 11,
    exerciseId: 42,
    sentenceId: 7,
    storage,
  });
  assert.equal(userA.byId.answerInput.value, '');
  assert.equal(storage.getItem('wp_draft_ex42_7'), 'Legacy unowned draft');
});

test('missing owner does not create a new global draft key and still allows typing', () => {
  const storage = createMemoryStorage();
  const mounted = mountPractice({
    ownerUserId: '',
    exerciseId: 42,
    sentenceId: 7,
    storage,
  });

  typeDraft(mounted.byId, 'Ephemeral typing without owner scope');
  assert.equal(mounted.byId.answerInput.value, 'Ephemeral typing without owner scope');
  assert.equal(storage.length, 0, 'no global/unowned draft key may be written');
});

test('owner namespaces for two sentences do not collide', () => {
  const storage = createMemoryStorage();

  const s1 = mountPractice({
    ownerUserId: 11,
    exerciseId: 42,
    sentenceId: 7,
    storage,
  });
  typeDraft(s1.byId, 'Sentence seven draft');

  const s2 = mountPractice({
    ownerUserId: 11,
    exerciseId: 42,
    sentenceId: 8,
    storage,
    sentences: [
      {
        id: 8,
        number: 2,
        vietnameseText: 'Tạm biệt',
        placeholder: 'Type here',
        breakAfter: false,
        attemptCount: 0,
        lastSubmittedAnswer: '',
        acceptedAnswer: '',
        hasAccepted: false,
        lastEvaluationPassed: null,
        isCompleted: false,
        acceptedText: '',
        lastEvaluation: null,
      },
    ],
  });
  typeDraft(s2.byId, 'Sentence eight draft');

  const reloadSeven = mountPractice({
    ownerUserId: 11,
    exerciseId: 42,
    sentenceId: 7,
    storage,
  });
  assert.equal(reloadSeven.byId.answerInput.value, 'Sentence seven draft');

  const reloadEight = mountPractice({
    ownerUserId: 11,
    exerciseId: 42,
    sentenceId: 8,
    storage,
    sentences: [
      {
        id: 8,
        number: 2,
        vietnameseText: 'Tạm biệt',
        placeholder: 'Type here',
        breakAfter: false,
        attemptCount: 0,
        lastSubmittedAnswer: '',
        acceptedAnswer: '',
        hasAccepted: false,
        lastEvaluationPassed: null,
        isCompleted: false,
        acceptedText: '',
        lastEvaluation: null,
      },
    ],
  });
  assert.equal(reloadEight.byId.answerInput.value, 'Sentence eight draft');
});
