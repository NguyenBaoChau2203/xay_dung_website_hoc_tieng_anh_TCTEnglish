'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');

const scriptPath = path.join(__dirname, '../../TCTEnglish/wwwroot/js/speaking.js');

for (const [kind, value] of [['topic', 'Business'], ['level', 'B2'], ['level', 'all']]) {
  test(`modified ${kind} link (${value}) preserves native open-in-new-tab behavior`, () => {
    const { document, body } = createDocument([]);
    const link = createElement('a');
    link.className = kind === 'topic' ? 'topic-filter-btn spk-chip' : 'level-filter-btn spk-pill-btn';
    link.dataset[kind] = value;
    link.href = `/Speaking?${kind}=${value}`;
    link.setAttribute('href', link.href);
    body.appendChild(link);

    const loadedCard = createElement('article');
    loadedCard.className = 'vi-video-col';
    loadedCard.dataset.level = 'A1';
    loadedCard.dataset.topic = 'General';
    body.appendChild(loadedCard);

    const historyWrites = [];
    const window = {
      document,
      location: { pathname: '/Speaking', search: '?level=A1&page=2' },
      history: { pushState(...args) { historyWrites.push(args); } },
      addEventListener() {},
    };
    loadScript(scriptPath, { window, document, URLSearchParams, console, setTimeout, clearTimeout });

    for (const ctrlKey of [true]) {
      let prevented = false;
      link.dispatchEvent({ type: 'click', target: link, ctrlKey, preventDefault() { prevented = true; } });
      assert.equal(prevented, false, 'A partial DOM must not replace a server catalog query');
    }
    assert.equal(historyWrites.length, 0, 'Do not show a server filter URL without loading its result');
  });
}

function setupCatalog({ fail = false, pages = [17], totals = null, delays = [] } = {}) {
  const { document, byId, body } = createDocument(['vi-no-results', 'vi-search', 'vi-video-count', 'spk-catalog-status']);
  const link = createElement('a'); link.className = 'level-filter-btn spk-pill-btn';
  link.dataset.level = 'B2'; link.setAttribute('href', '/Speaking?level=B2'); body.appendChild(link);
  const old = createElement('section'); old.className = 'vi-level-section'; old.dataset.level = 'A1'; body.appendChild(old);
  const events = {}, calls = [], historyWrites = [];
  byId['vi-search'].value = '';
  const window = { document, location: { pathname: '/Speaking', search: '?level=A1' },
    history: { pushState(state, title, url) { historyWrites.push(url); window.location.search = url.includes('?') ? url.slice(url.indexOf('?')) : ''; } },
    addEventListener(type, handler) { events[type] = handler; } };
  const originalQuery = document.querySelector;
  document.querySelector = selector => {
    const compound = /^\.([^\[]+)\[data-level="([^"]+)"\]$/.exec(selector);
    return compound ? document.querySelectorAll('.' + compound[1]).find(e=>e.dataset.level===compound[2]) || null : originalQuery(selector);
  };
  const originalCreate = document.createElement;
  document.createElement = tag => { const el=originalCreate(tag); el.removeAttribute=name=>{delete el.attributes[name];}; return el; };
  const fetch = async url => { const index=calls.length; calls.push(String(url)); if (delays[index]) await new Promise(r=>setTimeout(r,delays[index])); if (fail) throw Error('offline'); return { ok: true, redirected: false, text: async () => String(index) }; };
  class DOMParser {
    parseFromString(text) {
      const count = pages[Number(text)] || 0;
      const section = createElement('section'); section.className = 'vi-level-section'; section.dataset.level = 'B2';
      const track = createElement('div'); track.id = 'track-B2'; track.className = 'spk-bento-grid';
      track.closest = () => null; track.getBoundingClientRect = () => ({ top:100 }); section.appendChild(track);
      document._register(track.id, track);
      for (let i = 0; i < count; i++) {
        const card = createElement('div'); card.className = 'vi-video-col';
        card.dataset.level = 'B2'; card.dataset.topic = 'Business'; card.dataset.title = 'lesson ' + i;
        card.dataset.videoId = String(Number(text) * 100 + i); track.appendChild(card);
      }
      const pagination = createElement('div'); pagination.className = 'numbered-pagination-container'; pagination.dataset.level = 'B2'; section.appendChild(pagination);
      const counter = createElement('span'); counter.dataset.totalCount = String(totals ? totals[Number(text)] : pages.reduce((a,b)=>a+b,0));
      return { querySelector: q => q === '#vi-video-count' ? counter : null,
        querySelectorAll: q => q === '.vi-level-section' ? [section] : section.querySelectorAll(q) };
    }
  }
  loadScript(scriptPath, { window, document, URLSearchParams, console, setTimeout, clearTimeout, fetch, DOMParser, AbortController });
  return { window, document, byId, link, calls, historyWrites, events, old };
}

test('ordinary filters fetch missing catalog data without document navigation and show eight per slide', async () => {
  const { document, byId, link, calls, historyWrites } = setupCatalog();
  let prevented = false;
  link.dispatchEvent({ type:'click', target:link, preventDefault(){prevented=true;} });
  await new Promise(r=>setTimeout(r,40));
  assert.equal(prevented, true);
  assert.equal(calls.length, 1);
  assert.match(calls[0], /level=B2/);
  assert.equal(historyWrites.length, 1);
  assert.equal(byId['spk-catalog-status'].textContent, '', 'catalog rendering must finish successfully');
  const cards = document.querySelectorAll('.vi-video-col');
  assert.equal(cards.length, 17);
  assert.equal(cards.filter(e=>!e.classList.contains('vi-card-page-hidden')).length, 8);
  const pager=document.querySelector('.numbered-pagination-container');
  pager.children[1].click();
  assert.equal(cards.filter(e=>!e.classList.contains('vi-card-page-hidden')).length, 8);
  pager.children[2].click();
  assert.equal(cards.filter(e=>!e.classList.contains('vi-card-page-hidden')).length, 1);
});

test('filters collect every server page before counting or paginating results', async () => {
  const { document, link, calls } = setupCatalog({ pages:[9,3] });
  link.click(); await new Promise(r=>setTimeout(r,40));
  assert.equal(calls.length, 2);
  assert.equal(document.querySelectorAll('.vi-video-col').length, 12);
});

test('filter network failure retains existing content and announces an error', async () => {
  const { byId, link, old } = setupCatalog({ fail:true });
  link.click(); await new Promise(r=>setTimeout(r,40));
  assert.ok(old.parentNode);
  assert.match(byId['spk-catalog-status'].textContent, /Không thể|không tải/i);
});

test('a slower earlier filter response cannot replace the newest result', async () => {
  const { document, link, byId } = setupCatalog({ pages:[17,3], totals:[17,3], delays:[60,0] });
  link.click(); link.click(); await new Promise(r=>setTimeout(r,100));
  assert.equal(document.querySelectorAll('.vi-video-col').length, 3);
  assert.equal(byId['spk-catalog-status'].textContent, '');
});

test('popstate reloads matching content without adding another history entry', async () => {
  const { window, link, events, historyWrites, calls } = setupCatalog({ pages:[17,17], totals:[17,17] });
  link.click(); await new Promise(r=>setTimeout(r,40));
  window.location.search='?group=level&level=B2'; events.popstate(); await new Promise(r=>setTimeout(r,40));
  assert.equal(calls.length, 2);
  assert.equal(historyWrites.length, 1);
  assert.match(calls[1], /group=level/);
});
