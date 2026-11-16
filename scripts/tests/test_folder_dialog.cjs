'use strict';

// Behaviour of the create-folder dialog block of wwwroot/js/layout.js, run against a minimal fake DOM.

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', '..', 'TCTEnglish', 'wwwroot', 'js', 'layout.js'), 'utf8');

function node(text) {
  const n = { text, cloneNode: () => node(text) };
  return n;
}

function element(extra = {}) {
  const el = { attrs: {}, listeners: {}, hidden: false, value: '', disabled: false, focusCount: 0, selected: false, ...extra };
  el.addEventListener = (type, fn) => { (el.listeners[type] = el.listeners[type] || []).push(fn); };
  el.dispatch = (type, evt = {}) => {
    evt.type = type;
    if (!evt.target) evt.target = el;
    evt.defaultPrevented = false;
    evt.preventDefault = () => { evt.defaultPrevented = true; };
    (el.listeners[type] || []).forEach((fn) => fn(evt));
    return evt;
  };
  el.focus = () => { el.focusCount += 1; el.ownerDocument.activeElement = el; };
  el.select = () => { el.selected = true; };
  el.hasAttribute = (name) => Object.prototype.hasOwnProperty.call(el.attrs, name);
  el.getAttribute = (name) => (el.hasAttribute(name) ? el.attrs[name] : null);
  el.setAttribute = (name, value) => { el.attrs[name] = String(value); };
  el.removeAttribute = (name) => { delete el.attrs[name]; };
  return el;
}

// Builds the dialog, its trigger buttons and a document, then runs layout.js and fires DOMContentLoaded.
function setup({ openOnLoad = false, nativeDialog = true, withDialog = true, triggerInert = false, hash = '' } = {}) {
  const doc = { activeElement: null, listeners: {} };
  const replaced = [];
  const win = { listeners: {}, location: { hash, pathname: '/Sets', search: '?filter=own' }, history: { replaceState: (...args) => replaced.push(args) }, matchMedia: () => ({ matches: false, addEventListener() {} }) };
  win.addEventListener = (type, fn) => { (win.listeners[type] = win.listeners[type] || []).push(fn); };

  const make = (extra) => { const el = element(extra); el.ownerDocument = doc; return el; };

  const menuButton = make();
  const headerLink = make();
  const sidebarTrigger = make(triggerInert ? { closest: (selector) => (selector === "[inert]" ? {} : null) } : {});
  const pageTrigger = make();
  const cancel = make();
  const input = make();
  const count = make({ textContent: '0/100' });
  const error = make({ textContent: '', hidden: true });
  const submit = make({ childNodes: [node('icon'), node('Tạo thư mục')], attrs: { 'data-pending': 'Đang tạo…' } });
  submit.childNodes.forEach((n) => { n.cloneNode = () => node(n.text); });
  Object.defineProperty(submit, 'textContent', {
    get() { return submit.childNodes.map((n) => n.text).join(''); },
    set(value) { submit.childNodes = [node(String(value))]; },
  });
  submit.replaceChildren = (...nodes) => { submit.childNodes = nodes; };
  const form = make();

  const dialog = make({ open: false, showCount: 0, closeCount: 0 });
  if (openOnLoad) dialog.attrs['data-open-on-load'] = '';
  if (nativeDialog) {
    dialog.showModal = () => { dialog.open = true; dialog.showCount += 1; };
    dialog.close = () => { dialog.open = false; dialog.closeCount += 1; dialog.dispatch('close'); };
  }
  const queries = {
    form,
    "input[name='name']": input,
    '[data-folder-count]': count,
    '[data-folder-error]': error,
    "button[type='submit']": submit,
  };
  dialog.querySelector = (selector) => queries[selector] || null;

  const byId = { folderModal: withDialog ? dialog : null, newFolderBtn: sidebarTrigger, cancelFolderBtn: cancel, menuToggleBtn: menuButton };
  doc.getElementById = (id) => byId[id] || null;
  doc.querySelector = () => null;
  doc.querySelectorAll = (selector) => (selector.includes('newFolderBtn') ? [sidebarTrigger, pageTrigger, headerLink] : []);
  doc.addEventListener = (type, fn) => { (doc.listeners[type] = doc.listeners[type] || []).push(fn); };

  vm.runInNewContext(SOURCE, { document: doc, window: win, console, setTimeout, localStorage: { getItem: () => null, setItem() {} } });
  (doc.listeners.DOMContentLoaded || []).forEach((fn) => fn());
  return { replaced, headerLink, doc, win, menuButton, dialog, sidebarTrigger, pageTrigger, cancel, input, count, error, submit, form };
}

test('layout.js starts without a dialog on the page', () => {
  assert.doesNotThrow(() => setup({ withDialog: false }));
});

test('the sidebar button opens the dialog, focuses and selects the name and shows 0/100', () => {
  const t = setup();
  const evt = t.sidebarTrigger.dispatch('click');

  assert.equal(evt.defaultPrevented, true);
  assert.equal(t.dialog.open, true);
  assert.equal(t.dialog.showCount, 1);
  assert.equal(t.input.focusCount, 1);
  assert.equal(t.input.selected, true);
  assert.equal(t.count.textContent, '0/100');
});

