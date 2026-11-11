(function () {
    'use strict';

    var button = document.getElementById('enablePushDevice');
    var removeButton = document.getElementById('disablePushDevice');
    var status = document.getElementById('pushDeviceStatus');
    if (!button) return;
    var initiallyDisabled = button.disabled;
    var busy = false;

    function say(message) { if (status) status.textContent = message; }
    function setBusy(value) {
        busy = value;
        button.disabled = value || initiallyDisabled;
        if (removeButton) removeButton.disabled = value;
        button.setAttribute('aria-busy', value ? 'true' : 'false');
    }
    function supported() {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window && window.isSecureContext;
    }
    async function send(url, subscription) {
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (!token || !token.value) throw new Error('Hãy tải lại trang để lấy mã bảo vệ biểu mẫu.');
        var json = subscription.toJSON();
        return fetch(url, {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token.value },
            body: JSON.stringify({ endpoint: subscription.endpoint, p256dh: json.keys && json.keys.p256dh, auth: json.keys && json.keys.auth })
        });
    }

    button.addEventListener('click', async function () {
        if (busy) return;
        if (!supported()) {
            say('Trình duyệt không hỗ trợ push hoặc không ở ngữ cảnh bảo mật.');
            return;
        }
        // Start permission request synchronously inside the click gesture.
        var permissionRequest = Notification.permission === 'granted'
            ? Promise.resolve('granted') : Notification.requestPermission();
        setBusy(true);
        try {
            var permission = await permissionRequest;
            if (permission !== 'granted') {
                say(permission === 'denied' ? 'Quyền thông báo bị từ chối. Bạn có thể đổi trong cài đặt trình duyệt.' : 'Chưa cấp quyền thông báo.');
                return;
            }
            var keyResponse = await fetch('/Account/PushKey', { credentials: 'same-origin', cache: 'no-store' });
            if (!keyResponse.ok) throw new Error('Phiên đăng nhập đã thay đổi. Hãy tải lại trang.');
            var key = await keyResponse.json();
            if (!key.configured || !key.publicKey) { say('Máy chủ chưa có cấu hình VAPID hợp lệ.'); return; }
            await navigator.serviceWorker.register('/sw.js');
            var registration = await navigator.serviceWorker.ready;
            var options = { userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(key.publicKey) };
            var subscription = await registration.pushManager.getSubscription() || await registration.pushManager.subscribe(options);
            var response = await send('/Account/PushSubscribe', subscription);
            var result = await response.json();
            if (response.status === 409 && result.freshSubscriptionRequired) {
                if (!await subscription.unsubscribe()) throw new Error('Chưa hủy được đăng ký cũ của trình duyệt.');
                subscription = await registration.pushManager.subscribe(options);
                response = await send('/Account/PushSubscribe', subscription);
                result = await response.json();
            }
            if (!response.ok) throw new Error(result.error || 'Không đăng ký được thiết bị.');
            say('Thiết bị đã đăng ký. Lưu tùy chọn nhắc học và Push để nhận thông báo.');
        } catch (error) {
            say(error.message || 'Không đăng ký được thiết bị.');
        } finally { setBusy(false); }
    });

    if (removeButton) removeButton.addEventListener('click', async function () {
        if (busy) return;
        if (!('serviceWorker' in navigator)) { say('Trình duyệt không hỗ trợ service worker.'); return; }
        setBusy(true);
        try {
            var registration = await navigator.serviceWorker.getRegistration('/');
            var subscription = registration && await registration.pushManager.getSubscription();
            if (!subscription) { say('Thiết bị này chưa có đăng ký Push.'); return; }
            var response = await send('/Account/PushUnsubscribe', subscription);
            if (!response.ok && response.status !== 404) throw new Error('Chưa hủy được đăng ký ở máy chủ.');
            if (!await subscription.unsubscribe()) throw new Error('Máy chủ đã hủy; hãy kiểm tra lại quyền Push trong trình duyệt.');
            say('Đã hủy đăng ký thiết bị này. Tùy chọn của các thiết bị khác được giữ nguyên.');
        } catch (error) { say(error.message || 'Chưa hủy được đăng ký thiết bị.'); }
        finally { setBusy(false); }
    });

    function urlBase64ToUint8Array(base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var raw = window.atob(base64);
        var output = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) output[i] = raw.charCodeAt(i);
        return output;
    }
})();
