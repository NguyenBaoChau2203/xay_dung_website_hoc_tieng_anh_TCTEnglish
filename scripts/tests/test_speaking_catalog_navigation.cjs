'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDocument, createElement, loadScript } = require('./dom_fixture.cjs');

const scriptPath = path.join(__dirname, '../../TCTEnglish/wwwroot/js/speaking.js');

for (const [kind, value] of [['topic', 'Business'], ['level', 'B2'], ['level', 'all']]) {
  test(`server ${kind} link (${value}) keeps native navigation with only a partial catalog loaded`, () => {
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

    for (const ctrlKey of [false, true]) {
      let prevented = false;
      link.dispatchEvent({ type: 'click', target: link, ctrlKey, preventDefault() { prevented = true; } });
      assert.equal(prevented, false, 'A partial DOM must not replace a server catalog query');
    }
    assert.equal(historyWrites.length, 0, 'Do not show a server filter URL without loading its result');
  });
}
