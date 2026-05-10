/* =============================================================
   notifications.js — Bell icon dropdown & notification actions
   Conventions: camelCase vars, UPPER_CASE constants,
                DOM element vars prefixed with `el`,
                event handlers prefixed with `on` or `handle`.
   ============================================================= */

(function () {
    'use strict';

    // ── Constants ─────────────────────────────────────────────
    const POLL_INTERVAL_MS = 60_000;   // auto-poll every 60 s
    const PAGE_SIZE        = 10;

    // ── DOM Elements ──────────────────────────────────────────
    const elBell          = document.getElementById('notificationBtn');
    const elBadge         = document.getElementById('notificationBadge');
    const elPanel         = document.getElementById('notifPanel');
    const elList          = document.getElementById('notifList');
    const elMarkAllBtn    = document.getElementById('notifMarkAllBtn');
    const elLoadMoreBtn   = document.getElementById('notifLoadMoreBtn');
    const elSkeletonTpl   = document.getElementById('notifSkeletonTpl');

    // Guard — if layout has no bell, bail out silently
    if (!elBell || !elPanel) return;

    // ── State ─────────────────────────────────────────────────
    let currentPage   = 1;
    let isOpen        = false;
    let isFetching    = false;
    let hasMore       = false;
    let pollTimerId   = null;

    // ── CSRF Token helper ─────────────────────────────────────
    function getCsrfToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    // ── Badge helpers ─────────────────────────────────────────
    function updateBadge(count) {
        // Header badge (bell button)
        if (elBadge) {
            if (count > 0) {
                elBadge.textContent = count > 99 ? '99+' : String(count);
                elBadge.style.display = 'inline-block';
                if (elBell) elBell.classList.add('has-notif');
            } else {
                elBadge.style.display = 'none';
                if (elBell) elBell.classList.remove('has-notif');
            }
        }
        // Sidebar badge
        const elSidebarBadge = document.getElementById('sidebarNotifBadge');
        if (elSidebarBadge) {
            if (count > 0) {
                elSidebarBadge.textContent = count > 99 ? '99+' : String(count);
                elSidebarBadge.style.display = 'inline-block';
            } else {
                elSidebarBadge.style.display = 'none';
            }
        }
    }

    // ── Fetch unread count (polling) ──────────────────────────
    async function fetchUnreadCount() {
        try {
            const res  = await fetch('/api/notifications/unread-count', { credentials: 'same-origin' });
            if (!res.ok) return;
            const data = await res.json();
            updateBadge(data.count ?? 0);
        } catch (_) { /* network error — silent */ }
    }

    // ── Fetch notification list ───────────────────────────────
    async function fetchNotifications(page = 1, append = false) {
        if (isFetching) return;
        isFetching = true;

        if (!append) showSkeleton();

        try {
            const url  = `/api/notifications?page=${page}&pageSize=${PAGE_SIZE}`;
            const res  = await fetch(url, { credentials: 'same-origin' });
            if (!res.ok) throw new Error('fetch failed');

            const json = await res.json();
            if (!json.success) throw new Error('api error');

            const { items, unreadCount, hasMore: more } = json.data;

            hasMore     = more;
            currentPage = page;

            updateBadge(unreadCount);
            renderItems(items, append);
            toggleLoadMore(hasMore);
        } catch (_) {
            if (!append) renderError();
        } finally {
            isFetching = false;
        }
    }

    // ── Render skeleton ───────────────────────────────────────
    function showSkeleton() {
        elList.innerHTML = '';
        for (let i = 0; i < 3; i++) {
            elList.insertAdjacentHTML('beforeend', `
                <div class="notif-skeleton">
                    <div class="skeleton-circle"></div>
                    <div class="skeleton-lines">
                        <div class="skeleton-line"></div>
                        <div class="skeleton-line short"></div>
                    </div>
                </div>`);
        }
    }

    // ── Render items into list ────────────────────────────────
    function renderItems(items, append) {
        if (!append) elList.innerHTML = '';

        if (items.length === 0 && !append) {
            elList.innerHTML = `
                <div class="notif-empty">
                    <i class="fas fa-bell-slash"></i>
                    <p>Không có thông báo nào</p>
                </div>`;
            return;
        }

        items.forEach(n => {
            const el = buildItemElement(n);
            elList.appendChild(el);
        });
    }

    function renderError() {
        elList.innerHTML = `
            <div class="notif-empty">
                <i class="fas fa-exclamation-circle"></i>
                <p>Không thể tải thông báo</p>
            </div>`;
    }

    // ── Build a single notification <div> ─────────────────────
    function buildItemElement(n) {
        const div = document.createElement('div');
        div.className = 'notif-item' + (n.isRead ? '' : ' is-unread');
        div.dataset.id = n.id;
        div.setAttribute('role', 'button');
        div.setAttribute('tabindex', '0');

        const iconClass  = resolveIconClass(n.type, n.iconClass);
        const colorClass = resolveColorClass(n.type);

        div.innerHTML = `
            <div class="notif-icon ${colorClass}">
                <i class="${iconClass}"></i>
            </div>
            <div class="notif-body">
                <div class="notif-title">${escHtml(n.title)}</div>
                <div class="notif-msg">${escHtml(n.message)}</div>
                <div class="notif-time">${escHtml(n.timeAgo)}</div>
            </div>
            ${n.isRead ? '' : '<div class="notif-unread-dot"></div>'}`;

        div.addEventListener('click',   () => handleItemClick(n, div));
        div.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') handleItemClick(n, div); });
        return div;
    }

    function resolveIconClass(type, serverIconClass) {
        if (serverIconClass) return serverIconClass;
        const map = {
            'StreakWarning'     : 'fas fa-fire-flame-curved',
            'StreakRecord'      : 'fas fa-fire',
            'GoalProgress'      : 'fas fa-chart-line',
            'GoalCompleted'     : 'fas fa-check-circle',
            'BadgeEarned'       : 'fas fa-medal',
            'BadgeNear'         : 'fas fa-star-half-alt',
            'AdminAnnouncement' : 'fas fa-bullhorn',
        };
        return map[type] ?? 'fas fa-bell';
    }

    function resolveColorClass(type) {
        if (type === 'StreakWarning' || type === 'StreakRecord') return 'notif-icon--streak';
        if (type === 'GoalProgress'  || type === 'GoalCompleted') return 'notif-icon--goal';
        if (type === 'BadgeEarned'   || type === 'BadgeNear')      return 'notif-icon--badge';
        if (type === 'AdminAnnouncement')                           return 'notif-icon--admin';
        return '';
    }

    // ── Load More toggle ──────────────────────────────────────
    function toggleLoadMore(show) {
        if (!elLoadMoreBtn) return;
        elLoadMoreBtn.style.display = show ? 'block' : 'none';
    }

    // ── Mark single item as read ──────────────────────────────
    async function handleItemClick(n, elItem) {
        // Optimistically remove unread indicator
        elItem.classList.remove('is-unread');
        const dot = elItem.querySelector('.notif-unread-dot');
        if (dot) dot.remove();

        if (!n.isRead) {
            // Fire & forget — update badge afterward
            try {
                await fetch(`/api/notifications/${n.id}/mark-read`, {
                    method : 'POST',
                    headers: { 'RequestVerificationToken': getCsrfToken() },
                    credentials: 'same-origin'
                });
            } catch (_) { /* silent */ }

            n.isRead = true;
            await fetchUnreadCount();
        }

        if (n.relatedUrl) {
            window.location.href = n.relatedUrl;
        }
    }

    // ── Mark all as read ──────────────────────────────────────
    async function handleMarkAllClick() {
        try {
            const res = await fetch('/api/notifications/mark-all-read', {
                method : 'POST',
                headers: { 'RequestVerificationToken': getCsrfToken() },
                credentials: 'same-origin'
            });
            if (!res.ok) return;

            // Remove all unread indicators in DOM
            elList.querySelectorAll('.notif-item.is-unread').forEach(el => {
                el.classList.remove('is-unread');
                const dot = el.querySelector('.notif-unread-dot');
                if (dot) dot.remove();
            });

            updateBadge(0);
        } catch (_) { /* silent */ }
    }

    // ── Load More (next page) ─────────────────────────────────
    async function handleLoadMoreClick() {
        await fetchNotifications(currentPage + 1, true);
    }

    // ── Open / close panel ────────────────────────────────────
    function openPanel() {
        isOpen = true;
        elPanel.classList.add('is-open');
        elBell.setAttribute('aria-expanded', 'true');
        currentPage = 1;
        fetchNotifications(1, false);
    }

    function closePanel() {
        isOpen = false;
        elPanel.classList.remove('is-open');
        elBell.setAttribute('aria-expanded', 'false');
    }

    function onBellClick(e) {
        e.stopPropagation();
        isOpen ? closePanel() : openPanel();
    }

    // Close when clicking outside
    function onDocumentClick(e) {
        if (isOpen && !elPanel.contains(e.target) && e.target !== elBell) {
            closePanel();
        }
    }

    // Close on ESC
    function onDocumentKeydown(e) {
        if (isOpen && e.key === 'Escape') closePanel();
    }

    // ── HTML escape helper ────────────────────────────────────
    function escHtml(str) {
        const d = document.createElement('div');
        d.appendChild(document.createTextNode(str ?? ''));
        return d.innerHTML;
    }

    // ── Wire events ───────────────────────────────────────────
    elBell.addEventListener('click', onBellClick);
    document.addEventListener('click', onDocumentClick);
    document.addEventListener('keydown', onDocumentKeydown);

    if (elMarkAllBtn)  elMarkAllBtn.addEventListener('click',  handleMarkAllClick);
    if (elLoadMoreBtn) elLoadMoreBtn.addEventListener('click', handleLoadMoreClick);

    // ── Bootstrap: initial poll + schedule repeat ─────────────
    fetchUnreadCount();
    pollTimerId = setInterval(fetchUnreadCount, POLL_INTERVAL_MS);

    // Stop polling when page unloads (SPA nav or tab close)
    window.addEventListener('beforeunload', () => clearInterval(pollTimerId));

})();
