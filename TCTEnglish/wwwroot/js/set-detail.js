/* Set detail page: word list (filters, infinite scroll, selection, bulk actions), share dialog, study strip. */
(function () {
    'use strict';

    var T = window.TctSets;
    var configNode = document.getElementById('tsDetailConfig');
    if (!T || !configNode) { return; }

    var config;
    try { config = JSON.parse(configNode.textContent); } catch (e) { return; }

    var VIEW_KEY = 'tct:set-view';
    var STATUS_CLASS = {
        suspended: 'tct-badge ts-badge--dashed',
        hard: 'tct-badge tct-badge--danger',
        learned: 'tct-badge tct-badge--success',
        learning: 'tct-badge tct-badge--warning',
        'new': 'tct-badge'
    };

    function $(id) { return document.getElementById(id); }

    var els = {
        search: $('ts-search'), count: $('ts-count'), rows: $('ts-rows'), grid: $('ts-grid'),
        listView: $('ts-list-view'), skeleton: $('ts-skeleton'), empty: $('ts-empty'), error: $('ts-error'),
        retry: $('ts-retry'), sentinel: $('ts-sentinel'), more: $('ts-more'),
        selectToggle: $('ts-select-toggle'), bulk: $('ts-bulk'), bulkCount: $('ts-bulk-count')
    };

    var state = {
        filters: { q: '', sort: 'newest', status: '', topic: '', pos: '', level: '' },
        view: 'list',
        nextOffset: null,
        total: 0,
        loaded: 0,
        loading: false,
        requestId: 0,
        controller: null,
        selectMode: false,
        selected: new Set(),
        rowsById: new Map()
    };

    var storedView = T.safeStorage.get(VIEW_KEY);
    if (storedView === 'grid' || storedView === 'list') { state.view = storedView; }

    // ------------------------------------------------------------------ rendering

    function filtersActive() {
        var f = state.filters;
        return !!(f.q || f.status || f.topic || f.pos || f.level);
    }

    function statusBadge(status) {
        var badge = T.el('span', STATUS_CLASS[status.key] || 'tct-badge', status.label);
        return badge;
    }

    function speakButton(term) {
        if (!T.canSpeak()) { return null; }
        var button = T.el('button', 'btn btn-sm tct-btn-icon ts-speak');
        button.type = 'button';
        button.setAttribute('aria-label', 'Phát âm ' + term);
        button.appendChild(T.icon('fa-solid fa-volume-high'));
        button.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            T.speak(term);
        });
        return button;
    }

    function selectCheckbox(row) {
        var label = T.el('label', 'ts-select-box');
        var input = document.createElement('input');
        input.type = 'checkbox';
        input.className = 'form-check-input';
        input.checked = state.selected.has(row.cardId);
        input.setAttribute('aria-label', 'Chọn ' + row.term);
        input.addEventListener('change', function () { toggleSelected(row.cardId, input.checked); });
        label.appendChild(input);
        return label;
    }

    function renderListRow(row) {
        var tr = T.el('tr', row.isSuspended ? 'ts-row ts-row--suspended' : 'ts-row');
        tr.dataset.cardId = row.cardId;

        var selectCell = T.el('td', 'ts-col-select');
        selectCell.hidden = !state.selectMode;
        selectCell.appendChild(selectCheckbox(row));
        tr.appendChild(selectCell);

        var termCell = T.el('td', 'ts-col-term');
        var link = T.el('a', 'ts-term tct-break', row.term);
        link.href = config.urls.cardBase + row.cardId;
        termCell.appendChild(link);
        tr.appendChild(termCell);

        var phonetic = T.el('td', 'ts-col-ipa');
        if (row.phonetic) { phonetic.appendChild(T.el('span', 'tct-phonetic', row.phonetic)); }
        tr.appendChild(phonetic);

        var meaningCell = T.el('td', 'ts-col-meaning');
        meaningCell.appendChild(T.el('span', 'tct-clamp-2 tct-break', row.definition));
        tr.appendChild(meaningCell);
        tr.appendChild(T.el('td', 'ts-col-cefr', row.cefr || ''));

        var statusCell = T.el('td', 'ts-col-status');
        statusCell.appendChild(statusBadge(row.status));
        tr.appendChild(statusCell);

        var speakCell = T.el('td', 'ts-col-speak');
        var speak = speakButton(row.term);
        if (speak) { speakCell.appendChild(speak); }
        tr.appendChild(speakCell);
        return tr;
    }

    function renderGridItem(row) {
        var li = T.el('li', row.isSuspended ? 'ts-word-card ts-word-card--suspended' : 'ts-word-card');
        li.dataset.cardId = row.cardId;

        var art = T.el('div', 'ts-word-art');
        art.setAttribute('aria-hidden', 'true');
        if (row.imageUrl) {
            var img = document.createElement('img');
            img.src = row.imageUrl;
            img.alt = '';
            img.loading = 'lazy';
            art.appendChild(img);
        } else {
            art.appendChild(T.el('span', 'ts-word-glyph', row.emoji || Array.from(row.term)[0].toUpperCase()));
        }
        li.appendChild(art);

        var badge = statusBadge(row.status);
        badge.classList.add('ts-word-badge');
        li.appendChild(badge);

        if (state.selectMode) {
            var box = selectCheckbox(row);
            box.classList.add('ts-word-select');
            li.appendChild(box);
        }

        var body = T.el('div', 'ts-word-body');
        var link = T.el('a', 'ts-term tct-break', row.term);
        link.href = config.urls.cardBase + row.cardId;
        body.appendChild(link);
        body.appendChild(T.el('p', 'ts-word-meaning tct-clamp-2 tct-break', row.definition));
        li.appendChild(body);
        return li;
    }

    function appendRows(rows) {
        rows.forEach(function (row) {
            state.rowsById.set(row.cardId, row);
            els.rows.appendChild(renderListRow(row));
            els.grid.appendChild(renderGridItem(row));
        });
        state.loaded += rows.length;
    }

    function clearRows() {
        els.rows.textContent = '';
        els.grid.textContent = '';
        state.rowsById.clear();
        state.loaded = 0;
    }

    function applyView() {
        var isGrid = state.view === 'grid';
        els.listView.hidden = isGrid;
        els.grid.hidden = !isGrid;
        document.querySelectorAll('[data-ts-view]').forEach(function (button) {
            button.setAttribute('aria-pressed', String(button.getAttribute('data-ts-view') === state.view));
        });
    }

    function showEmpty() {
        els.empty.textContent = '';
        var box = T.el('div', 'tct-state tct-state--empty');
        box.setAttribute('role', 'status');
        var iconWrap = T.el('span', 'tct-state__icon');
        iconWrap.setAttribute('aria-hidden', 'true');
        iconWrap.appendChild(T.icon('fa-solid fa-book-open'));
        box.appendChild(iconWrap);
        if (filtersActive()) {
            box.appendChild(T.el('p', 'tct-state__title', 'Không có từ nào khớp.'));
            box.appendChild(T.el('p', 'tct-state__message', 'Thử bỏ bớt bộ lọc.'));
        } else {
            box.appendChild(T.el('p', 'tct-state__title', 'Bộ thẻ này chưa có từ nào.'));
            if (config.canEdit && config.urls.add) {
                var add = T.el('a', 'btn btn-primary', 'Thêm từ đầu tiên');
                add.href = config.urls.add;
                box.appendChild(add);
            }
        }
        els.empty.appendChild(box);
        els.empty.hidden = false;
    }

    function updateCount() {
        if (state.loading && state.loaded === 0) { els.count.textContent = 'Đang tải…'; return; }
        if (filtersActive()) {
            els.count.textContent = state.total + ' kết quả' + (state.nextOffset !== null ? '+' : '');
        } else {
            els.count.textContent = state.total + ' từ';
        }
    }

    // ------------------------------------------------------------------ loading

    function buildUrl(offset) {
        var f = state.filters;
        var params = new URLSearchParams();
        if (f.q) { params.set('q', f.q); }
        if (f.topic) { params.set('topic', f.topic); }
        if (f.pos) { params.set('pos', f.pos); }
        if (f.status) { params.set('status', f.status); }
        if (f.level) { params.set('level', f.level); }
        params.set('sort', f.sort);
        params.set('offset', String(offset));
        return config.urls.cards + '?' + params.toString();
    }

    /** reset=true starts over at offset 0 and aborts any in-flight request. */
    function load(reset) {
        if (reset) {
            if (state.controller) { state.controller.abort(); }
            state.nextOffset = 0;
            clearRows();
            state.total = 0;
            els.empty.hidden = true;
            els.skeleton.hidden = false;
        } else if (state.loading || state.nextOffset === null) {
            return;
        }

        var offset = state.nextOffset || 0;
        var requestId = ++state.requestId;
        var controller = new AbortController();
        state.controller = controller;
        state.loading = true;
        els.error.hidden = true;
        els.more.hidden = reset;
        updateCount();

        T.fetchJson(buildUrl(offset), { signal: controller.signal }).then(function (page) {
            if (requestId !== state.requestId) { return; } // a newer request owns the list now
            state.loading = false;
            els.skeleton.hidden = true;
            els.more.hidden = true;
            state.total = page.total;
            state.nextOffset = page.nextOffset === undefined ? null : page.nextOffset;
            appendRows(page.rows);
            if (state.loaded === 0) { showEmpty(); }
            updateCount();
            syncSelectAvailability();
            observeSentinel();
        }).catch(function (error) {
            if (error && error.name === 'AbortError') { return; }
            if (requestId !== state.requestId) { return; }
            state.loading = false;
            els.skeleton.hidden = true;
            els.more.hidden = true;
            els.error.hidden = false;
            els.count.textContent = '';
        });
    }

    var observer = null;
    function observeSentinel() {
        if (!('IntersectionObserver' in window)) {
            // Without IntersectionObserver, load everything progressively on demand via scroll.
            return;
        }
        if (!observer) {
            observer = new IntersectionObserver(function (entries) {
                if (entries.some(function (entry) { return entry.isIntersecting; })) { load(false); }
            }, { rootMargin: '400px' });
            observer.observe(els.sentinel);
        }
    }

    if (!('IntersectionObserver' in window)) {
        window.addEventListener('scroll', T.debounce(function () {
            var rect = els.sentinel.getBoundingClientRect();
            if (rect.top < window.innerHeight + 400) { load(false); }
        }, 100), { passive: true });
    }

    // ------------------------------------------------------------------ filters

    var onSearch = T.debounce(function () {
        state.filters.q = els.search.value.trim();
        refreshFilterHighlights();
        load(true);
    }, 300);
    els.search.addEventListener('input', onSearch);

    document.querySelectorAll('[data-ts-filter]').forEach(function (select) {
        select.addEventListener('change', function () {
            state.filters[select.getAttribute('data-ts-filter')] = select.value;
            refreshFilterHighlights();
            load(true);
        });
    });

    function refreshFilterHighlights() {
        document.querySelectorAll('[data-ts-filter]').forEach(function (select) {
            var key = select.getAttribute('data-ts-filter');
            var isDefault = key === 'sort' ? select.value === 'newest' : select.value === '';
            select.classList.toggle('ts-filter--active', !isDefault);
        });
        els.search.classList.toggle('ts-filter--active', els.search.value.trim() !== '');
        refreshSuspendLabel();
    }

    els.retry.addEventListener('click', function () { load(state.loaded === 0); });

    // ------------------------------------------------------------------ view toggle

    document.querySelectorAll('[data-ts-view]').forEach(function (button) {
        button.addEventListener('click', function () {
            state.view = button.getAttribute('data-ts-view');
            T.safeStorage.set(VIEW_KEY, state.view);
            applyView();
        });
    });

    // ------------------------------------------------------------------ selection and bulk actions

    function syncSelectAvailability() {
        if (!els.selectToggle) { return; }
        els.selectToggle.hidden = state.total === 0 && state.loaded === 0;
    }

    function setSelectMode(on) {
        state.selectMode = on;
        if (!on) { state.selected.clear(); }
        if (els.selectToggle) {
            els.selectToggle.setAttribute('aria-pressed', String(on));
            els.selectToggle.querySelector('[data-ts-select-label]').textContent = on ? 'Xong' : 'Chọn';
        }
        var head = document.querySelector('th.ts-col-select');
        if (head) { head.hidden = !on; }
        els.rows.querySelectorAll('.ts-col-select').forEach(function (cell) { cell.hidden = !on; });
        // Rebuild the grid so checkboxes appear/disappear; rows are cheap (<= a few hundred nodes).
        var snapshot = Array.from(state.rowsById.values());
        els.grid.textContent = '';
        snapshot.forEach(function (row) { els.grid.appendChild(renderGridItem(row)); });
        els.rows.querySelectorAll('input[type="checkbox"]').forEach(function (box) { box.checked = false; });
        updateBulkBar();
    }

    function toggleSelected(cardId, selected) {
        if (selected) { state.selected.add(cardId); } else { state.selected.delete(cardId); }
        // Keep the other view's checkbox in sync.
        document.querySelectorAll('[data-card-id="' + cardId + '"] input[type="checkbox"]').forEach(function (box) { box.checked = selected; });
        updateBulkBar();
    }

    function refreshSuspendLabel() {
        var button = $('ts-bulk-suspend');
        if (button) { button.textContent = state.filters.status === 'suspended' ? 'Bỏ ẩn' : 'Tạm ẩn'; }
    }

    function updateBulkBar() {
        if (!els.bulk) { return; }
        var count = state.selected.size;
        els.bulk.hidden = !(state.selectMode && count > 0);
        els.bulkCount.textContent = 'Đã chọn ' + count;
        refreshSuspendLabel();
    }

    function setBulkPending(on) {
        if (!els.bulk) { return; }
        els.bulk.querySelectorAll('button, select').forEach(function (control) { control.disabled = on; });
    }

    var SUCCESS_LABEL = { move: 'Đã chuyển', topic: 'Đã đổi chủ đề', suspend: 'Đã tạm ẩn', unsuspend: 'Đã bỏ ẩn', 'delete': 'Đã xóa' };

    function runBulk(kind, payload) {
        var ids = Array.from(state.selected);
        if (ids.length === 0) { return Promise.resolve(); }
        payload.cardIds = ids;
        setBulkPending(true);
        return T.fetchJson(config.urls.bulk, { method: 'POST', body: payload }).then(function (result) {
            var count = result && result.count !== undefined ? result.count : ids.length;
            var message = (SUCCESS_LABEL[kind] || 'Đã cập nhật') + ' ' + count + ' từ';
            if (result && result.skipped) { message += ' (bỏ qua ' + result.skipped + ' từ đã có)'; }
            T.toast(message);
            setSelectMode(false);
            load(true);
        }).catch(function (error) {
            T.toast(error.message, { type: 'danger' });
        }).then(function () { setBulkPending(false); });
    }

    if (els.selectToggle) {
        els.selectToggle.addEventListener('click', function () { setSelectMode(!state.selectMode); });
        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && state.selectMode && !document.querySelector('.modal.show')) {
                setSelectMode(false);
                els.selectToggle.focus();
            }
        });

        $('ts-bulk-clear').addEventListener('click', function () { setSelectMode(false); els.selectToggle.focus(); });

        $('ts-bulk-move').addEventListener('change', function (event) {
            var select = event.target;
            var targetId = parseInt(select.value, 10);
            select.value = '';
            if (!targetId) { return; }
            runBulk('move', { action: 'move', targetSetId: targetId });
        });

        $('ts-bulk-topic').addEventListener('change', function (event) {
            var select = event.target;
            var value = select.value;
            select.value = '';
            if (!value) { return; }
            runBulk('topic', { action: 'topic', topic: value === '__clear' ? null : value });
        });

        $('ts-bulk-suspend').addEventListener('click', function () {
            var unsuspend = state.filters.status === 'suspended';
            runBulk(unsuspend ? 'unsuspend' : 'suspend', { action: 'suspend', suspended: !unsuspend });
        });

        var deleteModalEl = $('tsBulkDeleteModal');
        $('ts-bulk-delete').addEventListener('click', function () {
            $('ts-bulk-delete-count').textContent = String(state.selected.size);
            if (window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(deleteModalEl).show(); }
        });
        $('ts-bulk-delete-confirm').addEventListener('click', function () {
            if (window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(deleteModalEl).hide(); }
            runBulk('delete', { action: 'delete' });
        });
    }

    // ------------------------------------------------------------------ share dialog

    (function initShare() {
        var toggle = $('ts-share-switch');
        if (!toggle) { return; }
        var box = $('ts-share-link');
        var input = $('ts-share-url');
        var copyButton = $('ts-share-copy');
        var regenerate = $('ts-share-regenerate');
        var modalEl = $('tsShareModal');

        // The opener lives in a dropdown that closes first, so Bootstrap cannot restore focus: send it to the menu button.
        if (modalEl) {
            modalEl.addEventListener('hidden.bs.modal', function () {
                var opener = document.querySelector('.ts-page [data-bs-toggle="dropdown"][aria-label="Thêm tùy chọn cho bộ thẻ"]');
                if (opener) { opener.focus(); }
            });
        }

        function showLink(url) {
            input.value = url || '';
            box.hidden = !url;
        }

        showLink(toggle.checked ? input.value : '');

        function callShare(body) {
            toggle.disabled = true;
            regenerate.disabled = true;
            return T.fetchJson(config.urls.shareLink, { method: 'POST', body: body }).then(function (result) {
                showLink(result && result.url ? result.url : '');
                toggle.checked = !!(result && result.url);
            }).catch(function (error) {
                toggle.checked = !toggle.checked; // revert
                T.toast(error.message, { type: 'danger' });
            }).then(function () {
                toggle.disabled = false;
                regenerate.disabled = false;
            });
        }

        toggle.addEventListener('change', function () {
            var wanted = toggle.checked;
            callShare({ enabled: wanted, regenerate: false });
        });

        regenerate.addEventListener('click', function () {
            if (!window.confirm('Link cũ sẽ không dùng được nữa. Tạo link mới?')) { return; }
            T.fetchJson(config.urls.shareLink, { method: 'POST', body: { enabled: true, regenerate: true } })
                .then(function (result) { showLink(result.url); T.toast('Đã tạo link mới'); })
                .catch(function (error) { T.toast(error.message, { type: 'danger' }); });
        });

        copyButton.addEventListener('click', function () {
            var url = input.value;
            if (!url) { return; }
            var coarse = window.matchMedia && window.matchMedia('(pointer: coarse)').matches && window.innerWidth < 768;
            var done = function () {
                var icon = copyButton.querySelector('i');
                if (icon) {
                    icon.className = 'fa-solid fa-check me-1';
                    setTimeout(function () { icon.className = 'fa-regular fa-copy me-1'; }, 2000);
                }
                T.toast('Đã sao chép link');
            };
            if (coarse && navigator.share) {
                navigator.share({ url: url }).catch(function () { /* user cancelled */ });
            } else if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(url).then(done).catch(function () { input.select(); });
            } else {
                input.select();
            }
        });
    })();

    // ------------------------------------------------------------------ study strip

    (function initStudyStrip() {
        var strip = document.querySelector('[data-ts-study]');
        if (!strip) { return; }

        var setId = strip.getAttribute('data-set-id');
        var due = parseInt(strip.getAttribute('data-due'), 10) || 0;
        var mastered = parseInt(strip.getAttribute('data-mastered'), 10) || 0;
        var words = parseInt(strip.getAttribute('data-words'), 10) || 0;

        var stats = null;
        try {
            var raw = T.safeStorage.get('tct:set-stats:' + setId);
            stats = raw ? JSON.parse(raw) : null;
        } catch (e) { stats = null; }
        stats = stats && typeof stats === 'object' ? stats : {};

        function note(mode, text) {
            var tile = strip.querySelector('[data-ts-mode="' + mode + '"] [data-ts-note]');
            if (tile) { tile.textContent = text; }
        }

        // Stats written by the practice modes (practice-core.js) and, for the best Match time, the legacy pages.
        var userId = strip.getAttribute('data-user-id') || '';
        var learn = typeof stats.learnPercent === 'number' ? Math.round(stats.learnPercent) : null;
        var test = typeof stats.testPercent === 'number' ? Math.round(stats.testPercent)
            : (typeof stats.quizPercent === 'number' ? Math.round(stats.quizPercent) : null);
        var best = typeof stats.matchMs === 'number' && stats.matchMs > 0 ? stats.matchMs : null;
        try {
            var rawBest = T.safeStorage.get('tct:match-best:' + userId + ':set:' + setId + ':6');
            var parsed = rawBest ? JSON.parse(rawBest) : null;
            if (parsed && typeof parsed.ms === 'number' && parsed.ms > 0 && (best === null || parsed.ms < best)) { best = parsed.ms; }
        } catch (e) { /* unreadable entry: ignore */ }

        function formatMs(ms) {
            var tenths = Math.round(ms / 100);
            if (tenths < 600) { return (tenths / 10).toFixed(1).replace('.', ',') + ' giây'; }
            var minutes = Math.floor(tenths / 600);
            var secs = ((tenths - minutes * 600) / 10).toFixed(1).replace('.', ',');
            return minutes + ' phút ' + (secs.length < 4 ? '0' + secs : secs) + ' giây';
        }

        if (learn !== null) { note('learn', 'Đã thuộc ' + learn + '%'); }
        if (test !== null) { note('test', 'Lần trước ' + test + '%'); }
        note('match', best !== null ? 'Kỷ lục ' + formatMs(best) : 'Chưa có kỷ lục');

        // Due words keep pointing at the hero button (no badge); otherwise the same rule as practice-core suggestMode().
        if (due > 0) { return; }
        var suggested = learn === null || learn < 100 ? 'learn'
            : test === null ? 'test'
            : test < 80 ? 'learn'
            : 'match';
        var tile = strip.querySelector('[data-ts-mode="' + suggested + '"] .ts-study-text');
        if (tile) {
            var badge = T.el('span', 'tct-badge tct-badge--primary ts-suggest', 'Gợi ý cho bạn');
            tile.insertBefore(badge, tile.firstChild);
        }
    })();

    // ------------------------------------------------------------------ boot

    applyView();
    refreshFilterHighlights();
    load(true);
})();
