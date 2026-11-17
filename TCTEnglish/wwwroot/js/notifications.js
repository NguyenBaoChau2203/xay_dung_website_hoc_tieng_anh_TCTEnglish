/* =============================================================
   notifications.js: the bell and its panel in the shell (plan 5.2).
   - Reads the list in keyset slices (no duplicates or gaps when a notification arrives), by scrolling to the end
     or with the visible "Tải thêm" button; tabs "Tất cả" / "Chưa đọc".
   - Items with a link are real anchors (mark read, then navigate, at most 1.5 s of waiting); items without a link
     are buttons that only mark read.
   - Keyboard: focus moves into the panel on open, Tab stays inside, Esc and an outside click close it and
     return focus to the bell, arrow keys switch tabs, Space does not scroll the page.
   - Keeps the page (/Notification) in step through the "tct:notifications-changed" event.
   Pure logic (badge text, accessible label, safe link, cursor query, tab index, poll back-off) lives in
   notification-core.js, loaded by the panel partial. No innerHTML: every node is built with createElement/textContent.
   Test hooks are exposed only when window.TCT_NOTIFICATIONS_TEST_MODE is set.
   ============================================================= */

(function () {
    'use strict';

    var core = window.TctNotificationCore;

    // ── DOM elements ──────────────────────────────────────────
    var elBell        = document.getElementById('notificationBtn');
    var elBadge       = document.getElementById('notificationBadge');
    var elPanel       = document.getElementById('notifPanel');
    var elTitle       = document.getElementById('notifTitle');
    var elNewChip     = document.getElementById('notifNewChip');
    var elList        = document.getElementById('notifList');
    var elMarkAllBtn  = document.getElementById('notifMarkAllBtn');
    var elLoadMoreBtn = document.getElementById('notifLoadMoreBtn');
    var elStatus      = document.getElementById('notifStatus');
    var elTabAll      = document.getElementById('notifTabAll');
    var elTabUnread   = document.getElementById('notifTabUnread');

    // Guard: no bell, no panel or list (signed out) or no core helpers: do nothing.
    if (!elBell || !elPanel || !elList || !core) { return; }

    var TEST_MODE = !!(typeof window !== 'undefined' && window.TCT_NOTIFICATIONS_TEST_MODE);

    var PAGE_SIZE = 8;
    var MARK_ALL_URL = '/api/notifications/mark-all-read';
    var ICON_PATTERN = /^(fas|far|fa-solid|fa-regular) fa-[a-z0-9-]+$/;
    var TONES = { streak: 1, success: 1, warning: 1, accent: 1, primary: 1 };
    var DEFAULT_ICON = 'fa-solid fa-bell';

    // ── State ─────────────────────────────────────────────────
    var items         = [];       // the API objects shown in the list (their isRead is the source of truth)
    var cursor        = null;     // the server's nextCursor
    var hasMore       = false;
    var tab           = 'all';    // 'all' | 'unread'
    var isOpen        = false;
    var isFetching    = false;    // load-more in flight
    var requestSeq    = 0;        // drops answers of a superseded first-slice request
    var unreadCount   = 0;
    var pollTimerId   = null;
    var unreadInFlight = false;
    var pollDelayMs   = core.POLL_BASE_MS;

    // ── Small helpers ─────────────────────────────────────────
    function getCsrfToken() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function make(tag, className, text) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        return node;
    }

    function setBusy(node, busy) {
        node.setAttribute('aria-busy', busy ? 'true' : 'false');
    }

    function clear(node) {
        while (node.firstChild) { node.removeChild(node.firstChild); }
    }

    function announce(message) {
        if (elStatus) { elStatus.textContent = message; }
    }

    function notifyChanged(detail) {
        if (typeof document.dispatchEvent !== 'function' || typeof CustomEvent !== 'function') { return; }
        try { document.dispatchEvent(new CustomEvent('tct:notifications-changed', { detail: detail })); }
        catch (_) { /* the page refreshes on its next load */ }
    }

    function itemUrl(n) { return core.safeLocalUrl(n.url || n.relatedUrl); }

    // ── Badges, label and header chip ─────────────────────────
    function setBadge(el, text) {
        if (!el) { return; }
        el.textContent = text;
        el.hidden = text === '';
        if (el.style) { el.style.display = ''; }   // an inline display from older markup must not override "hidden"
    }

    function updateBadge(count) {
        unreadCount = Math.max(0, Math.trunc(Number(count)) || 0);
        var text = core.badgeText(unreadCount);
        setBadge(elBadge, text);
        setBadge(document.getElementById('sidebarNotifBadge'), text);
        elBell.setAttribute('aria-label', core.bellLabel(unreadCount));
        if (unreadCount > 0) { elBell.classList.add('has-notif'); } else { elBell.classList.remove('has-notif'); }
        if (elNewChip) {
            var chip = core.newChipText(unreadCount);
            elNewChip.textContent = chip;
            elNewChip.hidden = chip === '';
        }
        if (elMarkAllBtn) { elMarkAllBtn.hidden = unreadCount <= 0; }
    }

    // ── Polling (unread count) ────────────────────────────────
    async function fetchUnreadCount() {
        if (unreadInFlight) { return; }
        if (typeof document !== 'undefined' && document.visibilityState === 'hidden') { return; }

        unreadInFlight = true;
        var ok = false;
        try {
            var res  = await fetch('/api/notifications/unread-count', { credentials: 'same-origin' });
            if (!res.ok) { throw new Error('unread-count-http'); }
            var data = await res.json();
            updateBadge(data.count === undefined || data.count === null ? 0 : data.count);
            ok = true;
        } catch (_) {
            ok = false;
        } finally {
            pollDelayMs = core.nextPollDelay(pollDelayMs, ok);
            unreadInFlight = false;
            scheduleNextPoll();
        }
    }

    function scheduleNextPoll() {
        if (TEST_MODE) { return; }
        if (pollTimerId) { clearTimeout(pollTimerId); }
        pollTimerId = setTimeout(function () { fetchUnreadCount(); }, pollDelayMs);
    }

    // ── Rendering (createElement only) ────────────────────────
    function showSkeleton() {
        clear(elList);
        setBusy(elList, true);
        for (var i = 0; i < 3; i++) {
            var row = make('div', 'notif-skeleton');
            row.appendChild(make('span', 'tct-skeleton notif-skeleton__tile'));
            var lines = make('div', 'notif-skeleton__lines');
            lines.appendChild(make('span', 'tct-skeleton tct-skeleton--line'));
            lines.appendChild(make('span', 'tct-skeleton tct-skeleton--line tct-skeleton--short'));
            row.appendChild(lines);
            elList.appendChild(row);
        }
    }

    function renderMessage(iconClass, text, retry) {
        clear(elList);
        setBusy(elList, false);
        var box = make('div', 'notif-empty');
        var icon = make('i', iconClass);
        icon.setAttribute('aria-hidden', 'true');
        box.appendChild(icon);
        box.appendChild(make('p', '', text));
        if (retry) {
            var button = make('button', 'notif-retry', 'Thử lại');
            button.setAttribute('type', 'button');
            button.addEventListener('click', function () { loadFirst(); });
            box.appendChild(button);
        }
        elList.appendChild(box);
    }

    function renderEmpty() {
        renderMessage('fa-solid fa-bell-slash',
            tab === 'unread' ? 'Không còn thông báo chưa đọc' : 'Không có thông báo nào', false);
    }

    function renderError() { renderMessage('fa-solid fa-circle-exclamation', 'Không tải được thông báo.', true); }

    function buildItem(n) {
        var url = itemUrl(n);
        var node = url ? make('a', 'notif-item') : make('button', 'notif-item');
        if (url) { node.setAttribute('href', url); } else { node.setAttribute('type', 'button'); }
        if (!n.isRead) { node.classList.add('is-unread'); }
        node.setAttribute('data-notification-id', String(n.id));

        var tone = TONES[n.tone] ? n.tone : 'primary';
        var iconClass = ICON_PATTERN.test(String(n.iconClass || '')) ? n.iconClass : DEFAULT_ICON;
        var tile = make('span', 'notif-icon notif-icon--' + tone);
        tile.setAttribute('aria-hidden', 'true');
        tile.appendChild(make('i', iconClass));

        var body = make('span', 'notif-item__body');
        body.appendChild(make('span', 'notif-item__title', n.title || ''));
        body.appendChild(make('span', 'notif-item__msg', n.message || ''));
        body.appendChild(make('span', 'notif-item__time', n.timeText || n.timeAgo || ''));

        node.appendChild(tile);
        node.appendChild(body);
        if (!n.isRead) { addUnreadMarks(node); }

        node.addEventListener('click', function (e) { onItemClick(e, n, node); });
        node.addEventListener('keydown', function (e) {
            // Space activates a link like a button and must not scroll the list or the page.
            if ((e.key === ' ' || e.key === 'Spacebar') && node.tagName === 'A') {
                e.preventDefault();
                node.click();
            }
        });
        return node;
    }

    function addUnreadMarks(node) {
        if (node.querySelector('.notif-unread-dot')) { return; }
        var hiddenText = make('span', 'visually-hidden notif-unread-label', 'Chưa đọc');
        var dot = make('span', 'notif-unread-dot');
        dot.setAttribute('aria-hidden', 'true');
        node.appendChild(dot);
        node.appendChild(hiddenText);
    }

    function removeUnreadMarks(node) {
        var dot = node.querySelector('.notif-unread-dot');
        if (dot) { dot.remove(); }
        var label = node.querySelector('.notif-unread-label');
        if (label) { label.remove(); }
    }

    function restoreUnreadItem(node) {
        node.classList.add('is-unread');
        addUnreadMarks(node);
    }

    function setItemRead(node) {
        node.classList.remove('is-unread');
        removeUnreadMarks(node);
    }

    function renderItems(list, append) {
        setBusy(elList, false);
        if (!append) { clear(elList); }
        if (list.length === 0 && !append) { renderEmpty(); return; }
        list.forEach(function (n) { elList.appendChild(buildItem(n)); });
    }

    function updateLoadMore() {
        if (elLoadMoreBtn) { elLoadMoreBtn.hidden = !(hasMore && cursor); }
    }

    // ── Loading ───────────────────────────────────────────────
    function listUrl(append) {
        var url = '/api/notifications?pageSize=' + PAGE_SIZE + (tab === 'unread' ? '&unreadOnly=true' : '');
        if (append) {
            var q = core.cursorQuery(cursor);
            if (q) { url += '&' + q; }
        }
        return url;
    }

    async function loadFirst() {
        var seq = ++requestSeq;
        isFetching = false;
        items = [];
        cursor = null;
        hasMore = false;
        updateLoadMore();
        showSkeleton();
        try {
            var res = await fetch(listUrl(false), { credentials: 'same-origin' });
            if (!res.ok) { throw new Error('fetch failed'); }
            var json = await res.json();
            if (!json.success) { throw new Error('api error'); }
            if (seq !== requestSeq) { return; }   // another tab / reopen took over
            items = json.data.items || [];
            cursor = json.data.nextCursor || null;
            hasMore = !!json.data.hasMore && !!cursor;
            updateBadge(json.data.unreadCount);
            renderItems(items, false);
            updateLoadMore();
        } catch (_) {
            if (seq === requestSeq) { renderError(); }
        }
    }

    async function loadMore() {
        if (isFetching || !hasMore || !cursor) { return; }
        isFetching = true;
        var seq = requestSeq;
        if (elLoadMoreBtn) { elLoadMoreBtn.disabled = true; setBusy(elLoadMoreBtn, true); }
        try {
            var res = await fetch(listUrl(true), { credentials: 'same-origin' });
            if (!res.ok) { throw new Error('fetch failed'); }
            var json = await res.json();
            if (!json.success) { throw new Error('api error'); }
            if (seq !== requestSeq) { return; }
            var before = items.length;
            items = core.mergeById(items, json.data.items || []);
            var fresh = items.slice(before);   // ids already shown are not shown twice
            cursor = json.data.nextCursor || null;
            hasMore = !!json.data.hasMore && !!cursor;
            updateBadge(json.data.unreadCount);
            renderItems(fresh, true);
            updateLoadMore();
        } catch (_) {
            announce('Không tải thêm được thông báo. Vui lòng thử lại.');
        } finally {
            isFetching = false;
            if (elLoadMoreBtn) { elLoadMoreBtn.disabled = false; setBusy(elLoadMoreBtn, false); }
        }
    }

    // ── Mark read ─────────────────────────────────────────────
    // Optimistic with a full rollback; resolves true when the server confirmed the change.
    async function markItem(n, node) {
        if (n.isRead) { return true; }

        var before = unreadCount;
        n.isRead = true;
        setItemRead(node);
        updateBadge(unreadCount - 1);

        try {
            var res = await fetch('/api/notifications/' + n.id + '/mark-read', {
                method: 'POST',
                headers: { 'RequestVerificationToken': getCsrfToken() },
                credentials: 'same-origin'
            });
            if (!res.ok) { throw new Error('mark-read-http'); }

            var body = await res.json().catch(function () { return {}; });
            if (body && body.success === false) { throw new Error('mark-read-payload'); }

            // The answer carries the new total: no second request before the learner can move on.
            if (body && typeof body.unreadCount === 'number') { updateBadge(body.unreadCount); }
            notifyChanged({ source: 'bell', id: n.id, unreadCount: unreadCount });
            return true;
        } catch (_) {
            n.isRead = false;
            restoreUnreadItem(node);
            updateBadge(before);
            announce(core.FAILURE_MESSAGE);
            return false;
        }
    }

    // Test hook and legacy entry point: mark read, then follow the link only when that worked.
    async function handleItemClick(n, elItem) {
        var url = itemUrl(n);
        if (n.isRead) {
            if (url) { window.location.href = url; }
            return true;
        }
        var ok = await markItem(n, elItem);
        if (ok && url) { window.location.href = url; }
        return ok;
    }

    function onItemClick(e, n, node) {
        var url = itemUrl(n);
        if (!url || node.tagName !== 'A') {          // no link: a button that only marks read
            markItem(n, node);
            return;
        }
        if (n.isRead) { return; }                    // already read: a plain link

        // New tab / window: mark read in the background and let the browser open the link itself.
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || (typeof e.button === 'number' && e.button !== 0)) {
            markItem(n, node);
            return;
        }

        // Mark read first, but never keep the learner waiting more than 1.5 s; a failed mark still goes on.
        e.preventDefault();
        var gone = false;
        function go() {
            if (gone) { return; }
            gone = true;
            window.location.href = url;
        }
        setTimeout(go, core.MARK_READ_NAVIGATE_TIMEOUT_MS);
        markItem(n, node).then(go, go);
    }

    // ── Mark all read ─────────────────────────────────────────
    function finishMarkAll() {
        if (elMarkAllBtn) { elMarkAllBtn.disabled = false; setBusy(elMarkAllBtn, false); }
    }

    async function handleMarkAllClick() {
        if (elMarkAllBtn) {
            elMarkAllBtn.disabled = true;
            setBusy(elMarkAllBtn, true);
        }

        try {
            var res = await fetch(MARK_ALL_URL, {
                method: 'POST',
                headers: { 'RequestVerificationToken': getCsrfToken() },
                credentials: 'same-origin'
            });
            if (!res.ok) {
                finishMarkAll();
                announce(core.FAILURE_MESSAGE);
                return;
            }

            var body = await res.json().catch(function () { return {}; });
            items.forEach(function (n) { n.isRead = true; });
            elList.querySelectorAll('.notif-item.is-unread').forEach(setItemRead);

            updateBadge(body && typeof body.unreadCount === 'number' ? body.unreadCount : 0);
            finishMarkAll();
            announce(core.markAllMessage(body && body.updated));
            if (tab === 'unread') { renderEmpty(); hasMore = false; updateLoadMore(); }
            notifyChanged({ source: 'bell', allRead: true, unreadCount: unreadCount });
        } catch (_) {
            finishMarkAll();
            announce(core.FAILURE_MESSAGE);
        }
    }

    // ── Panel: open, close, focus ─────────────────────────────
    function openPanel() {
        isOpen = true;
        elPanel.classList.add('is-open');
        elBell.setAttribute('aria-expanded', 'true');
        loadFirst();
        // Focus moves into the panel: its heading (not focusable by Tab, so the first Tab lands on the first control).
        if (elTitle && typeof elTitle.focus === 'function') { elTitle.focus(); }
    }

    function closePanel(returnFocus) {
        isOpen = false;
        elPanel.classList.remove('is-open');
        elBell.setAttribute('aria-expanded', 'false');
        if (returnFocus && typeof elBell.focus === 'function') { elBell.focus(); }
    }

    function onBellClick(e) {
        e.stopPropagation();
        if (isOpen) { closePanel(true); } else { openPanel(); }
    }

    // True when the click started inside the panel. The path is read from the event, not from the DOM now:
    // a handler (retry, tab, mark all) may already have replaced the clicked node, which would look "outside".
    function startedInside(e, node) {
        if (typeof e.composedPath === 'function') { return e.composedPath().indexOf(node) >= 0; }
        return node.contains(e.target);
    }

    function onDocumentClick(e) {
        if (!isOpen || startedInside(e, elPanel) || startedInside(e, elBell)) { return; }
        var active = document.activeElement;
        // Give focus back to the bell unless the learner already moved it to another control.
        closePanel(!active || active === document.body || elPanel.contains(active));
    }

    function focusableInPanel() {
        var nodes = elPanel.querySelectorAll('a[href], button, [tabindex]');
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            if (node.disabled || node.hidden || node.getAttribute('tabindex') === '-1') { continue; }
            if (typeof node.getClientRects === 'function' && node.getClientRects().length === 0) { continue; }
            result.push(node);
        }
        return result;
    }

    function onDocumentKeydown(e) {
        if (!isOpen) { return; }
        if (e.key === 'Escape') {
            e.preventDefault();
            closePanel(true);
            return;
        }
        if (e.key !== 'Tab' || !elPanel.contains(document.activeElement)) { return; }

        // Tab stays inside the open panel.
        var stops = focusableInPanel();
        if (stops.length === 0) { return; }
        var first = stops[0];
        var last = stops[stops.length - 1];
        var active = document.activeElement;
        if (e.shiftKey && (active === first || active === elTitle)) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && active === last) {
            e.preventDefault();
            first.focus();
        }
    }

    // ── Tabs ──────────────────────────────────────────────────
    function setTab(name, moveFocus) {
        tab = name === 'unread' ? 'unread' : 'all';
        [[elTabAll, 'all'], [elTabUnread, 'unread']].forEach(function (pair) {
            var el = pair[0];
            if (!el) { return; }
            var selected = pair[1] === tab;
            el.setAttribute('aria-selected', selected ? 'true' : 'false');
            el.setAttribute('tabindex', selected ? '0' : '-1');
            if (selected) {
                elList.setAttribute('aria-labelledby', el.id);
                if (moveFocus && typeof el.focus === 'function') { el.focus(); }
            }
        });
        loadFirst();
    }

    function onTabKeydown(e) {
        var tabs = [elTabAll, elTabUnread].filter(Boolean);
        var current = tabs.indexOf(document.activeElement);
        if (current < 0) { return; }
        var next = core.nextTabIndex(current, e.key, tabs.length);
        if (next === current) { return; }
        e.preventDefault();
        setTab(tabs[next] === elTabUnread ? 'unread' : 'all', true);
    }

    // ── Changes made on the /Notification page ────────────────
    function onChangedElsewhere(e) {
        var detail = e && e.detail;
        if (!detail || detail.source === 'bell') { return; }

        if (detail.allRead) {
            items.forEach(function (n) { n.isRead = true; });
            elList.querySelectorAll('.notif-item.is-unread').forEach(setItemRead);
        } else if (typeof detail.id === 'number') {
            items.forEach(function (n) {
                if (n.id !== detail.id || n.isRead) { return; }
                n.isRead = true;
                var node = elList.querySelector('.notif-item[data-notification-id="' + n.id + '"]');
                if (node) { setItemRead(node); }
            });
        }
        if (typeof detail.unreadCount === 'number') { updateBadge(detail.unreadCount); }
    }

    function onListScroll() {
        if (hasMore && core.nearBottom({
            scrollTop: elList.scrollTop, clientHeight: elList.clientHeight, scrollHeight: elList.scrollHeight
        })) {
            loadMore();
        }
    }

    // ── Wire events ───────────────────────────────────────────
    elBell.addEventListener('click', onBellClick);
    document.addEventListener('click', onDocumentClick);
    document.addEventListener('keydown', onDocumentKeydown);
    document.addEventListener('tct:notifications-changed', onChangedElsewhere);

    if (elMarkAllBtn)  { elMarkAllBtn.addEventListener('click', handleMarkAllClick); }
    if (elLoadMoreBtn) { elLoadMoreBtn.addEventListener('click', function () { loadMore(); }); }
    if (elTabAll)      { elTabAll.addEventListener('click', function () { if (tab !== 'all') { setTab('all', false); } }); elTabAll.addEventListener('keydown', onTabKeydown); }
    if (elTabUnread)   { elTabUnread.addEventListener('click', function () { if (tab !== 'unread') { setTab('unread', false); } }); elTabUnread.addEventListener('keydown', onTabKeydown); }
    elList.addEventListener('scroll', onListScroll);

    // ── Bootstrap: initial poll + visibility-aware schedule ───
    fetchUnreadCount();
    if (!TEST_MODE) {
        document.addEventListener('visibilitychange', function () {
            if (document.visibilityState === 'visible') {
                pollDelayMs = core.POLL_BASE_MS;
                fetchUnreadCount();
            } else if (pollTimerId) {
                clearTimeout(pollTimerId);
                pollTimerId = null;
            }
        });
        window.addEventListener('beforeunload', function () {
            if (pollTimerId) { clearTimeout(pollTimerId); }
        });
    }

    if (TEST_MODE) {
        window.TctNotifications = {
            handleItemClick: handleItemClick,
            handleMarkAllClick: handleMarkAllClick,
            updateBadge: updateBadge,
            fetchUnreadCount: fetchUnreadCount,
            stopPolling: function () {
                if (pollTimerId) { clearTimeout(pollTimerId); }
                pollTimerId = null;
            }
        };
    }

})();
