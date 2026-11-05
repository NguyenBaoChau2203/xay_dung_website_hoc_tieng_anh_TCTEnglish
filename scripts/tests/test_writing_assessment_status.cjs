'use strict';

/**
 * NeedsReview must keep its own label through normalize + sentence status UI.
 * Uses production writing.js helpers via a practice mount (same harness as drafts).
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
    get length() {
      return map.size;
    },
  };
}

function mountWithLastEvaluation(lastEvaluation) {
  const { document, byId, body } = createDocument(PRACTICE_IDS);
  const storage = createMemoryStorage();

  const root = createElement('div');
  root.setAttribute('data-writing-practice', '');
  root.setAttribute('data-owner-user-id', '11');
  root.setAttribute('data-exercise-id', '42');
  root.setAttribute('data-resume-sentence-id', '7');
  root.setAttribute('data-hint-url', '/Home/WritingHint');
  root.setAttribute('data-evaluate-url', '/Home/EvaluateWritingSentence');

  const csrf = createElement('input');
  csrf.name = '__RequestVerificationToken';
  csrf.value = 'test-csrf';
  csrf.setAttribute('name', '__RequestVerificationToken');
  root.appendChild(csrf);

  const sentenceItem = createElement('button');
  sentenceItem.setAttribute('data-sentence-item', '');
  sentenceItem.setAttribute('data-sentence-id', '7');
  const sentenceText = createElement('span');
  sentenceText.setAttribute('data-sentence-text', '');
  sentenceItem.appendChild(sentenceText);
  body.appendChild(sentenceItem);

  for (const id of PRACTICE_IDS) {
    root.appendChild(byId[id]);
  }
  body.appendChild(root);

  byId.writingPracticeData.textContent = JSON.stringify([
    {
      id: 7,
      number: 1,
      vietnameseText: 'Xin chào',
      placeholder: 'Type here',
      breakAfter: false,
      attemptCount: 1,
      lastSubmittedAnswer: 'Hello there',
      acceptedAnswer: '',
      hasAccepted: false,
      lastEvaluationPassed: false,
      isCompleted: false,
      acceptedText: '',
      lastEvaluation,
    },
  ]);
  byId.answerInput.tagName = 'TEXTAREA';
  byId.sessionExpiredPrompt.hidden = true;

  const windowObj = {
    document,
    localStorage: storage,
    location: { reload() {} },
    console,
    setTimeout() { return 0; },
    clearTimeout() {},
  };
  windowObj.window = windowObj;

  loadScript(WRITING_JS, {
    window: windowObj,
    document,
    localStorage: storage,
    console,
    setTimeout: windowObj.setTimeout,
    clearTimeout: windowObj.clearTimeout,
  });

  for (const fn of document._listeners.DOMContentLoaded || []) {
    fn({ type: 'DOMContentLoaded' });
  }

  return { byId, sentenceItem };
}

test('NeedsReview reload payload shows Cần xem lại, not Làm lại', () => {
  const { byId, sentenceItem } = mountWithLastEvaluation({
    passed: false,
    usedAi: false,
    evaluationSource: 'local-review',
    assessmentStatus: 'NeedsReview',
    assessmentSource: 'local-review',
    reviewRequired: true,
    summaryTitle: 'Cần xem lại câu này',
    summaryText: 'Câu chưa khớp đáp án đã duyệt.',
    reviewText: '',
    meaningFeedback: '',
    grammarFeedback: '',
    naturalnessFeedback: '',
    wordChoiceFeedback: '',
    suggestedRewrite: '',
  });

  assert.match(sentenceItem.textContent || sentenceItem.getAttribute('aria-label') || '', /Cần xem lại|xem lại/i);
  assert.doesNotMatch(
    `${sentenceItem.textContent || ''} ${sentenceItem.getAttribute('aria-label') || ''}`,
    /Làm lại/
  );
  assert.match(byId.feedbackTitle.textContent, /Cần xem lại/i);
});

test('NeedsRevision reload payload keeps Làm lại label', () => {
  const { sentenceItem } = mountWithLastEvaluation({
    passed: false,
    usedAi: false,
    evaluationSource: 'local-hard-fail',
    assessmentStatus: 'NeedsRevision',
    assessmentSource: 'local-hard-fail',
    reviewRequired: false,
    summaryTitle: 'Cần có từ tiếng Anh',
    summaryText: 'Hãy dùng ít nhất một từ tiếng Anh.',
    reviewText: '',
    meaningFeedback: '',
    grammarFeedback: '',
    naturalnessFeedback: '',
    wordChoiceFeedback: '',
    suggestedRewrite: '',
  });

  assert.match(
    `${sentenceItem.textContent || ''} ${sentenceItem.getAttribute('aria-label') || ''}`,
    /Làm lại/
  );
});
