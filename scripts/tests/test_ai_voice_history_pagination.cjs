'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
function harness(count = 20, fetchPage) {
    const source = fs.readFileSync('TCTEnglish/wwwroot/js/ai-voice-page.js', 'utf8');
    const start = source.indexOf("    $('voiceHistoryNext').onclick = async () => {");
    const end = source.indexOf('    // Keep the modals outside', start);
    assert.ok(start >= 0 && end > start);
    const buttons = { voiceHistoryNext: {}, voiceHistoryPrev: {} }, requests = [];
    const state = { $: id => buttons[id], allVoiceRows: Array.from({ length: count }, (_, id) => ({ id })),
        voicePageIndex: Math.ceil(count/4), serverHistoryPage: 1, serverHasMoreHistory: true,
        VOICE_PAGE_SIZE: 4, historyLoading: false, errors: 0,
        api: async path => { requests.push(path); return fetchPage ? fetchPage(requests.length) : Array.from({ length: 5 }, (_, i) => ({ id: count+i })); },
        renderVoiceHistoryPage() {}, explain() { state.errors++; } };
    vm.runInNewContext(source.slice(start, end), state);
    return { state, requests, next: buttons.voiceHistoryNext };
}
test('voice history retries the same server page after a failed fetch', async () => {
    const h = harness(20, async n => { if (n === 1) throw Error('offline'); return [{ id: 20 }]; });
    await h.next.onclick(); assert.equal(h.state.serverHistoryPage, 1);
    await h.next.onclick(); assert.deepEqual(h.requests, ['/history?page=2','/history?page=2']);
    assert.equal(h.state.serverHistoryPage, 2); assert.equal(h.state.errors, 1);
});
test('voice history fills a partial visible page before advancing over new rows', async () => {
    const h = harness(19); await h.next.onclick();
    assert.equal(h.state.voicePageIndex, 5); assert.equal(h.state.allVoiceRows[19].id, 19);
    await h.next.onclick(); assert.equal(h.state.voicePageIndex, 6);
});
test('voice history deduplicates concurrent Next clicks while loading', async () => {
    let resolve; const pending = new Promise(r => { resolve = r; });
    const h = harness(20, () => pending);
    const first = h.next.onclick(); const second = h.next.onclick();
    assert.equal(h.requests.length, 1); assert.equal(h.next.disabled, true);
    resolve([{ id: 20 }]); await Promise.all([first, second]);
    assert.equal(h.state.serverHistoryPage, 2);
});
