'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');
const script = path.join(__dirname, '../../TCTEnglish/wwwroot/js/writing-recent.js');
const tick = () => new Promise(resolve => setImmediate(resolve));
const result = (data, overrides = {}) => ({ ok: true, status: 200, redirected: false,
  headers: { get: () => 'application/json' }, json: async () => data, ...overrides });
const item = (id, title = 'Bài ' + id) => ({ id, title, level: 'advanced', levelTitle: 'Nâng cao', contentType: 'stories',
  contentTypeTitle: 'Truyện', topic: 'Topic', statusLabel: 'Chưa bắt đầu', completedSentenceCount: 0, sentenceCount: 3,
  lastOpenedAtDisplay: '15/11/2026', startActionLabel: 'Mở bài' });
function element(tag) {
  const el = createElement(tag);
  el.append = (...children) => children.forEach(child => child.nodeType === 11 ? el.append(...child.children) : el.appendChild(child));
  el.replaceChildren = (...children) => { el.children = []; el.append(...children); };
  el.before = child => el.parentNode.insertBefore(child, el);
  return el;
}
function mount(mode, fetch) {
  const { document, body } = createDocument([]);
  document.createElement = element;
  document.createDocumentFragment = () => Object.assign(element('fragment'), { nodeType: 11 });
  document.visibilityState = 'visible';
  const root = element('section');
  root.setAttribute(mode === 'practice' ? 'data-writing-practice' : 'data-writing-recent-list', '');
  root.setAttribute('data-owner-user-id', '101'); root.setAttribute('data-exercise-id', '7');
  const cards = element('div'); cards.setAttribute('data-recent-cards', '');
  const status = element('p'); status.setAttribute('data-recent-status', '');
  const retry = element('button'); retry.setAttribute('data-recent-retry', '');
  const csrf = element('input'); csrf.setAttribute('name', '__RequestVerificationToken'); csrf.value = 'token';
  root.append(cards, status, retry, csrf);
  if (mode !== 'guest') body.appendChild(root);
  const events = {};
  let reloads = 0;
  const window = { location: { origin: 'http://localhost', reload: () => reloads++ },
    addEventListener: (name, fn) => (events[name] ||= []).push(fn) };
  loadScript(script, { document, window, fetch, URL, URLSearchParams, AbortController,
    setTimeout: (...args) => setTimeout(...args).unref(), clearTimeout });
  const fire = (target, name, event = {}) => (target[name] || []).forEach(fn => fn(event));
  fire(document._listeners, 'DOMContentLoaded');
  return { root, cards, status, retry, body, reloads: () => reloads,
    page: (name, event) => fire(events, name, event),
    visibility: value => { document.visibilityState = value; fire(document._listeners, 'visibilitychange'); } };
}

