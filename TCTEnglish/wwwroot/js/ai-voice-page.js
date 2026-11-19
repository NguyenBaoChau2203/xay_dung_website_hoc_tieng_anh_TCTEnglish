(function () {
    'use strict';
    const page = document.getElementById('aiVoicePage');
    if (!page) return;
    const $ = id => document.getElementById(id);
    const token = () => page.querySelector('input[name="__RequestVerificationToken"]').value;
    const base = '/api/ai/voice';
    let topics = [], caps = null, controls = { canStart: true }, attemptTopic, deadline, historyPage = 1, editId;
    let voiceState = 'ready', hadAttempt = false;
    const statusPanel = page.querySelector('[data-voice-state]');
    function renderVoiceState() {
        const state = controls.muted && voiceState !== 'speaking' ? 'muted' : voiceState;
        if (statusPanel) statusPanel.dataset.voiceState = state === 'ready' && hadAttempt ? 'finished' : state;
        $('voiceStatus').textContent = state === 'muted' ? 'Micro đang tắt — bạn vẫn nghe được AI.' : statuses[state] || statuses.error;
        renderVoiceTimer();
    }
    function renderVoiceTimer() {
        $('voiceTimer').textContent = voiceState === 'connecting' ? 'Đang kết nối'
            : voiceState === 'closing' ? 'Đang đóng phiên'
            : deadline ? `Còn tối đa ${Math.max(0, Math.ceil((deadline - Date.now()) / 1000))} giây`
            : hadAttempt ? 'Phiên đã kết thúc' : 'Chưa bắt đầu';
    }
    const messages = { premium_required: 'Chủ đề riêng cần Premium. Bạn vẫn có thể xem và xóa dữ liệu.', voice_unavailable: 'Hội thoại chưa sẵn sàng. Bạn có thể tiếp tục chat bằng chữ.', voice_quota_exceeded: 'Đã hết hạn mức hôm nay. Hạn mức làm mới theo ngày UTC.', session_conflict: 'Một phiên khác đang mở hoặc chờ đóng. Hãy dừng phiên cũ trước.', capacity_reached: 'Đã đủ số phiên. Vui lòng thử lại sau.', cleanup_pending: 'Micro đã tắt; máy chủ đang xác nhận đóng phiên. Nhấn Dừng để kiểm tra lại.', not_found: 'Không tìm thấy nội dung hoặc bạn không có quyền truy cập.' };
    const statuses = { ready: 'Sẵn sàng khi bạn muốn nói', connecting: 'Đang kết nối… Chờ “AI đang lắng nghe” rồi hãy nói.', closing: 'Micro đã tắt. Đang xác nhận đóng phiên…', idle: 'AI đang lắng nghe', listening: 'AI đang lắng nghe', thinking: 'AI đang chuẩn bị trả lời… Hãy chờ đến lượt bạn.', speaking: 'AI đang nói — bạn chờ nghe hết nhé', error: 'Có sự cố. Nếu Dừng phiên vẫn sáng, hãy nhấn để thử đóng lại.' };
    function show(text, error = false) { $('voiceFeedback').textContent = text; $('voiceFeedback').hidden = !text; $('voiceFeedback').className = 'alert my-3 ' + (error ? 'alert-danger' : 'alert-info'); }
    const explain = e => show(messages[e.code] || 'Không thực hiện được. Kiểm tra quyền micro và kết nối rồi thử lại. Nếu phiên chưa đóng, hãy nhấn Dừng.', true);
    async function api(path, init = {}) {
        const r = await fetch(base + path, { ...init, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', RequestVerificationToken: token() }, signal: init.signal || AbortSignal.timeout(20000) });
        const data = await r.json().catch(() => null);
        if (!r.ok || r.redirected) throw Object.assign(new Error('voice_request_failed'), { code: data?.errorCode });
        return data;
    }
    const selected = () => topics.find(x => `${x.kind}:${x.id}` === $('voiceTopic').value);
    function renderControls() {
        const topic = selected();
        const supported = !!(navigator.mediaDevices?.getUserMedia && window.RTCPeerConnection && window.EventSource && crypto.randomUUID);
        $('voiceStart').disabled = !controls.canStart || !caps?.available || !topic || topic.kind === 'custom' && !caps.canCreateCustom || !supported;
        $('voiceMute').disabled = !controls.canMute; $('voiceStop').disabled = !controls.canStop;
        $('voiceMute').setAttribute('aria-pressed', String(!!controls.muted)); $('voiceMute').textContent = controls.muted ? 'Bật micro' : 'Tắt micro';
        $('voiceTopic').disabled = !controls.canStart || !topics.length;
        $('voiceTopicCreate').disabled = !controls.canStart || !caps?.canCreateCustom;
        $('voiceTopicEdit').disabled = !controls.canStart || !caps?.canCreateCustom;
        $('voiceTopicDelete').disabled = !controls.canStart;
        renderVoiceState();
        if (!supported && caps?.available) show('Cần HTTPS/localhost và trình duyệt hỗ trợ micro, WebRTC để luyện nói.', true);
    }
    function selectionChanged() {
        const t = selected();
        const summary = $('voiceTopicSelected');
        summary.textContent = t ? `${t.title} (${t.level})` : 'Chưa chọn chủ đề'; summary.title = summary.textContent;
        $('voiceTopicDescription').textContent = t ? `${t.scenario}\nAI: ${t.aiRole} · Bạn: ${t.learnerRole} · ${t.level}` : 'Chưa có chủ đề. Bạn có thể tải lại.';
        $('voiceTopicEdit').hidden = $('voiceTopicDelete').hidden = t?.kind !== 'custom'; renderControls();
    }
    async function loadTopics() {
        $('voiceReload').disabled = true;
        try {
            const old = $('voiceTopic').value; caps = await api('/topics'); topics = [...caps.catalog, ...caps.custom];
            $('voiceTopic').replaceChildren(...topics.map(t => new Option(`${t.kind === 'custom' ? 'Riêng · ' : ''}${t.title} (${t.level})`, `${t.kind}:${t.id}`)));
            if (topics.some(t => `${t.kind}:${t.id}` === old)) $('voiceTopic').value = old;
            if (!topics.length) $('voiceTopic').append(new Option('Chưa có chủ đề', ''));
            $('voicePremiumLock').hidden = caps.canCreateCustom;
            $('voiceAllowance').textContent = page.classList.contains('ai-voice-inline')
                ? `Đã dùng ${caps.usedSeconds}/${caps.dailySeconds} giây · Giữ chỗ ${caps.reservedSeconds} giây`
                : `Hôm nay: ${caps.usedSeconds}/${caps.dailySeconds} giây đã dùng · ${caps.reservedSeconds} giây giữ chỗ. Làm mới theo UTC.`;
            selectionChanged();
        } catch (e) { $('voiceTopic').replaceChildren(new Option('Không tải được chủ đề', '')); $('voiceTopicSelected').textContent = 'Chưa tải được chủ đề'; $('voiceTopicSelected').removeAttribute('title'); explain(e); }
        finally { $('voiceReload').disabled = false; }
    }
    function turns(target, rows) {
        target.replaceChildren();
        if (!rows.length) { const p = document.createElement('p'); p.textContent = 'Chưa có lượt nói hoàn tất.'; target.append(p); }
        for (const row of rows) {
            const p = document.createElement('div'); p.className = 'voice-turn';
            const label = document.createElement('strong'); label.textContent = row.speaker === 'user' ? 'Bạn: ' : 'AI: ';
            p.append(label, document.createTextNode(row.text));
            if (row.interrupted) { const note = document.createElement('p'); note.className = 'small text-muted'; note.textContent = 'Đã ngắt lời — bạn có thể chưa nghe toàn bộ nội dung này.'; p.append(note); }
            target.append(p);
        }
    }
    const coordinator = new window.TctVoiceSession.VoiceSessionCoordinator({
        createConnection: ({ onError }) => new window.TctVoiceAudio.AudioOnlyWebRtc({ remoteAudio: $('voiceAudio'), onError }),
        getCsrfToken: async () => token(),
        async exchangeOffer({ sessionId, offerSdp, signal }) {
            const r = await api('/sessions', { method: 'POST', body: JSON.stringify({ sessionId, offerSdp, topicKind: attemptTopic.kind, topicId: attemptTopic.id }), signal: AbortSignal.any([signal, AbortSignal.timeout(30000)]) });
            deadline = Date.parse(r.expiresAtUtc.endsWith('Z') ? r.expiresAtUtc : r.expiresAtUtc + 'Z'); return r.answerSdp;
        },
        async closeRemoteSession(id) {
            let pending = false;
            try {
                await window.TctVoiceSession.confirmRemoteSessionClosed({
                    close: timeout => api(`/sessions/${id}/close`, { method: 'POST', keepalive: true, signal: AbortSignal.timeout(timeout) }),
                    onPending() { pending = true; show('Micro đã tắt. Máy chủ đang đóng phiên; trang sẽ tự kiểm tra lại.'); }
                });
            } catch (error) { explain(error); throw error; }
            if (pending) show('');
            deadline = null; void loadTopics(); void loadHistory();
        },
        openEvents({ sessionId, onMessage, onError }) {
            const s = new EventSource(base + `/sessions/${sessionId}/events`);
            s.onmessage = e => { try { onMessage(JSON.parse(e.data)); } catch { s.close(); onError(); } };
            s.onerror = () => { s.close(); onError(); }; return s;
        },
        onControls(next) { controls = next; renderControls(); },
        onStatus(state) { voiceState = state; renderVoiceState(); },
        onFailure: explain,
        onEvent(e) { if (e.type === 'app.session.snapshot' && Array.isArray(e.turns)) turns($('voiceTranscript'), e.turns); }
    });
    $('voiceStart').onclick = () => {
        hadAttempt = true; attemptTopic = selected(); show(''); turns($('voiceTranscript'), []);
        if (page.classList.contains('ai-voice-inline')) {
            const workspace = page.querySelector('.voice-workspace');
            workspace.scrollTop += page.querySelector('.voice-session').getBoundingClientRect().top - workspace.getBoundingClientRect().top;
        }
        void coordinator.start();
    };
    $('voiceStop').onclick = () => { show(''); void coordinator.stop(); };
    $('voiceMute').onclick = () => coordinator.toggleMuted();
    $('voiceTopic').onchange = selectionChanged;
    $('voiceReload').onclick = () => { show(''); void loadTopics(); void loadHistory(); };
    window.addEventListener('pagehide', () => { void coordinator.stop(); });
    const VOICE_PAGE_SIZE = 4;
    let allVoiceRows = [];
    let voicePageIndex = 1;
    let serverHistoryPage = 1;
    let serverHasMoreHistory = false;
    let historyLoading = false;

    function renderVoiceHistoryPage() {
        const list = $('voiceHistoryList');
        list.replaceChildren();
        if (!allVoiceRows.length) {
            const emptyP = document.createElement('p');
            emptyP.className = 'text-muted small mb-0';
            emptyP.textContent = 'Chưa có hội thoại nào.';
            list.append(emptyP);
            $('voiceHistoryPrev').disabled = true;
            $('voiceHistoryNext').disabled = true;
            return;
        }

        const totalPages = Math.max(1, Math.ceil(allVoiceRows.length / VOICE_PAGE_SIZE));
        voicePageIndex = Math.max(1, Math.min(voicePageIndex, totalPages));
        if ($('voiceHistoryPageInfo')) $('voiceHistoryPageInfo').textContent = totalPages > 1 ? `(${voicePageIndex}/${totalPages})` : '';

        const start = (voicePageIndex - 1) * VOICE_PAGE_SIZE;
        const pageRows = allVoiceRows.slice(start, start + VOICE_PAGE_SIZE);

        for (const row of pageRows) {
            const item = document.createElement('div');
            item.className = 'voice-history-item';
            const view = document.createElement('button');
            view.type = 'button';
            view.className = 'voice-history-open';
            const title = document.createElement('span');
            title.className = 'voice-history-title';
            title.textContent = row.title;
            title.title = row.title;
            const timestamp = document.createElement('time');
            timestamp.className = 'voice-history-time';
            const createdAt = new Date(row.createdAtUtc.endsWith('Z') ? row.createdAtUtc : row.createdAtUtc + 'Z');
            timestamp.dateTime = createdAt.toISOString();
            timestamp.textContent = `${createdAt.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })} · ${createdAt.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}`;
            view.append(title, timestamp);
            if (row.accountingState === 'pending') {
                const note = document.createElement('span');
                note.className = 'voice-history-note';
                note.textContent = 'Đang đối soát';
                view.append(note);
            }
            view.onclick = async () => {
                try {
                    const d = await api('/history/' + row.id);
                    turns($('voiceHistoryTurns'), d.turns);
                    $('voiceHistoryDetail').hidden = false;
                    $('voiceHistoryTurns').focus();
                } catch (e) {
                    explain(e);
                }
            };
            const del = document.createElement('button');
            del.type = 'button';
            del.className = 'btn btn-outline-danger btn-sm voice-history-delete';
            del.title = 'Xóa hội thoại';
            del.setAttribute('aria-label', 'Xóa lịch sử ' + row.title);
            const trash = document.createElement('i');
            trash.className = 'fas fa-trash';
            trash.setAttribute('aria-hidden', 'true');
            del.append(trash);
            del.onclick = (e) => {
                e.stopPropagation();
                showVoiceConfirm({
                    title: 'Xóa phiên luyện nói',
                    message: 'Bạn có chắc chắn muốn xóa bản chép lời của phiên này?',
                    subMessage: 'Phiên đang mở sẽ được dừng và nội dung luyện nói sẽ bị xóa vĩnh viễn.',
                    confirmText: 'Xóa phiên này',
                    onConfirm: async () => {
                        del.disabled = true;
                        try {
                            await api('/history/' + row.id, { method: 'DELETE' });
                            $('voiceHistoryDetail').hidden = true;
                            allVoiceRows = allVoiceRows.filter(x => x.id !== row.id);
                            renderVoiceHistoryPage();
                            // Offset pages shift after deletion. Reload the first server
                            // batch rather than continuing with an obsolete offset.
                            await loadHistory();
                            show('Đã xóa phiên luyện nói.');
                        } catch (e) {
                            explain(e);
                            del.disabled = false;
                            throw e;
                        }
                    }
                });
            };
            item.append(view, del);
            list.append(item);
        }

        $('voiceHistoryPrev').disabled = historyLoading || voicePageIndex <= 1;
        $('voiceHistoryNext').disabled = historyLoading || voicePageIndex >= totalPages && !serverHasMoreHistory;
    }

    async function loadHistory() {
        try {
            const rows = await api('/history?page=1');
            serverHistoryPage = 1;
            voicePageIndex = 1;
            allVoiceRows = Array.isArray(rows) ? rows : [];
            serverHasMoreHistory = allVoiceRows.length >= 20;
            renderVoiceHistoryPage();
        } catch {
            $('voiceHistoryList').textContent = 'Không tải được lịch sử. Dùng nút tải lại để thử.';
            $('voiceHistoryPrev').disabled = $('voiceHistoryNext').disabled = true;
        }
    }
    $('voiceHistoryPrev').onclick = () => {
        if (!historyLoading && voicePageIndex > 1) {
            voicePageIndex--;
            renderVoiceHistoryPage();
        }
    };
    $('voiceHistoryNext').onclick = async () => {
        if (historyLoading) return;
        const totalPages = Math.ceil(allVoiceRows.length / VOICE_PAGE_SIZE);
        if (voicePageIndex < totalPages) {
            voicePageIndex++;
            renderVoiceHistoryPage();
        } else if (serverHasMoreHistory) {
            const previousCount = allVoiceRows.length;
            historyLoading = true;
            $('voiceHistoryPrev').disabled = $('voiceHistoryNext').disabled = true;
            try {
                const nextPage = serverHistoryPage + 1;
                const more = await api('/history?page=' + nextPage);
                if (!Array.isArray(more)) throw new Error('voice_history_invalid');
                serverHistoryPage = nextPage;
                if (more && more.length) {
                    allVoiceRows.push(...more.filter(row => !allVoiceRows.some(existing => existing.id === row.id)));
                    serverHasMoreHistory = more.length >= 20;
                    if (previousCount % VOICE_PAGE_SIZE === 0 && allVoiceRows.length > previousCount) voicePageIndex++;
                } else {
                    serverHasMoreHistory = false;
                }
            } catch (e) {
                explain(e);
            } finally {
                historyLoading = false;
                renderVoiceHistoryPage();
            }
        }
    };
    // Keep the modals outside scroll/size containers, including inside the launcher iframe.
    document.body.append($('voiceTopicModal'));
    if ($('voiceConfirmModal')) document.body.append($('voiceConfirmModal'));

    function showVoiceConfirm({ title, message, subMessage, confirmText, onConfirm }) {
        const titleSpan = $('voiceConfirmModalTitle')?.querySelector('span');
        if (titleSpan) titleSpan.textContent = title || 'Xác nhận xóa';
        const msgEl = $('voiceConfirmMessage');
        if (msgEl) msgEl.textContent = message || 'Bạn có chắc chắn muốn thực hiện thao tác này?';
        const subMsgEl = $('voiceConfirmSubMessage');
        if (subMsgEl) subMsgEl.textContent = subMessage || 'Hành động này không thể hoàn tác.';
        const errEl = $('voiceConfirmError');
        if (errEl) {
            errEl.hidden = true;
            errEl.textContent = '';
        }
        const okBtn = $('voiceConfirmOkBtn');
        if (okBtn) {
            okBtn.innerHTML = `<i class="fas fa-trash-can me-1" aria-hidden="true"></i>${confirmText || 'Xóa'}`;
            okBtn.disabled = false;
        }

        const modalEl = $('voiceConfirmModal');
        const modal = window.bootstrap?.Modal?.getOrCreateInstance(modalEl);

        if (okBtn) {
            okBtn.onclick = async () => {
                okBtn.disabled = true;
                try {
                    await onConfirm();
                    modal?.hide();
                } catch (e) {
                    if (errEl) {
                        errEl.textContent = e.message || 'Đã xảy ra lỗi khi thực hiện thao tác.';
                        errEl.hidden = false;
                    }
                } finally {
                    okBtn.disabled = false;
                }
            };
        }

        modal?.show();
    }

    const form = $('voiceTopicForm');
    let modalReturnFocus;
    $('voiceTopicModal').addEventListener('shown.bs.modal', () => $('voiceTopicTitle').focus());
    $('voiceTopicModal').addEventListener('hidden.bs.modal', () => {
        if (modalReturnFocus && !modalReturnFocus.disabled) modalReturnFocus.focus();
        else $('voiceTopic').focus();
    });
    function edit(existing) {
        const t = existing ? selected() : { title: '', scenario: '', aiRole: '', learnerRole: '', level: 'A2' }; editId = existing ? t.id : null;
        for (const k of ['title', 'scenario', 'aiRole', 'learnerRole', 'level']) form.elements[k].value = t[k];
        modalReturnFocus = existing ? $('voiceTopicEdit') : $('voiceTopicCreate');
        $('voiceTopicError').hidden = true; bootstrap.Modal.getOrCreateInstance($('voiceTopicModal')).show(modalReturnFocus);
    }
    $('voiceTopicCreate').onclick = () => edit(false); $('voiceTopicEdit').onclick = () => edit(true);
    $('voiceTopicDelete').onclick = () => {
        const t = selected();
        if (!t) return;
        showVoiceConfirm({
            title: 'Xóa chủ đề riêng',
            message: `Bạn có chắc chắn muốn xóa chủ đề "${t.title}"?`,
            subMessage: 'Chủ đề sẽ bị xóa khỏi danh sách của bạn. Lịch sử các phiên luyện nói trước đây vẫn được giữ nguyên.',
            confirmText: 'Xóa chủ đề',
            onConfirm: async () => {
                try {
                    await api('/topics/' + t.id, { method: 'DELETE' });
                    await loadTopics();
                    show('Đã xóa chủ đề.');
                } catch (e) {
                    explain(e);
                    throw e;
                }
            }
        });
    };
    form.onsubmit = async e => {
        e.preventDefault(); if (!form.reportValidity()) return; $('voiceTopicSave').disabled = true;
        try { await api('/topics' + (editId ? '/' + editId : ''), { method: editId ? 'PUT' : 'POST', body: JSON.stringify(Object.fromEntries(new FormData(form))) }); bootstrap.Modal.getInstance($('voiceTopicModal')).hide(); await loadTopics(); show('Đã lưu chủ đề riêng.'); }
        catch (error) { $('voiceTopicError').textContent = messages[error.code] || 'Không lưu được. Kiểm tra nội dung và thử lại.'; $('voiceTopicError').hidden = false; }
        finally { $('voiceTopicSave').disabled = false; }
    };
    setInterval(renderVoiceTimer, 1000);
    const shell = page.closest('[data-ai-chat-shell]');
    if (shell) {
        const footer = page.querySelector('.voice-controls');
        const consent = document.createElement('p');
        consent.className = 'voice-consent mb-0';
        consent.textContent = 'Micro chỉ bật khi nhấn Bắt đầu. Phiên đã mở tính trọn thời gian giữ chỗ; hạn mức làm mới theo UTC.';
        footer.prepend(consent); page.append(footer);
        let firstRender = true;
        const buttons = Array.from(shell.querySelectorAll('[data-ai-mode]'));
        const mode = new window.TctChatMode.ChatModeController({
            async closeVoice() { await coordinator.stop(); return controls.canStart === true; },
            onBusy(busy) { for (const button of buttons) { button.disabled = busy; button.setAttribute('aria-busy', String(busy)); } },
            showMode(next) {
                shell.dataset.chatMode = next;
                const layout = shell.closest('[data-ai-chat-layout]');
                if (layout) layout.dataset.chatMode = next;
                $('aiTextMode').hidden = next !== 'text'; $('aiVoiceMode').hidden = next !== 'voice';
                for (const button of buttons) button.setAttribute('aria-pressed', String(button.dataset.aiMode === next));
                if (next === 'voice') { void loadTopics(); void loadHistory(); }
                else if (!firstRender) $('messageInput').focus({ preventScroll: true });
                firstRender = false;
            }
        });
        for (const button of buttons) button.addEventListener('click', () => { void mode.request(button.dataset.aiMode); });
        window.addEventListener('message', event => {
            if (window.parent === window || event.source !== window.parent || event.origin !== window.location.origin || event.data?.type !== 'tct-ai-launcher-hidden') return;
            window.bootstrap?.Modal.getInstance($('voiceTopicModal'))?.hide();
            void mode.request('text');
        });
    } else { void loadTopics(); void loadHistory(); }
})();
