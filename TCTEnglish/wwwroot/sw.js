self.addEventListener('push', function (event) {
    var payload = {};
    try {
        payload = event.data ? event.data.json() : {};
    } catch (error) {
        payload = {};
    }

    event.waitUntil((async function () {
        if (typeof payload.accountCode !== 'string') return;
        var response = await fetch('/Account/PushKey', {
            credentials: 'same-origin', cache: 'no-store', redirect: 'error'
        });
        if (!response.ok) return;
        var current = await response.json();
        if (current.accountCode !== payload.accountCode) return;
        await self.registration.showNotification(typeof payload.title === 'string' ? payload.title.slice(0, 160) : 'TCT English', {
            body: typeof payload.body === 'string' ? payload.body.slice(0, 500) : '',
            data: { url: localTarget(payload.url) }
        });
    })().catch(function () { /* Do not display private notifications if the current account cannot be confirmed. */ }));
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    var target = event.notification.data && event.notification.data.url;
    event.waitUntil(clients.openWindow(localTarget(target)));
});

function localTarget(value) {
    try {
        var target = new URL(typeof value === 'string' ? value : '/Account/Profile', self.location.origin);
        if (target.origin === self.location.origin && target.protocol === 'https:' || target.origin === self.location.origin && target.protocol === 'http:') return target.href;
    } catch (error) {}
    return self.location.origin + '/Account/Profile';
}
