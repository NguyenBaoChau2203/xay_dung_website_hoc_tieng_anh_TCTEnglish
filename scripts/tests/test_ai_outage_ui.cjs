'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript, delay } = require('./dom_fixture.cjs');

const ROOT = path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js');
const SPEAKING_JS = path.join(ROOT, 'speaking.js');
const RECORDER_JS = path.join(ROOT, 'speech-recorder.js');
const AVAILABILITY_JS = path.join(ROOT, 'ai-availability.js');
const WRITING_JS = path.join(ROOT, 'writing-exercises.js');

function practiceDom() {
  const ids = [
    'btn-prev', 'btn-next', 'btn-replay', 'btn-record', 'record-icon', 'record-label',
    'sent-indicator', 'current-transcript-en', 'current-transcript-vi',
    'sentence-scroll-container', 'btn-slider-prev', 'btn-slider-next',
    'dic-sentence-scroll-container', 'btn-dic-slider-prev', 'btn-dic-slider-next',
    'btn-hide-text', 'prog-bar', 'prog-text', 'prog-pct', 'spk-toast',
    'tab-pronunciation', 'tab-dictation', 'pronunciation-content', 'dictation-content',
    'btn-dic-hide-video', 'video-overlay', 'youtube-player', 'dic-input', 'dic-feedback',
    'dic-sent-indicator', 'btn-dic-hint', 'btn-dic-play', 'dic-play-icon', 'dic-play-label',
    'btn-dic-next', 'btn-dic-slow-play', 'score-total', 'score-accuracy', 'score-fluency',
    'score-complete', 'ring-total', 'bar-accuracy', 'bar-fluency', 'bar-complete',
    'spk-assessment-status', 'spk-choice-automatic', 'spk-choice-self',
    'spk-self-practice-note', 'spk-self-playback', 'spk-self-audio',
    'spk-self-playback-label', 'spk-offer-self', 'spk-word-comparison', 'spk-word-comparison-body',
  ];
  const { document, byId } = createDocument(ids);
  for (const [index, id] of [['0', 'sent0'], ['1', 'sent1']]) {
    const button = createElement('button');
    button.classList.add('spk-sent-btn');
    button.dataset.index = index;
    byId['sentence-scroll-container'].appendChild(button);
  }
  byId['spk-self-audio'].pause = () => { byId['spk-self-audio'].pausedFlag = true; };
  byId['dic-input'].focus = () => {};
  byId['spk-offer-self'].hidden = true;
  byId['spk-self-playback'].hidden = true;
  byId['spk-self-practice-note'].hidden = true;
  return { document, byId };
}

function audioEnvironment(document, fetchImpl, options = {}) {
  const created = [];
  const revoked = [];
  const stopped = [];
  const processors = [];
  let urlId = 0;
  const sentences = options.sentences || [
    { id: 101, text: 'Hello world', vi: 'Xin chào', start: 0, end: 2, totalScore: 80, accuracyScore: 80, fluencyScore: 70, completenessScore: 70, isPracticed: true },
    { id: 102, text: 'Good morning', vi: 'Chào buổi sáng', start: 2, end: 4, totalScore: 0, accuracyScore: 0, fluencyScore: 0, completenessScore: 0, isPracticed: false },
  ];
  const fetchCalls = [];
  const sandbox = {
    document,
    console,
    setTimeout,
    clearTimeout,
    Blob: globalThis.Blob,
    FormData: globalThis.FormData,
    AbortController: globalThis.AbortController,
    Date,
    fetch: fetchImpl || (async (url, init) => {
      fetchCalls.push({ url: String(url), init });
      return { ok: true, status: 200, redirected: false, json: async () => ({ success: false, error: 'unused' }) };
    }),
    URL: {
      createObjectURL() {
        const url = 'blob:practice-' + (++urlId);
        created.push(url);
        return url;
      },
      revokeObjectURL(url) { revoked.push(url); },
    },
    navigator: {
      mediaDevices: {
        getUserMedia: options.getUserMedia || (async () => ({
          getTracks: () => [{ stop() { stopped.push('track'); } }],
        })),
      },
    },
    AudioContext: class {
      constructor() { this.sampleRate = 16000; this.destination = {}; }
      createMediaStreamSource() { return { connect() {}, disconnect() {} }; }
      createScriptProcessor() {
        const processor = { connect() {}, disconnect() {}, onaudioprocess: null };
        processors.push(processor);
        return processor;
      }
      close() { return Promise.resolve(); }
    },
    SPK_VIDEO_ID: 'video-1',
    SPK_SENTENCES: sentences,
    TCT_SPEAKING_TEST_MODE: true,
    SpeechRecognition: options.SpeechRecognition,
  };
  sandbox.window = sandbox;
  sandbox.global = sandbox;
  sandbox.self = sandbox;
  return { sandbox, created, revoked, stopped, processors, fetchCalls, sentences };
}

