'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
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

function buildPracticeDom() {
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

  return { document, byId, body, sent0, sent1 };
}

function createSandbox({ document, speechRecognition, fetchImpl }) {
  const progressPosts = [];
  const fetchCalls = [];

  const fetch =
    fetchImpl ||
    (async (url, options = {}) => {
      fetchCalls.push({ url: String(url), options });
      if (String(url).includes('/api/speaking/') && String(url).includes('/progress')) {
        progressPosts.push({
          url: String(url),
          body: options.body ? JSON.parse(options.body) : null,
        });
        return {
          ok: true,
          json: async () => ({ success: true, xpEarned: 20 }),
        };
      }
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

  // Alias used by IIFE (global)
  const sandbox = {
    window,
    document,
    console,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    fetch,
    SpeechRecognition: speechRecognition,
    webkitSpeechRecognition: speechRecognition,
    YT: window.YT,
  };
  // speaking.js uses (window) IIFE and `global` parameter name = window
  Object.assign(sandbox, window);

  return { sandbox, window, progressPosts, fetchCalls };
}

function loadSpeaking(sandbox) {
  loadScript(SPEAKING_JS, sandbox);
  return sandbox.window.TctSpeakingPractice;
}

test('missing SpeechRecognition does not POST progress after timers', async () => {
  const { document, byId } = buildPracticeDom();
  const { sandbox, window, progressPosts } = createSandbox({
    document,
    speechRecognition: undefined,
  });

  const api = loadSpeaking(sandbox);
  assert.ok(api, 'speaking practice API must be exposed for tests');
  api.selectSentence(0, false);

  byId['btn-record'].click();
  await delay(2800);

  assert.equal(progressPosts.length, 0, 'demo/random path must not save progress');
  assert.equal(window.SPK_SENTENCES[0].isPracticed, false);
});

test('no-speech / mic denied does not POST progress', async () => {
  let lastInstance = null;
  function FakeRecog() {
    lastInstance = this;
    this.lang = '';
    this.interimResults = false;
    this.continuous = false;
    this.maxAlternatives = 1;
    this.start = () => {};
    this.stop = () => {
      if (this.onerror) this.onerror({ error: 'not-allowed' });
      if (this.onend) this.onend();
    };
  }

  const { document, byId } = buildPracticeDom();
  const { sandbox, window, progressPosts } = createSandbox({
    document,
    speechRecognition: FakeRecog,
  });

  const api = loadSpeaking(sandbox);
  api.selectSentence(0, false);
  byId['btn-record'].click();
  // start recording then stop (user) — stop triggers not-allowed + empty transcript path via onend
  byId['btn-record'].click();
  await delay(50);

  // Also fire no-speech style end without transcript
  if (lastInstance && lastInstance.onerror) {
    lastInstance.onerror({ error: 'no-speech' });
  }
  await delay(50);

  assert.equal(progressPosts.length, 0);
  assert.equal(window.SPK_SENTENCES[0].isPracticed, false);
});

test('browser recognition preview must not POST scores or mark practiced', async () => {
  let instance = null;
  function FakeRecog() {
    instance = this;
    this.start = () => {};
    this.stop = () => {};
  }

  const { document, byId } = buildPracticeDom();
  const { sandbox, window, progressPosts } = createSandbox({
    document,
    speechRecognition: FakeRecog,
  });

  const api = loadSpeaking(sandbox);
  api.selectSentence(0, false);
  byId['btn-record'].click();
  assert.ok(instance);

  const result = { isFinal: true, length: 1 };
  result[0] = { transcript: 'Hello world' };
  instance.onresult({
    resultIndex: 0,
    results: { length: 1, 0: result },
  });
  instance.onend();
  await delay(50);

  assert.equal(progressPosts.length, 0, 'preview-only: no progress POST');
  assert.equal(window.SPK_SENTENCES[0].isPracticed, false);
  assert.equal(byId['score-fluency'].textContent, '—');
  assert.equal(byId['score-complete'].textContent, '—');
  assert.match(byId['spk-assessment-status'].textContent, /chưa lưu|xem trước/i);
  // Text-match preview may show a percent, but must not invent fluency/completeness.
  assert.match(byId['score-total'].textContent, /^\d+$/);
});

test('late recognition callback after sentence change does not attach to new sentence or POST', async () => {
  let instance = null;
  function FakeRecog() {
    instance = this;
    this.start = () => {};
    this.stop = () => {};
  }

  const { document, byId } = buildPracticeDom();
  const { sandbox, window, progressPosts } = createSandbox({
    document,
    speechRecognition: FakeRecog,
  });

  const api = loadSpeaking(sandbox);
  api.selectSentence(0, false);
  byId['btn-record'].click();
  assert.ok(instance);

  // Switch sentence while recognition still "in flight"
  api.selectSentence(1, false);

  instance.onresult({
    resultIndex: 0,
    results: [{ isFinal: true, 0: { transcript: 'Hello world' } }],
  });
  instance.onend();
  await delay(50);

  assert.equal(progressPosts.length, 0);
  assert.equal(window.SPK_SENTENCES[0].isPracticed, false);
  assert.equal(window.SPK_SENTENCES[1].isPracticed, false);
  // New sentence must not inherit preview scores from old callback
  assert.equal(Number(window.SPK_SENTENCES[1].totalScore) || 0, 0);
});

test('dispose clears recording timers so unload cannot POST later', async () => {
  const { document, byId } = buildPracticeDom();
  const { sandbox, progressPosts } = createSandbox({
    document,
    speechRecognition: undefined,
  });

  const api = loadSpeaking(sandbox);
  api.selectSentence(0, false);
  byId['btn-record'].click();
  api.dispose();
  await delay(2800);

  assert.equal(progressPosts.length, 0);
});

test('sentence change and dispose stop the real pcm recorder', async () => {
  const { document, byId } = buildPracticeDom();
  let stops = 0;
  let resolveMic;
  let processor = null;
  const { sandbox, window } = createSandbox({
    document,
    speechRecognition: undefined,
  });
  const track = { stop() { stops += 1; } };
  window.AudioContext = class {
    constructor() { this.sampleRate = 48000; }
    createMediaStreamSource() { return { connect() {}, disconnect() {} }; }
    createScriptProcessor() {
      processor = { connect() {}, disconnect() {}, onaudioprocess: null };
      return processor;
    }
    close() { return Promise.resolve(); }
  };
  window.navigator = {
    mediaDevices: {
      getUserMedia: () => new Promise((resolve) => { resolveMic = resolve; }),
    },
  };
  Object.assign(sandbox, {
    Blob: globalThis.Blob,
    DataView: globalThis.DataView,
    ArrayBuffer: globalThis.ArrayBuffer,
    Float32Array: globalThis.Float32Array,
    Uint8Array: globalThis.Uint8Array,
    AudioContext: window.AudioContext,
    navigator: window.navigator,
  });
  loadScript(path.join(__dirname, '../../TCTEnglish/wwwroot/js/speech-recorder.js'), sandbox);
  const api = loadSpeaking(sandbox);
  api.selectSentence(0, false);
  byId['btn-record'].click();
  await delay(10);
  api.selectSentence(1, false);
  resolveMic({ getTracks: () => [track] });
  await delay(20);
  assert.ok(stops >= 1, 'changing sentence must stop the microphone track');
  assert.equal(window.SPK_SENTENCES[1].isPracticed, false);

  stops = 0;
  let resolveDenied;
  window.navigator.mediaDevices.getUserMedia = () => new Promise((resolve, reject) => {
    resolveDenied = reject;
  });
  const rejections = [];
  const onRejection = (error) => rejections.push(error);
  process.on('unhandledRejection', onRejection);
  try {
    api.selectSentence(0, false);
    byId['btn-record'].click();
    await delay(10);
    api.dispose();
    resolveDenied(new Error('denied'));
    await delay(30);
    assert.equal(rejections.length, 0);
  } finally {
    process.off('unhandledRejection', onRejection);
  }
});
