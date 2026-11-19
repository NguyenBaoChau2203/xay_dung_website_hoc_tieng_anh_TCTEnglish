const { test } = require('node:test');
const assert = require('node:assert/strict');
const { ChatModeController } = require('../../TCTEnglish/wwwroot/js/ai-chat-mode.js');
const deferred = () => { let resolve; const promise = new Promise(r => resolve = r); return { resolve, promise }; };
function harness(closeVoice = async () => true) {
    const h = { modes: [], busy: [], closes: 0 };
    h.mode = new ChatModeController({ showMode: mode => h.modes.push(mode), onBusy: busy => h.busy.push(busy),
        closeVoice: async () => { h.closes++; return closeVoice(); } });
    return h;
}
test('opening voice changes the panel without starting or closing a call', async () => {
    const h = harness(); await h.mode.request('voice'); await h.mode.request('voice');
    assert.deepEqual(h.modes, ['text', 'voice']); assert.equal(h.closes, 0);
});
test('returning to text waits for confirmed cleanup and deduplicates repeated exits', async () => {
    const closing = deferred(); const h = harness(() => closing.promise); await h.mode.request('voice');
    const first = h.mode.request('text'), second = h.mode.request('text'); await Promise.resolve();
    assert.deepEqual(h.modes, ['text', 'voice']); assert.equal(h.closes, 1);
    assert.equal(await h.mode.request('voice'), false);
    closing.resolve(true); assert.equal(await first, true); assert.equal(await second, true);
    assert.deepEqual(h.modes, ['text', 'voice', 'text']); assert.deepEqual(h.busy, [true, false]);
});
test('pending or failed cleanup retains voice for retry, never hides its Stop controls', async () => {
    for (const fail of [async () => false, async () => { throw Error('offline'); }]) {
        let close = fail; const h = harness(() => close()); await h.mode.request('voice');
        assert.equal(await h.mode.request('text'), false); assert.deepEqual(h.modes, ['text', 'voice']);
        close = async () => true; assert.equal(await h.mode.request('text'), true);
        assert.deepEqual(h.modes, ['text', 'voice', 'text']); assert.equal(h.closes, 2);
    }
});
test('unsupported mode never changes or closes the conversation', async () => {
    const h = harness(); await assert.rejects(() => h.mode.request('other'), TypeError);
    assert.deepEqual(h.modes, ['text']); assert.equal(h.closes, 0);
});