function loadPractice(sandbox) {
  loadScript(AVAILABILITY_JS, sandbox);
  loadScript(RECORDER_JS, sandbox);
  loadScript(SPEAKING_JS, sandbox);
  return sandbox.TctSpeakingPractice;
}

async function recordOneTake(api, processors) {
  api.startRecording();
  await delay(20);
  const processor = processors[processors.length - 1];
  assert.ok(processor, 'actual recorder created a processor');
  const samples = new Float32Array(1600);
  samples[0] = 0.2;
  processor.onaudioprocess({ inputBuffer: { getChannelData: () => samples } });
  api.startRecording();
  await delay(20);
}

test('availability helper classifies speaking failures without fetching', () => {
  const sandbox = { window: {} };
  sandbox.window = sandbox;
  loadScript(AVAILABILITY_JS, sandbox);
  const api = sandbox.TctAiAvailability;
  assert.equal(api.message('outage'), 'Dịch vụ đánh giá đang gián đoạn.');
  assert.equal(api.message('selfPractice'), 'Bạn vẫn có thể luyện và nghe lại. Bản thu này chưa được chấm tự động.');
  const outage = api.classifySpeakingFailure({ status: 503 }, { code: 'circuit_open' });
  assert.equal(outage.kind, 'outage');
  const access = api.classifySpeakingFailure({ status: 401, redirected: false }, {});
  assert.equal(access.kind, 'access');
  const rate = api.classifySpeakingFailure({ status: 429 }, { code: 'engine_busy', error: 'Bạn đang gửi đánh giá quá nhanh. Thử lại sau.' });
  assert.equal(rate.kind, 'rate');
  const providerRate = api.classifySpeakingFailure({ status: 429 }, { code: 'engine_busy', error: 'ASR đang quá tải.' });
  assert.equal(providerRate.kind, 'rate');
  const queue = api.classifySpeakingFailure({ status: 429 }, { code: 'engine_busy', error: 'Hàng đợi nhận dạng đang đầy. Thử lại sau.' });
  assert.equal(queue.kind, 'busy');
  assert.match(queue.message, /Hàng đợi/);
  const billing = api.classifySpeakingFailure({ status: 503 }, { success: false, code: 'quota_exhausted', error: 'Tính năng AI tạm thời chưa sẵn sàng.' });
  assert.equal(billing.kind, 'unavailable');
  assert.doesNotMatch(billing.message, /quá nhanh/);
  assert.equal(typeof sandbox.fetch, 'undefined');
});

test('self-practice records and plays back with zero assess, progress, or recognition calls', async () => {
  const { document, byId } = practiceDom();
  let recognitionStarts = 0;
  const env = audioEnvironment(document, null, {
    SpeechRecognition: function SpeechRecognition() { recognitionStarts += 1; },
  });
  const api = loadPractice(env.sandbox);
  api.setAssessmentChoice('self');
  await recordOneTake(api, env.processors);
  assert.equal(env.fetchCalls.length, 0);
  assert.equal(recognitionStarts, 0);
  assert.equal(byId['spk-self-audio'].src, env.created[0]);
  assert.match(byId['spk-assessment-status'].textContent, /chưa được chấm tự động/i);
  assert.equal(byId['score-total'].textContent, '—');
  assert.equal(env.sentences[0].isPracticed, true);
  assert.equal(env.sentences[0].totalScore, 80);
  assert.equal(env.stopped.length > 0, true);
});

