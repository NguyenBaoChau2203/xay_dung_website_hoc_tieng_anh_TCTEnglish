/* Personal history has no dependency on the Premium AI form or evaluation. */
(function () {
    'use strict';
    document.addEventListener('DOMContentLoaded', function () {
        const list = document.querySelector('[data-writing-recent-list]');
        const practice = document.querySelector('[data-writing-practice]');
        if (!list && !practice) return;
        const root = list || practice;
        const originalTitle = document.title;
        const owner = String(root.getAttribute('data-owner-user-id') || '');
        const cards = list?.querySelector('[data-recent-cards]');
        const status = list?.querySelector('[data-recent-status]');
        let generation = 0;
        let activeRead;
        let visiting = false;
        let initialized = false;
        let needsVisit = false;
        let departed = false;
        let activationActive = true;

        function conceal() {
            generation++;
            activeRead?.abort();
            root.hidden = true;
            if (practice) document.title = 'Luyện viết - TCT English';
        }
        function invalidSession() {
            conceal();
            if (cards) cards.replaceChildren();
            // Reload also removes old practice payload/answers and uses current identity.
            window.location.reload();
        }
        async function json(response, envelope = true) {
            if (response.status === 401 || response.status === 403 || response.redirected
                || !(response.headers.get('content-type') || '').includes('application/json')) {
                const error = new Error('session'); error.session = true; throw error;
            }
            if (!response.ok) throw new Error('request');
            const data = await response.json();
            if (envelope && data.success !== true) throw new Error('request');
            return data;
        }
        function text(tag, content, className) {
            const el = document.createElement(tag);
            el.textContent = content;
            if (className) el.className = className;
            return el;
        }
        function getLevelClass(level) {
            if (level === 'beginner') return 'writing-card-chip--basic';
            if (level === 'intermediate') return 'writing-card-chip--intermediate';
            if (level === 'advanced') return 'writing-card-chip--advanced';
            return 'writing-card-chip--default';
        }
        function getLevelIcon(level) {
            if (level === 'beginner') return 'fa-solid fa-seedling';
            if (level === 'intermediate') return 'fa-solid fa-layer-group';
            if (level === 'advanced') return 'fa-solid fa-fire';
            return 'fa-solid fa-graduation-cap';
        }
        function getTypeIcon(type) {
            if (type === 'emails') return 'fa-regular fa-envelope';
            if (type === 'diaries') return 'fa-solid fa-book-bookmark';
            if (type === 'essays') return 'fa-solid fa-pen-nib';
            if (type === 'articles') return 'fa-regular fa-newspaper';
            if (type === 'stories') return 'fa-solid fa-masks-theater';
            if (type === 'reports') return 'fa-solid fa-chart-pie';
            return 'fa-regular fa-file-lines';
        }
        function render(items) {
            const fragment = document.createDocumentFragment();
            items.forEach(function (item) {
                const card = text('article', '', 'writing-recent-card');
                card.setAttribute('data-recent-exercise-id', item.id);

                // Child 0: Top row (chips)
                const topBar = text('div', '', 'writing-recent-card-top');
                const chipsLeft = text('div', '', 'd-flex align-items-center flex-wrap gap-1');

                const levelChip = text('span', '', 'writing-card-chip ' + getLevelClass(item.level));
                const levelIco = text('i', '', getLevelIcon(item.level) + ' me-1');
                levelIco.setAttribute('aria-hidden', 'true');
                levelChip.append(levelIco, text('span', item.levelTitle));
                chipsLeft.append(levelChip);

                const typeClass = 'writing-card-chip writing-card-chip--type' + (item.contentType ? ' writing-card-chip--type-' + item.contentType : '');
                const typeChip = text('span', '', typeClass);
                const typeIco = text('i', '', getTypeIcon(item.contentType) + ' me-1');
                typeIco.setAttribute('aria-hidden', 'true');
                typeChip.append(typeIco, text('span', item.contentTypeTitle));
                chipsLeft.append(typeChip);

                topBar.append(chipsLeft);

                if (item.isPrivate) {
                    const privChip = text('span', '', 'writing-card-chip writing-card-chip--private');
                    const privIco = text('i', '', 'fa-solid fa-lock me-1');
                    privIco.setAttribute('aria-hidden', 'true');
                    privChip.append(privIco, text('span', 'Riêng tư'));
                    topBar.append(privChip);
                }

                // Child 1: Title (MUST have textContent === item.title)
                const title = text('h3', item.title, 'writing-recent-card-title');

                // Child 2: Topic
                const meta = text('div', '', 'writing-recent-card-meta');
                const topicSpan = text('span', '', 'writing-recent-meta-topic');
                const topicIco = text('i', '', 'fa-solid fa-tag me-1 text-primary');
                topicIco.setAttribute('aria-hidden', 'true');
                topicSpan.append(topicIco, text('span', item.topic));
                meta.append(topicSpan);

                // Child 3: Status & Progress
                const statusWrap = text('div', '', 'writing-recent-card-status d-flex align-items-center justify-content-between flex-wrap gap-1');
                const isCompleted = item.statusKey === 'completed' || (item.completedSentenceCount > 0 && item.completedSentenceCount === item.sentenceCount);
                const isStarted = !isCompleted && (item.completedSentenceCount > 0 || item.statusKey === 'in-progress');

                const badgeCls = isCompleted ? 'bg-success-subtle text-success border border-success-subtle' :
                                 isStarted ? 'bg-warning-subtle text-warning-emphasis border border-warning-subtle' : 'bg-light text-secondary border';
                const statusBadge = text('span', '', 'badge rounded-pill ' + badgeCls + ' px-2 py-1 writing-status-badge');
                const statusIco = text('i', '', (isCompleted ? 'fas fa-check-circle' : isStarted ? 'fas fa-clock' : 'fas fa-circle') + ' me-1');
                statusIco.setAttribute('aria-hidden', 'true');
                statusBadge.append(statusIco, text('span', item.statusLabel));

                const progressSpan = text('span', '', 'writing-recent-progress-text text-muted small');
                const progIco = text('i', '', 'far fa-file-lines me-1');
                progIco.setAttribute('aria-hidden', 'true');
                progressSpan.append(progIco, text('span', item.completedSentenceCount + '/' + item.sentenceCount + ' câu'));

                statusWrap.append(statusBadge, progressSpan);

                // Child 4: Last opened time
                const timeWrap = text('div', '', 'writing-recent-card-time small text-muted');
                const timeIco = text('i', '', 'fa-regular fa-clock me-1');
                timeIco.setAttribute('aria-hidden', 'true');
                const timeEl = text('time', item.lastOpenedAtDisplay);
                timeWrap.append(timeIco, text('span', 'Đã mở: '), timeEl);

                // Child 5 (at(-1)): Action link
                const link = text('a', '', 'btn btn-outline-primary btn-sm rounded-pill w-100 mt-auto writing-recent-btn');
                const url = new URL('/Home/Writing/Practice', window.location.origin);
                url.search = new URLSearchParams({ level: item.level, contentType: item.contentType, exerciseId: item.id });
                link.href = url.href;
                link.setAttribute('aria-label', item.startActionLabel + ': ' + item.title);
                const btnLabel = text('span', item.startActionLabel);
                const arrowIco = text('i', '', 'fas fa-arrow-right ms-1');
                arrowIco.setAttribute('aria-hidden', 'true');
                link.append(btnLabel, arrowIco);

                card.append(topBar, title, meta, statusWrap, timeWrap, link);
                fragment.append(card);
            });
            cards.replaceChildren(fragment);
            status.textContent = items.length ? 'Lịch sử đã lưu trên tài khoản. Bản nháp chưa gửi chỉ ở trình duyệt này.'
                : 'Bạn chưa mở bài luyện viết nào. Hãy chọn một bài bên dưới.';
        }
        async function refresh() {
            const requestId = ++generation;
            activeRead?.abort();
            const controller = new AbortController(); activeRead = controller;
            const timeout = setTimeout(() => controller.abort(), 8000);
            // Do not display personal content until this identity has been revalidated.
            if (list) { cards.hidden = true; root.hidden = false; }
            else { root.hidden = true; document.title = 'Luyện viết - TCT English'; }
            if (status) status.textContent = 'Đang tải lịch sử…';
            try {
                const data = await json(await fetch('/api/writing/recent', {
                    credentials: 'same-origin', cache: 'no-store', signal: controller.signal,
                    headers: { Accept: 'application/json' }
                }));
                if (requestId !== generation || departed) return;
                if (String(data.ownerUserId) !== owner) { invalidSession(); return; }
                if (practice) {
                    const access = await fetch('/Home/Writing/Practice/Data?exerciseId=' + encodeURIComponent(practice.getAttribute('data-exercise-id')), {
                        credentials: 'same-origin', cache: 'no-store', signal: controller.signal, headers: { Accept: 'application/json' }
                    });
                    if (requestId !== generation || departed) return;
                    if (access.status === 404) { invalidSession(); return; }
                    await json(access, false);
                    if (requestId !== generation || departed) return;
                }
                if (list) render(data.data);
                if (cards) cards.hidden = false;
                root.hidden = false;
                document.title = originalTitle;
                visitNotice?.remove(); visitNotice = null;
                if (practice && needsVisit) { needsVisit = false; record(); }
            } catch (error) {
                if (requestId !== generation || departed) return;
                if (error.session) { invalidSession(); return; }
                if (list) {
                    cards.replaceChildren();
                    cards.hidden = false;
                    status.textContent = 'Chưa tải được lịch sử. Hãy thử Làm mới.';
                    root.hidden = false;
                } else {
                    // Preserve browser drafts; retry identity before revealing old work.
                    practice.hidden = true;
                    showVisitFailure('Chưa xác nhận được phiên đăng nhập. Kết nối lại rồi thử tải trang.');
                }
            } finally { clearTimeout(timeout); }
        }
        let visitNotice;
        function showVisitFailure(message) {
            if (!visitNotice) {
                visitNotice = text('div', '', 'alert alert-warning m-3');
                visitNotice.setAttribute('role', 'status');
                practice.before(visitNotice);
            }
            visitNotice.replaceChildren(text('span', message + ' '));
            const retry = text('button', 'Thử lại', 'btn btn-outline-primary btn-sm');
            retry.type = 'button'; retry.addEventListener('click', function () {
                needsVisit = true; refresh();
            });
            visitNotice.append(retry);
        }
        async function record() {
            if (!initialized || visiting || departed) return;
            visiting = true;
            const controller = new AbortController();
            const timeout = setTimeout(() => controller.abort(), 8000);
            try {
                const response = await fetch('/api/writing/' + encodeURIComponent(practice.getAttribute('data-exercise-id')) + '/recent', {
                    method: 'POST', credentials: 'same-origin', keepalive: true, signal: controller.signal,
                    headers: { Accept: 'application/json', RequestVerificationToken:
                        practice.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
                });
                if (response.status === 404 && !departed) { invalidSession(); return; }
                await json(response);
                visitNotice?.remove(); visitNotice = null;
            } catch (error) {
                if (!departed) {
                    if (error.session) invalidSession();
                    else showVisitFailure('Chưa lưu được lần mở bài này. Bản nháp vẫn ở trình duyệt.');
                }
            } finally { clearTimeout(timeout); visiting = false; }
        }
        list?.querySelector('[data-recent-retry]')?.addEventListener('click', refresh);
        practice?.addEventListener('writing:initialized', function () {
            if (initialized) return;
            initialized = true; record();
        });
        function deactivate() { activationActive = false; conceal(); }
        function activate() {
            if (document.visibilityState === 'hidden' || departed || activationActive) return;
            activationActive = true;
            needsVisit = Boolean(practice && initialized);
            refresh();
        }
        window.addEventListener('blur', deactivate);
        window.addEventListener('focus', activate);
        window.addEventListener('pagehide', function () { departed = true; deactivate(); });
        window.addEventListener('pageshow', function (event) {
            if (!event.persisted) return;
            departed = false;
            activationActive = true;
            if (practice) { invalidSession(); return; }
            refresh();
        });
        document.addEventListener('visibilitychange', function () {
            if (document.visibilityState === 'hidden') { deactivate(); return; }
            activate();
        });
    });
}());
