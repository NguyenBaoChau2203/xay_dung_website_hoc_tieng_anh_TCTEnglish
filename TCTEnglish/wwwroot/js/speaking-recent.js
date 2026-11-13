/* Personal visit history only. Recording, scores and learning time have separate owners. */
(function () {
    'use strict';

    const practice = document.querySelector('[data-speaking-visit-url]');
    const section = document.querySelector('[data-speaking-recent-list]');

    async function jsonRequest(url, options) {
        const controller = new AbortController();
        const timeout = setTimeout(function () { controller.abort(); }, 8000);
        try {
            const response = await fetch(url, Object.assign({ credentials: 'same-origin', cache: 'no-store', redirect: 'error', signal: controller.signal }, options));
            if (!response.ok || response.redirected || !(response.headers.get('content-type') || '').includes('application/json')) {
                throw new Error('Recent history request failed');
            }
            const body = await response.json();
            if (!body || body.success !== true) throw new Error('Recent history response invalid');
            return body;
        } finally {
            clearTimeout(timeout);
        }
    }

    if (practice && practice.dataset.speakingVisitUrl) {
        let pendingVisit = true;
        let writing = false;
        const feedback = practice.querySelector('[data-recent-visit-feedback]');
        async function recordWhenVisible() {
            if (!pendingVisit || writing || document.visibilityState !== 'visible') return;
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!token || !token.value) return;
            pendingVisit = false;
            writing = true;
            try {
                await jsonRequest(practice.dataset.speakingVisitUrl, {
                    method: 'POST', keepalive: true,
                    headers: { RequestVerificationToken: token.value, Accept: 'application/json' }
                });
                if (feedback) feedback.hidden = true;
            } catch (_) {
                if (feedback) {
                    feedback.textContent = 'Chưa lưu được bài vào mục gần đây. Bạn vẫn có thể luyện tập bình thường.';
                    feedback.hidden = false;
                }
            } finally {
                writing = false;
                // A restored page can become visible while its previous keepalive write finishes.
                if (pendingVisit) void recordWhenVisible();
            }
        }
        window.addEventListener('pageshow', function (event) {
            if (event.persisted) pendingVisit = true;
            void recordWhenVisible();
        });
        document.addEventListener('visibilitychange', function () { void recordWhenVisible(); });
        void recordWhenVisible();
    }

    if (!section) return;

    const items = section.querySelector('[data-recent-items]');
    const empty = section.querySelector('[data-recent-empty]');
    const feedback = section.querySelector('[data-recent-feedback]');
    const retry = section.querySelector('[data-recent-retry]');
    let generation = 0;
    let followUpTimer;

    function element(tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function localizeTimes() {
        section.querySelectorAll('[data-recent-time]').forEach(function (node) {
            const date = new Date(node.dateTime);
            if (Number.isNaN(date.getTime())) return;
            const value = date.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
            node.textContent = value;
            node.title = date.toLocaleString('vi-VN', { dateStyle: 'full', timeStyle: 'long' });
            node.setAttribute('aria-label', 'Mở gần nhất: ' + node.title);
        });
    }

    function buildCard(video) {
        const item = element('li', 'spk-recent-item');
        item.dataset.recentVideoId = String(video.id);
        const link = element('a', 'spk-recent-card');
        link.href = '/Speaking/Practice/' + encodeURIComponent(video.id);
        link.setAttribute('aria-label', 'Mở lại bài luyện nói: ' + video.title);
        const thumb = element('div', 'spk-recent-thumb');
        const img = element('img', '');
        img.alt = ''; img.loading = 'lazy';
        const fallback = 'https://img.youtube.com/vi/' + encodeURIComponent(video.youtubeId) + '/hqdefault.jpg';
        try {
            const url = new URL(video.thumbnailUrl || fallback, window.location.origin);
            img.src = /^(https?:)$/.test(url.protocol) ? url.href : fallback;
        } catch (_) { img.src = fallback; }
        thumb.appendChild(img);
        if (video.duration) thumb.appendChild(element('span', 'spk-recent-duration', video.duration));
        const body = element('div', 'spk-recent-body');
        const meta = element('div', 'spk-recent-meta');
        if (video.level) meta.appendChild(element('span', 'spk-level-chip', video.level));
        if (video.isPrivate) meta.appendChild(element('span', '', 'Riêng tư'));
        if (video.topic) meta.appendChild(element('span', 'text-truncate', video.topic));
        const time = element('time', 'spk-recent-time');
        time.dateTime = video.lastOpenedAtUtc;
        time.setAttribute('data-recent-time', '');
        body.append(meta, element('h3', 'spk-recent-card-title', video.title), time,
            element('span', 'spk-recent-action', 'Mở lại bài luyện nói →'));
        link.append(thumb, body); item.appendChild(link);
        return item;
    }

    async function refresh() {
        const requestGeneration = ++generation;
        section.setAttribute('aria-busy', 'true');
        try {
            const body = await jsonRequest(section.dataset.recentUrl, { headers: { Accept: 'application/json' } });
            if (!Array.isArray(body.data) || body.data.length > 6 || body.data.some(v => !Number.isInteger(v.id) || v.id <= 0)) {
                throw new Error('Recent history list invalid');
            }
            if (requestGeneration !== generation) return;
            const focused = document.activeElement && document.activeElement.closest('[data-recent-video-id]');
            const focusedId = focused && focused.dataset.recentVideoId;
            items.replaceChildren(...body.data.map(buildCard));
            empty.hidden = body.data.length > 0;
            feedback.hidden = true; retry.hidden = true;
            localizeTimes();
            if (focusedId) {
                const current = Array.from(items.children).find(item => item.dataset.recentVideoId === focusedId);
                if (current) current.querySelector('a').focus();
            }
        } catch (_) {
            if (requestGeneration !== generation) return;
            feedback.textContent = 'Chưa cập nhật được danh sách gần đây. Các bài đang hiển thị vẫn được giữ lại.';
            feedback.hidden = false; retry.hidden = false;
        } finally {
            if (requestGeneration === generation) section.setAttribute('aria-busy', 'false');
        }
    }

    function refreshAfterNavigation(event) {
        clearTimeout(followUpTimer);
        void refresh();
        const entry = window.performance && window.performance.getEntriesByType('navigation')[0];
        if (event.persisted || (entry && entry.type === 'back_forward')) {
            // One bounded follow-up accommodates the practice page's in-flight
            // keepalive POST on a quick Back. This is not a polling loop.
            followUpTimer = setTimeout(function () { void refresh(); }, 700);
        }
    }

    retry.addEventListener('click', function () { void refresh(); });
    window.addEventListener('pageshow', refreshAfterNavigation);
    window.addEventListener('pagehide', function () { ++generation; clearTimeout(followUpTimer); });
    localizeTimes();
    if (document.readyState === 'complete') refreshAfterNavigation({ persisted: false });
})();
