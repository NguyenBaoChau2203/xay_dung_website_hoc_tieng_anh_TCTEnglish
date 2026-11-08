'use strict';

/**
 * Behavior tests for Goals UI interactions (goals.js).
 * Executes production TCTEnglish/wwwroot/js/goals.js in a Node harness
 * with fake DOM using scripts/tests/dom_fixture.cjs.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const fs = require('node:fs');
const { createDocument, createElement, delay } = require('./dom_fixture.cjs');

const GOALS_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/goals.js');

function setupGoalsDom(options = {}) {
  const { document: doc, byId, body } = createDocument([]);

  function dispatchWithBubble(target, evt) {
    evt.target = target;
    let cur = target;
    while (cur) {
      if (cur._listeners && cur._listeners[evt.type]) {
        for (const fn of cur._listeners[evt.type]) fn.call(cur, evt);
      }
      cur = cur.parentNode;
    }
    if (target !== doc && doc._listeners && doc._listeners[evt.type]) {
      for (const fn of doc._listeners[evt.type]) fn.call(doc, evt);
    }
  }

  doc.dispatchEvent = (evt) => {
    dispatchWithBubble(doc, evt);
  };

  function make(tag, id) {
    const el = createElement(tag, id);
    if (id) byId[id] = el;
    el.removeAttribute = (name) => {
      delete el.attributes[name];
    };
    el.dispatchEvent = (evt) => dispatchWithBubble(el, evt);
    el.click = () => {
      dispatchWithBubble(el, { type: 'click', target: el, preventDefault() {} });
    };
    return el;
  }

  // Header trigger button
  const headerCta = make('button', 'goalHeaderCta');
  headerCta.setAttribute('data-testid', 'goal-header-cta');
  headerCta.setAttribute('data-bs-target', '#goalEditorModal');
  let focusedElement = null;
  headerCta.focus = () => { focusedElement = headerCta; };
  body.appendChild(headerCta);

  // Modal element
  const modal = make('div', 'goalEditorModal');
  modal.dataset.openOnLoad = options.openOnLoad ? 'true' : 'false';
  body.appendChild(modal);

  // Form inside modal
  const form = make('form', 'goalEditorForm');
  form.checkValidity = () => options.validity !== false;
  modal.appendChild(form);

  // Target input
  const targetInput = make('input', 'goalTargetInput');
  targetInput.className = 'goal-input';
  targetInput.value = '10';
  targetInput.focus = () => { focusedElement = targetInput; };
  targetInput.select = () => {};
  form.appendChild(targetInput);

  // Submit button
  const submitBtn = make('button', 'goalSubmitBtn');
  submitBtn.setAttribute('data-testid', 'goal-modal-submit');
  form.appendChild(submitBtn);

  // Stepper buttons
  const decBtn = make('button', 'goalTargetDecBtn');
  form.appendChild(decBtn);
  const incBtn = make('button', 'goalTargetIncBtn');
  form.appendChild(incBtn);

  // Area select & Unit badge
  const areaSelect = make('select', 'goalAreaSelect');
  areaSelect.value = 'Vocabulary';
  form.appendChild(areaSelect);

  const unitBadge = make('span', 'goalUnitBadge');
  form.appendChild(unitBadge);

  // Presets
  const preset5 = make('button', 'preset5');
  preset5.className = 'goal-preset-btn';
  preset5.setAttribute('data-preset', '5');
  form.appendChild(preset5);

  const preset20 = make('button', 'preset20');
  preset20.className = 'goal-preset-btn';
  preset20.setAttribute('data-preset', '20');
  form.appendChild(preset20);

  // Toast
  const toast = make('div', 'goalUpdateToast');
  const dismissBtn = make('button', 'toastDismissBtn');
  dismissBtn.setAttribute('data-goals-dismiss', 'goalUpdateToast');
  toast.appendChild(dismissBtn);
  body.appendChild(toast);

  // Bootstrap Modal mock
  let modalShownCount = 0;
  const mockModalInstance = {
    show: () => {
      modalShownCount++;
      dispatchWithBubble(modal, { type: 'shown.bs.modal' });
    },
    hide: () => {
      dispatchWithBubble(modal, { type: 'hidden.bs.modal' });
    }
  };

  const windowMock = {
    document: doc,
    bootstrap: {
      Modal: {
        getOrCreateInstance: (el) => mockModalInstance
      }
    },
    setTimeout: (fn, ms) => setTimeout(fn, ms),
    clearTimeout: (id) => clearTimeout(id),
    addEventListener: (type, fn) => {
      if (!doc._listeners[type]) doc._listeners[type] = [];
      doc._listeners[type].push(fn);
    }
  };

  return {
    doc,
    body,
    windowMock,
    headerCta,
    modal,
    form,
    targetInput,
    submitBtn,
    preset5,
    preset20,
    decBtn,
    incBtn,
    areaSelect,
    unitBadge,
    toast,
    dismissBtn,
    getFocusedElement: () => focusedElement,
    getModalShownCount: () => modalShownCount,
    mockModalInstance,
    dispatchWithBubble
  };
}

function runGoalsJs(windowMock) {
  const code = fs.readFileSync(GOALS_JS_PATH, 'utf8');
  const context = vm.createContext({
    document: windowMock.document,
    window: windowMock,
    Event: function (type, init) { this.type = type; this.bubbles = !!init?.bubbles; },
    setTimeout: windowMock.setTimeout,
    clearTimeout: windowMock.clearTimeout
  });
  vm.runInContext(code, context);
  // Trigger DOMContentLoaded
  windowMock.document.dispatchEvent({ type: 'DOMContentLoaded' });
}

test('Goals UI — Quick preset buttons update target input', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  assert.equal(env.targetInput.value, '10');
  env.preset20.click();
  assert.equal(env.targetInput.value, '20');
  assert.equal(env.getFocusedElement(), env.targetInput);

  env.preset5.click();
  assert.equal(env.targetInput.value, '5');
});

test('Goals UI — Modal auto-opens when data-open-on-load is true', () => {
  const env = setupGoalsDom({ openOnLoad: true });
  runGoalsJs(env.windowMock);

  assert.equal(env.getModalShownCount(), 1);
});

test('Goals UI — Modal focuses target input on shown, returns focus to trigger on hide', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Click trigger
  env.headerCta.click();

  // Show modal
  env.mockModalInstance.show();
  assert.equal(env.getFocusedElement(), env.targetInput);

  // Hide modal
  env.mockModalInstance.hide();
  assert.equal(env.getFocusedElement(), env.headerCta);
});

test('Goals UI — Double-submit prevention disables submit button on valid submit and restores on hide', async () => {
  const env = setupGoalsDom({ validity: true });
  runGoalsJs(env.windowMock);

  assert.equal(env.submitBtn.disabled, false);

  env.dispatchWithBubble(env.form, { type: 'submit', preventDefault() {} });
  await delay(50);

  assert.equal(env.submitBtn.disabled, true);
  assert.equal(env.submitBtn.getAttribute('aria-busy'), 'true');

  // Modal hidden event resets state
  env.mockModalInstance.hide();
  assert.equal(env.submitBtn.disabled, false);
});

test('Goals UI — Feedback toast adds is-visible and responds to dismiss', async () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  await delay(50);
  assert.equal(env.toast.classList.contains('is-visible'), true);

  env.dismissBtn.click();
  assert.equal(env.toast.classList.contains('is-visible'), false);
});


test('Goals UI — Stepper buttons decrement and increment target value clamped between 0 and 500', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  env.targetInput.value = '10';
  env.decBtn.click();
  assert.equal(env.targetInput.value, 5);

  env.decBtn.click();
  assert.equal(env.targetInput.value, 0);

  // Clamping at min 0
  env.decBtn.click();
  assert.equal(env.targetInput.value, 0);

  // Incrementing from 0 steps by 5
  env.incBtn.click();
  assert.equal(env.targetInput.value, 5);

  env.incBtn.click();
  assert.equal(env.targetInput.value, 10);

  // Sub-5 stepping (e.g. 3 -> 4 -> 5)
  env.targetInput.value = '3';
  env.incBtn.click();
  assert.equal(env.targetInput.value, 4);

  // Clamping at max 500
  env.targetInput.value = '498';
  env.incBtn.click();
  assert.equal(env.targetInput.value, 500);
});

test('Goals UI — Preset chips sync active state when input value changes', () => {
  const env = setupGoalsDom();
  runGoalsJs(env.windowMock);

  // Initial value is '10' so preset5 is not active
  assert.equal(env.preset5.classList.contains('is-active'), false);

  // Click preset5
  env.preset5.click();
  assert.equal(env.targetInput.value, '5');
  assert.equal(env.preset5.classList.contains('is-active'), true);
  assert.equal(env.preset20.classList.contains('is-active'), false);

  // Change input to 20
  env.targetInput.value = '20';
  env.dispatchWithBubble(env.targetInput, { type: 'input' });
  assert.equal(env.preset20.classList.contains('is-active'), true);
  assert.equal(env.preset5.classList.contains('is-active'), false);
});
