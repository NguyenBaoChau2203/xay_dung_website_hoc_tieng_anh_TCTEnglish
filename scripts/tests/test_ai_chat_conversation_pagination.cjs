'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function harness(initialCount, extraCount) {
    const source = fs.readFileSync('TCTEnglish/wwwroot/js/ai-chat.js', 'utf8');
    const start = source.indexOf('    function initConversationPagination(');
    const end = source.indexOf('    async function loadOlderMessages(', start);
    assert.ok(start >= 0 && end > start);
    const rows = Array.from({ length: initialCount }, (_, i) => ({ id: i + 1, hidden: false }));
    const button = () => ({ disabled: false, addEventListener(_, callback) { this.click = callback; } });
    const prev = button(), next = button(), info = {};
    const nav = { classList: { add() {}, remove() {} } }; let hasMore = true, fetches = 0;
    const elements = { '[data-ai-history-nav]': nav, '[data-ai-history-prev]': prev,
        '[data-ai-history-next]': next, '[data-ai-history-page-info]': info };
    const history = { list: { querySelectorAll: () => rows } };
    const context = { document: { querySelector: selector => selector === '[data-ai-load-older="conversations"]'
        ? hasMore ? {} : null : elements[selector] },
        loadOlderConversations: async () => {
            fetches++;
            const offset = rows.length;
            rows.push(...Array.from({ length: extraCount }, (_, i) => ({ id: offset + i + 1, hidden: false })));
            hasMore = false;
            history.updatePagination();
        } };
    vm.runInNewContext(source.slice(start, end) + '\nglobalThis.bind = initConversationPagination;', context);
    context.bind(history);
    return { next, prev, info, rows, fetches: () => fetches, visible: () => rows.filter(x => !x.hidden).map(x => x.id) };
}

test('partial last conversation page fills before advancing so newly loaded rows are not skipped', async () => {
    const h = harness(29, 8); // The server initially loaded30; one conversation was deleted.
    for (let i = 0; i < 4; i++) await h.next.click();
    assert.deepEqual(h.visible(), [25,26,27,28,29]);
    await h.next.click();
    assert.deepEqual(h.visible(), [25,26,27,28,29,30]);
    await h.next.click(); assert.deepEqual(h.visible(), [31,32,33,34,35,36]);
    await h.next.click(); assert.deepEqual(h.visible(), [37]);
    assert.equal(h.next.disabled, true); assert.equal(h.fetches(), 1);
});

test('full last page advances directly into the next server batch', async () => {
    const h = harness(30, 8);
    for (let i = 0; i < 5; i++) await h.next.click();
    assert.deepEqual(h.visible(), [31,32,33,34,35,36]);
    await h.prev.click(); assert.deepEqual(h.visible(), [25,26,27,28,29,30]);
    assert.equal(h.fetches(), 1);
});

test('empty final server batch preserves the last visible page and disables Next', async () => {
    const h = harness(5, 0); await h.next.click();
    assert.deepEqual(h.visible(), [1,2,3,4,5]); assert.equal(h.next.disabled, true);
});