test('self-practice cleanup revokes the clip and ignores a stale completion', async () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document);
  const api = loadPractice(env.sandbox);
  const realCreate = env.sandbox.TctSpeechRecorder.create.bind(env.sandbox.TctSpeechRecorder);
  const pending = [];
  env.sandbox.TctSpeechRecorder.create = function (options) {
    return realCreate({
      onComplete(blob) { pending.push(() => options.onComplete(blob)); },
      onError(error) { if (options.onError) options.onError(error); },
    });
  };
  api.setAssessmentChoice('self');
  await recordOneTake(api, env.processors);
  assert.equal(pending.length, 1);
  api.selectSentence(1);
  pending[0]();
  assert.equal(env.fetchCalls.length, 0);
  assert.equal(env.revoked.length, 0);
  assert.equal(byId['spk-self-audio'].src || '', '');
  api.setAssessmentChoice('self');
  await recordOneTake(api, env.processors);
  pending[1]();
  assert.equal(env.created.length, 1);
  const tab = byId['tab-dictation'];
  for (const fn of tab._listeners.click || []) {
    fn.call(tab, { type: 'click', target: tab, preventDefault() {} });
  }
  assert.equal(env.revoked.includes(env.created[0]), true);
  assert.equal(env.fetchCalls.length, 0);
});

test('microphone denial stays a microphone error', async () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document, null, {
    getUserMedia: async () => { throw new Error('not-allowed'); },
  });
  const api = loadPractice(env.sandbox);
  api.setAssessmentChoice('self');
  api.startRecording();
  await delay(20);
  assert.equal(env.fetchCalls.length, 0);
  assert.match(byId['spk-assessment-status'].textContent, /Mic bị từ chối|không khả dụng/i);
  assert.doesNotMatch(byId['spk-assessment-status'].textContent, /gián đoạn|OpenAI/i);
});

test('billing quota offers self-practice and does not say the learner is too fast', async () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document, async (url) => {
    env.fetchCalls.push(String(url));
    return {
      ok: false,
      status: 503,
      redirected: false,
      json: async () => ({ success: false, code: 'quota_exhausted', error: 'Tính năng AI tạm thời chưa sẵn sàng.' }),
    };
  });
  loadPractice(env.sandbox);
  await recordOneTake(env.sandbox.TctSpeakingPractice, env.processors);
  assert.deepEqual(env.fetchCalls, ['/api/speaking/101/assess']);
  assert.match(byId['spk-assessment-status'].textContent, /Tính năng AI tạm thời chưa sẵn sàng/);
  assert.doesNotMatch(byId['spk-assessment-status'].textContent, /quá nhanh/);
  assert.equal(byId['spk-offer-self'].hidden, false);
  assert.equal(env.sentences[0].totalScore, 80);
});

test('automatic failure keeps the stored score, offers self-practice, and does not resubmit', async () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document, async (url) => {
    env.fetchCalls.push(String(url));
    if (String(url).includes('/assess')) {
      return {
        ok: false,
        status: 503,
        redirected: false,
        json: async () => ({ success: false, code: 'circuit_open', error: 'paused' }),
      };
    }
    return { ok: true, status: 200, redirected: false, json: async () => ({ success: true }) };
  });
  const api = loadPractice(env.sandbox);
  assert.equal(byId['score-total'].textContent, '80');
  await recordOneTake(api, env.processors);
  assert.deepEqual(env.fetchCalls, ['/api/speaking/101/assess']);
  assert.equal(byId['spk-offer-self'].hidden, false);
  assert.equal(byId['score-total'].textContent, '80');
  assert.equal(env.sentences[0].totalScore, 80);
  byId['spk-offer-self'].click();
  await delay(10);
  assert.equal(api.getAssessmentChoice(), 'self');
  assert.deepEqual(env.fetchCalls, ['/api/speaking/101/assess']);
  assert.match(byId['spk-assessment-status'].textContent, /chưa được chấm tự động/i);
});

test('automatic success still posts assessment and progress once', async () => {
  const { document } = practiceDom();
  const env = audioEnvironment(document, async (url) => {
    env.fetchCalls.push(String(url));
    if (String(url).includes('/assess')) {
      return {
        ok: true,
        status: 200,
        redirected: false,
        json: async () => ({ success: true, canCommitProgress: true, assessmentId: 'assess-1', textMatchScore: 91, comparison: null }),
      };
    }
    return {
      ok: true,
      status: 200,
      redirected: false,
      json: async () => ({ success: true, totalScore: 91, accuracyScore: 91, fluencyScore: 0, completenessScore: 0, xpEarned: 0 }),
    };
  });
  const api = loadPractice(env.sandbox);
  await recordOneTake(api, env.processors);
  assert.deepEqual(env.fetchCalls, ['/api/speaking/101/assess', '/api/speaking/101/progress']);
  assert.equal(env.sentences[0].isPracticed, true);
  assert.equal(env.sentences[0].totalScore, 91);
});

