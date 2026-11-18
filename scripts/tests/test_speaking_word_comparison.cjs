'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement, loadScript, delay } = require('./dom_fixture.cjs');

const SPEAKING_JS = path.join(
  __dirname,
  '..',
  '..',
  'TCTEnglish',
  'wwwroot',
  'js',
  'speaking.js'
);

test('speaking.js exposes safe word-comparison helpers without innerHTML', () => {
  const source = fs.readFileSync(SPEAKING_JS, 'utf8');
  assert.match(source, /function renderWordComparison/);
  assert.match(source, /function clearWordComparison/);
  assert.match(source, /Đối chiếu nội dung nhận diện|spk-word-comparison/);
  assert.match(source, /textContent/);
  assert.doesNotMatch(
    source.slice(source.indexOf('function renderWordComparison'), source.indexOf('function formatScoreDisplay')),
    /innerHTML\s*=/
  );
  assert.match(source, /assessBody\.comparison/);
  assert.match(source, /clearWordComparison\(\)/);
  assert.match(
    source.slice(source.indexOf('function startRecording'), source.indexOf('async function startPcmRecording')),
    /clearWordComparison\(\)/
  );
});

function buildPracticeDomWithComparison() {
  const ids = [
    'btn-prev',
    'btn-next',
    'btn-replay',
    'btn-record',
    'record-icon',
    'record-label',
    'sent-indicator',
    'current-transcript-en',
    'current-transcript-vi',
    'sentence-scroll-container',
    'btn-slider-prev',
    'btn-slider-next',
    'dic-sentence-scroll-container',
    'btn-dic-slider-prev',
    'btn-dic-slider-next',
    'btn-hide-text',
    'spk-transcript-panel',
    'spk-transcript-list',
    'prog-bar',
    'prog-text',
    'prog-pct',
    'spk-toast',
    'tab-pronunciation',
    'tab-dictation',
    'pronunciation-content',
    'dictation-content',
    'btn-dic-hide-video',
    'video-overlay',
    'youtube-player',
    'dic-input',
    'dic-feedback',
    'dic-sent-indicator',
    'btn-dic-hint',
    'btn-dic-play',
    'dic-play-icon',
    'dic-play-label',
    'btn-dic-next',
    'btn-dic-slow-play',
    'score-total',
    'score-accuracy',
    'score-fluency',
    'score-complete',
    'ring-total',
    'bar-accuracy',
    'bar-fluency',
    'bar-complete',
    'spk-assessment-status',
    'spk-word-comparison',
    'spk-word-comparison-body',
  ];
  const { document, byId, body } = createDocument(ids);

  const sent0 = createElement('button');
  sent0.classList.add('spk-sent-btn');
  sent0.dataset.index = '0';
  byId['sentence-scroll-container'].appendChild(sent0);

  const sent1 = createElement('button');
  sent1.classList.add('spk-sent-btn');
  sent1.dataset.index = '1';
  byId['sentence-scroll-container'].appendChild(sent1);

  const dic0 = createElement('button');
  dic0.classList.add('spk-sent-btn');
  dic0.dataset.dicIndex = '0';
  byId['dic-sentence-scroll-container'].appendChild(dic0);

  const dic1 = createElement('button');
  dic1.classList.add('spk-sent-btn');
  dic1.dataset.dicIndex = '1';
  byId['dic-sentence-scroll-container'].appendChild(dic1);

  byId['spk-word-comparison'].hidden = true;
  return { document, byId, body };
}

function createControllableRecorder() {
  let onComplete = null;
  let onError = null;
  const recorder = {
    isSupported: () => true,
    create: (hooks = {}) => {
      onComplete = hooks.onComplete || null;
      onError = hooks.onError || null;
      return {
        start: async () => {},
        stop: async () => {
          if (typeof onComplete === 'function') {
            onComplete(new Blob(['RIFF-fake-wav'], { type: 'audio/wav' }));
          }
        },
        dispose: () => {
          onComplete = null;
          onError = null;
        },
      };
    },
    _failStart: async () => {
      if (typeof onError === 'function') onError(new Error('recorder-error'));
    },
  };
  return recorder;
}

