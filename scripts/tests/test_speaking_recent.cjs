'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, loadScript } = require('./dom_fixture.cjs');
const script = path.join(__dirname, '../../TCTEnglish/wwwroot/js/speaking-recent.js');
const flush = () => new Promise(resolve => setImmediate(resolve));
const response = (data = [], extra = {}) => Object.assign({ ok: true, redirected: false,
  headers: { get: () => 'application/json' }, json: async () => ({ success: true, data }) }, extra);

function setup(kind, fetcher = async () => response(), visible = true) {
  const { document } = createDocument([]);
  document.visibilityState = visible ? 'visible' : 'hidden';
  document.activeElement = null;
  const make = document.createElement;
  document.createElement = tag => {
    const el = make(tag);
    el.append = (...children) => children.forEach(child => el.appendChild(child));
    el.replaceChildren = (...children) => { el.children.length = 0; el.append(...children); };
    return el;
  };
  const root = document.createElement('section');
  const node = attr => { const el = document.createElement('div'); el.setAttribute(attr, ''); root.appendChild(el); return el; };
  let items, empty, feedback, retry;
  if (kind === 'practice') {
    root.dataset.speakingVisitUrl = '/api/speaking/701/recent';
    feedback = node('data-recent-visit-feedback'); feedback.hidden = true;
  } else if (kind === 'index') {
    root.setAttribute('data-speaking-recent-list', ''); root.dataset.recentUrl = '/api/speaking/recent';
    items = node('data-recent-items'); empty = node('data-recent-empty');
    feedback = node('data-recent-feedback'); feedback.hidden = true;
    retry = node('data-recent-retry'); retry.hidden = true;
  }
  document.body.appendChild(root);
  const handlers = {}, timers = new Map(), calls = [];
  const window = { location: { origin: 'http://localhost:5100' }, performance: { getEntriesByType: () => [] },
    addEventListener: (type, fn) => { (handlers[type] ||= []).push(fn); } };
  const emit = (type, fields = {}) => (handlers[type] || []).forEach(fn => fn({ type, persisted: false, ...fields }));
  const visibility = value => { document.visibilityState = value; (document._listeners.visibilitychange || []).forEach(fn => fn()); };
  loadScript(script, { document, window, URL, AbortController, console,
    fetch: async (url, options) => { calls.push({ url, options }); return fetcher(url, options); },
    setTimeout: (fn, ms) => { const id = Symbol(); timers.set(id, { fn, ms }); return id; },
    clearTimeout: id => timers.delete(id) });
  const tick = async ms => { for (const [id, timer] of [...timers]) if (timer.ms === ms) { timers.delete(id); timer.fn(); } await flush(); };
  return { root, items, empty, feedback, retry, document, calls, emit, visibility, timers, tick };
}

test('history script is harmless outside Speaking', async () => {
  const f = setup('none'); await flush(); assert.equal(f.calls.length, 0);
});

test('initial visible visit posts once with CSRF and keepalive; focus does not write', async () => {
  const f = setup('practice'); f.emit('pageshow'); f.visibility('visible'); await flush();
  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0].options.method, 'POST');
  assert.equal(f.calls[0].options.headers.RequestVerificationToken, 'test-csrf');
  assert.equal(f.calls[0].options.keepalive, true);
  f.emit('focus'); f.visibility('hidden'); f.visibility('visible'); await flush();
  assert.equal(f.calls.length, 1);
  f.emit('pageshow', { persisted: true }); await flush(); assert.equal(f.calls.length, 2);
});

test('background practice waits for visibility and does not require a player or recording', async () => {
  const f = setup('practice', undefined, false); f.emit('pageshow'); await flush();
  assert.equal(f.calls.length, 0); f.visibility('visible'); await flush(); assert.equal(f.calls.length, 1);
});

test('visit failure is nonblocking and does not retry on ordinary focus', async () => {
  const f = setup('practice', async () => { throw new Error('offline'); }); await flush();
  assert.equal(f.feedback.hidden, false); assert.match(f.feedback.textContent, /vẫn có thể luyện tập/);
  f.visibility('visible'); await flush(); assert.equal(f.calls.length, 1);
});

test('recent cards safely render literal titles, UTC dates and separate behavior hooks', async () => {
  const title = '<img src=x onerror=alert(1)>';
  const f = setup('index', async () => response([{ id: 1, title, youtubeId: 'abcdefghijk',
    thumbnailUrl: 'javascript:alert(1)', lastOpenedAtUtc: '2026-11-13T01:00:00Z', isPrivate: true }]));
  await flush(); assert.equal(f.items.children.length, 1); assert.equal(f.empty.hidden, true);
  assert.equal(f.items.querySelector('h3').textContent, title);
  assert.equal(f.items.querySelectorAll('.vi-video-col').length, 0);
  assert.equal(f.items.querySelector('a').href, '/Speaking/Practice/1');
  assert.match(f.items.querySelector('img').src, /^https:\/\/img.youtube.com/);
  assert.equal(f.items.querySelector('time').dateTime, '2026-11-13T01:00:00Z');
});

test('bfcache Index gets one bounded follow-up and clears it when leaving', async () => {
  const f = setup('index'); await flush();
  f.emit('pageshow', { persisted: true }); await flush();
  assert.equal(f.calls.length, 2); await f.tick(700); assert.equal(f.calls.length, 3);
  await f.tick(700); assert.equal(f.calls.length, 3);
  f.emit('pageshow', { persisted: true }); await flush(); f.emit('pagehide');
  await f.tick(700); assert.equal(f.calls.length, 4);
});

test('bad JSON, auth redirects and oversized responses preserve SSR cards with retry', async () => {
  for (const bad of [response([], { redirected: true }), response([], { headers: { get: () => 'text/html' } }),
    response(Array.from({ length: 7 }, (_, i) => ({ id: i + 1 })))]) {
    const f = setup('index', async () => bad);
    f.items.appendChild(f.document.createElement('article'));
    await flush(); assert.equal(f.items.children.length, 1); assert.equal(f.retry.hidden, false);
    assert.equal(f.root.getAttribute('aria-busy'), 'false');
  }
});

test('a stale earlier response cannot overwrite a newer refresh', async () => {
  let finish;
  const deferred = new Promise(resolve => { finish = resolve; });
  let count = 0;
  const f = setup('index', async () => ++count === 1 ? deferred : response([]));
  f.emit('pageshow', { persisted: true }); await flush();
  finish(response([{ id: 1, title: 'stale', youtubeId: 'abcdefghijk', lastOpenedAtUtc: '2026-11-13T01:00:00Z' }]));
  await flush(); assert.equal(f.items.children.length, 0);
});

test('hung requests have an abort deadline and leave learning usable', async () => {
  const f = setup('practice', async (_, options) => new Promise((_, reject) => {
    options.signal.addEventListener('abort', () => reject(new Error('timeout')));
  }));
  await f.tick(8000); assert.equal(f.feedback.hidden, false); assert.equal(f.calls.length, 1);
});