test('NoSpeech does not commit progress or replace a stored score', async () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document, async (url) => {
    env.fetchCalls.push(String(url));
    return {
      ok: true,
      status: 200,
      redirected: false,
      json: async () => ({ success: true, status: 'NoSpeech', canCommitProgress: false, code: 'no_speech', message: 'Chưa nhận được lời nói.' }),
    };
  });
  loadPractice(env.sandbox);
  await recordOneTake(env.sandbox.TctSpeakingPractice, env.processors);
  assert.deepEqual(env.fetchCalls, ['/api/speaking/101/assess']);
  assert.equal(byId['score-total'].textContent, '80');
  assert.equal(env.sentences[0].totalScore, 80);
  assert.match(byId['spk-assessment-status'].textContent, /Chưa nhận được lời nói/);
});

test('failed AI writing creation keeps the source text and links to the filtered catalog', async () => {
  const { document, body } = createDocument([]);
  const form = createElement('form');
  form.setAttribute('data-writing-create-form', '');
  form.setAttribute('data-create-url', '/Study/WritingExercises/CreateFromAi');
  form.setAttribute('data-practice-url', '/Study/WritingPractice');
  form.setAttribute('data-catalog-url', '/Study/WritingExercises?level=intermediate&contentType=articles&topic=travel#system-catalog-heading');
  const token = createElement('input');
  token.setAttribute('name', '__RequestVerificationToken');
  token.value = 'token';
  form.appendChild(token);
  const textarea = createElement('textarea');
  textarea.setAttribute('name', 'sourceText');
  textarea.value = 'Nội dung người học đã nhập';
  form.appendChild(textarea);
  const button = createElement('button');
  button.setAttribute('data-writing-create-submit', '');
  form.appendChild(button);
  const label = createElement('span');
  label.setAttribute('data-writing-create-submit-text', '');
  form.appendChild(label);
  const message = createElement('p');
  message.setAttribute('data-writing-create-message', '');
  message.hidden = true;
  form.appendChild(message);
  body.appendChild(form);
  const calls = [];
  const sandbox = {
    document,
    console,
    window: { crypto: { randomUUID: () => 'idem-1' } },
    fetch: async (url) => {
      calls.push(String(url));
      return { ok: false, status: 503, headers: { get: () => 'text/plain' }, json: async () => { throw new Error('not json'); } };
    },
  };
  sandbox.window.document = document;
  sandbox.window.TctAiAvailability = undefined;
  loadScript(AVAILABILITY_JS, sandbox);
  loadScript(WRITING_JS, sandbox);
  for (const fn of document._listeners.DOMContentLoaded || []) fn({ type: 'DOMContentLoaded' });
  const submit = form._listeners.submit[0];
  await submit({ preventDefault() {} });
  assert.deepEqual(calls, ['/Study/WritingExercises/CreateFromAi']);
  assert.equal(textarea.value, 'Nội dung người học đã nhập');
  assert.match(message.textContent, /Dịch vụ đánh giá đang gián đoạn/);
  const link = form.querySelector('[data-writing-catalog-link]');
  assert.equal(link.textContent, 'Chọn bài có sẵn');
  assert.equal(link.href, '/Study/WritingExercises?level=intermediate&contentType=articles&topic=travel#system-catalog-heading');
  assert.equal(link.href.includes('exerciseId'), false);
  assert.equal(button.disabled, false);
});