function createSandbox({ document, speechRecognition, speechRecorder, fetchImpl, sourceOverride }) {
  const fetchCalls = [];
  const fetch =
    fetchImpl ||
    (async (url, options = {}) => {
      fetchCalls.push({ url: String(url), options });
      return { ok: true, json: async () => ({}) };
    });

  const window = {
    SPK_VIDEO_ID: 'testVideo',
    SPK_SENTENCES: [
      {
        id: 101,
        text: 'Hello world',
        vi: 'Xin chào',
        start: 0,
        end: 2,
        totalScore: 0,
        accuracyScore: 0,
        fluencyScore: 0,
        completenessScore: 0,
        isPracticed: false,
      },
      {
        id: 102,
        text: 'Good morning',
        vi: 'Chào buổi sáng',
        start: 2,
        end: 4,
        totalScore: 0,
        accuracyScore: 0,
        fluencyScore: 0,
        completenessScore: 0,
        isPracticed: false,
      },
    ],
    TCT_SPEAKING_TEST_MODE: true,
    SpeechRecognition: speechRecognition,
    webkitSpeechRecognition: speechRecognition,
    TctSpeechRecorder: speechRecorder,
    AbortController: globalThis.AbortController,
    FormData: globalThis.FormData,
    Blob: globalThis.Blob,
    YT: {
      PlayerState: { PLAYING: 1, PAUSED: 2, ENDED: 0 },
      Player: function () {
        return {
          getPlayerState: () => 2,
          getCurrentTime: () => 0,
          seekTo() {},
          playVideo() {},
          pauseVideo() {},
          setPlaybackRate() {},
        };
      },
    },
    document,
    fetch,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    addEventListener: (...args) => document.addEventListener(...args),
  };
  window.window = window;
  window.global = window;
  window.self = window;

  const sandbox = {
    window,
    document,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    fetch,
    AbortController: globalThis.AbortController,
    FormData: globalThis.FormData,
    Blob: globalThis.Blob,
    SpeechRecognition: speechRecognition,
    webkitSpeechRecognition: speechRecognition,
    YT: window.YT,
    TctSpeechRecorder: speechRecorder,
  };
  Object.assign(sandbox, window);

  if (sourceOverride) {
    vm.runInNewContext(sourceOverride, sandbox, { filename: 'speaking.mutated.js' });
  } else {
    loadScript(SPEAKING_JS, sandbox);
  }

  return { sandbox, window, fetchCalls };
}

function chipLabels(body) {
  const labels = [];
  const walk = (node) => {
    if (!node) return;
    if (node.classList && node.classList.contains('spk-word-chip-label')) {
      labels.push(node.textContent);
    }
    for (const child of node.children || []) walk(child);
  };
  walk(body);
  return labels;
}

function countChips(body) {
  let count = 0;
  const walk = (node) => {
    if (!node) return;
    if (node.classList && node.classList.contains('spk-word-chip')) count += 1;
    for (const child of node.children || []) walk(child);
  };
  walk(body);
  return count;
}

function buildAssessFetch(sequence) {
  let assessIndex = 0;
  const fetchCalls = [];
  const fetchImpl = async (url, options = {}) => {
    const href = String(url);
    fetchCalls.push({ url: href, options });
    if (href.includes('/assess')) {
      const payload = sequence[assessIndex] || sequence[sequence.length - 1];
      assessIndex += 1;
      return {
        ok: payload.ok !== false,
        json: async () => payload.body,
      };
    }
    if (href.includes('/progress')) {
      return {
        ok: true,
        json: async () => ({
          success: true,
          totalScore: 88,
          accuracyScore: 88,
          fluencyScore: 0,
          completenessScore: 0,
          xpEarned: 0,
          firstTimeVideoCompletion: false,
        }),
      };
    }
    return { ok: true, json: async () => ({}) };
  };
  return { fetchImpl, fetchCalls, getAssessCount: () => assessIndex };
}

async function recordAndStop(byId) {
  byId['btn-record'].click();
  await delay(10);
  byId['btn-record'].click();
  await delay(40);
}

test('committed score and word comparison survive sentence 1 to 2 to 1 without another request', async () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const { fetchImpl, fetchCalls } = buildAssessFetch([{ body: {
    success: true, canCommitProgress: true, assessmentId: 'receipt-1', textMatchScore: 88,
    fluencyAvailable: false, completenessAvailable: false,
    comparison: { available: true, operations: [
      { kind: 'matched', referenceToken: 'Hello', hypothesisToken: 'Hello' },
      { kind: 'missing', referenceToken: 'world' },
    ] },
  } }]);
  const { window } = createSandbox({ document, speechRecorder: createControllableRecorder(), fetchImpl });
  await recordAndStop(byId);
  const before = chipLabels(byId['spk-word-comparison-body']);
  window.TctSpeakingPractice.selectSentence(1, false);
  assert.equal(byId['spk-word-comparison'].hidden, true);
  window.TctSpeakingPractice.selectSentence(0, false);
  assert.equal(byId['score-total'].textContent, '88');
  assert.equal(byId['score-accuracy'].textContent, '88');
  assert.equal(byId['score-fluency'].textContent, '—');
  assert.equal(byId['score-complete'].textContent, '—');
  assert.equal(byId['spk-word-comparison'].hidden, false);
  assert.deepEqual(chipLabels(byId['spk-word-comparison-body']), before);
  assert.match(byId['spk-assessment-status'].textContent, /Đã lưu/);
  assert.equal(fetchCalls.length, 2);
});

