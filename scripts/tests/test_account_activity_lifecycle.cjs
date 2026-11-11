const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const trackerSource = fs.readFileSync(path.join(__dirname, '../../TCTEnglish/wwwroot/js/time-tracker.js'), 'utf8');
const swSource = fs.readFileSync(path.join(__dirname, '../../TCTEnglish/wwwroot/sw.js'), 'utf8');
const drain = () => new Promise(resolve => setImmediate(resolve));

function trackerHarness({ pendingStart = false, token = 'csrf' } = {}) {
    let now = 0;
    let nextTimer = 1;
    let sessionCounter = 0;
    let resolveStart;
    const timers = new Map();
    const handlers = new Map();
    const calls = [];
    const listen = (name, callback) => {
        if (!handlers.has(name)) handlers.set(name, []);
        handlers.get(name).push(callback);
    };
    const document = {
        visibilityState: 'visible',
        currentScript: { getAttribute: key => key === 'data-feature' ? 'Flashcard' : null },
        querySelector: () => ({ value: token }),
        addEventListener: listen
    };
    const window = {
        addEventListener: listen,
        setInterval(callback, interval) {
            const id = nextTimer++;
            timers.set(id, { callback, interval, next: now + interval });
            return id;
        },
        clearInterval(id) { timers.delete(id); }
    };
    class FakeDate extends Date { static now() { return now; } }
    const fetch = (url, options) => {
        const body = JSON.parse(options.body);
        calls.push({ url, options, body });
        if (url.endsWith('activity-session')) {
            const response = { ok: true, json: async () => ({ sessionId: `session-${++sessionCounter}`, leaseToken: 'lease' }) };
            if (pendingStart) return new Promise(resolve => { resolveStart = () => resolve(response); });
            return Promise.resolve(response);
        }
        return Promise.resolve({ ok: true, json: async () => ({ accepted: true }) });
    };
    vm.runInNewContext(trackerSource, { document, window, fetch, Date: FakeDate, console });
    return {
        calls, timers,
        async emit(name) {
            for (const callback of handlers.get(name) || []) callback({ isTrusted: true, target: { tagName: 'BUTTON' } });
            await drain();
        },
        async hidden() { document.visibilityState = 'hidden'; await this.emit('visibilitychange'); },
        async visible() { document.visibilityState = 'visible'; await this.emit('visibilitychange'); },
        async advance(seconds) {
            for (let i = 0; i < seconds; i++) {
                now += 1000;
                for (const [id, timer] of [...timers]) {
                    if (timers.has(id) && timer.next <= now) {
                        timer.next += timer.interval;
                        timer.callback();
                    }
                }
                await drain();
            }
        },
        async resolveStart() { resolveStart(); await drain(); }
    };
}

const checkpoints = harness => harness.calls.filter(call => call.url.endsWith('activity-checkpoint'));

test('tracker flushes visible partial time and ends the lease when the tab is hidden', async () => {
    const h = trackerHarness();
    await h.emit('pointerdown');
    await h.advance(10);
    await h.hidden();
    const last = checkpoints(h).at(-1);
    assert.ok(last, '10 seconds before hiding must not disappear');
    assert.equal(last.body.cumulativeActiveSeconds, 10);
    assert.equal(last.body.endSession, true);
    assert.equal(last.options.headers.RequestVerificationToken, 'csrf');
    assert.ok(!last.url.includes('?'));
    await h.emit('pagehide');
    assert.equal(checkpoints(h).length, 1);
});

test('tracker stops at the idle cutoff and does not count the idle gap on resume', async () => {
    const h = trackerHarness();
    await h.emit('pointerdown');
    await h.advance(120);
    assert.equal(checkpoints(h).at(-1).body.cumulativeActiveSeconds, 90);
    assert.equal(checkpoints(h).at(-1).body.endSession, true);
    await h.emit('keydown');
    await h.advance(10);
    await h.hidden();
    const last = checkpoints(h).at(-1);
    assert.equal(last.body.cumulativeActiveSeconds, 10);
    assert.notEqual(last.body.sessionId, checkpoints(h)[0].body.sessionId);
});

test('a session response arriving after pagehide cannot create a background timer', async () => {
    const h = trackerHarness({ pendingStart: true });
    await h.emit('pointerdown');
    await h.hidden();
    await h.resolveStart();
    assert.equal(h.timers.size, 0);
    await h.advance(60);
    assert.equal(checkpoints(h).length, 0);
});

test('tracker makes no unauthenticated telemetry attempt when there is no CSRF token', async () => {
    const h = trackerHarness({ token: '' });
    await h.emit('pointerdown');
    await h.advance(60);
    assert.equal(h.calls.length, 0);
});

function workerHarness(accountCode) {
    const handlers = new Map();
    const notifications = [];
    const opened = [];
    const self = {
        location: { origin: 'https://local.example.test' },
        addEventListener: (name, callback) => handlers.set(name, callback),
        registration: { showNotification: async (title, options) => notifications.push({ title, options }) }
    };
    const clients = { openWindow: async url => opened.push(url) };
    const fetch = async () => ({ ok: true, json: async () => ({ accountCode }) });
    vm.runInNewContext(swSource, { self, clients, fetch, URL, console });
    return {
        notifications, opened,
        async push(payload) {
            let pending;
            handlers.get('push')({ data: { json: () => payload }, waitUntil: promise => { pending = promise; } });
            await pending;
        },
        async click(url) {
            let pending;
            handlers.get('notificationclick')({ notification: { close() {}, data: { url } }, waitUntil: promise => { pending = promise; } });
            await pending;
        }
    };
}

test('service worker does not display another accounts queued push after account switch', async () => {
    const h = workerHarness('TCT-00000002');
    await h.push({ title: 'reminder', accountCode: 'TCT-00000001' });
    assert.equal(h.notifications.length, 0);
});

test('service worker displays a push only for the current authenticated account', async () => {
    const h = workerHarness('TCT-00000002');
    await h.push({ title: 'reminder', accountCode: 'TCT-00000002', url: '/Goals' });
    assert.equal(h.notifications.length, 1);
});

test('service worker normalizes backslashes before rejecting a cross-origin click target', async () => {
    const h = workerHarness('TCT-00000002');
    await h.click('/\\outside.example.test/path');
    assert.equal(new URL(h.opened[0], 'https://local.example.test').origin, 'https://local.example.test');
});