test('writing creation access failure is not relabelled as an outage', async () => {
  const { document, body } = createDocument([]);
  const form = createElement('form');
  form.setAttribute('data-writing-create-form', '');
  form.setAttribute('data-create-url', '/Study/WritingExercises/CreateFromAi');
  form.setAttribute('data-catalog-url', '/Study/WritingExercises?level=beginner#system-catalog-heading');
  const token = createElement('input');
  token.setAttribute('name', '__RequestVerificationToken');
  token.value = 'token';
  form.appendChild(token);
  const textarea = createElement('textarea');
  textarea.setAttribute('name', 'sourceText');
  textarea.value = 'private draft';
  form.appendChild(textarea);
  const button = createElement('button');
  button.setAttribute('data-writing-create-submit', '');
  form.appendChild(button);
  const label = createElement('span');
  label.setAttribute('data-writing-create-submit-text', '');
  form.appendChild(label);
  const message = createElement('p');
  message.setAttribute('data-writing-create-message', '');
  form.appendChild(message);
  body.appendChild(form);
  const sandbox = {
    document,
    console,
    window: {},
    fetch: async () => ({
      ok: false,
      status: 403,
      headers: { get: () => 'application/json' },
      json: async () => ({ error: 'Bạn không có quyền tạo bài này.', errorCode: 'forbidden' }),
    }),
  };
  loadScript(WRITING_JS, sandbox);
  for (const fn of document._listeners.DOMContentLoaded || []) fn({ type: 'DOMContentLoaded' });
  await form._listeners.submit[0]({ preventDefault() {} });
  assert.match(message.textContent, /không có quyền/);
  assert.doesNotMatch(message.textContent, /gián đoạn/);
  assert.equal(textarea.value, 'private draft');
  assert.equal(form.querySelector('[data-writing-catalog-link]').textContent, 'Chọn bài có sẵn');
});

function emitOn(document, type, event) {
  const list = ((document._listeners && document._listeners[type]) || []).slice();
  for (const fn of list) fn(event);
}

function keydown(target, code, extra = {}) {
  return {
    type: 'keydown',
    code,
    key: code === 'Space' ? ' ' : code === 'Tab' ? 'Tab' : code === 'Enter' ? 'Enter' : code,
    target,
    shiftKey: false,
    ctrlKey: false,
    metaKey: false,
    altKey: false,
    isComposing: false,
    defaultPrevented: false,
    preventDefault() { this.defaultPrevented = true; },
    ...extra,
  };
}

function bindLifecycleTarget(document, sandbox) {
  sandbox.addEventListener = (type, fn) => document.addEventListener(type, fn);
  sandbox.removeEventListener = (type, fn) => document.removeEventListener(type, fn);
}

