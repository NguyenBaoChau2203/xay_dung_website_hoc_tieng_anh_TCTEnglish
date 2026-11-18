document.addEventListener('DOMContentLoaded', function () {
    const filterForm = document.querySelector('[data-writing-filter-form]');
    const filterSelects = document.querySelectorAll('[data-filter-select]');
    const createForm = document.querySelector('[data-writing-create-form]');
    const deleteForms = document.querySelectorAll('[data-writing-delete-form]');

    // ─── Filter Form Handler ──────────────────────────────
    if (filterForm && filterSelects.length > 0) {
        filterSelects.forEach(function (select) {
            select.addEventListener('change', function () {
                filterForm.submit();
            });
        });
    }

    // ─── Delete Form Confirmation ─────────────────────────
    deleteForms.forEach(function (form) {
        form.addEventListener('submit', function (event) {
            const confirmed = window.confirm('Bạn có chắc muốn xóa bài viết này khỏi khu vực Bài viết của tôi không?');
            if (!confirmed) {
                event.preventDefault();
            }
        });
    });

    if (!createForm) {
        return;
    }

    // ─── Elements & Attributes ────────────────────────────
    const modeInput = createForm.querySelector('[data-writing-mode-input]');
    const modeTabs = createForm.querySelectorAll('[data-mode-tab]');
    const modePanels = createForm.querySelectorAll('[data-mode-panel]');

    const briefInput = createForm.querySelector('[data-brief-input]');
    const sourceTextInput = createForm.querySelector('textarea[name="sourceText"]');
    const briefCounter = createForm.querySelector('[data-brief-char-counter]');
    const sourceCounter = createForm.querySelector('[data-source-char-counter]');
    const charSummary = createForm.querySelector('[data-writing-char-summary]');

    const levelSelect = createForm.querySelector('[data-brief-control="level"]');
    const contentTypeSelect = createForm.querySelector('[data-brief-control="contentType"]');
    const lengthSelect = createForm.querySelector('[data-brief-control="length"]');
    const toneSelect = createForm.querySelector('[data-brief-control="tone"]');

    const exampleChips = createForm.querySelectorAll('[data-example-chip]');

    const submitButton = createForm.querySelector('[data-writing-create-submit]');
    const submitText = createForm.querySelector('[data-writing-create-submit-text]');
    const messageBox = createForm.querySelector('[data-writing-create-message]');

    // Template Offer Card Elements
    const offerCard = createForm.querySelector('[data-template-offer-card]');
    const offerCovered = createForm.querySelector('[data-offer-covered]');
    const offerMissing = createForm.querySelector('[data-offer-missing]');
    const offerTitle = createForm.querySelector('[data-offer-title]');
    const offerTemplateSelect = createForm.querySelector('[data-offer-template-select]');
    const offerTemplateChoice = createForm.querySelector('[data-offer-template-choice]');
    const offerPreviewLines = createForm.querySelector('[data-offer-preview-lines]');
    const offerSlotsWrap = createForm.querySelector('[data-offer-slots-wrap]');
    const offerSlotsContainer = createForm.querySelector('[data-offer-slots-container]');
    const offerAcceptCheck = createForm.querySelector('[data-offer-accept-check]');
    const offerConfirmBtn = createForm.querySelector('[data-offer-confirm-btn]');
    const offerCancelBtn = createForm.querySelector('[data-offer-cancel-btn]');
    const offerCatalogBtn = createForm.querySelector('[data-offer-catalog-btn]');

    const antiForgeryToken = createForm.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const createUrl = createForm.getAttribute('data-create-url') || '';
    const practiceUrl = createForm.getAttribute('data-practice-url') || '';
    const catalogUrl = createForm.getAttribute('data-catalog-url') || '';
    const statusUrlTemplate = createForm.getAttribute('data-status-url') || '/api/writing/generation/{operationId}';
    const confirmUrlTemplate = createForm.getAttribute('data-confirm-url') || '/api/writing/generation/{operationId}/confirm-template';
    const cancelUrlTemplate = createForm.getAttribute('data-cancel-url') || '/api/writing/generation/{operationId}/cancel';
    const currentUserId = Number(createForm.getAttribute('data-owner-user-id') || 0);

    if (!submitButton || !submitText || !messageBox || !antiForgeryToken || !createUrl) {
        return;
    }

    const STORAGE_KEY = 'tct_writing_gen_' + currentUserId;
    const personalRoot = document.querySelector('[data-writing-personal]') || createForm;
    const identityNotice = document.querySelector('[data-writing-identity-message]');
    const quotaLabel = document.querySelector('[data-writing-quota]');
    const requestControls = [briefInput, sourceTextInput, levelSelect, contentTypeSelect, lengthSelect, toneSelect,
        ...modeTabs, ...exampleChips].filter(Boolean);
    let active = true;
    let activationTask = null;
    let submissionState = { logicalIdempotencyKey: null, payloadHash: null, fingerprint: null, operationId: null,
        currentOffer: null, isSubmitting: false, pending: false, retainKey: false, submitSeq: 0, pollTimer: null };

    function saveOperation() {
        try {
            if (submissionState.logicalIdempotencyKey) window.sessionStorage?.setItem(STORAGE_KEY, JSON.stringify({
                version: 1, idempotencyKey: submissionState.logicalIdempotencyKey, fingerprint: submissionState.fingerprint,
                operationId: submissionState.operationId, mode: getActiveMode(), pending: submissionState.pending,
                retainKey: submissionState.retainKey
            }));
        } catch { /* In-memory recovery remains available when storage is unavailable. */ }
    }
    function clearStoredOperation() { try { window.sessionStorage?.removeItem(STORAGE_KEY); } catch {} }
    function resetOperationState(invalidateKey) {
        if (submissionState.isSubmitting || submissionState.pending) return;
        submissionState.submitSeq++;
        if (submissionState.pollTimer) clearTimeout(submissionState.pollTimer);
        submissionState.pollTimer = null;
        submissionState.operationId = null;
        submissionState.currentOffer = null;
        if (invalidateKey && !submissionState.retainKey) {
            submissionState.logicalIdempotencyKey = null;
            submissionState.payloadHash = null;
            submissionState.fingerprint = null;
            clearStoredOperation();
        }
        hideOfferCard();
    }
    function updateBusyControls() {
        const locked = submissionState.isSubmitting || submissionState.pending || !active;
        requestControls.forEach(el => { el.disabled = locked; });
        submitButton.disabled = submissionState.isSubmitting || !active;
        if (submissionState.pending && !submissionState.isSubmitting) submitText.textContent = 'Kiểm tra yêu cầu trước';
        if (offerCancelBtn) offerCancelBtn.disabled = submissionState.isSubmitting || !active;
        if (offerTemplateSelect) offerTemplateSelect.disabled = submissionState.isSubmitting || submissionState.pending || !active;
        createForm.setAttribute('aria-busy', submissionState.isSubmitting ? 'true' : 'false');
        updateConfirmButtonState();
    }
    function setSubmitting(value) {
        submissionState.isSubmitting = value;
        submitText.textContent = value ? 'Đang xử lý yêu cầu...' : getActiveMode() === 'from_brief' ? 'Tạo bài theo mô tả' : 'Tạo bài bằng AI';
        if (!value && submissionState.terminal) submitText.textContent = 'Tạo yêu cầu mới';
        if (offerConfirmBtn) offerConfirmBtn.textContent = 'Xác nhận tạo bài theo khung nội bộ';
        updateBusyControls();
    }
    async function payloadFingerprint(payload) {
        if (!window.crypto?.subtle) return null;
        const bytes = await window.crypto.subtle.digest('SHA-256', new TextEncoder().encode(payload));
        return Array.from(new Uint8Array(bytes), n => n.toString(16).padStart(2, '0')).join('');
    }
    async function getOrGenerateIdempotencyKey(payload) {
        const fingerprint = await payloadFingerprint(payload);
        const same = submissionState.payloadHash === payload || (fingerprint && fingerprint === submissionState.fingerprint);
        if (!submissionState.logicalIdempotencyKey || (!same && !submissionState.retainKey))
            submissionState.logicalIdempotencyKey = createIdempotencyKey();
        submissionState.payloadHash = payload;
        submissionState.fingerprint = fingerprint;
        saveOperation();
        return submissionState.logicalIdempotencyKey;
    }
    function sessionInvalid() {
        active = false; submissionState.submitSeq++;
        personalRoot.hidden = true;
        if (submissionState.pollTimer) clearTimeout(submissionState.pollTimer);
        window.location.reload();
    }
    async function requestJson(url, options = {}, timeoutMs = 12000) {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), timeoutMs);
        try {
            const response = await fetch(url, { ...options, credentials: 'same-origin', cache: 'no-store', signal: controller.signal,
                headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest', ...options.headers } });
            if (response.status === 401 || response.status === 403 || response.redirected) {
                sessionInvalid(); const error = new Error('session'); error.session = true; throw error;
            }
            if (!(response.headers?.get('content-type') || '').includes('application/json')) {
                if (response.status === 404) return { response, payload: {} };
                if (response.ok) { sessionInvalid(); const error = new Error('session'); error.session = true; throw error; }
                throw new Error('unknown response');
            }
            const payload = await response.json();
            return { response, payload };
        } finally { clearTimeout(timer); }
    }
    async function checkOwner() {
        // A fresh account-bound read also supplies the actual shared allowance; it never generates content.
        if (!currentUserId) return true;
        const { response, payload } = await requestJson('/api/writing/generation/quota');
        if (!response.ok || payload.success !== true || String(payload.data?.ownerUserId) !== String(currentUserId)) {
            sessionInvalid(); return false;
        }
        if (quotaLabel) {
            const q = payload.data;
            quotaLabel.textContent = q.isUnlimited ? 'Admin: không giới hạn số bài tạo'
                : `Còn ${q.remaining}/${q.dailyLimit} lượt · ${q.succeeded} bài đã tạo${q.reserved ? ` · ${q.reserved} đang xử lý` : ''}. Dùng chung hai chế độ và khung nội bộ; đặt lại lúc 07:00 (UTC+7).`;
        }
        return true;
    }
    function unknownResult(message) {
        submissionState.pending = true; saveOperation(); setSubmitting(false);
        setCreateMessage('is-error', message || 'Chưa xác nhận được kết quả. Hãy kiểm tra yêu cầu trước; không tạo lại bằng khóa mới.');
        showCatalogAlternative();
    }
    function resultLink(data) {
        const url = buildPracticeUrl(data);
        if (!url || !Number.isInteger(Number(data.exerciseId)) || Number(data.exerciseId) <= 0) return;
        let link = createForm.querySelector('[data-writing-result-link]');
        if (!link) {
            link = document.createElement('a'); link.setAttribute('data-writing-result-link', '');
            link.className = 'btn btn-outline-primary mt-2'; messageBox.parentNode.appendChild(link);
        }
        link.textContent = 'Mở bài vừa tạo'; link.href = url; link.hidden = false;
    }
    function finishResult(data, payload, response, seq, navigate) {
        if (seq !== submissionState.submitSeq || !active) return;
        if (data?.operationId) submissionState.operationId = data.operationId;
        if (data?.status === 'succeeded' && Number(data.exerciseId) > 0) {
            submissionState.pending = false; submissionState.retainKey = false; setSubmitting(false);
            const label = data.generationMethod === 'internal_template' ? 'theo khung song ngữ nội bộ' : data.generationMethod === 'openai' ? 'bằng AI' : 'riêng tư';
            resetOperationState(true);
            setCreateMessage('is-success', `Đã tạo bài viết ${label}.`);
            if (navigate && buildPracticeUrl(data)) window.location.assign(buildPracticeUrl(data)); else resultLink(data);
            return;
        }
        if (data?.status === 'running' || data?.status === 'recovery_required' || response.status === 202) {
            submissionState.pending = true; saveOperation(); setSubmitting(false);
            setCreateMessage('is-info', 'Yêu cầu đang được xử lý. Đang kiểm tra kết quả; không gửi tạo bài mới.');
            if (submissionState.operationId) schedulePoll(seq, navigate);
            return;
        }
        if (data?.status === 'awaiting_template') {
            submissionState.pending = false; saveOperation(); setSubmitting(false); showOfferCard(data); return;
        }
        if (data?.status === 'cancelled') {
            submissionState.pending = false; submissionState.retainKey = false; setSubmitting(false); resetOperationState(true);
            setCreateMessage('is-info', 'Đã hủy bỏ yêu cầu tạo bài. Bạn có thể tiếp tục chỉnh sửa mô tả.'); return;
        }
        const code = payload.errorCode || data?.errorCode || '';
        if (response.status === 409) {
            submissionState.pending = true; saveOperation(); setSubmitting(false);
            setCreateMessage('is-error', 'Khóa đang thuộc một nội dung khác. Hãy kiểm tra yêu cầu đã lưu; không tự gửi lại bằng khóa mới.'); return;
        }
        if (data?.status === 'failed' || data?.status === 'result_deleted' || response.status === 400 || response.status === 429 || response.status === 404) {
            submissionState.pending = false; submissionState.retainKey = false; submissionState.currentOffer = null; submissionState.terminal = true; setSubmitting(false); saveOperation(); hideOfferCard();
            let message = payload.error || data?.errorMessage || 'Yêu cầu chưa tạo được bài.';
            if (code === 'refusal') message = 'Yêu cầu này đã bị từ chối. Khung nội bộ không được dùng để bỏ qua quyết định đó.';
            else if (code === 'daily_quota_exceeded') message = 'Hạn mức chung đã hết hoặc đang được giữ cho yêu cầu khác. Hãy kiểm tra hạn mức hoặc chọn bài có sẵn.';
            else if (code === 'result_deleted') message = 'Bài của yêu cầu này đã bị xóa. Hệ thống không tạo lại tự động.';
            else if (getActiveMode() === 'from_text' && response.status === 503)
                message = 'AI đang gián đoạn; bài dán không thể dịch ngoại tuyến. Nội dung vẫn ở đây để chỉnh sửa hoặc thử lại bằng yêu cầu mới.';
            else if (getActiveMode() === 'from_brief' && data?.coverage === 'Unsupported'
                && ['network', 'timeout', 'circuit_open', 'quota_exhausted', 'invalid_configuration', 'authentication', 'provider_unavailable', 'rate_limited', 'invalid_ai_payload', 'ai_constraint_mismatch'].includes(code))
                message = 'OpenAI chưa tạo được bài và yêu cầu này chưa có khung nội bộ tương thích. Mô tả vẫn được giữ để chỉnh sửa; bạn cũng có thể chọn bài có sẵn.';
            setCreateMessage('is-error', message); showCatalogAlternative(); return;
        }
        unknownResult();
    }
    function schedulePoll(seq, navigate, attempt = 1) {
        if (submissionState.pollTimer) clearTimeout(submissionState.pollTimer);
        submissionState.pollTimer = null;
        if (attempt > 30 || !active) { unknownResult(); return; }
        submissionState.pollTimer = setTimeout(async function () {
            submissionState.pollTimer = null;
            if (seq !== submissionState.submitSeq || !active) return;
            await recoverOperation(navigate, attempt, seq);
        }, 2500);
    }
    async function recoverOperation(navigate = false, attempt = 1, expectedSeq = submissionState.submitSeq) {
        if (!submissionState.logicalIdempotencyKey && !submissionState.operationId) return false;
        try {
            if (!await checkOwner() || expectedSeq !== submissionState.submitSeq || !active) return false;
            const url = submissionState.operationId ? statusUrlTemplate.replace('{operationId}', encodeURIComponent(submissionState.operationId))
                : '/api/writing/generation/by-key/' + encodeURIComponent(submissionState.logicalIdempotencyKey);
            const { response, payload } = await requestJson(url);
            if (expectedSeq !== submissionState.submitSeq || !active) return false;
            if (response.status === 404 && !submissionState.operationId) {
                // Retain this key even if the original POST has not reached its claim yet.
                submissionState.pending = false; submissionState.retainKey = true; saveOperation(); setSubmitting(false);
                setCreateMessage('is-info', 'Yêu cầu chưa được ghi nhận. Nhấn tạo để gửi với cùng khóa an toàn.'); return true;
            }
            finishResult(payload.data, payload, response, expectedSeq, navigate);
            if (submissionState.pending && payload.data?.status === 'running') schedulePoll(expectedSeq, navigate, attempt + 1);
            return false;
        } catch (error) { if (!error.session && active && expectedSeq === submissionState.submitSeq) unknownResult(); return false; }
    }
    async function mutation(url, body, seq, navigate) {
        try {
            const { response, payload } = await requestJson(url, { method: 'POST', headers: {
                'Content-Type': 'application/json', RequestVerificationToken: antiForgeryToken
            }, body: body ? JSON.stringify(body) : undefined }, 100000);
            if (seq !== submissionState.submitSeq || !active) return;
            finishResult(payload.data, payload, response, seq, navigate);
        } catch (error) { if (!error.session && seq === submissionState.submitSeq && active) unknownResult(); }
    }
    function currentRequest() {
        const mode = getActiveMode();
        if (mode === 'from_brief' && briefInput) {
            const brief = briefInput.value.trim().normalize('NFC');
            if (!brief) { setCreateMessage('is-error', 'Vui lòng nhập mô tả bài viết bạn muốn luyện.'); briefInput.focus(); return; }
            if (brief.length < 20) { setCreateMessage('is-error', 'Mô tả quá ngắn (tối thiểu 20 ký tự).'); briefInput.focus(); return; }
            if (brief.length > 1200) { setCreateMessage('is-error', 'Mô tả vượt quá giới hạn 1200 ký tự.'); return; }
            if ((brief.match(/[\p{L}\p{N}]/gu) || []).length < 10) { setCreateMessage('is-error', 'Mô tả cần ít nhất 10 chữ cái hoặc chữ số có nghĩa.'); return; }
            if (/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/.test(brief)) { setCreateMessage('is-error', 'Mô tả chứa ký tự điều khiển không hợp lệ.'); return; }
            return { mode: 'from_brief', brief, level: levelSelect.value, contentType: contentTypeSelect.value, length: lengthSelect.value, tone: toneSelect.value };
        }
        const sourceText = sourceTextInput?.value.trim().normalize('NFC') || '';
        if (!sourceText) { setCreateMessage('is-error', 'Vui lòng dán bài viết trước khi gửi cho AI.'); sourceTextInput?.focus(); return; }
        if (sourceText.length < 20) { setCreateMessage('is-error', 'Nội dung quá ngắn (tối thiểu 20 ký tự).'); return; }
        if (sourceText.length > 4000) { setCreateMessage('is-error', 'Nội dung vượt quá giới hạn 4000 ký tự.'); return; }
        return { mode: 'from_text', sourceText };
    }
    createForm.addEventListener('submit', async function (event) {
        event.preventDefault();
        if (submissionState.isSubmitting || !active) return;
        if (submissionState.pending || submissionState.currentOffer) {
            setSubmitting(true); const missing = await recoverOperation(false); if (active) setSubmitting(false); if (!missing) return;
        }
        if (submissionState.terminal) { resetOperationState(true); submissionState.terminal = false; }
        const request = currentRequest(); if (!request) return;
        setSubmitting(true); clearCreateMessage();
        const seq = ++submissionState.submitSeq;
        try {
            if (!await checkOwner() || seq !== submissionState.submitSeq || !active) return;
            request.idempotencyKey = await getOrGenerateIdempotencyKey(JSON.stringify(request));
            if (seq !== submissionState.submitSeq || !active) return;
            submissionState.pending = true; saveOperation();
            await mutation(createUrl, request, seq, true);
        } catch (error) { if (!error.session && active && seq === submissionState.submitSeq) {
            if (submissionState.logicalIdempotencyKey) unknownResult();
            else { setSubmitting(false); setCreateMessage('is-error', 'Chưa kiểm tra được phiên đăng nhập. Hãy kết nối lại rồi thử.'); }
        } }
    });
    offerConfirmBtn?.addEventListener('click', async function () {
        if (submissionState.isSubmitting || submissionState.pending || offerConfirmBtn.disabled || !active
            || !submissionState.currentOffer || !validateOfferSlots()) return;
        const candidate = selectedTemplate(); if (!candidate) return;
        const slots = {};
        (candidate.slotDefinitions || []).forEach(slot => { slots[slot.key] = offerSlotsContainer.querySelector(`[name="${slot.key}"]`)?.value.trim() || ''; });
        setSubmitting(true); const seq = ++submissionState.submitSeq;
        try {
            if (!await checkOwner() || seq !== submissionState.submitSeq || !active) return;
            submissionState.pending = true; saveOperation();
            await mutation(confirmUrlTemplate.replace('{operationId}', submissionState.operationId), {
                templateKey: candidate.key, templateVersion: candidate.version, acceptLimitedCoverage: true, slots
            }, seq, true);
        } catch (error) { if (!error.session && active && seq === submissionState.submitSeq) unknownResult(); }
    });
    offerCancelBtn?.addEventListener('click', async function () {
        if (submissionState.isSubmitting || !active || !submissionState.operationId) return;
        setSubmitting(true); const seq = ++submissionState.submitSeq;
        try {
            if (!await checkOwner() || seq !== submissionState.submitSeq || !active) return;
            submissionState.pending = true; saveOperation();
            await mutation(cancelUrlTemplate.replace('{operationId}', submissionState.operationId), null, seq, false);
        } catch (error) { if (!error.session && active && seq === submissionState.submitSeq) unknownResult('Chưa xác nhận được việc hủy. Hãy kiểm tra yêu cầu trước.'); }
    });
    function deactivate() {
        active = false; submissionState.submitSeq++; personalRoot.hidden = true;
        if (submissionState.pollTimer) clearTimeout(submissionState.pollTimer);
        submissionState.pollTimer = null; submissionState.isSubmitting = false; saveOperation();
        if (identityNotice) { identityNotice.hidden = false; identityNotice.textContent = 'Đang kiểm tra phiên đăng nhập…'; }
    }
    async function activate() {
        if (document.visibilityState === 'hidden' || active) return;
        if (activationTask) return activationTask;
        const seq = submissionState.submitSeq;
        activationTask = (async function () {
            try {
                if (!await checkOwner() || seq !== submissionState.submitSeq) return;
                active = true; personalRoot.hidden = false;
                if (identityNotice) identityNotice.hidden = true;
                setSubmitting(false);
                updateBriefCounter(); updateSourceCounter();
                if (submissionState.pending || submissionState.operationId) await recoverOperation(false);
            } catch (error) {
                if (identityNotice && !error.session) identityNotice.textContent = 'Chưa xác nhận được tài khoản. Kết nối lại rồi quay lại trang hoặc tải lại.';
            } finally { activationTask = null; }
        })();
        return activationTask;
    }
    window.addEventListener?.('blur', deactivate);
    window.addEventListener?.('pagehide', deactivate);
    window.addEventListener?.('focus', activate);
    window.addEventListener?.('pageshow', event => { if (event.persisted) { deactivate(); activate(); } });
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') deactivate(); else activate(); });
    try {
        const storage = window.sessionStorage;
        for (let i = (storage?.length || 0) - 1; i >= 0; i--) {
            const key = storage.key(i); if (key?.startsWith('tct_writing_gen_') && key !== STORAGE_KEY) storage.removeItem(key);
        }
        const saved = JSON.parse(storage?.getItem(STORAGE_KEY) || 'null');
        if (saved?.version === 1 && typeof saved.idempotencyKey === 'string' && /^[a-z0-9_-]{1,100}$/.test(saved.idempotencyKey)) {
            if (modeInput && ['from_text', 'from_brief'].includes(saved.mode)) switchMode(saved.mode);
            submissionState.logicalIdempotencyKey = saved.idempotencyKey;
            submissionState.fingerprint = saved.fingerprint;
            submissionState.operationId = saved.operationId;
            submissionState.pending = true; submissionState.retainKey = true;
            setSubmitting(false); recoverOperation(false);
        } else checkOwner().catch(() => { if (quotaLabel) quotaLabel.textContent = 'Chưa tải được hạn mức. Hệ thống sẽ kiểm tra trước khi tạo bài.'; });
    } catch { /* A malformed receipt cannot dispatch a request. */ }

    // ─── Mode Switching (Mô tả bài vs Dán bài) ─────────────
    function getActiveMode() {
        if (modeInput && modeInput.value) {
            return modeInput.value;
        }
        const activeTab = createForm.querySelector('[data-mode-tab].is-active');
        return activeTab ? activeTab.getAttribute('data-mode-tab') : 'from_brief';
    }

    function switchMode(newMode) {
        if (!newMode || submissionState.isSubmitting || submissionState.pending) return;

        if (modeInput) {
            modeInput.value = newMode;
        }

        modeTabs.forEach(function (tab) {
            const isTarget = tab.getAttribute('data-mode-tab') === newMode;
            tab.classList.toggle('is-active', isTarget);
            tab.setAttribute('aria-selected', isTarget ? 'true' : 'false');
            if (isTarget) {
                if (typeof tab.removeAttribute === 'function') {
                    tab.removeAttribute('tabindex');
                } else {
                    tab.setAttribute('tabindex', '0');
                }
            } else {
                tab.setAttribute('tabindex', '-1');
            }
        });

        modePanels.forEach(function (panel) {
            const isTarget = panel.getAttribute('data-mode-panel') === newMode;
            panel.hidden = !isTarget;
        });

        if (newMode === 'from_brief') {
            submitText.textContent = 'Tạo bài theo mô tả';
            if (charSummary) charSummary.textContent = 'Tối đa 1200 ký tự';
        } else {
            submitText.textContent = 'Tạo bài bằng AI';
            if (charSummary) charSummary.textContent = 'Tối đa 4000 ký tự';
        }

        clearCreateMessage();
        resetOperationState(true);
    }

    modeTabs.forEach(function (tab, index) {
        tab.addEventListener('click', function () {
            switchMode(tab.getAttribute('data-mode-tab'));
        });

        tab.addEventListener('keydown', function (e) {
            let targetTab = null;
            if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
                targetTab = modeTabs[(index + 1) % modeTabs.length];
            } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
                targetTab = modeTabs[(index - 1 + modeTabs.length) % modeTabs.length];
            } else if (e.key === 'Home') {
                targetTab = modeTabs[0];
            } else if (e.key === 'End') {
                targetTab = modeTabs[modeTabs.length - 1];
            }

            if (targetTab) {
                e.preventDefault();
                targetTab.focus();
                switchMode(targetTab.getAttribute('data-mode-tab'));
            }
        });
    });

    // ─── Live Character Counters ──────────────────────────
    function updateBriefCounter() {
        if (!briefInput || !briefCounter) return;
        const len = briefInput.value.length;
        briefCounter.textContent = len + ' / 1200 ký tự';
    }

    function updateSourceCounter() {
        if (!sourceTextInput || !sourceCounter) return;
        const len = sourceTextInput.value.length;
        sourceCounter.textContent = len + ' / 4000 ký tự';
    }

    if (briefInput) {
        briefInput.addEventListener('input', function () {
            updateBriefCounter();
            if (!messageBox.hidden) clearCreateMessage();
            resetOperationState(true);
        });
    }

    if (sourceTextInput) {
        sourceTextInput.addEventListener('input', function () {
            updateSourceCounter();
            if (!messageBox.hidden) clearCreateMessage();
            resetOperationState(true);
        });
    }

    [levelSelect, contentTypeSelect, lengthSelect, toneSelect].forEach(function (sel) {
        if (sel) {
            sel.addEventListener('change', function () {
                resetOperationState(true);
                clearCreateMessage();
            });
        }
    });

    // ─── Example Chips Interaction ────────────────────────
    exampleChips.forEach(function (chip) {
        chip.addEventListener('click', function () {
            const exBrief = chip.getAttribute('data-example-brief') || '';
            const exLevel = chip.getAttribute('data-example-level') || '';
            const exContentType = chip.getAttribute('data-example-content-type') || '';
            const exLength = chip.getAttribute('data-example-length') || 'short';
            const exTone = chip.getAttribute('data-example-tone') || 'formal';

            if (briefInput && exBrief) {
                briefInput.value = exBrief;
                updateBriefCounter();
            }

            if (levelSelect && exLevel) levelSelect.value = exLevel;
            if (contentTypeSelect && exContentType) contentTypeSelect.value = exContentType;
            if (lengthSelect && exLength) lengthSelect.value = exLength;
            if (toneSelect && exTone) toneSelect.value = exTone;

            clearCreateMessage();
            resetOperationState(true);

            if (briefInput) {
                briefInput.focus();
            }
        });
    });

    // ─── Message Helpers ──────────────────────────────────
    function setCreateMessage(type, text) {
        messageBox.hidden = false;
        messageBox.textContent = text;
        messageBox.classList.remove('is-error', 'is-success', 'is-info');
        messageBox.classList.add(type);
    }

    function clearCreateMessage() {
        messageBox.hidden = true;
        messageBox.textContent = '';
        messageBox.classList.remove('is-error', 'is-success', 'is-info');
        const result = createForm.querySelector('[data-writing-result-link]');
        if (result) result.hidden = true;
        const link = createForm.querySelector('[data-writing-catalog-link]');
        if (link) link.hidden = true;
    }

    function availabilityMessage(kind) {
        const helper = window.TctAiAvailability;
        if (helper && typeof helper.message === 'function') return helper.message(kind);
        if (kind === 'catalog') return 'Chọn bài có sẵn';
        if (kind === 'unavailable') return 'Tính năng AI tạm thời chưa sẵn sàng.';
        if (kind === 'rate') return 'Bạn đang gửi yêu cầu quá nhanh.';
        if (kind === 'access') return 'Phiên đăng nhập đã hết hoặc bạn không có quyền.';
        return 'Dịch vụ đánh giá đang gián đoạn.';
    }

    function showCatalogAlternative() {
        if (!catalogUrl) return null;
        let link = createForm.querySelector('[data-writing-catalog-link]');
        if (!link) {
            link = document.createElement('a');
            link.setAttribute('data-writing-catalog-link', 'true');
            link.className = 'btn btn-outline-primary btn-sm mt-2';
            link.textContent = availabilityMessage('catalog');
            if (messageBox.parentNode) messageBox.parentNode.appendChild(link);
        }
        link.href = catalogUrl;
        link.hidden = false;
        return link;
    }

    function createIdempotencyKey() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') {
            return window.crypto.randomUUID().toLowerCase();
        }
        return 'writing-' + Date.now() + '-' + Math.random().toString(36).slice(2, 10);
    }

    function buildPracticeUrl(result) {
        if (!practiceUrl) return '';
        if (!['beginner', 'intermediate', 'advanced'].includes(result.level) || !['emails', 'diaries', 'essays', 'articles', 'stories', 'reports'].includes(result.contentType) || !Number.isInteger(Number(result.exerciseId)) || Number(result.exerciseId) <= 0) return '';
        const nextUrl = new URL(practiceUrl, window.location.origin);
        nextUrl.searchParams.set('level', result.level || 'intermediate');
        nextUrl.searchParams.set('contentType', result.contentType || 'articles');
        nextUrl.searchParams.set('exerciseId', String(result.exerciseId));
        return nextUrl.pathname + nextUrl.search;
    }

    // ─── Outage Template Offer UI ─────────────────────────
    function hideOfferCard() {
        if (offerCard) offerCard.hidden = true;
    }

    function showOfferCard(offerData) {
        if (!offerCard) return;

        submissionState.currentOffer = offerData;
        submissionState.operationId = offerData.operationId;
        if (offerTemplateSelect) {
            offerTemplateSelect.replaceChildren();
            (offerData.templates || []).forEach((candidate, index) => {
                const option = document.createElement('option'); option.value = String(index); option.textContent = candidate.title;
                offerTemplateSelect.appendChild(option);
            });
            offerTemplateSelect.value = '0';
            if (offerTemplateChoice) offerTemplateChoice.hidden = (offerData.templates || []).length < 2;
        }

        // Render covered & missing constraints
        if (offerCovered) {
            offerCovered.textContent = (offerData.coveredConstraints && offerData.coveredConstraints.length > 0)
                ? offerCoveredText(offerData.coveredConstraints)
                : 'Cấp độ, định dạng và độ dài được hỗ trợ theo khung.';
        }

        if (offerMissing) {
            offerMissing.textContent = (offerData.missingConstraints && offerData.missingConstraints.length > 0)
                ? offerMissingText(offerData.missingConstraints)
                : 'Mô tả tự do đầy đủ không được phân tích chi tiết; các chi tiết ngoài khung sẽ không được giữ lại.';
        }

        renderSelectedTemplate();
        offerCard.hidden = false;
        setCreateMessage('is-info', 'OpenAI chưa tạo được bài. Bạn có thể xem và chấp nhận khung song ngữ giới hạn bên dưới.');
        saveOperation();
    }

    function selectedTemplate() {
        return submissionState.currentOffer?.templates?.[Number(offerTemplateSelect?.value || 0)] || null;
    }

    offerTemplateSelect?.addEventListener('change', function () {
        if (!submissionState.isSubmitting && !submissionState.pending) renderSelectedTemplate();
    });

    function renderSelectedTemplate() {
        const candidate = selectedTemplate();

        if (candidate) {
            if (offerTitle) {
                offerTitle.textContent = candidate.title + ' (' + candidate.level + ', ' + candidate.contentType + ')';
            }

            if (offerPreviewLines && candidate.unfilledPreview) {
                offerPreviewLines.textContent = candidate.unfilledPreview.map(line => line.vietnameseText + '\n' + line.englishMeaning).join('\n\n');
            }

            renderOfferSlots(candidate);
        }

        if (offerAcceptCheck) {
            offerAcceptCheck.checked = false;
        }

        updateConfirmButtonState();
    }

    function offerCoveredText(constraints) {
        return constraints.map(function (c) {
            return c.replace('Selected level, format, short length and advertised register.', 'Đã chọn cấp độ, định dạng và độ dài ngắn phù hợp.');
        }).join('; ');
    }

    function offerMissingText(constraints) {
        return constraints.map(function (c) {
            return c.replace('The complete free description is not interpreted by this engine.', 'Mô tả tự do không được dịch ngoại tuyến.')
                    .replace('Only explicitly confirmed frame slots will be carried over; all other details are omitted with your consent.', 'Chỉ các trường thông tin trong khung được lưu; chi tiết khác sẽ lược bỏ.');
        }).join(' ');
    }

    function renderOfferSlots(candidate) {
        if (!offerSlotsWrap || !offerSlotsContainer) return;

        offerSlotsContainer.innerHTML = '';
        const slots = candidate.slotDefinitions || [];

        if (slots.length === 0) {
            offerSlotsWrap.hidden = true;
            return;
        }

        offerSlotsWrap.hidden = false;
        slots.forEach(function (slot) {
            const col = document.createElement('div');
            col.className = 'col-sm-6';

            const label = document.createElement('label');
            label.className = 'form-label small fw-semibold text-dark mb-1';

            if (slot.key === 'recipient') {
                label.textContent = 'Tên người nhận (ví dụ: An, Linh):';
                const input = document.createElement('input');
                input.type = 'text';
                input.name = 'recipient';
                input.setAttribute('type', 'text');
                input.setAttribute('name', 'recipient');
                input.className = 'form-control form-control-sm';
                input.required = true;
                input.maxLength = 60;
                input.placeholder = 'Ví dụ: An';
                input.addEventListener('input', updateConfirmButtonState);
                col.appendChild(label);
                col.appendChild(input);
            } else if (slot.key === 'startDate') {
                label.textContent = 'Ngày bắt đầu nghỉ:';
                const input = document.createElement('input');
                input.type = 'date';
                input.name = 'startDate';
                input.setAttribute('type', 'date');
                input.setAttribute('name', 'startDate');
                input.className = 'form-control form-control-sm';
                input.required = true;
                input.addEventListener('change', updateConfirmButtonState);
                col.appendChild(label);
                col.appendChild(input);
            } else if (slot.key === 'endDate') {
                label.textContent = 'Ngày kết thúc nghỉ:';
                const input = document.createElement('input');
                input.type = 'date';
                input.name = 'endDate';
                input.setAttribute('type', 'date');
                input.setAttribute('name', 'endDate');
                input.className = 'form-control form-control-sm';
                input.required = true;
                input.addEventListener('change', updateConfirmButtonState);
                col.appendChild(label);
                col.appendChild(input);
            } else if (slot.key === 'reason') {
                label.textContent = 'Lý do nghỉ phép:';
                const select = document.createElement('select');
                select.name = 'reason';
                select.setAttribute('name', 'reason');
                select.className = 'form-select form-select-sm';
                select.required = true;
                const opts = [
                    { value: 'family', label: 'Thăm gia đình' },
                    { value: 'health', label: 'Chăm sóc sức khỏe (Health)' },
                    { value: 'personal', label: 'Việc cá nhân (Personal)' }
                ];
                opts.forEach(function (opt) {
                    const o = document.createElement('option');
                    o.value = opt.value;
                    o.textContent = opt.label;
                    select.appendChild(o);
                });
                select.addEventListener('change', updateConfirmButtonState);
                col.appendChild(label);
                col.appendChild(select);
            }

            const control = col.querySelector('input') || col.querySelector('select');
            if (control) { control.id = 'writing-template-' + slot.key; control.setAttribute('id', control.id); label.setAttribute('for', control.id); }
            offerSlotsContainer.appendChild(col);
        });
    }

    function validateOfferSlots() {
        if (!offerSlotsContainer) return true;
        const candidate = selectedTemplate();
        if (!candidate) return false;
        if (!candidate.slotDefinitions || candidate.slotDefinitions.length === 0) {
            return true;
        }

        const recipientInput = offerSlotsContainer.querySelector('input[name="recipient"]');
        if (recipientInput) {
            const val = recipientInput.value.trim();
            if (val.length < 1 || val.length > 60) return false;
            if (!/^[\p{L} '-]+$/u.test(val)) return false;
        }

        const startInput = offerSlotsContainer.querySelector('input[name="startDate"]');
        const endInput = offerSlotsContainer.querySelector('input[name="endDate"]');
        if (startInput && endInput) {
            if (!startInput.value || !endInput.value) return false;
            const start = new Date(startInput.value);
            const end = new Date(endInput.value);
            if (isNaN(start.getTime()) || isNaN(end.getTime())) return false;
            if (end < start) return false;
            const diffDays = Math.round((end - start) / (1000 * 60 * 60 * 24));
            if (diffDays > 31) return false;
        }

        const reasonSelect = offerSlotsContainer.querySelector('select[name="reason"]');
        if (reasonSelect) {
            if (!['family', 'health', 'personal'].includes(reasonSelect.value)) return false;
        }

        return true;
    }

    function updateConfirmButtonState() {
        if (!offerConfirmBtn) return;
        const accepted = offerAcceptCheck ? offerAcceptCheck.checked : false;
        const slotsValid = validateOfferSlots();
        offerConfirmBtn.disabled = submissionState.isSubmitting || submissionState.pending || !active || !(accepted && slotsValid);
        if (offerSlotsContainer) offerSlotsContainer.querySelectorAll('input, select').forEach(input => { input.disabled = submissionState.isSubmitting || submissionState.pending; });
        if (offerAcceptCheck) offerAcceptCheck.disabled = submissionState.isSubmitting || submissionState.pending;
    }

    if (offerAcceptCheck) {
        offerAcceptCheck.addEventListener('change', updateConfirmButtonState);
    }


});