test('a saved zero score remains assessed and restores its missing-word feedback', () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const { window } = createSandbox({ document });
  Object.assign(window.SPK_SENTENCES[0], { isPracticed: true, totalScore: 0, accuracyScore: 0,
    fluencyAvailable: false, completenessAvailable: false,
    comparison: { available: true, operations: [{ kind: 'missing', referenceToken: 'Hello' }] } });
  window.TctSpeakingPractice.selectSentence(0, false);
  assert.equal(byId['score-total'].textContent, '0');
  assert.equal(byId['score-fluency'].textContent, '—');
  assert.equal(byId['spk-word-comparison'].hidden, false);
  assert.match(byId['spk-assessment-status'].textContent, /Đã lưu/);
});

test('available component scores including a measured zero survive sentence navigation', () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const { window, fetchCalls } = createSandbox({ document });
  Object.assign(window.SPK_SENTENCES[0], { isPracticed: true, totalScore: 90, accuracyScore: 90,
    fluencyScore: 76, completenessScore: 0, fluencyAvailable: true, completenessAvailable: true });
  for (const index of [0, 1, 0]) window.TctSpeakingPractice.selectSentence(index, false);
  assert.equal(byId['score-fluency'].textContent, '76');
  assert.equal(byId['bar-fluency'].style.width, '76%');
  assert.equal(byId['score-complete'].textContent, '0');
  assert.equal(byId['score-complete'].title, '');
  assert.match(byId['spk-assessment-status'].textContent, /ước lượng/);
  assert.equal(fetchCalls.length, 0);
});

test('commit renders the retained best comparison and component scores instead of the lower new take', async () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const { fetchImpl: assessFetch, fetchCalls } = buildAssessFetch([{ body: {
    success: true, canCommitProgress: true, assessmentId: 'lower-take', textMatchScore: 40,
    comparison: { available: true, operations: [{ kind: 'missing', referenceToken: 'new' }] },
  } }]);
  const fetchImpl = async (url, options) => {
    if (String(url).includes('/progress')) return { ok: true, json: async () => ({
      success: true, totalScore: 90, accuracyScore: 90, fluencyScore: 77, completenessScore: 100,
      fluencyAvailable: true, completenessAvailable: true,
      comparison: { available: true, operations: [{ kind: 'matched', referenceToken: 'best', hypothesisToken: 'best' }] },
    }) };
    return assessFetch(url, options);
  };
  const { window } = createSandbox({ document, speechRecorder: createControllableRecorder(), fetchImpl });
  await recordAndStop(byId);
  window.TctSpeakingPractice.selectSentence(1, false);
  window.TctSpeakingPractice.selectSentence(0, false);
  assert.equal(byId['score-total'].textContent, '90');
  assert.equal(byId['score-fluency'].textContent, '77');
  assert.equal(byId['score-complete'].textContent, '100');
  assert.deepEqual(chipLabels(byId['spk-word-comparison-body']), ['best']);
  assert.equal(fetchCalls.filter(call => call.url.includes('/assess')).length, 1);
});

test('multiple saved sentences retain distinct scores and comparisons through arbitrary navigation', () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const { window, fetchCalls } = createSandbox({ document });
  const scores = [90, 0, 61];
  window.SPK_SENTENCES.push({ id: 103, text: 'Third sentence', start: 4, end: 6 });
  window.SPK_SENTENCES.forEach((sentence, i) => Object.assign(sentence, {
    isPracticed: true, totalScore: scores[i], accuracyScore: scores[i], fluencyAvailable: false, completenessAvailable: false,
    comparison: { available: true, operations: [{ kind: 'substituted', referenceToken: 'source' + i, hypothesisToken: 'heard' + i }] },
  }));
  for (const index of [0, 2, 1, 2, 0]) {
    window.TctSpeakingPractice.selectSentence(index, false);
    assert.equal(byId['score-total'].textContent, String(scores[index]));
    assert.deepEqual(chipLabels(byId['spk-word-comparison-body']), ['source' + index + ' → heard' + index]);
    assert.equal(byId['score-fluency'].textContent, '—');
  }
  assert.equal(fetchCalls.length, 0);
});

