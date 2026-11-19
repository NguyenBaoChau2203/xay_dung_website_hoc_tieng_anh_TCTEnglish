// Race/error cases adapted from OpenAI's pinned voiceSessionCoordinator.test.ts (MIT).
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { VoiceSessionCoordinator, confirmRemoteSessionClosed } = require('../../TCTEnglish/wwwroot/js/ai-voice-session.js');
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };
function harness(overrides = {}) {
    const h = { creates: 0, closes: 0, remoteCloses: [], streamsClosed: 0 };
    h.c = new VoiceSessionCoordinator({
        createConnection({ onError }) { h.creates++; h.onError = onError; return { muted: false, setMuted(m) { this.muted = m; }, setListeningEnabled(enabled) { h.listeningEnabled = enabled; }, close: () => h.closes++, connect: async exchange => exchange('offer', new AbortController().signal) }; },
        getCsrfToken: async () => 'token', exchangeOffer: async () => 'answer',
        closeRemoteSession: async id => h.remoteCloses.push(id),
        openEvents(e) { h.events = e; return { close: () => h.streamsClosed++ }; },
        onControls: c => h.controls = c, onStatus: s => h.status = s, ...overrides
    }); return h;
}
test('double Start/Stop creates one call and closes once', async () => {
    const h = harness(); await Promise.all([h.c.start(), h.c.start()]); assert.equal(h.creates, 1);
    h.c.toggleMuted(); assert.equal(h.controls.muted, true);
    await Promise.all([h.c.stop(), h.c.stop()]); assert.equal(h.closes, 1); assert.equal(h.remoteCloses.length, 1); assert.equal(h.controls.canStart, true);
});
test('lost signaling response still closes the original client session ID', async () => {
    const response = deferred(); let id;
    const h = harness({ exchangeOffer: input => { id = input.sessionId; return response.promise; } });
    const starting = h.c.start(); while (!id) await Promise.resolve(); await h.c.stop(); response.reject(new Error('lost')); await starting;
    assert.deepEqual(h.remoteCloses, [id]); assert.equal(h.controls.canStart, true);
});
test('failed Stop retains ID and blocks Start until retry confirms cleanup', async () => {
    let failed = true; const ids = [];
    const h = harness({ closeRemoteSession: async id => { ids.push(id); if (failed) throw new Error('pending'); } });
    await h.c.start(); await h.c.stop(); assert.equal(h.controls.canStart, false); assert.equal(h.controls.canStop, true);
    await h.c.start(); assert.equal(h.creates, 1); failed = false; await h.c.stop(); assert.equal(ids[0], ids[1]); assert.equal(h.controls.canStart, true);
});
test('old events/errors cannot alter a replacement', async () => {
    const h = harness(); await h.c.start(); const old = h.events; await h.c.stop(); await h.c.start();
    old.onMessage({ type: 'app.session.closed' }); old.onError(); await Promise.resolve();
    assert.equal(h.controls.canStart, false); assert.equal(h.remoteCloses.length, 1); await h.c.stop();
});
for (const failure of ['SSE', 'media', 'provider']) test(`${failure} error converges on cleanup`, async () => {
    const h = harness(); await h.c.start();
    if (failure === 'SSE') h.events.onError(); else if (failure === 'media') h.onError(); else h.events.onMessage({ type: 'app.error', code: 'VOICE_SESSION_ERROR' });
    for (let i = 0; i < 8; i++) await Promise.resolve(); assert.equal(h.closes, 1); assert.equal(h.streamsClosed, 1); assert.equal(h.remoteCloses.length, 1);
});
test('playback state comes from allowlisted snapshot and ignores generation-done', async () => {
    const h = harness(); await h.c.start(); h.events.onMessage({ type: 'app.session.snapshot', state: 'speaking', turns: [] }); assert.equal(h.status, 'speaking');
    h.events.onMessage({ type: 'response.done' }); assert.equal(h.status, 'speaking');
    h.events.onMessage({ type: 'app.session.snapshot', state: 'listening', turns: [] }); assert.equal(h.status, 'listening'); await h.c.stop();
});
test('sequential turns block input until listening and keep it blocked until playback drains', async () => {
    const h = harness(); await h.c.start(); assert.equal(h.listeningEnabled, false);
    h.events.onMessage({ type: 'app.session.snapshot', state: 'idle' }); assert.equal(h.listeningEnabled, true);
    h.events.onMessage({ type: 'app.agent.state', state: 'thinking' }); assert.equal(h.listeningEnabled, false);
    h.events.onMessage({ type: 'app.session.snapshot', state: 'speaking' }); assert.equal(h.listeningEnabled, false);
    h.c.toggleMuted(); assert.equal(h.controls.muted, true);
    h.events.onMessage({ type: 'response.done' }); assert.equal(h.listeningEnabled, false);
    h.events.onMessage({ type: 'app.session.snapshot', state: 'untrusted' }); assert.equal(h.listeningEnabled, false);
    h.events.onMessage({ type: 'app.session.snapshot', state: 'listening' }); assert.equal(h.listeningEnabled, true);
    assert.equal(h.controls.muted, true); await h.c.stop();
    await h.c.start(); const replacement = h.events; await h.c.stop(); await h.c.start();
    assert.equal(h.listeningEnabled, false);
    replacement.onMessage({ type: 'app.agent.state', state: 'listening' }); assert.equal(h.listeningEnabled, false);
    await h.c.stop();
});

test('Stop waits through accepted cleanup and returns to ready without another click or create', async () => {
    let checks = 0, pending = 0;
    const h = harness({ closeRemoteSession: () => confirmRemoteSessionClosed({
        close: async () => ++checks < 3 ? { closed: false, cleanupPending: true } : { closed: true, cleanupPending: false },
        onPending: () => pending++, wait: async () => {}
    }) });
    await h.c.start(); await h.c.stop();
    assert.equal(checks, 3); assert.equal(pending, 2);
    assert.equal(h.creates, 1); assert.equal(h.closes, 1);
    assert.equal(h.status, 'ready'); assert.equal(h.controls.canStart, true); assert.equal(h.controls.canStop, false);
});

test('unconfirmed cleanup has a deadline and retains the same ID for manual retry', async () => {
    let now = 0, confirmed = false; const ids = [];
    const h = harness({ closeRemoteSession: id => confirmRemoteSessionClosed({
        close: async () => { ids.push(id); return { closed: confirmed, cleanupPending: !confirmed }; },
        wait: async delay => { now += delay; }, now: () => now, timeoutMs: 2000
    }) });
    await h.c.start(); await h.c.stop();
    assert.equal(now, 2000); assert.equal(h.controls.canStart, false); assert.equal(h.controls.canStop, true);
    confirmed = true; await h.c.stop();
    assert.equal(new Set(ids).size, 1); assert.equal(h.controls.canStart, true); assert.equal(h.creates, 1);
});

test('cleanup transport errors and malformed confirmation are never treated as closed', async () => {
    for (const close of [async () => { throw Error('offline'); }, async () => ({}), async () => ({ closed: 'true' })]) {
        await assert.rejects(() => confirmRemoteSessionClosed({ close, wait: async () => assert.fail('must not retry an invalid response') }));
    }
});