test('R4-A focused controls keep native Enter and Space while background shortcuts stay scoped', async () => {
  const { document, byId } = practiceDom();
  byId['spk-choice-automatic'].tagName = 'BUTTON';
  byId['spk-choice-self'].tagName = 'BUTTON';
  byId['btn-record'].tagName = 'BUTTON';
  byId['dic-input'].tagName = 'TEXTAREA';
  const editable = createElement('div');
  editable.isContentEditable = true;
  document.body.appendChild(editable);
  const env = audioEnvironment(document, null, { SpeechRecognition: undefined });
  const api = loadPractice(env.sandbox);
  const playCalls = [];
  env.sandbox.YT = {
    PlayerState: { PLAYING: 1, PAUSED: 2 },
    Player: function () {
      return {
        getPlayerState: () => 2,
        playVideo() { playCalls.push('play'); },
        pauseVideo() { playCalls.push('pause'); },
        getCurrentTime: () => 0,
        seekTo() {},
        setPlaybackRate() {},
      };
    },
  };

  const selfEnter = keydown(byId['spk-choice-self'], 'Enter');
  emitOn(document, 'keydown', selfEnter);
  assert.equal(selfEnter.defaultPrevented, false);
  assert.equal(api.getAssessmentChoice(), 'automatic');
  assert.equal(env.processors.length, 0);
  byId['spk-choice-self'].click();
  assert.equal(api.getAssessmentChoice(), 'self');
  assert.equal(env.processors.length, 0);

  const selfSpace = keydown(byId['spk-choice-self'], 'Space');
  emitOn(document, 'keydown', selfSpace);
  assert.equal(selfSpace.defaultPrevented, false);
  assert.equal(playCalls.length, 0);
  assert.equal(byId['sent-indicator'].textContent, '(Câu hỏi 1/2)');

  api.setAssessmentChoice('automatic');
  const recordEnter = keydown(byId['btn-record'], 'Enter');
  emitOn(document, 'keydown', recordEnter);
  assert.equal(recordEnter.defaultPrevented, false);
  assert.equal(env.processors.length, 0);
  byId['btn-record'].click();
  await delay(30);
  assert.equal(env.processors.length, 1);
  api.dispose();

  const restarted = audioEnvironment(document, null, { SpeechRecognition: undefined });
  bindLifecycleTarget(document, restarted.sandbox);
  const again = loadPractice(restarted.sandbox);
  const backgroundEnter = keydown(document.body, 'Enter');
  emitOn(document, 'keydown', backgroundEnter);
  assert.equal(backgroundEnter.defaultPrevented, true);
  await delay(30);
  assert.equal(restarted.processors.length, 1);

  const tab = keydown(document.body, 'Tab');
  emitOn(document, 'keydown', tab);
  assert.equal(tab.defaultPrevented, false);
  assert.equal(byId['sent-indicator'].textContent, '(Câu hỏi 1/2)');
  const shiftTab = keydown(document.body, 'Tab', { shiftKey: true });
  emitOn(document, 'keydown', shiftTab);
  assert.equal(shiftTab.defaultPrevented, false);
  assert.equal(byId['sent-indicator'].textContent, '(Câu hỏi 1/2)');

  const modified = keydown(document.body, 'Enter', { ctrlKey: true });
  emitOn(document, 'keydown', modified);
  assert.equal(modified.defaultPrevented, false);
  assert.equal(restarted.processors.length, 1);
  const composing = keydown(document.body, 'Space', { isComposing: true });
  emitOn(document, 'keydown', composing);
  assert.equal(composing.defaultPrevented, false);
  const already = keydown(document.body, 'Enter', { defaultPrevented: true, preventDefault() {} });
  emitOn(document, 'keydown', already);
  assert.equal(restarted.processors.length, 1);

  const field = keydown(byId['dic-input'], 'Enter');
  emitOn(document, 'keydown', field);
  assert.equal(field.defaultPrevented, false);
  const rich = keydown(editable, 'Enter');
  emitOn(document, 'keydown', rich);
  assert.equal(rich.defaultPrevented, false);

  const next = keydown(document.body, 'ArrowRight', { shiftKey: true });
  emitOn(document, 'keydown', next);
  assert.equal(next.defaultPrevented, true);
  assert.equal(byId['sent-indicator'].textContent, '(Câu hỏi 2/2)');
  again.dispose();
});

test('R4-C pagehide releases media and pageshow does not record or upload', async () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document);
  bindLifecycleTarget(document, env.sandbox);
  const api = loadPractice(env.sandbox);
  assert.equal((document._listeners.pagehide || []).length, 1);

  api.setAssessmentChoice('self');
  await recordOneTake(api, env.processors);
  assert.equal(env.created.length, 1);
  assert.equal(byId['spk-self-playback'].hidden, false);

  emitOn(document, 'pagehide', { type: 'pagehide', persisted: true });
  assert.equal(env.revoked.length, 1);
  assert.equal(byId['spk-self-playback'].hidden, true);
  assert.equal(byId['spk-self-audio'].src, '');
  emitOn(document, 'pagehide', { type: 'pagehide', persisted: true });
  assert.equal(env.revoked.length, 1);

  emitOn(document, 'pageshow', { type: 'pageshow', persisted: true });
  assert.equal((document._listeners.pagehide || []).length, 1);
  assert.equal(env.fetchCalls.length, 0);
  assert.equal(env.processors.length, 1);
  assert.equal(byId['record-label'].textContent, 'Thu và nghe lại');
  byId['spk-choice-automatic'].click();
  assert.equal(api.getAssessmentChoice(), 'automatic');
  api.setAssessmentChoice('self');
  await recordOneTake(api, env.processors);
  const revokedBeforeMode = env.revoked.length;
  for (const fn of byId['tab-dictation']._listeners.click || []) {
    fn.call(byId['tab-dictation'], { type: 'click', target: byId['tab-dictation'] });
  }
  assert.equal(env.revoked.length, revokedBeforeMode + 1);
  assert.equal(byId['spk-self-playback'].hidden, true);
  assert.equal(env.fetchCalls.length, 0);
  api.dispose();
});

