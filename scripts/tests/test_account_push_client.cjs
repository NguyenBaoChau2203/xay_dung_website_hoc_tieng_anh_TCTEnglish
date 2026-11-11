const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../TCTEnglish/wwwroot/js/account-push.js'), 'utf8');
const drain = () => new Promise(resolve => setImmediate(resolve));

function harness({ permission = 'granted', reject = false, conflict = false } = {}) {
    const events = [];
    const requests = [];
    let subscribeCount = 0;
    let unsubscribeCount = 0;
    let serverSubscriptions = 0;
    function button(label) {
        const handlers = {};
        return { textContent: label, disabled: false, handlers, setAttribute() {},
            addEventListener: (name, handler) => { handlers[name] = handler; } };
    }
    const enable = button('Register');
    const disable = button('Unregister');
    const status = { textContent: '' };
    function subscription(name) {
        return { endpoint: `https://fcm.googleapis.com/fcm/send/${name}`,
            toJSON: () => ({ keys: { p256dh: 'public-key', auth: 'auth-key' } }),
            unsubscribe: async () => { unsubscribeCount++; return true; } };
    }
    const original = subscription('old-device');
    const registration = { pushManager: {
        getSubscription: async () => original,
        subscribe: async () => { subscribeCount++; return subscription('fresh-device'); }
    } };
    const navigator = { serviceWorker: { register: async () => registration, ready: Promise.resolve(registration), getRegistration: async () => registration } };
    const Notification = { permission, requestPermission() { events.push('permission'); return Promise.resolve('granted'); } };
    const window = { isSecureContext: true, PushManager: function () {}, Notification,
        atob: value => Buffer.from(value, 'base64').toString('binary') };
    const document = {
        getElementById: id => ({ enablePushDevice: enable, disablePushDevice: disable, pushDeviceStatus: status }[id] || null),
        querySelector: () => ({ value: 'csrf' })
    };
    const fetch = async (url, options) => {
        requests.push({ url, options });
        if (url.endsWith('PushKey')) {
            events.push('key');
            return { ok: true, json: async () => ({ configured: true, publicKey: 'AQID' }) };
        }
        if (url.endsWith('PushSubscribe')) {
            serverSubscriptions++;
            const failed = reject || (conflict && serverSubscriptions === 1);
            return { ok: !failed, status: conflict && serverSubscriptions === 1 ? 409 : failed ? 400 : 200,
                json: async () => failed ? { error: 'Đăng ký bị từ chối', freshSubscriptionRequired: conflict } : { success: true } };
        }
        return { ok: true, status: 200, json: async () => ({ success: true }) };
    };
    vm.runInNewContext(source, { document, window, navigator, Notification, fetch, Uint8Array, console });
    return { events, requests, status, enable, disable,
        get subscribeCount() { return subscribeCount; }, get unsubscribeCount() { return unsubscribeCount; },
        async click(which) { if (which.handlers.click) await which.handlers.click(); await drain(); } };
}

test('push permission is requested directly by the user gesture before asynchronous key lookup', async () => {
    const h = harness({ permission: 'default' });
    await h.click(h.enable);
    assert.equal(h.events[0], 'permission');
});

test('server rejection is visible and does not claim that the device is registered', async () => {
    const h = harness({ reject: true });
    await h.click(h.enable);
    assert.match(h.status.textContent, /Đăng ký bị từ chối/);
    assert.equal(h.enable.disabled, false);
});

test('account switch creates a fresh browser subscription instead of taking over the old endpoint', async () => {
    const h = harness({ conflict: true });
    await h.click(h.enable);
    assert.equal(h.unsubscribeCount, 1);
    assert.equal(h.subscribeCount, 1);
    assert.equal(h.requests.filter(request => request.url.endsWith('PushSubscribe')).length, 2);
});

test('unregister revokes the owned server subscription and unsubscribes the browser', async () => {
    const h = harness();
    await h.click(h.disable);
    const request = h.requests.find(row => row.url.endsWith('PushUnsubscribe'));
    assert.ok(request);
    assert.equal(request.options.headers.RequestVerificationToken, 'csrf');
    assert.equal(h.unsubscribeCount, 1);
});
