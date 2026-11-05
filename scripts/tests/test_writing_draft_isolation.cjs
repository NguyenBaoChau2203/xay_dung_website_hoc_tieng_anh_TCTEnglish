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

function mountPractice({ ownerUserId, exerciseId, sentenceId, storage, sentences }) {
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
  };

  loadScript(WRITING_JS, sandbox);
  const listeners = document._listeners && document._listeners.DOMContentLoaded;
  assert.ok(listeners && listeners.length > 0, 'writing.js must register DOMContentLoaded');
  for (const fn of listeners) {
    fn({ type: 'DOMContentLoaded' });
  }

  return { document, byId, root, storage, timers };
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