test('typing updates the counter and clears an earlier message', () => {
  const t = setup();
  t.sidebarTrigger.dispatch('click');
  t.error.textContent = 'Hãy nhập tên thư mục.';
  t.error.hidden = false;

  t.input.value = 'Công việc';
  t.input.dispatch('input');

  assert.equal(t.count.textContent, '9/100');
  assert.equal(t.error.hidden, true);
  assert.equal(t.error.textContent, '');
});

test('a blank name is not sent: the message appears, focus returns to the field and the button stays enabled', () => {
  const t = setup();
  t.sidebarTrigger.dispatch('click');
  t.input.value = '   ';
  const before = t.input.focusCount;

  const evt = t.form.dispatch('submit');

  assert.equal(evt.defaultPrevented, true);
  assert.equal(t.error.textContent, 'Hãy nhập tên thư mục.');
  assert.equal(t.error.hidden, false);
  assert.equal(t.input.focusCount, before + 1);
  assert.equal(t.submit.disabled, false);
});

test('a real name is sent once: the button is disabled and shows the pending text', () => {
  const t = setup();
  t.sidebarTrigger.dispatch('click');
  t.input.value = 'Công việc';

  const evt = t.form.dispatch('submit');

  assert.equal(evt.defaultPrevented, false);
  assert.equal(t.submit.disabled, true);
  assert.equal(t.submit.textContent, 'Đang tạo…');
});

test('coming back to the page (bfcache) restores the button and its icon', () => {
  const t = setup();
  t.sidebarTrigger.dispatch('click');
  t.input.value = 'Công việc';
  t.form.dispatch('submit');

  t.win.listeners.pageshow.forEach((fn) => fn());

  assert.equal(t.submit.disabled, false);
  assert.deepEqual(t.submit.childNodes.map((n) => n.text), ['icon', 'Tạo thư mục']);
});

test('Hủy closes the dialog and gives focus back to the button that opened it', () => {
  const t = setup();
  t.pageTrigger.dispatch('click');
  assert.equal(t.dialog.open, true);

  t.cancel.dispatch('click');

  assert.equal(t.dialog.open, false);
  assert.equal(t.doc.activeElement, t.pageTrigger);
});

test('when the phone drawer made the trigger inert, focus goes to the menu button instead', () => {
  const t = setup({ triggerInert: true });
  t.sidebarTrigger.dispatch('click');

  t.cancel.dispatch('click');

  assert.equal(t.dialog.open, false);
  assert.equal(t.doc.activeElement, t.menuButton);
});

test('the header link to #tao-thu-muc opens the dialog without navigating', () => {
  const t = setup();
  const evt = t.headerLink.dispatch('click');

  assert.equal(evt.defaultPrevented, true);
  assert.equal(t.dialog.open, true);
  assert.equal(t.input.focusCount, 1);
});

test('arriving on /Sets#tao-thu-muc opens the dialog, and closing it drops the hash', () => {
  const t = setup({ hash: '#tao-thu-muc' });
  assert.equal(t.dialog.open, true);

  t.cancel.dispatch('click');

  assert.deepEqual(t.replaced, [[null, '', '/Sets?filter=own']]);
});

test('changing the hash to #tao-thu-muc on the page opens the dialog', () => {
  const t = setup();
  assert.equal(t.dialog.open, false);

  t.win.location.hash = '#tao-thu-muc';
  t.win.listeners.hashchange.forEach((fn) => fn());

  assert.equal(t.dialog.open, true);
});

test('any other hash leaves the dialog closed and the address alone', () => {
  const t = setup({ hash: '#something-else' });
  assert.equal(t.dialog.open, false);
  assert.deepEqual(t.replaced, []);
});

test('a click on the backdrop closes the dialog but a click inside the form does not', () => {
  const t = setup();
  t.sidebarTrigger.dispatch('click');

  t.dialog.dispatch('click', { target: t.input });
  assert.equal(t.dialog.open, true);

  t.dialog.dispatch('click', { target: t.dialog });
  assert.equal(t.dialog.open, false);
  assert.equal(t.doc.activeElement, t.sidebarTrigger);
});

test('after a failed create the server asks for the dialog to reopen, and it does so at load', () => {
  const t = setup({ openOnLoad: true });

  assert.equal(t.dialog.open, true);
  assert.equal(t.dialog.showCount, 1);
  assert.equal(t.input.focusCount, 1);
});

test('without the data-open-on-load attribute the dialog stays closed at load', () => {
  const t = setup({ openOnLoad: false });

  assert.equal(t.dialog.open, false);
  assert.equal(t.dialog.showCount, 0);
});

test('a browser without showModal still opens and closes the dialog through the open attribute', () => {
  const t = setup({ nativeDialog: false });

  t.sidebarTrigger.dispatch('click');
  assert.equal(t.dialog.hasAttribute('open'), true);

  t.cancel.dispatch('click');
  assert.equal(t.dialog.hasAttribute('open'), false);
});

test('layout.js builds no HTML strings for the dialog', () => {
  const block = SOURCE.slice(SOURCE.indexOf('// 3. HỘP THOẠI TẠO THƯ MỤC'), SOURCE.indexOf('// 4. XỬ LÝ USER DROPDOWN'));
  assert.ok(block.length > 500, 'dialog block found');
  assert.ok(!/innerHTML|outerHTML|insertAdjacentHTML|document\.write/.test(block));
});
