'use strict';

/**
 * Behavior tests for Contact UI interactions (contact.js).
 * Executes production TCTEnglish/wwwroot/js/contact.js in a Node harness
 * with fake DOM using scripts/tests/dom_fixture.cjs.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const fs = require('node:fs');
const { createDocument, createElement } = require('./dom_fixture.cjs');

const CONTACT_JS_PATH = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/contact.js');

function setupContactDom(options = {}) {
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
    el.focus = () => {
      doc.activeElement = el;
    };
    return el;
  }

  // Alerts
  if (options.hasStatusAlert) {
    const statusAlert = make('div', 'contactStatus');
    statusAlert.dataset.contactState = options.statusState || 'Invalid';
    body.appendChild(statusAlert);
  }

  if (options.hasSuccessAlert) {
    const successAlert = make('div', 'contactSuccessAlert');
    body.appendChild(successAlert);
  }

  // Form
  const form = make('form', 'contactForm');
  form.checkValidity = () => options.validity !== false;
  body.appendChild(form);

  const nonce = make('input', 'ContactNonce');
  nonce.value = 'synthetic-nonce-123';
  form.appendChild(nonce);

  const nameInput = make('input', 'Name');
  nameInput.value = options.name || '';
  if (options.nativeInvalid) nameInput.validity = { valid: false };
  form.appendChild(nameInput);

  const emailInput = make('input', 'Email');
  emailInput.value = options.email || '';
  form.appendChild(emailInput);

  const subjectSelect = make('select', 'Subject');
  subjectSelect.value = options.subject || 'general';
  form.appendChild(subjectSelect);

  const messageInput = make('textarea', 'Message');
  messageInput.value = options.message || '';
  form.appendChild(messageInput);

  const counter = make('span', 'messageCounter');
  const currentCount = make('span', 'messageCurrentCount');
  currentCount.textContent = String(messageInput.value.length);
  counter.appendChild(currentCount);
  body.appendChild(counter);

  const submitBtn = make('button', 'contactSubmitBtn');
  submitBtn.type = 'submit';
  form.appendChild(submitBtn);

  if (options.nameHasError) {
    nameInput.classList.add('input-validation-error');
  }

  // Query selector support for harness
  form.querySelectorAll = (selector) => {
    if (selector === 'input, select, textarea') {
      return [nonce, nameInput, emailInput, subjectSelect, messageInput];
    }
    return [];
  };

  form.querySelector = (selector) => {
    if (selector.includes('input-validation-error')) {
      return options.nameHasError ? nameInput : null;
    }
    return null;
  };

  const windowListeners = Object.create(null);
  const win = {
    document: doc,
    setTimeout: (fn) => fn(),
    addEventListener: (type, fn) => {
      if (!windowListeners[type]) windowListeners[type] = [];
      windowListeners[type].push(fn);
    },
    removeEventListener: (type, fn) => {
      if (windowListeners[type]) {
        windowListeners[type] = windowListeners[type].filter((cb) => cb !== fn);
      }
    },
    dispatchEvent: (evt) => {
      if (windowListeners[evt.type]) {
        for (const fn of windowListeners[evt.type]) fn(evt);
      }
    },
    jQuery: options.jQuery,
  };

  return { doc, byId, win, form, submitBtn, messageInput, counter, currentCount, nameInput };
}

function runContactScript(context) {
  const code = fs.readFileSync(CONTACT_JS_PATH, 'utf8');
  const sandbox = {
    document: context.doc,
    window: context.win,
    setTimeout: context.win.setTimeout,
    console,
  };
  vm.createContext(sandbox);
  vm.runInContext(code, sandbox);
}

test('Contact UI — updates character counter matching UTF-16 code units', () => {
  const env = setupContactDom({ message: 'Xin chào' });
  runContactScript(env);

  assert.equal(env.currentCount.textContent, '8');

  // Type more Vietnamese text
  env.messageInput.value = 'Xin chào TCT English 🌟';
  env.messageInput.dispatchEvent({ type: 'input' });

  // "Xin chào TCT English " is 21 chars + emoji 🌟 is 2 UTF-16 code units = 23
  assert.equal(env.currentCount.textContent, '23');
  assert.equal(env.counter.classList.contains('counter-limit-reached'), false);
});

test('Contact UI — marks counter limit reached and aria-invalid when message exceeds 2000 code units', () => {
  const env = setupContactDom({ message: 'A'.repeat(2001) });
  runContactScript(env);

  assert.equal(env.currentCount.textContent, '2001');
  assert.equal(env.counter.classList.contains('counter-limit-reached'), true);
  assert.equal(env.messageInput.attributes['aria-invalid'], 'true');

  // Reduce back to 2000
  env.messageInput.value = 'A'.repeat(2000);
  env.messageInput.dispatchEvent({ type: 'input' });

  assert.equal(env.currentCount.textContent, '2000');
  assert.equal(env.counter.classList.contains('counter-limit-reached'), false);
  assert.equal(env.messageInput.attributes['aria-invalid'], undefined);
});

test('Contact UI — double-submit prevention enables loading state on valid submission', () => {
  const env = setupContactDom({ message: 'Nội dung hợp lệ' });
  runContactScript(env);

  assert.equal(env.submitBtn.disabled, false);
  assert.equal(env.submitBtn.classList.contains('is-loading'), false);

  let defaultPrevented = false;
  const submitEvent = {
    type: 'submit',
    defaultPrevented: false,
    preventDefault: () => { defaultPrevented = true; },
  };

  env.form.dispatchEvent(submitEvent);

  assert.equal(env.submitBtn.disabled, true);
  assert.equal(env.submitBtn.classList.contains('is-loading'), true);
  assert.equal(env.submitBtn.attributes['aria-busy'], 'true');
  assert.equal(defaultPrevented, false);

  // Subsequent submit while disabled is prevented
  let secondPrevented = false;
  env.form.dispatchEvent({
    type: 'submit',
    defaultPrevented: false,
    preventDefault: () => { secondPrevented = true; },
  });
  assert.equal(secondPrevented, true);
});

test('Contact UI — restores button state on pageshow navigation', () => {
  const env = setupContactDom({ message: 'Nội dung hợp lệ' });
  runContactScript(env);

  env.form.dispatchEvent({
    type: 'submit',
    defaultPrevented: false,
    preventDefault: () => {},
  });
  assert.equal(env.submitBtn.disabled, true);

  // Trigger pageshow (bfcache navigation back)
  env.win.dispatchEvent({ type: 'pageshow' });

  assert.equal(env.submitBtn.disabled, false);
  assert.equal(env.submitBtn.classList.contains('is-loading'), false);
  assert.equal(env.submitBtn.attributes['aria-busy'], 'false');
});

test('Contact UI — halts submission and prevents loading state when jQuery validation is invalid', () => {
  let validated = false;
  const mockJQuery = () => ({
    valid: () => {
      validated = true;
      return false; // Form is invalid
    },
  });

  const env = setupContactDom({ message: 'Nội dung', jQuery: mockJQuery, nameHasError: true });
  runContactScript(env);

  let prevented = false;
  env.form.dispatchEvent({
    type: 'submit',
    defaultPrevented: false,
    preventDefault: () => { prevented = true; },
  });

  assert.equal(validated, true);
  assert.equal(prevented, true, 'novalidate form must cancel the browser POST');
  // Must NOT enter loading state
  assert.equal(env.submitBtn.disabled, false);
  assert.equal(env.submitBtn.classList.contains('is-loading'), false);
});

test('Contact UI — native invalid form prevents POST and reports validity without jQuery', () => {
  const env = setupContactDom({ validity: false });
  let reported = false;
  env.form.reportValidity = () => { reported = true; };
  runContactScript(env);
  let prevented = false;
  env.form.dispatchEvent({ type: 'submit', defaultPrevented: false, preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(reported, true);
  assert.equal(env.submitBtn.disabled, false);
});

test('Contact UI — native required fields are announced invalid only after a submit attempt', () => {
  const env = setupContactDom({ validity: false, nativeInvalid: true });
  runContactScript(env);
  assert.equal(env.nameInput.attributes['aria-invalid'], undefined);
  env.form.dispatchEvent({ type: 'submit', defaultPrevented: false, preventDefault: () => {} });
  assert.equal(env.nameInput.attributes['aria-invalid'], 'true');
});

test('Contact UI — corrected validation clears stale aria-invalid while preserving message limit', () => {
  const env = setupContactDom({ nameHasError: true });
  runContactScript(env);
  assert.equal(env.nameInput.attributes['aria-invalid'], 'true');
  env.nameInput.classList.remove('input-validation-error');
  env.form.dispatchEvent({ type: 'submit', defaultPrevented: false, preventDefault: () => {} });
  assert.equal(env.nameInput.attributes['aria-invalid'], undefined);
});

test('Contact UI — focuses status alert on load for screen reader announcement', () => {
  const env = setupContactDom({ hasStatusAlert: true, statusState: 'Unknown' });
  runContactScript(env);

  const statusAlert = env.byId['contactStatus'];
  assert.equal(statusAlert.attributes['tabindex'], '-1');
  assert.equal(env.doc.activeElement, statusAlert);
});

test('Contact UI — syncs aria-invalid on input with validation error', () => {
  const env = setupContactDom({ nameHasError: true });
  runContactScript(env);

  assert.equal(env.nameInput.attributes['aria-invalid'], 'true');
});

test('Contact UI — page CSS uses defined shared tokens without a private palette', () => {
  const css = fs.readFileSync(path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/contact.css'), 'utf8');
  const shared = fs.readFileSync(path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/tct-learning-ui.css'), 'utf8');
  const defined = new Set([...shared.matchAll(/(--tct-[\w-]+)\s*:/g)].map(match => match[1]));
  const missing = [...new Set([...css.matchAll(/var\((--tct-[\w-]+)/g)].map(match => match[1]))].filter(name => !defined.has(name));
  assert.deepEqual(missing, []);
  assert.doesNotMatch(css, /#[0-9a-f]{3,8}\b/i);
});
