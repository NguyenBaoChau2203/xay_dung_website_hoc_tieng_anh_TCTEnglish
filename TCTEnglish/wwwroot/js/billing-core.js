/* TCT English billing pages: pure logic (money, saving, summary, countdown, polling schedule, stage handling).
   No DOM, no network, no storage. Loaded in the browser as window.TctBilling and in Node by the tests.
   The Razor views carry the same numbers as data-* attributes; this file only does the arithmetic. */
(function (root, factory) {
    'use strict';
    var api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    else { root.TctBilling = api; }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var NBSP = ' ';
    var DAY_MS = 24 * 60 * 60 * 1000;
    var VN_OFFSET_MS = 7 * 60 * 60 * 1000; // Vietnam has no daylight saving time

    // Polling (plan 5.2): 3 s while a MoMo order waits, 5 s while confirming, 15 s after the first minute,
    // for at most 10 minutes; "confirming" also posts the refresh every 15 s for the first 3 minutes.
    var POLL = {
        waitingMs: 3000,
        confirmingMs: 5000,
        backoffAfterMs: 60 * 1000,
        backoffMs: 15000,
        maxMs: 10 * 60 * 1000,
        refreshEveryMs: 15000,
        refreshForMs: 3 * 60 * 1000
    };

    function pad(n) { return n < 10 ? '0' + n : String(n); }

    /** 399000 -> "399.000 ₫" (dot grouping, non-breaking space before the sign). */
    function formatMoney(amount) {
        var n = Math.trunc(Number(amount) || 0);
        var sign = n < 0 ? '-' : '';
        var digits = String(Math.abs(n)).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
        return sign + digits + NBSP + '₫';
    }

    /** Whole months a plan lasts for the "per month" line: 30 days -> 1, 365 days -> 12. */
    function monthsOf(days) { return Math.max(1, Math.round(Number(days) / 30)); }

    /** Price per month, rounded: 399000 over 365 days -> 33250. */
    function perMonth(price, days) { return Math.round(Number(price) / monthsOf(days)); }

    /**
     * Saving of a plan against buying the cheapest monthly plan for the same number of months, as a whole percent.
     * Returns 0 when there is nothing to show (not positive, or no monthly plan to compare with).
     */
    function savingPercent(price, days, monthlyPrice) {
        if (!(Number(monthlyPrice) > 0) || !(Number(price) > 0)) { return 0; }
        var months = monthsOf(days);
        if (months <= 1) { return 0; }
        var pct = Math.round((1 - Number(price) / (Number(monthlyPrice) * months)) * 100);
        return pct > 0 ? pct : 0;
    }

    /** dd/MM/yyyy in Vietnam time for a time given in milliseconds since the epoch. */
    function formatDate(ms) {
        var d = new Date(Number(ms) + VN_OFFSET_MS);
        return pad(d.getUTCDate()) + '/' + pad(d.getUTCMonth() + 1) + '/' + d.getUTCFullYear();
    }

    /** dd/MM/yyyy HH:mm in Vietnam time. */
    function formatDateTime(ms) {
        var d = new Date(Number(ms) + VN_OFFSET_MS);
        return formatDate(ms) + ' ' + pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes());
    }

    /** HH:mm:ss in Vietnam time (the 'lần gần nhất' line). */
    function formatClock(ms) {
        var d = new Date(Number(ms) + VN_OFFSET_MS);
        return pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes()) + ':' + pad(d.getUTCSeconds());
    }

    /**
     * Summary of the pricing form for the selected plan and method. When the buyer already has Premium, the new plan
     * starts at the current end date (purchases stack), otherwise now.
     */
    function summary(opts) {
        var days = Number(opts.days) || 0;
        var startMs = opts.currentEndMs && opts.currentEndMs > opts.nowMs ? opts.currentEndMs : opts.nowMs;
        var endMs = startMs + days * DAY_MS;
        var total = formatMoney(opts.price);
        var providerName = opts.providerName || '';
        return {
            plan: opts.planName || '',
            validity: days + ' ngày, đến ' + formatDate(endMs),
            total: total,
            button: 'Thanh toán ' + total,
            note: providerName
                ? 'Bạn sẽ được chuyển sang trang của ' + providerName + ' để hoàn tất. TCT English không lưu thông tin thẻ của bạn.'
                : '',
            endMs: endMs
        };
    }

    /** "14:32" for the time left until an order expires; never negative. */
    function formatCountdown(ms) {
        var total = Math.max(0, Math.ceil(Number(ms) / 1000));
        var m = Math.floor(total / 60);
        var s = total % 60;
        return pad(m) + ':' + pad(s);
    }

    /** Time left, using the server's clock: offset = serverNow - clientNow measured when the page loaded. */
    function remainingMs(expiresMs, nowMs, offsetMs) {
        return Math.max(0, Number(expiresMs) - (Number(nowMs) + (Number(offsetMs) || 0)));
    }

    /** Milliseconds until the next status poll, or -1 when polling must stop. */
    function nextDelay(stage, elapsedMs) {
        if (elapsedMs >= POLL.maxMs) { return -1; }
        if (stage !== 'waiting' && stage !== 'confirming') { return -1; }
        if (elapsedMs >= POLL.backoffAfterMs) { return POLL.backoffMs; }
        return stage === 'waiting' ? POLL.waitingMs : POLL.confirmingMs;
    }

    /** True when the "confirming" page should also post the refresh now (every 15 s, first 3 minutes). */
    function refreshDue(stage, elapsedMs, sinceLastRefreshMs) {
        return stage === 'confirming'
            && elapsedMs <= POLL.refreshForMs
            && (sinceLastRefreshMs === null || sinceLastRefreshMs === undefined || sinceLastRefreshMs >= POLL.refreshEveryMs);
    }

    /** What to do with a status answer: reload once on paid, stop on a final state, otherwise keep going. */
    function reactTo(data, currentStage) {
        if (!data || typeof data !== 'object') { return { action: 'continue', stage: currentStage }; }
        var stage = data.stage || currentStage;
        if (data.paid === true || stage === 'paid') { return { action: 'reload', stage: 'paid' }; }
        if (stage === 'not_completed') { return { action: 'reload', stage: stage }; }
        if (stage !== currentStage) { return { action: 'reload', stage: stage }; }
        return { action: 'continue', stage: stage };
    }

    /** A 401, or a fetch that was redirected to the login page, means the session ended (plan 4.6). */
    function isSessionEnded(response) {
        if (!response) { return false; }
        if (response.status === 401) { return true; }
        if (response.type === 'opaqueredirect') { return true; }
        return !!(response.redirected && /\/Account\/Login/i.test(String(response.url || '')));
    }

    /** The login address that brings the buyer back to the order page. Local path only. */
    function loginUrlFor(orderCode) {
        return '/Account/Login?returnUrl=' + encodeURIComponent('/Billing/Order/' + encodeURIComponent(String(orderCode || '')));
    }

    return {
        POLL: POLL,
        formatMoney: formatMoney,
        monthsOf: monthsOf,
        perMonth: perMonth,
        savingPercent: savingPercent,
        formatDate: formatDate,
        formatDateTime: formatDateTime,
        formatClock: formatClock,
        summary: summary,
        formatCountdown: formatCountdown,
        remainingMs: remainingMs,
        nextDelay: nextDelay,
        refreshDue: refreshDue,
        reactTo: reactTo,
        isSessionEnded: isSessionEnded,
        loginUrlFor: loginUrlFor
    };
});
