// Behavioral cases adapted from OpenAI's pinned audioOnlyWebRtc.test.ts (MIT).
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { AudioOnlyWebRtc } = require('../../TCTEnglish/wwwroot/js/ai-voice-audio.js');
function harness(overrides = {}) {
    const h = { stops: 0, closes: 0, errors: 0, remote: { srcObject: null, play: async () => {} } };
    h.track = { enabled: true, readyState: 'live', stop() { h.stops++; } };
    h.stream = { getTracks: () => [h.track], getAudioTracks: () => [h.track] };
    h.peer = { connectionState: 'new', addTrack: () => {}, close: () => h.closes++, createDataChannel() { assert.fail('Data channels forbidden'); }, createOffer: async () => ({ type: 'offer', sdp: 'audio-offer' }), async setLocalDescription(d) { this.localDescription = d; }, async setRemoteDescription(d) { assert.equal(d.sdp, 'answer'); } };
    h.audio = new AudioOnlyWebRtc({ remoteAudio: h.remote, onError: () => h.errors++, getUserMedia: async () => h.stream, createPeerConnection: () => h.peer, ...overrides });
    return h;
}
test('one audio track, real mute and idempotent resource cleanup', async () => {
    const h = harness(); await h.audio.connect(async offer => { assert.equal(offer, 'audio-offer'); return 'answer'; });
    h.audio.setMuted(true); assert.equal(h.track.enabled, false); assert.equal(h.audio.muted, true);
    h.audio.setMuted(false); assert.equal(h.track.enabled, true);
    h.peer.ontrack({ streams: ['remote'] }); assert.equal(h.remote.srcObject, 'remote');
    h.audio.close(); h.audio.close(); assert.equal(h.stops, 1); assert.equal(h.closes, 1); assert.equal(h.remote.srcObject, null);
});
test('turn gate suppresses input without changing manual mute or stopping the microphone', async () => {
    const h = harness(); await h.audio.connect(async () => 'answer');
    h.audio.setListeningEnabled(false);
    assert.equal(h.track.enabled, false); assert.equal(h.audio.muted, false); assert.equal(h.stops, 0);
    h.audio.setMuted(true); h.audio.setListeningEnabled(true);
    assert.equal(h.track.enabled, false); assert.equal(h.audio.muted, true);
    h.audio.setListeningEnabled(false); h.audio.setMuted(false);
    assert.equal(h.track.enabled, false); assert.equal(h.audio.muted, false);
    h.audio.setListeningEnabled(true); assert.equal(h.track.enabled, true);
    h.audio.close(); h.audio.setListeningEnabled(true); assert.equal(h.stops, 1);
});
test('turn gate set before permission applies to the arriving track', async () => {
    const h = harness(); h.audio.setListeningEnabled(false);
    await h.audio.connect(async () => { assert.equal(h.track.enabled, false); return 'answer'; });
    assert.equal(h.track.enabled, false); h.audio.setListeningEnabled(true);
    assert.equal(h.track.enabled, true); h.audio.close();
});
test('late permission after Stop stops the track without signaling', async () => {
    let resolve; const pending = new Promise(r => resolve = r); let exchanges = 0;
    const h = harness({ getUserMedia: () => pending });
    const start = h.audio.connect(async () => { exchanges++; return 'answer'; });
    h.audio.close(); resolve(h.stream); await assert.rejects(start); assert.equal(h.stops, 1); assert.equal(exchanges, 0);
});
test('permission refusal does not create a peer or signal', async () => {
    let peers = 0; const h = harness({ getUserMedia: async () => { throw new Error('denied'); }, createPeerConnection: () => peers++ });
    await assert.rejects(h.audio.connect(() => assert.fail('no signal'))); assert.equal(peers, 0);
});
for (const failure of ['peer', 'mic', 'playback']) test(`${failure} failure closes owned resources`, async () => {
    const h = harness(); await h.audio.connect(async () => 'answer');
    if (failure === 'peer') { h.peer.connectionState = 'failed'; h.peer.onconnectionstatechange(); }
    else if (failure === 'mic') h.track.onended();
    else { h.remote.play = async () => { throw new Error('autoplay denied'); }; h.peer.ontrack({ streams: [{}] }); await Promise.resolve(); await Promise.resolve(); }
    assert.equal(h.errors, 1); assert.equal(h.stops, 1); assert.equal(h.closes, 1);
});
test('sustained disconnect expires but transient disconnect recovers', async t => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    const h = harness(); await h.audio.connect(async () => 'answer');
    h.peer.connectionState = 'disconnected'; h.peer.onconnectionstatechange(); t.mock.timers.tick(9000);
    h.peer.connectionState = 'connected'; h.peer.onconnectionstatechange(); t.mock.timers.tick(11000); assert.equal(h.errors, 0);
    h.peer.connectionState = 'disconnected'; h.peer.onconnectionstatechange(); t.mock.timers.tick(10000); assert.equal(h.errors, 1);
});
test('close aborts in-flight signaling and helper cannot be reused', async () => {
    const h = harness(); let signal;
    const start = h.audio.connect((_, s) => { signal = s; return new Promise((resolve, reject) => s.addEventListener('abort', () => reject(s.reason))); });
    while (!signal) await Promise.resolve(); h.audio.close(); await assert.rejects(start); assert.equal(signal.aborted, true);
    await assert.rejects(h.audio.connect(async () => 'answer')); assert.equal(h.stops, 1);
});
