'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');

function fixture() {
  const { document, body } = createDocument(['li-search', 'li-no-results', 'li-total-count']);
  document.getElementById('li-no-results').className = 'li-empty li-hidden';
  const tabs = ['all', 'A1', 'C2'].map(level => {
    const tab = createElement('button');
    const add = tab.addEventListener;
    tab.addEventListener = (type, handler) => add(type, handler.bind(tab));
    tab.className = 'btn-tab'; tab.dataset.level = level; body.appendChild(tab); return tab;
  });
  for (const level of ['A1', 'C2']) {
    const section = createElement('section'); section.className = 'li-level-section'; section.dataset.level = level;
    for (let i = 0; i < 15; i++) {
      const card = createElement('div'); card.className = 'li-card-col card-visible';
      card.dataset.title = `${level} lesson ${i}`; card.dataset.topic = level === 'A1' ? 'Daily life' : 'Proficiency'; section.appendChild(card);
    }
    const pagination = createElement('div'); pagination.className = 'li-pagination'; section.appendChild(pagination); body.appendChild(section);
  }
  loadScript(path.join(__dirname, '../../TCTEnglish/wwwroot/js/listening-index.js'), { document, console, setTimeout: fn => { fn(); return 1; }, clearTimeout() {} });
  for (const listener of document._listeners.DOMContentLoaded || []) listener();
  return { document, tabs };
}

test('C2 count and empty state use the intersection of level and search filters', () => {
  const { document, tabs } = fixture();
  tabs[2].dispatchEvent({ type: 'click' });
  assert.equal(document.getElementById('li-total-count').textContent, '15 bài học');
  const search = document.getElementById('li-search'); search.value = 'A1'; search.dispatchEvent({ type: 'keyup' });
  assert.equal(document.getElementById('li-total-count').textContent, '0 bài học');
  assert.equal(document.getElementById('li-no-results').classList.contains('li-hidden'), false);
  assert.ok([...document.querySelectorAll('.li-level-section')].every(s => s.style.display === 'none'));
});

test('C2 pagination exposes all 15 cards across three pages and filters reset it', () => {
  const { document, tabs } = fixture();
  tabs[2].dispatchEvent({ type: 'click' });
  const section = [...document.querySelectorAll('.li-level-section')].find(s => s.dataset.level === 'C2');
  const seen = new Set();
  for (let page = 1; page <= 3; page++) {
    const button = [...section.querySelectorAll('.page-link')].find(b => b.dataset.page === page || b.dataset.page === String(page));
    assert.ok(button); button.dispatchEvent({ type: 'click' });
    for (const card of section.querySelectorAll('.li-card-col')) if (card.style.display === 'block') seen.add(card.dataset.title);
  }
  assert.equal(seen.size, 15);
  tabs[1].dispatchEvent({ type: 'click' }); tabs[2].dispatchEvent({ type: 'click' });
  assert.equal([...section.querySelectorAll('.li-card-col')].filter(c => c.style.display === 'block').length, 6);
});
