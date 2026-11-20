/*
 * /Home/Listening: band pills, status toggles, topic select and search (client-side filters), level shelves and band grid,
 * the YouTube import card and the delete modal, thumbnail fallback.
 * Needs window.TctSets (personal-sets-common.js) and window.TctListening.core (listening-core.js).
 * DOM is built with createElement and textContent only; server data is never turned into markup.
 */
(function () {
    'use strict';

    var core = window.TctListening && window.TctListening.core;
    var Sets = window.TctSets;
    var page = document.querySelector('.tl-page--index');
    if (!core || !Sets || !page) { return; }

    var config = {};
    try { config = JSON.parse(document.getElementById('tlConfig').textContent) || {}; } catch (e) { config = {}; }
    var shelfSize = config.shelfSize > 0 ? config.shelfSize : 4;
    var gridSize = config.gridSize > 0 ? config.gridSize : 8;

    // ------------------------------------------------------------------ thumbnails

    function emptyThumb() {
        var span = Sets.el('span', 'tl-thumb--empty');
        span.setAttribute('aria-hidden', 'true');
        span.appendChild(Sets.icon('fa-solid fa-headphones'));
        return span;
    }

    function swapBrokenThumb(img) {
        if (!img || !img.parentNode || !img.classList.contains('tl-thumb')) { return; }
        img.parentNode.replaceChild(emptyThumb(), img);
    }

    // error events do not bubble: listen in the capture phase for every image, including ones added later
    page.addEventListener('error', function (event) {
        if (event.target && event.target.tagName === 'IMG') { swapBrokenThumb(event.target); }
    }, true);
    Array.prototype.forEach.call(page.querySelectorAll('img.tl-thumb'), function (img) {
        if (img.complete && img.naturalWidth === 0) { swapBrokenThumb(img); }
    });

    // ------------------------------------------------------------------ filters, shelves

    var host = document.getElementById('tlLevelsHost');
    var searchInput = document.getElementById('tlSearch');
    var levelsGroup = document.getElementById('tlLevels');
    var statusGroup = document.getElementById('tlStatuses');
    var topicSelect = document.getElementById('tlTopic');
    var countEl = document.getElementById('tlCount');
    var emptyEl = document.getElementById('tlEmpty');
    var clearBtn = document.getElementById('tlClearFilters');

    if (host && levelsGroup) { initFilters(); }

    /**
     * Band pills, status toggles, topic select and search. "Tất cả" shows one shelf per level (at most shelfSize cards each);
     * a band shows one grid of every matching lesson with paging. Cards stay in the DOM; only hidden or moved.
     */
    function initFilters() {
        var bandSection = document.getElementById('tlBand');
        var bandTitle = document.getElementById('tlBandTitle');
        var bandGrid = document.getElementById('tlBandGrid');
        var bandMore = document.getElementById('tlBandMore');
        var items = Array.prototype.map.call(host.querySelectorAll('[data-level-section] .tl-grid__item'), function (el, index) {
            return {
                id: Number(el.getAttribute('data-id')),
                title: el.getAttribute('data-title') || '',
                topic: el.getAttribute('data-topic') || '',
                level: el.getAttribute('data-level') || '',
                status: el.getAttribute('data-status') || 'new',
                el: el,
                home: el.parentNode,
                order: index
            };
        });
        var sections = Array.prototype.map.call(host.querySelectorAll('[data-level-section]'), function (section) {
            return {
                level: section.getAttribute('data-level-section'),
                el: section,
                heading: section.querySelector('.tl-section-title'),
                count: section.querySelector('[data-level-count]'),
                all: section.querySelector('[data-level-all]')
            };
        });
        var lessons = items.map(function (it) { return { id: it.id, title: it.title, topic: it.topic, level: it.level, status: it.status }; });
        var byId = {};
        items.forEach(function (it) { byId[it.id] = it; });
        var radios = Array.prototype.slice.call(levelsGroup.querySelectorAll('[role="radio"]'));
        var availableBands = radios.map(function (r) { return r.getAttribute('data-band'); });

        // the URL keeps a level; its band is what the pills show. A band without lessons has no pill: start on "all".
        var parsed = core.parseFilterQuery(window.location.search, config.topics);
        var state = {
            band: parsed.level === 'all' ? 'all' : (core.bandOf(parsed.level) || 'all'),
            topic: parsed.topic,
            status: config.isAuthenticated ? parsed.status : 'all',
            q: parsed.q
        };
        if (availableBands.indexOf(state.band) < 0) { state.band = 'all'; }
        var bandShown = gridSize;

        function syncRadios() {
            radios.forEach(function (radio) {
                var on = radio.getAttribute('data-band') === state.band;
                radio.setAttribute('aria-checked', on ? 'true' : 'false');
                radio.tabIndex = on ? 0 : -1;
            });
        }

        function syncStatuses() {
            if (!statusGroup) { return; }
            Array.prototype.forEach.call(statusGroup.querySelectorAll('button[data-status]'), function (btn) {
                btn.setAttribute('aria-pressed', btn.getAttribute('data-status') === state.status ? 'true' : 'false');
            });
        }

        // back to the level grids, in the original order
        function restoreCards() {
            items.forEach(function (it) { if (it.el.parentNode !== it.home) { it.home.appendChild(it.el); } });
        }

        function render(reveal) {
            var matchIds = core.filterLessons(lessons, { band: state.band, topic: state.topic, status: state.status, q: state.q });
            var matching = {};
            matchIds.forEach(function (id) { (matching[byId[id].level] = matching[byId[id].level] || []).push(id); });
            var shelves = state.band === 'all';
            var focusTarget = null;

            if (shelves) {
                restoreCards();
                bandSection.hidden = true;
                sections.forEach(function (s) {
                    var ids = matching[s.level] || [];
                    var slice = core.shelfSlice(ids, shelfSize);
                    var visible = {};
                    slice.visible.forEach(function (id) { visible[id] = true; });
                    items.forEach(function (it) { if (it.level === s.level) { it.el.hidden = !visible[it.id]; } });
                    s.el.hidden = ids.length === 0;
                    if (s.count && !config.isAuthenticated) { s.count.textContent = ids.length + ' bài'; }
                    if (s.all) {
                        s.all.hidden = slice.rest === 0;
                        s.all.textContent = 'Xem cả ' + ids.length + ' bài';
                    }
                });
                bandMore.hidden = true;
            } else {
                var slice = core.paginate(matchIds, bandShown);
                var visible = {};
                slice.visible.forEach(function (id) { visible[id] = true; });
                sections.forEach(function (s) { s.el.hidden = true; });
                items.forEach(function (it) { it.el.hidden = !visible[it.id]; });
                // one grid for the whole band, the matching cards in lesson order
                slice.visible.forEach(function (id) { bandGrid.appendChild(byId[id].el); });
                items.forEach(function (it) { if (!visible[it.id] && it.el.parentNode !== it.home) { it.home.appendChild(it.el); } });
                bandSection.hidden = matchIds.length === 0;
                bandTitle.textContent = state.band + ' · ' + matchIds.length + ' bài';
                bandMore.hidden = !slice.hasMore;
                if (reveal && reveal.index < slice.visible.length) {
                    focusTarget = byId[slice.visible[reveal.index]].el.querySelector('a');
                }
            }

            countEl.textContent = matchIds.length + ' bài nghe';
            if (emptyEl) { emptyEl.hidden = matchIds.length > 0; }

            syncRadios();
            syncStatuses();
            if (topicSelect) { topicSelect.value = state.topic; }

            var query = core.buildFilterQuery({ level: core.levelOfBand(state.band), topic: state.topic, status: state.status, q: state.q });
            try { window.history.replaceState(null, '', window.location.pathname + query); } catch (e) { /* ignore */ }
            if (focusTarget) { focusTarget.focus(); }
        }

        function change(patch) {
            Object.keys(patch).forEach(function (k) { state[k] = patch[k]; });
            bandShown = gridSize;
            render();
        }

        levelsGroup.addEventListener('click', function (event) {
            var radio = event.target.closest('[role="radio"]');
            if (radio) { change({ band: radio.getAttribute('data-band') }); }
        });

        // radiogroup: arrows move the selection, Home/End jump to the ends
        levelsGroup.addEventListener('keydown', function (event) {
            var current = radios.indexOf(document.activeElement);
            if (current < 0) { return; }
            var next = -1;
            if (event.key === 'ArrowRight' || event.key === 'ArrowDown') { next = (current + 1) % radios.length; }
            else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') { next = (current - 1 + radios.length) % radios.length; }
            else if (event.key === 'Home') { next = 0; }
            else if (event.key === 'End') { next = radios.length - 1; }
            if (next < 0) { return; }
            event.preventDefault();
            change({ band: radios[next].getAttribute('data-band') });
            radios[next].focus();
        });

        // status pills are toggles: pressing the selected one goes back to every status
        if (statusGroup) {
            statusGroup.addEventListener('click', function (event) {
                var btn = event.target.closest('button[data-status]');
                if (!btn) { return; }
                var value = btn.getAttribute('data-status');
                change({ status: state.status === value ? 'all' : value });
            });
        }
        if (topicSelect) {
            topicSelect.addEventListener('change', function () { change({ topic: topicSelect.value }); });
        }
        if (searchInput) {
            searchInput.value = state.q;
            searchInput.addEventListener('input', Sets.debounce(function () { change({ q: searchInput.value.trim() }); }, 150));
        }

        function showTop(el) {
            if (!el) { return; }
            el.tabIndex = -1;
            el.focus();
            el.scrollIntoView({ block: 'start', behavior: 'auto' });
        }

        // "Xem cả n bài" on a shelf selects the band of that level and jumps to the grid
        sections.forEach(function (s) {
            if (s.all) {
                s.all.addEventListener('click', function () {
                    var band = core.bandOf(s.level);
                    if (availableBands.indexOf(band) < 0) { return; }
                    change({ band: band });
                    showTop(bandTitle);
                });
            }
        });
        if (bandMore) {
            bandMore.addEventListener('click', function () {
                var from = bandShown;
                bandShown = from + gridSize;
                render({ index: from });
            });
        }
        if (clearBtn) {
            clearBtn.addEventListener('click', function () {
                state = { band: 'all', topic: 'all', status: 'all', q: '' };
                if (searchInput) { searchInput.value = ''; searchInput.focus(); }
                bandShown = gridSize;
                render();
            });
        }

        // the "all lessons finished" hero offers to review them: apply the filter instead of leaving the page
        Array.prototype.forEach.call(page.querySelectorAll('[data-tl-hero-done]'), function (link) {
            link.addEventListener('click', function (event) {
                if (!config.isAuthenticated) { return; }
                event.preventDefault();
                change({ band: 'all', topic: 'all', q: '', status: 'done' });
                if (searchInput) { searchInput.value = ''; }
                showTop(levelsGroup.querySelector('[aria-checked="true"]'));
            });
        });

        render();
    }

    // ------------------------------------------------------------------ my lessons: import and delete

    var importForm = document.getElementById('tlImport');
    var mineGrid = document.getElementById('tlMineGrid');
    var mineSection = document.getElementById('tlMine');
    var deleteModalEl = document.getElementById('tlDeleteLesson');
    if (importForm) { initImport(); }
    if (deleteModalEl) { initDelete(); }

    // "Xem tất cả" in the YouTube card: scroll to the "Bài nghe của tôi" shelf and focus its heading
    var jumpBtn = document.getElementById('tlMineJump');
    if (jumpBtn) {
        jumpBtn.addEventListener('click', function () {
            var heading = document.getElementById('tl-mine-title');
            if (!heading || !mineSection || mineSection.hidden) { return; }
            heading.focus();
            heading.scrollIntoView({ block: 'start', behavior: 'auto' });
        });
    }

    /** Keeps the count in the YouTube card, the shelf count, the shelf itself and the "Xem tất cả" button in step with the grid. */
    function syncMine() {
        var left = mineGrid ? mineGrid.querySelectorAll('[data-mine-id]').length : 0;
        var cardCount = document.getElementById('tlMineCount');
        var shelfCount = document.getElementById('tlMineShelfCount');
        var jump = document.getElementById('tlMineJumpWrap');
        if (cardCount) { cardCount.textContent = String(left); }
        if (shelfCount) { shelfCount.textContent = left + ' bài'; }
        if (mineSection) { mineSection.hidden = left === 0; }
        if (jump) { jump.hidden = left === 0; }
        return left;
    }

    function initImport() {
        var urlInput = document.getElementById('tlImportUrl');
        var submit = document.getElementById('tlImportSubmit');
        var errorEl = document.getElementById('tlImportError');
        var statusEl = document.getElementById('tlImportStatus');
        var doneEl = document.getElementById('tlImportDone');
        var submitChildren = Array.prototype.slice.call(submit.childNodes);
        var busy = false;
        var genericError = 'Không thêm được bài nghe. Hãy kiểm tra video có phụ đề tiếng Anh rồi thử lại.';

        function setError(text, upgrade) {
            errorEl.textContent = '';
            if (text) {
                errorEl.appendChild(document.createTextNode(text));
                if (upgrade) {
                    errorEl.appendChild(document.createTextNode(' '));
                    var link = Sets.el('a', null, 'Nâng cấp Premium');
                    link.href = (config.urls && config.urls.premium) || '/Account/Premium';
                    errorEl.appendChild(link);
                }
            }
            errorEl.hidden = !text;
            if (text) { urlInput.setAttribute('aria-invalid', 'true'); } else { urlInput.removeAttribute('aria-invalid'); }
        }

        function setStatus(text) {
            statusEl.textContent = text || '';
        }

        // while the server works: the button shows a spinner and cannot be pressed again, the field cannot be edited
        function setBusy(on) {
            busy = on;
            urlInput.readOnly = on;
            submit.disabled = on;
            submit.textContent = '';
            if (on) {
                var spinner = Sets.el('span', 'spinner-border spinner-border-sm');
                spinner.setAttribute('aria-hidden', 'true');
                submit.appendChild(spinner);
                submit.appendChild(document.createTextNode(' Đang tạo bài nghe…'));
                submit.setAttribute('aria-busy', 'true');
            } else {
                submitChildren.forEach(function (node) { submit.appendChild(node); });
                submit.removeAttribute('aria-busy');
            }
        }

        // The create endpoint answers with the lesson id only: the finished card is read back from the catalogue page
        // (same origin, server-rendered, Razor-encoded) and the one <li> of the new lesson is adopted into the shelf.
        function fetchNewCard(id) {
            return window.fetch(window.location.pathname, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (response) { return response.ok ? response.text() : ''; })
                .then(function (html) {
                    if (!html) { return null; }
                    var parsed = new DOMParser().parseFromString(html, 'text/html');
                    var li = parsed.querySelector('li[data-mine-id="' + id + '"]');
                    return li ? document.importNode(li, true) : null;
                })
                .catch(function () { return null; });
        }

        function showDone(id, card) {
            var title = card ? (card.getAttribute('data-title') || '') : '';
            importForm.hidden = true;
            doneEl.textContent = '';
            var text = Sets.el('p', 'tl-yt__done-text', title ? 'Đã tạo bài nghe "' + title + '"' : 'Đã tạo bài nghe.');
            var actions = Sets.el('div', 'tl-yt__done-actions');
            var open = Sets.el('a', 'btn btn-primary btn-sm', 'Mở bài nghe');
            open.href = config.urls.practice + id;
            var again = Sets.el('button', 'btn btn-outline-primary btn-sm', 'Tạo bài khác');
            again.type = 'button';
            again.addEventListener('click', function () {
                doneEl.hidden = true;
                doneEl.textContent = '';
                importForm.hidden = false;
                urlInput.value = '';
                urlInput.focus();
            });
            actions.appendChild(open);
            actions.appendChild(again);
            doneEl.appendChild(text);
            doneEl.appendChild(actions);
            doneEl.setAttribute('role', 'status');
            doneEl.hidden = false;
            open.focus();
        }

        importForm.addEventListener('submit', function (event) {
            event.preventDefault();
            if (busy) { return; }
            setError('');
            setStatus('');

            var value = urlInput.value.trim();
            if (!core.isYoutubeUrl(value)) {
                setError('Link YouTube không hợp lệ.');
                urlInput.focus();
                return;
            }

            setBusy(true);
            setStatus('Đang lấy phụ đề và tạo bài nghe… Việc này có thể mất đến một phút.');

            var form = new FormData();
            form.append('youtubeUrl', value);
            Sets.fetchJson(config.urls.create, { method: 'POST', body: form }).then(function (data) {
                var id = data && Number(data.lessonId);
                if (!id) { setBusy(false); setStatus(''); setError(genericError); return null; }
                return fetchNewCard(id).then(function (card) {
                    setBusy(false);
                    setStatus('');
                    if (card && mineGrid && !mineGrid.querySelector('[data-mine-id="' + id + '"]')) { mineGrid.insertBefore(card, mineGrid.firstChild); }
                    syncMine();
                    showDone(id, card);
                });
            }).catch(function (error) {
                var data = error && error.data;
                var message = data && typeof data.message === 'string' && data.message ? data.message : genericError;
                setBusy(false);
                setStatus('');
                setError(message, !!(error && error.status === 403 && data && data.requiresUpgrade));
            });
        });

        urlInput.addEventListener('input', function () { if (errorEl.textContent) { setError(''); } });
    }

    function initDelete() {
        var bodyEl = document.getElementById('tlDeleteBody');
        var confirmBtn = document.getElementById('tlDeleteConfirm');
        var current = null;
        var modal = null;

        function getModal() {
            if (!modal && window.bootstrap && window.bootstrap.Modal) { modal = window.bootstrap.Modal.getOrCreateInstance(deleteModalEl); }
            return modal;
        }

        function afterHidden(fn) { deleteModalEl.addEventListener('hidden.bs.modal', fn, { once: true }); }

        // Bootstrap only restores focus for data-api triggers; this modal is opened from code.
        deleteModalEl.addEventListener('hidden.bs.modal', function () {
            if (current && current.trigger && document.body.contains(current.trigger)) { current.trigger.focus(); }
        });

        document.addEventListener('click', function (event) {
            var trigger = event.target.closest('[data-tl-delete]');
            if (!trigger) { return; }
            var m = getModal();
            if (!m) { Sets.toast('Không mở được hộp thoại. Hãy tải lại trang.', { type: 'danger' }); return; }
            current = { id: trigger.getAttribute('data-tl-delete'), title: trigger.getAttribute('data-title') || '', trigger: trigger };
            bodyEl.textContent = 'Bài “' + current.title + '” và tiến độ học của bài sẽ bị xóa vĩnh viễn.';
            m.show(trigger);
        });

        function removeCard(id) {
            var item = mineGrid && mineGrid.querySelector('[data-mine-id="' + id + '"]');
            if (item) { item.remove(); }
            return syncMine();
        }

        confirmBtn.addEventListener('click', function () {
            if (!current || confirmBtn.disabled) { return; }
            var target = current;
            Sets.pending(confirmBtn, true, 'Đang xóa…');
            var form = new FormData();
            form.append('lessonId', target.id);
            Sets.fetchJson(config.urls.delete, { method: 'POST', body: form }).then(function () {
                Sets.pending(confirmBtn, false);
                afterHidden(function () {
                    var left = removeCard(target.id);
                    Sets.toast('Đã xóa bài nghe.');
                    // focus stays useful: the shelf heading while there are lessons left, otherwise the import field
                    var next = left > 0 ? document.getElementById('tl-mine-title') : document.getElementById('tlImportUrl');
                    if (next) { next.focus(); }
                });
                getModal().hide();
            }).catch(function () {
                Sets.pending(confirmBtn, false);
                afterHidden(function () { Sets.toast('Không xóa được bài nghe. Vui lòng thử lại.', { type: 'danger' }); });
                getModal().hide();
            });
        });
    }
}());
