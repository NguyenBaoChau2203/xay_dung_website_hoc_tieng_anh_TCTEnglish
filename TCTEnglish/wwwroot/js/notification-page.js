/* =============================================================
   notification-page.js: the /Notification page.
   The server renders the list, filters and counts; this file only adds the mark-read actions:
   - "Đánh dấu đã đọc" on a card, and a link click that marks the card read first (at most 1.5 s), then goes on;
   - "Đánh dấu tất cả đã đọc" in the header.
   It keeps the page, the shell badges and the bell panel in step (event "tct:notifications-changed").
   Needs notification-core.js (loaded by the shell's notification panel). No innerHTML with data.
   ============================================================= */

(function () {
    'use strict';

    var core = window.TctNotificationCore;
    var elRoot = document.getElementById('tnRoot');
    if (!elRoot || !core) { return; }

    var MARK_ALL_URL = '/api/notifications/mark-all-read';
    var TOAST_MS = 4000;

    var elMarkAll    = document.getElementById('tnMarkAll');
    var elSummary    = document.getElementById('tnSummary');
    var elLists      = document.getElementById('tnLists');
    var elEmptyUnread = document.getElementById('tnEmptyUnread');
    var elToast      = document.getElementById('tnToast');
    var elToastText  = document.getElementById('tnToastText');
    var elToastIcon  = document.getElementById('tnToastIcon');

    var filter = elRoot.getAttribute('data-filter') || 'all';
    var toastTimer = null;
    var state = readState();

    // ---------------------------------------------------------------- state from the server-rendered page

    function readState() {
        var items = [];
        var nodes = elRoot.querySelectorAll('.tn-item[data-notification-id]');
        for (var i = 0; i < nodes.length; i++) {
            items.push({
                id: parseInt(nodes[i].getAttribute('data-notification-id'), 10),
                isRead: !nodes[i].classList.contains('is-unread'),
                category: nodes[i].getAttribute('data-category') || ''
            });
        }
        return { items: items, unread: parseInt(elRoot.getAttribute('data-unread'), 10) || 0 };
    }

    function getCsrfToken() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function findItem(id) {
        return elRoot.querySelector('.tn-item[data-notification-id="' + id + '"]');
    }

    // ---------------------------------------------------------------- requests

    // Mark one item read (only confirmed after an HTTP success).
    async function markAsRead(id) {
        var res = await fetch('/api/notifications/' + id + '/mark-read', {
            method: 'POST',
            headers: { 'RequestVerificationToken': getCsrfToken() },
            credentials: 'same-origin'
        });
        if (!res.ok) { throw new Error('mark-read-http'); }
        var body = await res.json().catch(function () { return {}; });
        if (body && body.success === false) { throw new Error('mark-read-payload'); }
        return body || {};
    }

    // ---------------------------------------------------------------- DOM updates (createElement / textContent only)

    function clearUnreadMarks(el) {
        el.classList.remove('is-unread');
        var side = el.querySelector('.tn-item__side');
        if (side) {
            while (side.firstChild) { side.removeChild(side.firstChild); }
        }
    }

    // Puts the unread dot, its hidden text and the "mark read" button back (after a failed request).
    function restoreUnreadPageItem(el) {
        el.classList.add('is-unread');
        var side = el.querySelector('.tn-item__side');
        if (!side || side.querySelector('.tn-item__dot')) { return; }
        var titleEl = el.querySelector('.tn-item__title');
        var title = titleEl ? titleEl.textContent : '';

        var dot = document.createElement('span');
        dot.className = 'tn-item__dot';
        dot.setAttribute('aria-hidden', 'true');
        var hidden = document.createElement('span');
        hidden.className = 'visually-hidden';
        hidden.textContent = 'Chưa đọc';
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'tn-item__read';
        button.setAttribute('data-notification-read', '');
        button.setAttribute('aria-label', 'Đánh dấu đã đọc: ' + title);
        button.title = 'Đánh dấu đã đọc';
        var icon = document.createElement('i');
        icon.className = 'fa-solid fa-check';
        icon.setAttribute('aria-hidden', 'true');
        button.appendChild(icon);

        side.appendChild(dot);
        side.appendChild(hidden);
        side.appendChild(button);
    }

    function setChipCount(key, count) {
        var el = elRoot.querySelector('.tn-chip__count[data-count="' + key + '"]');
        if (!el) { return; }
        var text = core.badgeText(count);
        // The first child is the number; the visually hidden " chưa đọc" span stays.
        var hiddenSpan = el.querySelector('.visually-hidden');
        el.textContent = text;
        if (hiddenSpan) { el.appendChild(hiddenSpan); }
        el.hidden = text === '';
    }

    function updateShellBadges(unread) {
        var text = core.badgeText(unread);
        ['notificationBadge', 'sidebarNotifBadge'].forEach(function (id) {
            var el = document.getElementById(id);
            if (!el) { return; }
            el.textContent = text;
            el.hidden = text === '';
            el.style.display = '';   // the shell may still carry an inline display from the old script
        });
        var bell = document.getElementById('notificationBtn');
        if (bell) {
            bell.setAttribute('aria-label', core.bellLabel(unread));
            bell.classList.toggle('has-notif', unread > 0);
        }
    }

    function applyUnread(unread) {
        state.unread = Math.max(0, unread);
        elRoot.setAttribute('data-unread', String(state.unread));
        if (elSummary) { elSummary.textContent = core.summaryText(state.unread); }
        if (elMarkAll) { elMarkAll.hidden = state.unread <= 0; }
        setChipCount('unread', state.unread);
        updateShellBadges(state.unread);
    }

    // The chip shows the server's count for the whole category (not only the loaded page), so it is nudged by one.
    function adjustChip(key, delta) {
        var el = elRoot.querySelector('.tn-chip__count[data-count="' + key + '"]');
        if (!el || !el.firstChild) { return; }
        var raw = el.firstChild.textContent || '';
        var n = parseInt(raw, 10);
        if (isNaN(n) || raw.indexOf('+') >= 0) { return; }   // "99+" is not exact: leave it
        setChipCount(key, Math.max(0, n + delta));
    }

    function notifyOthers(detail) {
        try {
            document.dispatchEvent(new CustomEvent('tct:notifications-changed', { detail: detail }));
        } catch (_) { /* old browser: the bell refreshes on its next poll */ }
    }

    function showToast(message, danger) {
        if (!elToast || !elToastText) { return; }
        elToastText.textContent = message;
        elToast.classList.toggle('tn-toast--danger', !!danger);
        if (elToastIcon) {
            elToastIcon.className = danger ? 'fa-solid fa-triangle-exclamation' : 'fa-solid fa-circle-check';
        }
        elToast.classList.add('is-visible');
        if (toastTimer) { clearTimeout(toastTimer); }
        toastTimer = setTimeout(function () { elToast.classList.remove('is-visible'); }, TOAST_MS);
    }

    // On the "Chưa đọc" filter nothing is left to list: swap in the empty state and keep the keyboard focus on the page.
    function showUnreadEmpty() {
        if (filter !== 'unread' || !elEmptyUnread) { return; }
        if (elLists) { elLists.hidden = true; }
        elEmptyUnread.hidden = false;
        var title = elEmptyUnread.querySelector('.tn-empty__title');
        if (title) { title.focus(); }
    }

    // ---------------------------------------------------------------- mark one

    // Optimistic: the card turns read at once and is rolled back when the request fails.
    async function markOne(el) {
        var id = parseInt(el.getAttribute('data-notification-id'), 10);
        if (!el.classList.contains('is-unread')) { return true; }

        var category = el.getAttribute('data-category') || '';
        state = core.markRead(state, id);
        clearUnreadMarks(el);
        applyUnread(state.unread);
        adjustChip(category, -1);

        try {
            var body = await markAsRead(id);
            if (typeof body.unreadCount === 'number') { applyUnread(body.unreadCount); }
            notifyOthers({ source: 'page', id: id, unreadCount: state.unread });
            return true;
        } catch (_) {
            state = core.restoreUnread(state, id);
            restoreUnreadPageItem(el);
            applyUnread(state.unread);
            adjustChip(category, 1);
            showToast(core.FAILURE_MESSAGE, true);
            return false;
        }
    }

    // ---------------------------------------------------------------- clicks (one delegated listener)

    elRoot.addEventListener('click', function (e) {
        var target = e.target;
        if (!target || !target.closest) { return; }

        var readButton = target.closest('[data-notification-read]');
        if (readButton) {
            var card = readButton.closest('.tn-item');
            if (!card) { return; }
            readButton.disabled = true;
            markOne(card).then(function (ok) { if (!ok) { readButton.disabled = false; } });
            return;
        }

        var link = target.closest('a[data-notification-link]');
        if (!link) { return; }
        var item = link.closest('.tn-item');
        if (!item || !item.classList.contains('is-unread')) { return; }   // already read: a plain link

        var href = link.getAttribute('href');
        if (!core.isSafeLocalUrl(href)) { e.preventDefault(); return; }

        // Open in a new tab / window: mark read in the background and let the browser do its normal thing.
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || (typeof e.button === 'number' && e.button !== 0)) {
            markOne(item);
            return;
        }

        // Mark read first, but never keep the learner waiting more than 1.5 s; a failed mark still navigates.
        e.preventDefault();
        var gone = false;
        function go() {
            if (gone) { return; }
            gone = true;
            window.location.href = href;
        }
        setTimeout(go, core.MARK_READ_NAVIGATE_TIMEOUT_MS);
        markOne(item).then(go, go);
    });

    // ---------------------------------------------------------------- mark all

    if (elMarkAll) {
        elMarkAll.addEventListener('click', async function () {
            elMarkAll.disabled = true;
            elMarkAll.setAttribute('aria-busy', 'true');
            try {
                var res = await fetch(MARK_ALL_URL, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': getCsrfToken() },
                    credentials: 'same-origin'
                });
                if (!res.ok) {
                    elMarkAll.disabled = false;
                    elMarkAll.removeAttribute('aria-busy');
                    showToast(core.FAILURE_MESSAGE, true);
                    return;
                }
                var body = await res.json().catch(function () { return {}; });
                state = core.applyMarkAll(state, body);

                var cards = elRoot.querySelectorAll('.tn-item.is-unread');
                for (var i = 0; i < cards.length; i++) { clearUnreadMarks(cards[i]); }
                elRoot.querySelectorAll('.tn-chip__count').forEach(function (el) { el.hidden = true; });
                applyUnread(state.unread);

                elMarkAll.disabled = false;
                elMarkAll.removeAttribute('aria-busy');
                showToast(core.markAllMessage(body && body.updated), false);
                showUnreadEmpty();
                notifyOthers({ source: 'page', allRead: true, unreadCount: state.unread });
            } catch (_) {
                elMarkAll.disabled = false;
                elMarkAll.removeAttribute('aria-busy');
                showToast(core.FAILURE_MESSAGE, true);
            }
        });
    }

    // ---------------------------------------------------------------- changes made in the bell panel

    document.addEventListener('tct:notifications-changed', function (e) {
        var detail = e && e.detail;
        if (!detail || detail.source === 'page') { return; }

        if (detail.allRead) {
            state = core.applyMarkAll(state, { unreadCount: detail.unreadCount });
            var cards = elRoot.querySelectorAll('.tn-item.is-unread');
            for (var i = 0; i < cards.length; i++) { clearUnreadMarks(cards[i]); }
            elRoot.querySelectorAll('.tn-chip__count').forEach(function (el) { el.hidden = true; });
            showUnreadEmpty();
        } else if (typeof detail.id === 'number') {
            var card = findItem(detail.id);
            if (card && card.classList.contains('is-unread')) {
                state = core.markRead(state, detail.id);
                clearUnreadMarks(card);
            }
        }

        if (typeof detail.unreadCount === 'number') { applyUnread(detail.unreadCount); }
    });
})();