test('transcript toggle hides the whole list and stays hidden when selecting another sentence', () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const { window } = createSandbox({ document });
  byId['btn-hide-text'].click();
  assert.equal(byId['spk-transcript-list'].hidden, true);
  assert.equal(byId['btn-hide-text'].getAttribute('aria-expanded'), 'false');
  assert.match(byId['btn-hide-text'].textContent, /Hiện/);
  window.TctSpeakingPractice.selectSentence(1, false);
  assert.equal(byId['spk-transcript-list'].hidden, true);
  byId['btn-hide-text'].click();
  assert.equal(byId['spk-transcript-list'].hidden, false);
  assert.equal(byId['btn-hide-text'].getAttribute('aria-expanded'), 'true');
});

test('new record click clears prior word comparison before mic request', async () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const panel = byId['spk-word-comparison'];
  const body = byId['spk-word-comparison-body'];
  const stale = createElement('span');
  stale.textContent = 'stale-match';
  stale.classList.add('spk-word-chip');
  body.appendChild(stale);
  panel.hidden = false;

  let startCalls = 0;
  const speechRecorder = {
    isSupported: () => true,
    create: () => ({
      start: async () => {
        startCalls += 1;
        assert.equal(panel.hidden, true, 'comparison must clear before mic start');
        assert.equal(body.children.length, 0, 'prior chips must be removed');
      },
      stop: async () => {},
      dispose: () => {},
    }),
  };

  const { sandbox } = createSandbox({
    document,
    speechRecognition: undefined,
    speechRecorder,
  });
  const api = sandbox.window.TctSpeakingPractice;
  api.selectSentence(0, false);

  body.appendChild(stale);
  panel.hidden = false;
  assert.equal(panel.hidden, false);
  assert.equal(body.children.length, 1);

  byId['btn-record'].click();
  await delay(20);

  assert.equal(startCalls, 1);
  assert.equal(panel.hidden, true);
  assert.equal(body.children.length, 0);
});

test('mic denied keeps comparison cleared and does not restore stale chips', async () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const panel = byId['spk-word-comparison'];
  const body = byId['spk-word-comparison-body'];

  const speechRecorder = {
    isSupported: () => true,
    create: () => ({
      start: async () => {
        throw new Error('NotAllowedError');
      },
      stop: async () => {},
      dispose: () => {},
    }),
  };

  const { sandbox } = createSandbox({
    document,
    speechRecognition: undefined,
    speechRecorder,
  });
  const api = sandbox.window.TctSpeakingPractice;
  api.selectSentence(0, false);

  const stale = createElement('span');
  stale.textContent = 'old-diff';
  body.appendChild(stale);
  panel.hidden = false;

  byId['btn-record'].click();
  await delay(30);

  assert.equal(panel.hidden, true);
  assert.equal(body.children.length, 0);
  assert.match(byId['spk-assessment-status'].textContent, /Mic bị từ chối|không khả dụng/i);
});

test('two production assess cycles replace chips 2→1 and keep literal markup', async () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const panel = byId['spk-word-comparison'];
  const body = byId['spk-word-comparison-body'];
  const recorder = createControllableRecorder();
  const { fetchImpl, fetchCalls, getAssessCount } = buildAssessFetch([
    {
      body: {
        success: true,
        canCommitProgress: true,
        assessmentId: 'assess-1',
        textMatchScore: 80,
        comparison: {
          available: true,
          operations: [
            { kind: 'matched', referenceToken: 'Hello', hypothesisToken: 'Hello' },
            { kind: 'extra', hypothesisToken: '<img src=x onerror=alert(1)>' },
          ],
        },
      },
    },
    {
      body: {
        success: true,
        canCommitProgress: true,
        assessmentId: 'assess-2',
        textMatchScore: 90,
        comparison: {
          available: true,
          operations: [
            { kind: 'matched', referenceToken: 'Hello', hypothesisToken: 'Hello' },
          ],
        },
      },
    },
  ]);

  const { sandbox } = createSandbox({
    document,
    speechRecognition: undefined,
    speechRecorder: recorder,
    fetchImpl,
  });
  const api = sandbox.window.TctSpeakingPractice;
  api.selectSentence(0, false);

  await recordAndStop(byId);
  assert.equal(getAssessCount(), 1);
  assert.equal(panel.hidden, false);
  assert.equal(countChips(body), 2);
  assert.deepEqual(chipLabels(body), ['Hello', '<img src=x onerror=alert(1)>']);
  assert.equal(body.innerHTML.includes('<img'), false);

  await recordAndStop(byId);
  assert.equal(getAssessCount(), 2);
  assert.equal(countChips(body), 1, 'second render must replace, not accumulate');
  assert.deepEqual(chipLabels(body), ['Hello']);
  assert.equal(
    fetchCalls.filter((call) => call.url.includes('/assess')).length,
    2
  );
  assert.equal(
    fetchCalls.filter((call) => call.url.includes('/progress')).length,
    2
  );
});

