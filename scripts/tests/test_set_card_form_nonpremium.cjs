'use strict';

// Regression for SETS-CARD-DETAILS-NONPREMIUM-NULL: the card detail form must mount and fill
// for a Standard account, whose markup has no image-search block (no search button, query
// field or results box).

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const vm = require('node:vm');

const SOURCE = fs.readFileSync(
  path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'set-card-form.js'),
  'utf8'
);

const IMAGE_SEARCH_SELECTORS = [
  '[data-ts-image-search]',
  '[data-ts-image-query]',
  '[data-ts-image-results]',
  '[data-ts-image-locked]',
];

// A permissive stand-in element: stores assigned properties, no-ops for methods.
function stubElement(root) {
  const store = Object.create(null);
  const target = function () {};
  const proxy = new Proxy(target, {
    get(_t, prop) {
      if (prop in store) return store[prop];
      if (prop === 'querySelector') return () => stubElement(root);
      if (prop === 'querySelectorAll') return () => [];
      if (prop === 'classList') return { add() {}, remove() {}, toggle() {}, contains: () => false };
      if (prop === 'children') return [];
      if (prop === 'files') return [];
      if (typeof prop === 'symbol') return undefined;
      return () => stubElement(root);
    },
    set(_t, prop, value) {
      store[prop] = value;
      return true;
    },
    apply() {
      return stubElement(root);
    },
  });
  return proxy;
}

function mountForm({ premium }) {
  const stubs = new Map();
  const root = {
    querySelector(selector) {
      if (!premium && IMAGE_SEARCH_SELECTORS.includes(selector)) return null;
      if (!stubs.has(selector)) stubs.set(selector, stubElement(root));
      return stubs.get(selector);
    },
    querySelectorAll() {
      return [];
    },
  };

  const window = {
    TctSets: {
      el: () => stubElement(root),
      icon: () => stubElement(root),
      posLabels: {},
      fetchJson: () => Promise.resolve([]),
      toast() {},
    },
  };
  const document = {
    createElement: () => stubElement(root),
    createTextNode: () => stubElement(root),
  };
  vm.runInNewContext(SOURCE, { window, document, JSON, Object, Array, String, Promise });

  const form = window.TctSets.cardForm.mount(root, { isPremium: premium, urls: {} });
  return { form, stubs };
}

test('Standard account: setData fills the detail form without throwing', () => {
  const { form } = mountForm({ premium: false });
  assert.doesNotThrow(() => form.setData({ phonetic: '/test/', cefr: 'B1' }));
  assert.equal(form.getData().phonetic, '/test/');
  assert.equal(form.getData().cefr, 'B1');
});

test('Standard account: setData with no data (open details on a fresh card) does not throw', () => {
  const { form } = mountForm({ premium: false });
  assert.doesNotThrow(() => form.setData());
  assert.equal(form.isDirty(), false);
});

test('Premium account: setData still clears the image search results and query', () => {
  const { form, stubs } = mountForm({ premium: true });
  const results = stubs.get('[data-ts-image-results]');
  const query = stubs.get('[data-ts-image-query]');
  results.textContent = 'stale hits';
  query.value = 'stale query';
  form.setData({ phonetic: '/x/' });
  assert.equal(results.textContent, '');
  assert.equal(query.value, '');
});
