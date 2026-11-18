/* Order page (/Billing/Order/{code}): draws the MoMo QR code, counts down, and follows the order until it is paid or closed.
   Schedules, stage handling and session-ended detection are in billing-core.js (and tested there). Everything the page says
   is also in the server-rendered HTML, so it stays readable without JavaScript. */
(function () {
    'use strict';

    var core = window.TctBilling;
    var root = document.querySelector('[data-tb-order]');
    if (!core || !root) { return; }

    var code = root.getAttribute('data-order-code');
    var stage = root.getAttribute('data-stage');
    var follows = root.getAttribute('data-follows') === 'true';
    var manualReview = root.getAttribute('data-manual-review') === 'true';
    var statusUrl = root.getAttribute('data-status-url');
    var expiresMs = Number(root.getAttribute('data-expires-ms'));
    var serverOffset = Number(root.getAttribute('data-now-ms')) - Date.now(); // server clock minus browser clock
    var startedAt = Date.now();
    var lastRefreshAt = null;
    var timer = null;
    var inFlight = false;
    var ended = false;

    var live = root.querySelector('[data-tb-live]');
    var liveText = root.querySelector('[data-tb-live-text]');
    var refreshForm = root.querySelector('[data-tb-refresh-form]');
    var refreshButton = root.querySelector('[data-tb-refresh]');

    function say(text, offline) {
        if (liveText && liveText.textContent !== text) { liveText.textContent = text; }
        if (live) { live.classList.toggle('tb-live--offline', !!offline); }
    }

    function checkedLine() { return 'Đang kiểm tra… lần gần nhất ' + core.formatClock(Date.now() + serverOffset); }

    // ---- QR code (pinned qrcodejs) -------------------------------------------------------------
    var qr = document.getElementById('tb-qr');
    if (qr && window.QRCode && qr.getAttribute('data-qr')) {
        /* eslint-disable-next-line no-new */
        new window.QRCode(qr, { text: qr.getAttribute('data-qr'), width: 240, height: 240, correctLevel: window.QRCode.CorrectLevel.M });
    }

    // ---- "Mở ứng dụng MoMo": the app link on a phone, the payment page elsewhere ------------------
    var appLink = root.querySelector('[data-tb-app-link]');
    if (appLink) {
        var coarse = !!(window.matchMedia && window.matchMedia('(pointer: coarse)').matches);
        var deepLink = appLink.getAttribute('data-deeplink');
        var payUrl = appLink.getAttribute('data-payurl');
        if (coarse && deepLink) { appLink.setAttribute('href', deepLink); }
        else if (payUrl) { appLink.setAttribute('href', payUrl); appLink.setAttribute('target', '_blank'); appLink.setAttribute('rel', 'noopener'); }
    }

    // ---- duplicate-submit guard for the development-only simulate button ---------------------------
    var mock = root.querySelector('[data-tb-mock]');
    if (mock) {
        mock.addEventListener('submit', function (event) {
            if (mock.getAttribute('data-sent') === 'true') { event.preventDefault(); return; }
            mock.setAttribute('data-sent', 'true');
            var button = mock.querySelector('button[type="submit"]');
            if (button) { window.setTimeout(function () { button.disabled = true; button.setAttribute('aria-busy', 'true'); }, 0); }
        });
    }

    // ---- countdown (MoMo waiting) ---------------------------------------------------------------
    var countdown = root.querySelector('[data-tb-countdown]');
    if (countdown && stage === 'waiting') {
        var expiredAnnounced = false;
        var tickCountdown = function () {
            var left = core.remainingMs(expiresMs, Date.now(), serverOffset);
            countdown.textContent = core.formatCountdown(left);
            if (left <= 0 && !expiredAnnounced) {
                expiredAnnounced = true;
                say('Mã đã hết hạn. Đang kiểm tra…');
                poll();
            }
        };
        tickCountdown();
        window.setInterval(tickCountdown, 1000);
    }

    // ---- following the order ----------------------------------------------------------------------
    function sessionEnded() {
        ended = true;
        say('Phiên đăng nhập đã kết thúc. Đang chuyển đến trang đăng nhập…');
        window.location.assign(core.loginUrlFor(code));
    }

    function reloadOnce(target) {
        var key = 'tb-reload:' + code + ':' + target;
        try {
            if (window.sessionStorage.getItem(key) === '1') { ended = true; return; } // already reloaded for this stage
            window.sessionStorage.setItem(key, '1');
        } catch (e) { /* storage unavailable: reload anyway */ }
        ended = true;
        window.location.reload();
    }

    function react(data) {
        var result = core.reactTo(data, stage);
        if (result.action === 'reload') { reloadOnce(result.stage); return true; }
        return false;
    }

    async function postRefresh() {
        if (!refreshForm) { return null; }
        var tokenInput = refreshForm.querySelector('input[name="__RequestVerificationToken"]');
        var headers = { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' };
        if (tokenInput) { headers['RequestVerificationToken'] = tokenInput.value; }
        lastRefreshAt = Date.now();
        var response = await window.fetch(refreshForm.getAttribute('action'), {
            method: 'POST', headers: headers, credentials: 'same-origin', redirect: 'manual', cache: 'no-store'
        });
        if (core.isSessionEnded(response)) { sessionEnded(); return null; }
        if (!response.ok) { throw new Error('refresh ' + response.status); }
        return response.json();
    }

    async function poll() {
        if (ended || inFlight || document.hidden || !follows) { return; }
        if (timer) { window.clearTimeout(timer); timer = null; }
        inFlight = true;
        try {
            var response = await window.fetch(statusUrl, {
                headers: { 'Accept': 'application/json' }, credentials: 'same-origin', redirect: 'manual', cache: 'no-store'
            });
            if (core.isSessionEnded(response)) { sessionEnded(); return; }
            if (!response.ok) { throw new Error('status ' + response.status); }
            var data = await response.json();
            if (stage === 'confirming') { say(checkedLine()); }
            if (react(data)) { return; }

            var elapsed = Date.now() - startedAt;
            var since = lastRefreshAt === null ? null : Date.now() - lastRefreshAt;
            if (!manualReview && core.refreshDue(stage, elapsed, since)) {
                var refreshed = await postRefresh();
                if (refreshed) { say(checkedLine()); if (react(refreshed)) { return; } }
            }
        } catch (e) {
            say('Mất kết nối. Đang thử lại…', true);
        } finally {
            inFlight = false;
            schedule();
        }
    }

    function schedule() {
        if (ended || !follows || manualReview || document.hidden) { return; }
        var delay = core.nextDelay(stage, Date.now() - startedAt);
        if (delay < 0) { return; }
        if (timer) { window.clearTimeout(timer); }
        timer = window.setTimeout(poll, delay);
    }

    if (follows && !manualReview) {
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) { if (timer) { window.clearTimeout(timer); timer = null; } }
            else { poll(); }
        });
        schedule();
    }

    // ---- "Kiểm tra lại ngay" ------------------------------------------------------------------------
    if (refreshForm && refreshButton) {
        refreshForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            if (refreshButton.disabled) { return; }
            refreshButton.disabled = true;
            refreshButton.setAttribute('aria-busy', 'true');
            try {
                var data = await postRefresh();
                if (data) {
                    say(data.checked === 'Throttled' ? 'Vừa kiểm tra xong. Vui lòng đợi vài giây rồi thử lại.' : checkedLine());
                    react(data);
                }
            } catch (e) {
                say('Mất kết nối. Đang thử lại…', true);
            } finally {
                window.setTimeout(function () { refreshButton.disabled = false; refreshButton.removeAttribute('aria-busy'); }, 3000);
            }
        });
    }

    var error = document.querySelector('[data-tb-error]');
    if (error) { error.focus(); }
})();