test('guest has no personal requests or lifecycle handlers', async () => {
  let calls = 0; const m = mount('guest', () => calls++);
  m.page('pageshow', { persisted: true }); m.visibility('visible'); await tick(); assert.equal(calls, 0);
});
test('Standard list refresh works without AI form; title encoded as text and link owns metadata', async () => {
  const m = mount('list', async () => result({ success: true, ownerUserId: 101, data: [item(9, '<img onerror=bad>')] }));
  m.retry.click(); await tick();
  assert.equal(m.cards.children.length, 1); assert.equal(m.cards.children[0].children[1].textContent, '<img onerror=bad>');
  assert.match(m.cards.children[0].children.at(-1).href, /level=advanced&contentType=stories&exerciseId=9/);
  assert.equal(m.root.hidden, false);
});
test('older fetch cannot replace newer state even if transport ignores abort', async () => {
  const pending = []; const m = mount('list', () => new Promise(resolve => pending.push(resolve)));
  m.retry.click(); m.retry.click();
  pending[1](result({ success: true, ownerUserId: 101, data: [item(2)] })); await tick();
  pending[0](result({ success: true, ownerUserId: 101, data: [item(1)] })); await tick();
  assert.equal(m.cards.children[0].getAttribute('data-recent-exercise-id'), '2');
});
test('pagehide conceals cached personal cards and invalidates pending fetch; persisted pageshow revalidates', async () => {
  const pending = []; const m = mount('list', () => new Promise(resolve => pending.push(resolve)));
  m.retry.click(); m.page('pagehide');
  pending[0](result({ success: true, ownerUserId: 101, data: [item(1)] })); await tick(); assert.equal(m.root.hidden, true);
  m.page('pageshow', { persisted: true }); assert.equal(m.cards.hidden, true);
  pending[1](result({ success: true, ownerUserId: 101, data: [item(2)] })); await tick(); assert.equal(m.root.hidden, false);
});
test('account change and HTML login redirect cannot display old cards', async () => {
  for (const response of [result({ success: true, ownerUserId: 103, data: [item(3)] }),
    result({}, { redirected: true, headers: { get: () => 'text/html' } })]) {
    const m = mount('list', async () => response); m.retry.click(); await tick();
    assert.equal(m.reloads(), 1); assert.equal(m.root.hidden, true); assert.equal(m.cards.children.length, 0);
  }
});
test('network failure shows real retry, then empty state', async () => {
  let fail = true; const m = mount('list', async () => { if (fail) throw Error('offline'); return result({ success: true, ownerUserId: 101, data: [] }); });
  m.retry.click(); await tick(); assert.match(m.status.textContent, /Chưa tải được/);
  fail = false; m.retry.click(); await tick(); assert.match(m.status.textContent, /chưa mở bài/);
});
test('window blur conceals account content; focus revalidates account even without visibilitychange', async () => {
  let calls = 0;
  const m = mount('list', async () => { calls++; return result({ success: true, ownerUserId: 103, data: [item(3)] }); });
  m.page('blur'); assert.equal(m.root.hidden, true);
  m.page('focus'); m.visibility('visible'); await tick();
  assert.equal(calls, 1); assert.equal(m.reloads(), 1); assert.equal(m.root.hidden, true);
});
test('only initialized practice records a visit; duplicate init deduped and CSRF/keepalive included', async () => {
  const calls = []; const m = mount('practice', async (url, options) => { calls.push({ url, options }); return result({ success: true }); });
  await tick(); assert.equal(calls.length, 0);
  m.root.dispatchEvent({ type: 'writing:initialized' }); m.root.dispatchEvent({ type: 'writing:initialized' }); await tick();
  assert.equal(calls.length, 1); assert.equal(calls[0].url, '/api/writing/7/recent');
  assert.equal(calls[0].options.method, 'POST'); assert.equal(calls[0].options.keepalive, true);
  assert.equal(calls[0].options.headers.RequestVerificationToken, 'token');
});
test('failed visit reports failure without hiding editor or discarding drafts; bfcache practice reloads', async () => {
  const m = mount('practice', async () => { throw Error('offline'); });
  m.root.dispatchEvent({ type: 'writing:initialized' }); await tick();
  assert.notEqual(m.root.hidden, true);
  assert.match(m.body.children.at(-2).children[0].textContent, /Chưa lưu được/);
  m.page('pagehide'); m.page('pageshow', { persisted: true }); assert.equal(m.reloads(), 1); assert.equal(m.root.hidden, true);
});
test('practice activation rechecks account and current exercise; focus/visibility overlap records once', async () => {
  const calls = [];
  const m = mount('practice', async (url, options) => {
    calls.push({ url, options });
    return result(url === '/api/writing/recent' ? { success: true, ownerUserId: 101, data: [] } : { success: true, exerciseId: 7 });
  });
  m.root.dispatchEvent({ type: 'writing:initialized' }); await tick();
  m.page('blur'); assert.equal(m.root.hidden, true);
  m.page('focus'); m.visibility('visible'); m.page('focus'); await tick();
  assert.deepEqual(calls.map(c => c.url), ['/api/writing/7/recent', '/api/writing/recent', '/Home/Writing/Practice/Data?exerciseId=7', '/api/writing/7/recent']);
  assert.equal(m.root.hidden, false);
});
test('removed practice is concealed and reloaded after access revalidation fails', async () => {
  const m = mount('practice', async url => url.includes('/Practice/Data') ? result({}, { status: 404, ok: false }) : result({ success: true, ownerUserId: 101, data: [] }));
  m.root.dispatchEvent({ type: 'writing:initialized' }); await tick();
  m.page('blur'); m.page('focus'); await tick();
  assert.equal(m.root.hidden, true); assert.equal(m.reloads(), 1);
});
