'use strict';

/**
 * Minimal DOM/fetch fixture for Node client regressions.
 * Not a full browser — only the surface speaking.js / notifications.js need.
 */

function createElement(tagName, id) {
  const el = {
    tagName: String(tagName || 'div').toUpperCase(),
    id: id || '',
    textContent: '',
    innerHTML: '',
    value: '',
    disabled: false,
    style: {},
    dataset: {},
    attributes: Object.create(null),
    children: [],
    parentNode: null,
    _listeners: Object.create(null),
    _className: '',
  };

  const classSet = new Set();
  const syncClassName = () => {
    el._className = [...classSet].join(' ');
  };

  Object.defineProperty(el, 'className', {
    get() {
      return el._className;
    },
    set(value) {
      classSet.clear();
      String(value || '')
        .split(/\s+/)
        .filter(Boolean)
        .forEach((n) => classSet.add(n));
      syncClassName();
    },
    configurable: true,
  });

  el.classList = {
    add(...names) {
      names.forEach((n) => classSet.add(n));
      syncClassName();
    },
    remove(...names) {
      names.forEach((n) => classSet.delete(n));
      syncClassName();
    },
    toggle(name, force) {
      if (force === true) classSet.add(name);
      else if (force === false) classSet.delete(name);
      else if (classSet.has(name)) classSet.delete(name);
      else classSet.add(name);
      syncClassName();
      return classSet.has(name);
    },
    contains(name) {
      return classSet.has(name);
    },
  };

  el.setAttribute = (name, value) => {
    el.attributes[name] = String(value);
    if (name === 'class') el.className = String(value);
  };
  el.getAttribute = (name) =>
    Object.prototype.hasOwnProperty.call(el.attributes, name)
      ? el.attributes[name]
      : null;
  el.addEventListener = (type, fn) => {
    if (!el._listeners[type]) el._listeners[type] = [];
    el._listeners[type].push(fn);
  };
  el.removeEventListener = (type, fn) => {
    const list = el._listeners[type];
    if (!list) return;
    el._listeners[type] = list.filter((f) => f !== fn);
  };
  el.dispatchEvent = (evt) => {
    const list = el._listeners[evt.type] || [];
    for (const fn of list) fn(evt);
  };
  el.click = () => el.dispatchEvent({ type: 'click', target: el, preventDefault() {} });
  el.querySelector = (sel) => queryOne(el, sel);
  el.querySelectorAll = (sel) => queryAll(el, sel);
  el.appendChild = (child) => {
    child.parentNode = el;
    el.children.push(child);
    return child;
  };
  el.remove = () => {
    if (!el.parentNode) return;
    el.parentNode.children = el.parentNode.children.filter((c) => c !== el);
    el.parentNode = null;
  };
  el.scrollIntoView = () => {};
  el.scrollBy = () => {};
  el.insertAdjacentHTML = () => {};
  return el;
}

function matches(el, selector) {
  if (!el || !selector) return false;
  if (selector.startsWith('#')) return el.id === selector.slice(1);
  if (selector.startsWith('.')) {
    const parts = selector.split('.').filter(Boolean);
    return parts.every((p) => el.classList.contains(p));
  }
  const attr = selector.match(/^([a-z0-9-]+)\[([^=\]]+)(?:=\"([^\"]*)\")?\]$/i);
  if (attr) {
    const [, tag, key, val] = attr;
    if (el.tagName !== tag.toUpperCase()) return false;
    if (key.startsWith('data-')) {
      const dataKey = key
        .slice(5)
        .replace(/-([a-z])/g, (_, c) => c.toUpperCase());
      if (val === undefined) return el.dataset[dataKey] != null;
      return String(el.dataset[dataKey]) === val;
    }
    return el.getAttribute(key) === val;
  }
  if (/^[a-z0-9-]+$/i.test(selector)) return el.tagName === selector.toUpperCase();
  // compound: .spk-sent-btn[data-index="0"]
  const compound = selector.match(/^\.([^\[]+)\[([^=\]]+)=\"([^\"]*)\"\]$/);
  if (compound) {
    const [, cls, key, val] = compound;
    if (!el.classList.contains(cls)) return false;
    if (key.startsWith('data-')) {
      const dataKey = key
        .slice(5)
        .replace(/-([a-z])/g, (_, c) => c.toUpperCase());
      return String(el.dataset[dataKey]) === val;
    }
  }
  return false;
}

function walk(root, visit) {
  visit(root);
  for (const child of root.children || []) walk(child, visit);
}

function queryAll(root, selector) {
  const out = [];
  walk(root, (node) => {
    if (node !== root && matches(node, selector)) out.push(node);
    if (node === root && matches(node, selector)) out.push(node);
  });
  // Also search registered ids when querying from document
  return out;
}

function queryOne(root, selector) {
  return queryAll(root, selector)[0] || null;
}

function createDocument(ids) {
  const byId = Object.create(null);
  const body = createElement('body', '');
  const head = createElement('head', '');
  const documentElement = createElement('html', '');

  for (const id of ids) {
    const el = createElement('div', id);
    byId[id] = el;
    body.appendChild(el);
  }

  const document = {
    readyState: 'complete',
    body,
    head,
    documentElement,
    getElementById: (id) => byId[id] || null,
    querySelector: (sel) => {
      if (sel.startsWith('#')) return byId[sel.slice(1)] || null;
      if (sel === 'input[name="__RequestVerificationToken"]') {
        return byId.__csrf || null;
      }
      return queryOne(body, sel) || queryOne(documentElement, sel);
    },
    querySelectorAll: (sel) => {
      if (sel.startsWith('.')) {
        const out = [];
        walk(body, (n) => {
          if (matches(n, sel)) out.push(n);
        });
        return out;
      }
      return queryAll(body, sel);
    },
    createElement: (tag) => createElement(tag),
    addEventListener: (type, fn) => {
      if (!document._listeners) document._listeners = Object.create(null);
      if (!document._listeners[type]) document._listeners[type] = [];
      document._listeners[type].push(fn);
    },
    removeEventListener: (type, fn) => {
      if (!document._listeners || !document._listeners[type]) return;
      document._listeners[type] = document._listeners[type].filter((f) => f !== fn);
    },
    _listeners: Object.create(null),
    _register(id, el) {
      byId[id] = el;
    },
  };

  const csrf = createElement('input', '');
  csrf.name = '__RequestVerificationToken';
  csrf.value = 'test-csrf';
  byId.__csrf = csrf;
  body.appendChild(csrf);

  return { document, byId, body };
}

function loadScript(filePath, sandbox) {
  const fs = require('node:fs');
  const vm = require('node:vm');
  const code = fs.readFileSync(filePath, 'utf8');
  vm.runInNewContext(code, sandbox, { filename: filePath });
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

module.exports = {
  createElement,
  createDocument,
  loadScript,
  delay,
};
