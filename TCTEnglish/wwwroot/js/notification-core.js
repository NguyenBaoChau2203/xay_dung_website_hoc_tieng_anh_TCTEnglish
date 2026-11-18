/* TCT English notifications: pure logic shared by the bell panel (notifications.js) and the page
   (notification-page.js). No DOM, no network, no storage. Loaded in the browser as window.TctNotificationCore
   and in Node by the tests. The server builds every title, time text and link; this file only counts, groups,
   merges and reduces state, and mirrors the server's local-link rule. */
(function (root, factory) {
    'use strict';
    var api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    else { root.TctNotificationCore = api; }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var MAX_URL_LENGTH = 500;
    var MARK_READ_NAVIGATE_TIMEOUT_MS = 1500;   // a link click waits at most this long for "mark read", then goes on
    var POLL_BASE_MS = 60000;
    var POLL_MAX_MS = 5 * 60000;
    var FAILURE_MESSAGE = 'Chưa đánh dấu được. Vui lòng thử lại.';
    var GROUPS = [
        { key: 'today', label: 'Hôm nay' },
        { key: 'yesterday', label: 'Hôm qua' },
        { key: 'earlier', label: 'Trước đó' }
    ];

    function whole(value) {
        var n = Math.trunc(Number(value));
        return isFinite(n) ? n : 0;
    }

    // ---------------------------------------------------------------- badge and labels

    /** Text of the red badge: nothing for 0, "99+" above 99. */
    function badgeText(count) {
        var n = whole(count);
        if (n <= 0) { return ''; }
        return n > 99 ? '99+' : String(n);
    }

    /** Accessible name of the bell button; the visible badge is hidden from screen readers, this carries the count. */
    function bellLabel(count) {
        var n = whole(count);
        if (n <= 0) { return 'Thông báo'; }
        return n > 99 ? 'Thông báo, hơn 99 chưa đọc' : 'Thông báo, ' + n + ' chưa đọc';
    }

    /** The "2 mới" chip in the panel header. */
    function newChipText(count) {
        var n = whole(count);
        if (n <= 0) { return ''; }
        return (n > 99 ? '99+' : String(n)) + ' mới';
    }

    /** Sub-title of the page. */
    function summaryText(unread) {
        var n = whole(unread);
        return n > 0 ? 'Bạn có ' + n + ' thông báo chưa đọc.' : 'Bạn đã đọc hết thông báo.';
    }

    /** Toast after "mark all read". */
    function markAllMessage(updated) {
        var n = whole(updated);
        return n > 0 ? 'Đã đánh dấu ' + n + ' thông báo là đã đọc.' : 'Không còn thông báo chưa đọc.';
    }

    // ---------------------------------------------------------------- links (mirror of NotificationLinks.ToSafeLocal)

    /** The trimmed local path, or null for anything that is not a single-slash path inside this site. */
    function safeLocalUrl(value) {
        if (typeof value !== 'string') { return null; }
        var v = value.trim();
        if (v.length === 0 || v.charAt(0) !== '/') { return null; }
        if (v.length > 1 && (v.charAt(1) === '/' || v.charAt(1) === '\\')) { return null; }
        for (var i = 0; i < v.length; i++) {
            var code = v.charCodeAt(i);
            if (code === 92 || code < 32 || code === 127) { return null; }   // backslash or control character
        }
        return v.length > MAX_URL_LENGTH ? v.slice(0, MAX_URL_LENGTH) : v;
    }

    function isSafeLocalUrl(value) { return safeLocalUrl(value) !== null; }

    // ---------------------------------------------------------------- lists

    function dayGroupLabel(key) {
        for (var i = 0; i < GROUPS.length; i++) {
            if (GROUPS[i].key === key) { return GROUPS[i].label; }
        }
        return GROUPS[2].label;
    }

    /** Items in their given order, split into "Hôm nay", "Hôm qua", "Trước đó" (empty groups omitted). */
    function groupByDay(items) {
        var buckets = { today: [], yesterday: [], earlier: [] };
        (items || []).forEach(function (item) {
            var key = item && (item.dayGroup === 'today' || item.dayGroup === 'yesterday') ? item.dayGroup : 'earlier';
            buckets[key].push(item);
        });
        return GROUPS
            .filter(function (g) { return buckets[g.key].length > 0; })
            .map(function (g) { return { key: g.key, label: g.label, items: buckets[g.key] }; });
    }

    /** Appends the incoming items that are not in the list yet (by id), keeping order. Returns a new array. */
    function mergeById(existing, incoming) {
        var seen = {};
        var result = [];
        (existing || []).forEach(function (item) {
            if (item && !seen[item.id]) { seen[item.id] = true; result.push(item); }
        });
        (incoming || []).forEach(function (item) {
            if (item && !seen[item.id]) { seen[item.id] = true; result.push(item); }
        });
        return result;
    }

    /** Query string for the next keyset slice from the server's nextCursor; '' when there is none. */
    function cursorQuery(cursor) {
        if (!cursor || typeof cursor.before !== 'string' || cursor.before === '') { return ''; }
        var id = whole(cursor.beforeId);
        return 'before=' + encodeURIComponent(cursor.before) + (id > 0 ? '&beforeId=' + id : '');
    }

    /** Which empty state the page shows: 'none' (nothing ever), 'unread', 'category', or null when there are items. */
    function emptyKind(filter, category, itemCount) {
        if (whole(itemCount) > 0) { return null; }
        if (filter === 'unread') { return 'unread'; }
        if (category) { return 'category'; }
        return 'none';
    }

    // ---------------------------------------------------------------- state (items + unread total)

    function copyState(state) {
        return {
            items: (state.items || []).map(function (i) { return Object.assign({}, i); }),
            unread: Math.max(0, whole(state.unread))
        };
    }

    /** One item read. The unread total drops only when the item was loaded and unread. */
    function markRead(state, id) {
        var next = copyState(state);
        for (var i = 0; i < next.items.length; i++) {
            if (next.items[i].id === id) {
                if (!next.items[i].isRead) {
                    next.items[i].isRead = true;
                    next.unread = Math.max(0, next.unread - 1);
                }
                break;
            }
        }
        return next;
    }

    /** Rolls back markRead after a failed request. */
    function restoreUnread(state, id) {
        var next = copyState(state);
        for (var i = 0; i < next.items.length; i++) {
            if (next.items[i].id === id) {
                if (next.items[i].isRead) {
                    next.items[i].isRead = false;
                    next.unread += 1;
                }
                break;
            }
        }
        return next;
    }

    /** "Mark all" answered: loaded items (of the category, if any) are read; the total is the server's. */
    function applyMarkAll(state, result, category) {
        var next = copyState(state);
        next.items.forEach(function (item) {
            if (!category || item.category === category) { item.isRead = true; }
        });
        if (result && typeof result.unreadCount !== 'undefined') {
            next.unread = Math.max(0, whole(result.unreadCount));
        } else if (!category) {
            next.unread = 0;
        }
        return next;
    }

    // ---------------------------------------------------------------- behaviour helpers

    /** Tab key handling for a tablist: the index to focus next. */
    function nextTabIndex(current, key, count) {
        var n = whole(count);
        if (n <= 0) { return 0; }
        if (key === 'ArrowRight' || key === 'ArrowDown') { return (current + 1) % n; }
        if (key === 'ArrowLeft' || key === 'ArrowUp') { return (current - 1 + n) % n; }
        if (key === 'Home') { return 0; }
        if (key === 'End') { return n - 1; }
        return current;
    }

    /** True when the list has been scrolled to (near) its end and the next slice should load. */
    function nearBottom(metrics, threshold) {
        var gap = (threshold === undefined ? 48 : threshold);
        return whole(metrics.scrollHeight) - whole(metrics.scrollTop) - whole(metrics.clientHeight) <= gap;
    }

    /** Next polling delay: the base after a good answer, doubled up to five minutes after a failure. */
    function nextPollDelay(previousMs, succeeded) {
        if (succeeded) { return POLL_BASE_MS; }
        return Math.min(POLL_MAX_MS, Math.max(POLL_BASE_MS * 2, whole(previousMs) * 2));
    }

    return {
        MARK_READ_NAVIGATE_TIMEOUT_MS: MARK_READ_NAVIGATE_TIMEOUT_MS,
        POLL_BASE_MS: POLL_BASE_MS,
        POLL_MAX_MS: POLL_MAX_MS,
        FAILURE_MESSAGE: FAILURE_MESSAGE,
        badgeText: badgeText,
        bellLabel: bellLabel,
        newChipText: newChipText,
        summaryText: summaryText,
        markAllMessage: markAllMessage,
        safeLocalUrl: safeLocalUrl,
        isSafeLocalUrl: isSafeLocalUrl,
        dayGroupLabel: dayGroupLabel,
        groupByDay: groupByDay,
        mergeById: mergeById,
        cursorQuery: cursorQuery,
        emptyKind: emptyKind,
        markRead: markRead,
        restoreUnread: restoreUnread,
        applyMarkAll: applyMarkAll,
        nextTabIndex: nextTabIndex,
        nearBottom: nearBottom,
        nextPollDelay: nextPollDelay
    };
});