test('noSpeech assess response clears comparison and does not POST progress', async () => {
  const { document, byId } = buildPracticeDomWithComparison();
  const panel = byId['spk-word-comparison'];
  const body = byId['spk-word-comparison-body'];
  const recorder = createControllableRecorder();
  const { fetchImpl, fetchCalls } = buildAssessFetch([
    {
      body: {
        success: true,
        canCommitProgress: false,
        message: 'Chưa nhận được lời nói.',
        comparison: { available: false, unavailableReason: 'no_speech' },
      },
    },
  ]);

  const { sandbox } = createSandbox({
    document,
    speechRecognition: undefined,
    speechRecorder: recorder,
    fetchImpl,
  });
  sandbox.window.TctSpeakingPractice.selectSentence(0, false);

  // Seed prior chips then record a no-speech result.
  const stale = createElement('li');
  stale.classList.add('spk-word-chip');
  body.appendChild(stale);
  panel.hidden = false;

  await recordAndStop(byId);
  assert.equal(panel.hidden, true);
  assert.equal(countChips(body), 0);
  assert.equal(fetchCalls.filter((call) => call.url.includes('/progress')).length, 0);
  assert.equal(fetchCalls.filter((call) => call.url.includes('/assess')).length, 1);
  assert.equal(byId['spk-assessment-status'].textContent, 'Chưa nhận được lời nói.');
});

test('mutated renderer without clear fails the two-cycle accumulation assert', async () => {
  const production = fs.readFileSync(SPEAKING_JS, 'utf8');
  let mutatedSource = production.replace(
    /function clearWordComparison\(\) \{[\s\S]*?\n    \}/,
    'function clearWordComparison() { /* mutation: noop */ }'
  );
  const renderStart = mutatedSource.indexOf('function renderWordComparison');
  const renderEnd = mutatedSource.indexOf('function formatScoreDisplay');
  assert.ok(renderStart >= 0 && renderEnd > renderStart);
  const renderSlice = mutatedSource.slice(renderStart, renderEnd);
  const cleared = renderSlice.replace(
    /while \(body\.firstChild\) \{\s*body\.removeChild\(body\.firstChild\);\s*\}/,
    '/* mutation: skip clear */'
  );
  assert.notEqual(cleared, renderSlice, 'mutation must alter production clear loop');
  mutatedSource = mutatedSource.slice(0, renderStart) + cleared + mutatedSource.slice(renderEnd);
  assert.notEqual(mutatedSource, production, 'in-memory mutant must differ from production');
  assert.equal(fs.readFileSync(SPEAKING_JS, 'utf8'), production, 'production file must stay untouched');

  const { document, byId } = buildPracticeDomWithComparison();
  const body = byId['spk-word-comparison-body'];
  const recorder = createControllableRecorder();
  const { fetchImpl, getAssessCount } = buildAssessFetch([
    {
      body: {
        success: true,
        canCommitProgress: true,
        assessmentId: 'm1',
        textMatchScore: 70,
        comparison: {
          available: true,
          operations: [
            { kind: 'matched', referenceToken: 'A', hypothesisToken: 'A' },
            { kind: 'matched', referenceToken: 'B', hypothesisToken: 'B' },
          ],
        },
      },
    },
    {
      body: {
        success: true,
        canCommitProgress: true,
        assessmentId: 'm2',
        textMatchScore: 75,
        comparison: {
          available: true,
          operations: [{ kind: 'matched', referenceToken: 'C', hypothesisToken: 'C' }],
        },
      },
    },
  ]);

  const { sandbox } = createSandbox({
    document,
    speechRecognition: undefined,
    speechRecorder: recorder,
    fetchImpl,
    sourceOverride: mutatedSource,
  });
  sandbox.window.TctSpeakingPractice.selectSentence(0, false);

  await recordAndStop(byId);
  await recordAndStop(byId);
  assert.equal(getAssessCount(), 2);
  assert.notEqual(
    countChips(body),
    1,
    'without production clear, second render must accumulate chips so this guard stays red'
  );
});
