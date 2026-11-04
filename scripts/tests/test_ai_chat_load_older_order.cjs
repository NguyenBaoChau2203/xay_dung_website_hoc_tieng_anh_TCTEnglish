'use strict';

/**
 * C4 + D3: production loadOlderMessages keeps chronological order across multiple pages.
 * Executes wwwroot/js/ai-chat.js against the shared Node DOM fixture + fake fetch.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createDocument, createElement } = require('./dom_fixture.cjs');

const AI_CHAT_JS = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/ai-chat.js');

function enhanceTree(root) {
  const stack = [root];
  while (stack.length) {
    const el = stack.pop();
    if (!el || el.__c4) continue;
    el.__c4 = true;

    if (!el.insertBefore) {
      el.insertBefore = (node, reference) => {
        if (node && typeof node.nodeType === 'number' && node.nodeType === 11) {
          const kids = [...(node.children || [])];
          for (const child of kids) {
            el.insertBefore(child, reference);
          }
          return node;
        }

        if (node.parentNode && node.parentNode.children) {
          node.parentNode.children = node.parentNode.children.filter((c) => c !== node);
        }
        node.parentNode = el;
        if (!reference) {
          el.children.push(node);
        } else {
          const idx = el.children.indexOf(reference);
          if (idx < 0) el.children.push(node);
          else el.children.splice(idx, 0, node);
        }
        return node;
      };
    }

    if (!el.remove) {
      el.remove = () => {
        if (el.parentNode && el.parentNode.children) {
          el.parentNode.children = el.parentNode.children.filter((c) => c !== el);
        }
        el.parentNode = null;
      };
    }

    if (!el.closest) {
      el.closest = (selector) => {
        let node = el;
        while (node) {
          if (selector.startsWith('.') && node.classList?.contains(selector.slice(1))) return node;
          if (selector.includes('[data-ai-load-older') && node.getAttribute?.('data-ai-load-older')) return node;
          if (node.classList?.contains('ai-chat-load-older-row')) return node;
          node = node.parentNode;
        }
        return null;
      };
    }

    Object.defineProperty(el, 'nextSibling', {
      configurable: true,
      get() {
        if (!el.parentNode || !el.parentNode.children) return null;
        const idx = el.parentNode.children.indexOf(el);
        return idx >= 0 ? el.parentNode.children[idx + 1] || null : null;
      },
    });

    for (const child of el.children || []) stack.push(child);
  }
}

function messageIds(chatWindow) {
  return chatWindow.children
    .filter((n) => n.getAttribute && n.getAttribute('data-message-id'))
    .map((n) => n.getAttribute('data-message-id'));
}

function buildHarness(pages) {
  const { document, body } = createDocument([]);
  const chatWindow = createElement('div', 'chatWindow');
  Object.defineProperty(chatWindow, 'scrollHeight', { get: () => 200, configurable: true });
  Object.defineProperty(chatWindow, 'scrollTop', { value: 0, writable: true, configurable: true });

  const loadRow = createElement('div', '');
  loadRow.className = 'ai-chat-load-older-row';
  const button = createElement('button', '');
  button.setAttribute('data-ai-load-older', 'messages');
  button.setAttribute('data-ai-load-older-url', '/AI/Chat/Messages');
  button.setAttribute('data-conversation-id', 'c1');
  button.setAttribute('data-ai-message-cursor-time', '2026-01-01T00:04:00.000Z');
  button.setAttribute('data-ai-message-cursor-id', 'm4');
  loadRow.appendChild(button);

  function makeMsg(id) {
    const row = createElement('div', '');
    row.className = 'ai-chat-message';
    row.setAttribute('data-message-id', id);
    row.setAttribute('data-role', 'assistant');
    return row;
  }

  chatWindow.appendChild(loadRow);
  chatWindow.appendChild(makeMsg('m4'));
  chatWindow.appendChild(makeMsg('m5'));
  body.appendChild(chatWindow);

  const shell = createElement('div', '');
  shell.setAttribute('data-ai-avatar', '/x.png');

  enhanceTree(document.body);
  enhanceTree(chatWindow);
  enhanceTree(loadRow);
  enhanceTree(button);
  enhanceTree(shell);

  document.createDocumentFragment = () => {
    const frag = createElement('fragment', '');
    frag.nodeType = 11;
    enhanceTree(frag);
    return frag;
  };

  let request = 0;
  const windowObj = {
    document,
    location: { origin: 'http://localhost' },
    URL,
    fetch: async () => {
      const page = pages[request++] || { hasMore: false, items: [] };
      return {
        ok: true,
        async json() {
          return page;
        },
      };
    },
    tctAiChat: undefined,
  };
  windowObj.window = windowObj;

  vm.runInNewContext(fs.readFileSync(AI_CHAT_JS, 'utf8'), {
    window: windowObj,
    document,
    console,
    setTimeout,
    clearTimeout,
    URL,
    fetch: windowObj.fetch,
  });

  return { windowObj, button, chatWindow, shell, loadRow };
}

test('C4: loadOlderMessages inserts chronological page before existing window', async () => {
  const { windowObj, button, chatWindow, shell } = buildHarness([
    {
      hasMore: false,
      items: [
        { id: 'm0', role: 'assistant', content: 'm0', createdAtUtc: '2026-01-01T00:00:00.000Z' },
        { id: 'm1', role: 'assistant', content: 'm1', createdAtUtc: '2026-01-01T00:01:00.000Z' },
        { id: 'm2', role: 'assistant', content: 'm2', createdAtUtc: '2026-01-01T00:02:00.000Z' },
        { id: 'm3', role: 'assistant', content: 'm3', createdAtUtc: '2026-01-01T00:03:00.000Z' },
      ],
    },
  ]);

  assert.equal(typeof windowObj.tctAiChat.loadOlderMessages, 'function');
  await windowObj.tctAiChat.loadOlderMessages(button, chatWindow, shell);
  assert.deepEqual(messageIds(chatWindow), ['m0', 'm1', 'm2', 'm3', 'm4', 'm5']);
});

test('D3: loadOlderMessages keeps order across two pages with stable top anchor', async () => {
  const { windowObj, button, chatWindow, shell, loadRow } = buildHarness([
    {
      hasMore: true,
      items: [
        { id: 'm2', role: 'assistant', content: 'm2', createdAtUtc: '2026-01-01T00:02:00.000Z' },
        { id: 'm3', role: 'assistant', content: 'm3', createdAtUtc: '2026-01-01T00:03:00.000Z' },
      ],
    },
    {
      hasMore: false,
      items: [
        { id: 'm0', role: 'assistant', content: 'm0', createdAtUtc: '2026-01-01T00:00:00.000Z' },
        { id: 'm1', role: 'assistant', content: 'm1', createdAtUtc: '2026-01-01T00:01:00.000Z' },
      ],
    },
  ]);

  await windowObj.tctAiChat.loadOlderMessages(button, chatWindow, shell);
  assert.deepEqual(messageIds(chatWindow), ['m2', 'm3', 'm4', 'm5']);
  assert.equal(chatWindow.children[0], loadRow, 'load-older control must stay at the top after page 1');

  await windowObj.tctAiChat.loadOlderMessages(button, chatWindow, shell);
  assert.deepEqual(messageIds(chatWindow), ['m0', 'm1', 'm2', 'm3', 'm4', 'm5']);
});
