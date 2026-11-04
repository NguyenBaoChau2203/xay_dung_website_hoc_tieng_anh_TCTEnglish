'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, loadScript, delay } = require('./dom_fixture.cjs');

const SPEAKING_JS = path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'speaking.js');

function loadImport(fetchImpl, { privateCatalog = false } = {}) {
  const { document, byId } = createDocument([
    'mip-submit-btn',
    'mip-url-input',
    'mip-subtitle-input',
    'mip-feedback',
    'mip-btn-label',
    'mip-btn-icon',
    ...(privateCatalog ? ['my-videos-grid', 'my-sec-count', 'my-empty-state', 'deleteVideoModal', 'delete-modal-title'] : []),
  ]);
  if (privateCatalog) {
    byId['my-sec-count'].dataset.totalCount = '25';
    byId['my-videos-grid'].querySelectorAll = () => Array(24).fill({});
  }
  byId['mip-url-input'].value = '';
  byId['mip-subtitle-input'].files = [];
  const calls = [];
  const window = {
    document,
    fetch: async (url, options = {}) => {
      calls.push({ url: String(url), options });
      return fetchImpl(String(url), options, calls);
    },
    FormData: globalThis.FormData,
    setTimeout: globalThis.setTimeout,
    clearTimeout: globalThis.clearTimeout,
    location: { reload() { window.reloaded = true; } },
    TctSpeakingImportTest: { initialDelayMs: 5, maxDelayMs: 20, giveUpMs: 80, reloadDelayMs: 5 },
    navigator: { onLine: true },
    console,
  };
  window.window = window;
  const sandbox = {
    window,
    document,
    console,
    fetch: window.fetch,
    FormData: globalThis.FormData,
    setTimeout: globalThis.setTimeout,
    clearTimeout: globalThis.clearTimeout,
    navigator: window.navigator,
    ...(privateCatalog ? { bootstrap: { Modal: class {
      show() { window.modalShows = (window.modalShows || 0) + 1; }
      hide() {}
    } } } : {}),
  };
  loadScript(SPEAKING_JS, sandbox);
  return { window, byId, calls, document };
}

test('private catalog preserves server total and opens a newly loaded delete button on its first activation', () => {
  const { window, byId, document } = loadImport(async () => ({ ok: true }), { privateCatalog: true });
  assert.equal(byId['my-sec-count'].textContent, '25 bài');
  const button = document.createElement('button');
  button.dataset.id = '25';
  button.dataset.title = 'Loaded private video';
  button.closest = (selector) => selector === '.my-btn-delete' ? button : {};
  // Browsers suppress nested click() during an element's activation.
  button.click = () => { throw new Error('Do not replay a click already in progress'); };
  byId['my-videos-grid'].dispatchEvent({ type: 'click', target: button });
  assert.equal(window.modalShows, 1);
  assert.equal(byId['delete-modal-title'].textContent, 'Loaded private video');
});

test('import form posts the job api and polls until success', async () => {
  let polls = 0;
  const { window, byId, calls } = loadImport(async (url) => {
    if (url === '/api/speaking/imports') {
      return { status: 202, json: async () => ({ jobId: 'job-1', statusUrl: '/api/speaking/imports/job-1' }) };
    }
    polls += 1;
    const status = polls < 2 ? 'queued' : 'succeeded';
    return {
      ok: true,
      json: async () => ({ status, videoId: status === 'succeeded' ? 9 : null, error: null }),
    };
  });

  byId['mip-url-input'].value = 'https://www.youtube.com/watch?v=abcdefghijk';
  byId['mip-submit-btn'].click();
  await delay(150);
  assert.equal(calls.some((call) => call.url === '/Speaking/My/Create'), false);
  assert.equal(calls[0].url, '/api/speaking/imports');
  assert.equal(window.reloaded, true);
});

test('optional subtitle file uses the subtitle endpoint', async () => {
  const { byId, calls } = loadImport(async () => ({
    status: 400,
    json: async () => ({ error: 'bad subtitle' }),
  }));
  byId['mip-url-input'].value = 'https://www.youtube.com/watch?v=abcdefghijk';
  byId['mip-subtitle-input'].files = [new Blob(['WEBVTT'], { type: 'text/vtt' })];
  byId['mip-submit-btn'].click();
  await delay(20);
  assert.equal(calls[0].url, '/api/speaking/imports/subtitles');
  assert.equal(byId['mip-feedback'].textContent.includes('bad subtitle') || byId['mip-feedback'].innerHTML.includes('bad subtitle'), true);
});

test('poll gives up without cancelling the job and can resume', async () => {
  const { window, byId, calls } = loadImport(async (url) => {
    if (url.endsWith('/imports')) {
      return { status: 202, json: async () => ({ jobId: 'job-2', statusUrl: '/api/speaking/imports/job-2' }) };
    }
    return { ok: true, json: async () => ({ status: 'running', videoId: null }) };
  });
  byId['mip-url-input'].value = 'https://www.youtube.com/watch?v=abcdefghijk';
  byId['mip-submit-btn'].click();
  await delay(200);
  assert.match(byId['mip-feedback'].innerHTML, /job-2/i);
  assert.equal(calls.some((call) => call.url.includes('cancel')), false);
  const before = calls.length;
  window.TctSpeakingImport.resume('job-2');
  await delay(30);
  assert.ok(calls.length > before);
});