test('R4-C pagehide stops a held recording and aborts a pending assessment', async () => {
  const { document, byId } = practiceDom();
  let releaseFetch;
  const fetchCalls = [];
  const env = audioEnvironment(document, (url, init) => new Promise((resolve, reject) => {
    fetchCalls.push(String(url));
    const signal = init && init.signal;
    const fail = () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' }));
    if (signal && signal.aborted) {
      fail();
      return;
    }
    if (signal) signal.addEventListener('abort', fail, { once: true });
    releaseFetch = () => resolve({
      ok: true,
      status: 200,
      redirected: false,
      json: async () => ({ success: true, canCommitProgress: true, assessmentId: 7, textMatchScore: 90 }),
    });
  }));
  bindLifecycleTarget(document, env.sandbox);
  const api = loadPractice(env.sandbox);

  const started = api.startRecording();
  await delay(30);
  assert.match(byId['record-label'].textContent, /Đang ghi âm/);
  emitOn(document, 'pagehide', { type: 'pagehide', persisted: false });
  await Promise.resolve(started);
  assert.equal(env.stopped.length > 0, true);
  assert.equal(fetchCalls.length, 0);
  assert.equal(byId['record-label'].textContent, 'Kiểm tra phát âm');

  await api.startRecording();
  await delay(20);
  const processor = env.processors[env.processors.length - 1];
  processor.onaudioprocess({ inputBuffer: { getChannelData: () => { const samples = new Float32Array(1600); samples[0] = 0.2; return samples; } } });
  api.startRecording();
  await delay(40);
  assert.equal(fetchCalls.some((url) => url.includes('/assess')), true);
  emitOn(document, 'pagehide', { type: 'pagehide', persisted: false });
  if (releaseFetch) releaseFetch();
  await delay(30);
  assert.equal(fetchCalls.some((url) => url.includes('/progress')), false);
  assert.equal(env.sentences[0].totalScore, 80);
  api.dispose();
});

const ACTIVE_STATUS = /Đang gửi|Đang ghi âm|Đang thu/;

test('R4-D lifecycle cancellation clears active recording and sending status', async () => {
  const idleDom = practiceDom();
  const idleEnv = audioEnvironment(idleDom.document);
  bindLifecycleTarget(idleDom.document, idleEnv.sandbox);
  const idleApi = loadPractice(idleEnv.sandbox);
  const idleStatus = idleDom.byId['spk-assessment-status'].textContent;
  assert.match(idleStatus, /Đã lưu/);
  emitOn(idleDom.document, 'pagehide', { type: 'pagehide', persisted: true });
  emitOn(idleDom.document, 'pageshow', { type: 'pageshow', persisted: true });
  assert.equal(idleDom.byId['spk-assessment-status'].textContent, idleStatus);
  assert.equal(idleDom.byId['score-total'].textContent, '80');
  idleApi.dispose();

  const selfDom = practiceDom();
  const selfEnv = audioEnvironment(selfDom.document);
  bindLifecycleTarget(selfDom.document, selfEnv.sandbox);
  const selfApi = loadPractice(selfEnv.sandbox);
  selfApi.setAssessmentChoice('self');
  await selfApi.startRecording();
  await delay(30);
  assert.match(selfDom.byId['spk-assessment-status'].textContent, /Đang thu để nghe lại/);
  emitOn(selfDom.document, 'pagehide', { type: 'pagehide', persisted: true });
  emitOn(selfDom.document, 'pageshow', { type: 'pageshow', persisted: true });
  const selfStatus = selfDom.byId['spk-assessment-status'].textContent;
  assert.doesNotMatch(selfStatus, ACTIVE_STATUS);
  assert.equal(selfStatus.length > 0, true);
  assert.equal(selfApi.getAssessmentChoice(), 'self');
  assert.equal(selfDom.byId['record-label'].textContent, 'Thu và nghe lại');
  assert.equal(selfEnv.fetchCalls.length, 0);
  assert.equal(selfEnv.sentences[0].totalScore, 80);
  assert.equal(selfEnv.stopped.length > 0, true);
  emitOn(selfDom.document, 'pageshow', { type: 'pageshow', persisted: true });
  assert.equal(selfDom.byId['spk-assessment-status'].textContent, selfStatus);
  selfApi.dispose();

  const autoDom = practiceDom();
  const autoEnv = audioEnvironment(autoDom.document);
  bindLifecycleTarget(autoDom.document, autoEnv.sandbox);
  const autoApi = loadPractice(autoEnv.sandbox);
  await autoApi.startRecording();
  await delay(30);
  assert.match(autoDom.byId['spk-assessment-status'].textContent, /Đang thu PCM/);
  emitOn(autoDom.document, 'pagehide', { type: 'pagehide', persisted: false });
  emitOn(autoDom.document, 'pageshow', { type: 'pageshow', persisted: true });
  assert.doesNotMatch(autoDom.byId['spk-assessment-status'].textContent, ACTIVE_STATUS);
  assert.equal(autoApi.getAssessmentChoice(), 'automatic');
  assert.equal(autoDom.byId['record-label'].textContent, 'Kiểm tra phát âm');
  assert.equal(autoEnv.fetchCalls.length, 0);
  assert.equal(autoEnv.sentences[0].isPracticed, true);
  autoApi.dispose();

  let releaseFetch;
  const pendingCalls = [];
  const pendingDom = practiceDom();
  const pendingEnv = audioEnvironment(pendingDom.document, (url, init) => new Promise((resolve, reject) => {
    pendingCalls.push(String(url));
    const signal = init && init.signal;
    const fail = () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' }));
    if (signal && signal.aborted) {
      fail();
      return;
    }
    if (signal) signal.addEventListener('abort', fail, { once: true });
    releaseFetch = () => resolve({
      ok: true,
      status: 200,
      redirected: false,
      json: async () => ({ success: true, canCommitProgress: true, assessmentId: 7, textMatchScore: 90, totalScore: 99 }),
    });
  }));
  bindLifecycleTarget(pendingDom.document, pendingEnv.sandbox);
  const pendingApi = loadPractice(pendingEnv.sandbox);
  await pendingApi.startRecording();
  await delay(20);
  const processor = pendingEnv.processors[pendingEnv.processors.length - 1];
  processor.onaudioprocess({ inputBuffer: { getChannelData: () => { const samples = new Float32Array(1600); samples[0] = 0.4; return samples; } } });
  pendingApi.startRecording();
  await delay(40);
  assert.match(pendingDom.byId['spk-assessment-status'].textContent, /Đang gửi máy chủ nhận dạng/);
  emitOn(pendingDom.document, 'pagehide', { type: 'pagehide', persisted: true });
  emitOn(pendingDom.document, 'pageshow', { type: 'pageshow', persisted: true });
  const canceled = pendingDom.byId['spk-assessment-status'].textContent;
  assert.doesNotMatch(canceled, ACTIVE_STATUS);
  assert.doesNotMatch(canceled, /không được gửi|chưa gửi/);
  if (releaseFetch) releaseFetch();
  await delay(30);
  emitOn(pendingDom.document, 'pageshow', { type: 'pageshow', persisted: true });
  assert.equal(pendingDom.byId['spk-assessment-status'].textContent, canceled);
  assert.equal(pendingCalls.some((url) => url.includes('/progress')), false);
  assert.equal(pendingEnv.sentences[0].totalScore, 80);
  assert.equal(pendingDom.byId['score-total'].textContent, '80');
  assert.equal(pendingApi.getAssessmentChoice(), 'automatic');
  pendingApi.dispose();
});

test('switching back to self practice without a clip clears the automatic-mode status', () => {
  const { document, byId } = practiceDom();
  const env = audioEnvironment(document);
  const api = loadPractice(env.sandbox);
  try {
    api.setAssessmentChoice('self');
    api.setAssessmentChoice('automatic');
    assert.match(byId['spk-assessment-status'].textContent, /Đã về chấm tự động/);
    api.setAssessmentChoice('self');
    assert.equal(api.getAssessmentChoice(), 'self');
    assert.match(byId['spk-assessment-status'].textContent, /Tự luyện/);
    assert.doesNotMatch(byId['spk-assessment-status'].textContent, /Đã về chấm tự động/);
    assert.equal(byId['record-label'].textContent, 'Thu và nghe lại');
    assert.equal(byId['score-total'].textContent, '80');
    assert.equal(env.fetchCalls.length, 0);
    assert.equal(env.processors.length, 0);
  } finally {
    api.dispose();
  }
});
