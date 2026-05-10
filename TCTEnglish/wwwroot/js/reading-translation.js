/**
 * READING TRANSLATION JAVASCRIPT
 * Chức năng: Tạo bản dịch theo câu, submit AI chấm, publish, load community translations.
 */

document.addEventListener("DOMContentLoaded", function () {
    const passageId = parseInt(document.getElementById('passageId')?.value || '0');
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    let isTranslationMode = false;
    let originalPassageHTML = '';

    // ─── Load community translations ─────────────────────────────────────────
    loadCommunityTranslations();

    // ─── Nút "Bản dịch của tôi" ──────────────────────────────────────────────
    const btnMyTranslation = document.getElementById('btnMyTranslation');
    const btnAddTranslation = document.getElementById('btnAddTranslation');

    if (btnMyTranslation) {
        btnMyTranslation.addEventListener('click', () => toggleTranslationMode());
    }
    if (btnAddTranslation) {
        btnAddTranslation.addEventListener('click', () => toggleTranslationMode());
    }

    // ─── Toggle translation mode ─────────────────────────────────────────────
    async function toggleTranslationMode() {
        const contentEl = document.getElementById('passageContent');
        const titleEl = document.getElementById('passageTitle');

        if (isTranslationMode) {
            // Restore original
            contentEl.innerHTML = originalPassageHTML;
            titleEl.innerHTML = titleEl.dataset.originalTitle || titleEl.textContent;
            if (btnMyTranslation) {
                btnMyTranslation.innerHTML = '<i class="bi bi-pencil-square"></i> <span>Bản dịch của tôi</span>';
                btnMyTranslation.classList.remove('btn-success');
                btnMyTranslation.classList.add('btn-outline-success');
            }
            isTranslationMode = false;
            return;
        }

        // Save original
        originalPassageHTML = contentEl.innerHTML;
        titleEl.dataset.originalTitle = titleEl.textContent;
        isTranslationMode = true;

        if (btnMyTranslation) {
            btnMyTranslation.innerHTML = '<i class="bi bi-x-lg"></i> <span>Hủy dịch</span>';
            btnMyTranslation.classList.remove('btn-outline-success');
            btnMyTranslation.classList.add('btn-success');
        }

        // Load existing translation (if any)
        let existingData = null;
        try {
            const res = await fetch(`/Reading/MyTranslation/${passageId}`);
            const data = await res.json();
            if (data.exists) existingData = data;
        } catch { }

        // Build translation UI
        buildTranslationUI(contentEl, titleEl, existingData);
    }

    // ─── Build translation form ──────────────────────────────────────────────
    function buildTranslationUI(contentEl, titleEl, existingData) {
        const originalTitle = titleEl.dataset.originalTitle || titleEl.textContent;

        // Parse existing content
        let existingPairs = [];
        let existingTitle = '';
        if (existingData) {
            existingTitle = existingData.translatedTitle || '';
            try {
                existingPairs = JSON.parse(existingData.translatedContent);
            } catch { }
        }

        // Title translation
        titleEl.innerHTML = `
            <div class="translation-edit-block mb-3">
                <div class="original-text fw-bold mb-2" style="font-size:1.3rem;">${escapeHtml(originalTitle)}</div>
                <textarea class="form-control translation-input" id="titleTranslationInput"
                    placeholder="Dịch tiêu đề..." rows="1"
                    style="border-left:3px solid #198754;">${escapeHtml(existingTitle)}</textarea>
            </div>
        `;

        // Collect all sentences from paragraphs
        const paragraphs = contentEl.querySelectorAll('.paragraph-block');
        const allSentences = [];

        paragraphs.forEach(block => {
            const textEl = block.querySelector('.passage-text');
            if (!textEl) return;
            const text = textEl.textContent.trim();
            if (text.length < 2) return;

            // Tách câu theo dấu chấm, chấm hỏi, chấm than
            const sentences = text.match(/[^.!?]+[.!?]+/g) || [text];
            sentences.forEach(s => {
                const clean = s.trim();
                if (clean.length > 1) allSentences.push(clean);
            });
        });

        // Build sentence-by-sentence translation UI
        let html = '';
        allSentences.forEach((sentence, index) => {
            const existingTranslation = existingPairs[index]?.translated || '';
            html += `
                <div class="sentence-translation-block mb-3" data-index="${index}">
                    <div class="original-sentence p-3 bg-light rounded-top" style="line-height:1.8; border:1px solid #e9ecef; border-bottom:none;">
                        ${escapeHtml(sentence)}
                    </div>
                    <textarea class="form-control sentence-input rounded-0 rounded-bottom"
                        placeholder="Nhập bản dịch cho câu này..."
                        rows="2"
                        style="border-left:3px solid #198754; border-top:none; resize:vertical;"
                        data-original="${escapeHtml(sentence)}">${escapeHtml(existingTranslation)}</textarea>
                </div>
            `;
        });

        // AI Score display (if already graded)
        if (existingData?.exists && existingData.aiScore != null) {
            const approved = existingData.isAiApproved;
            html += `
                <div class="ai-inline-result mt-4 p-3 rounded ${approved ? 'bg-success bg-opacity-10 border border-success border-opacity-25' : 'bg-danger bg-opacity-10 border border-danger border-opacity-25'}">
                    <div class="d-flex align-items-center gap-2 mb-1">
                        <i class="bi bi-robot ${approved ? 'text-success' : 'text-danger'}"></i>
                        <span class="fw-bold small">Đánh giá của AI</span>
                        <span class="badge ${approved ? 'bg-success' : 'bg-danger'} ms-auto">${existingData.aiScore}/100</span>
                    </div>
                    ${existingData.aiFeedback ? `<p class="mb-0 small text-muted">${escapeHtml(existingData.aiFeedback)}</p>` : ''}
                </div>
            `;
        }

        // Status bar
        html += `
            <div class="d-flex justify-content-between align-items-center mt-4 p-3 bg-light rounded">
                <div class="text-muted small">
                    <i class="bi bi-info-circle me-1"></i>
                    Dịch <span id="filledCount">0</span>/<span id="totalCount">${allSentences.length}</span> câu
                </div>
                <div class="d-flex gap-2">
        `;

        if (existingData?.exists) {
            html += `
                    <button class="btn btn-outline-danger btn-sm" id="btnDeleteTranslation">
                        <i class="bi bi-trash"></i> Xóa bản dịch
                    </button>
            `;
            // Only show publish/hide button when AI has approved the translation
            if (existingData.isAiApproved === true) {
                html += `
                    <button class="btn btn-${existingData.isPublic ? 'warning' : 'info'} btn-sm" id="btnPublishTranslation" data-id="${existingData.id}">
                        <i class="bi bi-${existingData.isPublic ? 'eye-slash' : 'globe'}"></i>
                        ${existingData.isPublic ? 'Ẩn khỏi cộng đồng' : 'Chia sẻ cho cộng đồng'}
                    </button>
                `;
            }
        }

        html += `
                    <button class="btn btn-primary btn-sm px-4" id="btnSubmitTranslation">
                        <i class="bi bi-check-lg"></i> Hoàn thành
                    </button>
                </div>
            </div>
        `;

        contentEl.innerHTML = html;

        // Update filled count
        updateFilledCount();
        contentEl.querySelectorAll('.sentence-input').forEach(input => {
            input.addEventListener('input', updateFilledCount);
        });

        // Submit handler
        document.getElementById('btnSubmitTranslation')?.addEventListener('click', submitTranslation);
        document.getElementById('btnDeleteTranslation')?.addEventListener('click', deleteTranslation);
        document.getElementById('btnPublishTranslation')?.addEventListener('click', publishTranslation);
    }

    // ─── Update filled count ─────────────────────────────────────────────────
    function updateFilledCount() {
        const inputs = document.querySelectorAll('.sentence-input');
        let filled = 0;
        inputs.forEach(i => { if (i.value.trim()) filled++; });
        const el = document.getElementById('filledCount');
        if (el) el.textContent = filled;
    }

    // ─── Submit translation ──────────────────────────────────────────────────
    async function submitTranslation() {
        const btn = document.getElementById('btnSubmitTranslation');
        const inputs = document.querySelectorAll('.sentence-input');
        const titleInput = document.getElementById('titleTranslationInput');

        // Validate: at least some translations
        let hasContent = false;
        inputs.forEach(i => { if (i.value.trim()) hasContent = true; });
        if (!hasContent) {
            showToast('Vui lòng dịch ít nhất một câu.', 'warning');
            return;
        }

        // Build JSON array
        const pairs = [];
        inputs.forEach(input => {
            pairs.push({
                original: input.dataset.original || '',
                translated: input.value.trim()
            });
        });

        const translatedContentJson = JSON.stringify(pairs);

        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Đang chấm...';

        try {
            const res = await fetch('/Reading/SubmitTranslation', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify({
                    passageId: passageId,
                    translatedTitle: titleInput?.value?.trim() || null,
                    translatedContent: translatedContentJson
                })
            });

            const result = await res.json();

            // Show AI feedback modal
            showAiFeedback(result);
            
            // Reload sidebar list because translation might have changed
            loadCommunityTranslations();

            // Update inline AI result and publish button to reflect new state
            refreshInlineAiAndButtons(result);
        } catch (err) {
            showToast('Lỗi kết nối. Vui lòng thử lại.', 'danger');
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check-lg"></i> Hoàn thành';
        }
    }

    // ─── Delete translation ──────────────────────────────────────────────────
    async function deleteTranslation() {
        if (!confirm('Bạn có chắc muốn xóa bản dịch này?')) return;

        try {
            const myRes = await fetch(`/Reading/MyTranslation/${passageId}`);
            const myData = await myRes.json();
            if (!myData.exists) return;

            const res = await fetch('/Reading/DeleteTranslation', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify({ translationId: myData.id })
            });
            const result = await res.json();
            if (result.success) {
                showToast('Đã xóa bản dịch.', 'success');
                toggleTranslationMode(); // Exit mode
                loadCommunityTranslations();
            }
        } catch (err) {
            showToast('Lỗi khi xóa.', 'danger');
        }
    }

    // ─── Publish translation ─────────────────────────────────────────────────
    async function publishTranslation() {
        const btn = document.getElementById('btnPublishTranslation');
        const translationId = parseInt(btn.dataset.id);
        btn.disabled = true;

        try {
            const res = await fetch('/Reading/PublishTranslation', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify({ translationId })
            });
            const result = await res.json();
            if (result.success) {
                showToast(result.message, 'success');
                loadCommunityTranslations();
                // Toggle button state
                const isNowPublic = btn.innerHTML.includes('Ẩn');
                btn.innerHTML = isNowPublic
                    ? '<i class="bi bi-globe"></i> Chia sẻ cho cộng đồng'
                    : '<i class="bi bi-eye-slash"></i> Ẩn khỏi cộng đồng';
                btn.className = isNowPublic
                    ? 'btn btn-info btn-sm'
                    : 'btn btn-warning btn-sm';
            } else {
                showToast(result.message, 'danger');
            }
        } catch {
            showToast('Lỗi kết nối.', 'danger');
        } finally {
            btn.disabled = false;
        }
    }

    // ─── Refresh inline AI result + publish button after submit ───────────
    function refreshInlineAiAndButtons(result) {
        // Update or insert inline AI result
        let aiBlock = document.querySelector('.ai-inline-result');
        const approved = result.isAiApproved === true;
        const aiHtml = `
            <div class="ai-inline-result mt-4 p-3 rounded ${approved ? 'bg-success bg-opacity-10 border border-success border-opacity-25' : 'bg-danger bg-opacity-10 border border-danger border-opacity-25'}">
                <div class="d-flex align-items-center gap-2 mb-1">
                    <i class="bi bi-robot ${approved ? 'text-success' : 'text-danger'}"></i>
                    <span class="fw-bold small">Đánh giá của AI</span>
                    <span class="badge ${approved ? 'bg-success' : 'bg-danger'} ms-auto">${result.aiScore ?? '--'}/100</span>
                </div>
                ${result.aiFeedback ? `<p class="mb-0 small text-muted">${escapeHtml(result.aiFeedback)}</p>` : ''}
            </div>
        `;

        if (aiBlock) {
            aiBlock.outerHTML = aiHtml;
        } else {
            // Insert before the status bar
            const statusBar = document.querySelector('.d-flex.justify-content-between.align-items-center.mt-4.p-3.bg-light.rounded');
            if (statusBar) {
                statusBar.insertAdjacentHTML('beforebegin', aiHtml);
            }
        }

        // Update publish button
        const existingPublishBtn = document.getElementById('btnPublishTranslation');
        if (approved && result.translationId) {
            const isPublic = result.isPublic === true;
            const publishHtml = `
                <button class="btn btn-${isPublic ? 'warning' : 'info'} btn-sm" id="btnPublishTranslation" data-id="${result.translationId}">
                    <i class="bi bi-${isPublic ? 'eye-slash' : 'globe'}"></i>
                    ${isPublic ? 'Ẩn khỏi cộng đồng' : 'Chia sẻ cho cộng đồng'}
                </button>
            `;
            if (existingPublishBtn) {
                existingPublishBtn.outerHTML = publishHtml;
            } else {
                // Insert before the submit button
                const submitBtn = document.getElementById('btnSubmitTranslation');
                if (submitBtn) {
                    submitBtn.insertAdjacentHTML('beforebegin', publishHtml);
                }
            }
            // Re-attach event listener
            document.getElementById('btnPublishTranslation')?.addEventListener('click', publishTranslation);
        } else {
            // AI did not approve: remove publish button if it exists
            if (existingPublishBtn) {
                existingPublishBtn.remove();
            }
        }
    }

    // ─── Show AI Feedback Modal ──────────────────────────────────────────────
    function showAiFeedback(result) {
        const scoreEl = document.getElementById('aiScoreValue');
        const badgeEl = document.getElementById('aiApprovalBadge');
        const feedbackEl = document.getElementById('aiFeedbackText');
        const actionsEl = document.getElementById('aiActions');

        scoreEl.textContent = result.aiScore ?? '--';
        const scoreCircle = document.querySelector('.ai-score-circle');
        if (scoreCircle) {
            scoreCircle.className = 'ai-score-circle mx-auto mb-2';
            if (result.isAiApproved) {
                scoreCircle.classList.add('approved');
            } else {
                scoreCircle.classList.add('rejected');
            }
        }

        badgeEl.innerHTML = result.isAiApproved
            ? '<span class="badge bg-success px-3 py-2"><i class="bi bi-check-circle me-1"></i>Đạt yêu cầu</span>'
            : '<span class="badge bg-danger px-3 py-2"><i class="bi bi-x-circle me-1"></i>Chưa đạt</span>';

        feedbackEl.textContent = result.aiFeedback || result.message || '';

        // Actions
        let actionsHtml = '';
        if (result.isAiApproved && result.translationId) {
            if (!result.isPublic) {
                actionsHtml = `
                    <button class="btn btn-success btn-sm px-3" onclick="quickPublish(${result.translationId})">
                        <i class="bi bi-globe me-1"></i>Chia sẻ ngay
                    </button>
                `;
            } else {
                feedbackEl.textContent += " Bản dịch đã được tự động cập nhật trên cộng đồng.";
            }
        }
        actionsHtml += `<button class="btn btn-outline-secondary btn-sm" data-bs-dismiss="modal">Đóng</button>`;
        actionsEl.innerHTML = actionsHtml;

        const modal = new bootstrap.Modal(document.getElementById('aiFeedbackModal'));
        modal.show();
    }

    // ─── Quick publish from modal ────────────────────────────────────────────
    window.quickPublish = async function (translationId) {
        try {
            const res = await fetch('/Reading/PublishTranslation', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify({ translationId })
            });
            const result = await res.json();
            if (result.success) {
                showToast('Bản dịch đã được chia sẻ!', 'success');
                bootstrap.Modal.getInstance(document.getElementById('aiFeedbackModal'))?.hide();
                loadCommunityTranslations();
            }
        } catch {
            showToast('Lỗi kết nối.', 'danger');
        }
    };

    // ─── Load community translations ─────────────────────────────────────────
    async function loadCommunityTranslations() {
        const container = document.getElementById('translationsList');
        if (!container) return;

        try {
            const res = await fetch(`/Reading/Translations/${passageId}`);
            const data = await res.json();

            if (!data.items || data.items.length === 0) {
                container.innerHTML = '<p class="text-muted text-center small py-2">Chưa có bản dịch nào từ cộng đồng.</p>';
                return;
            }

            container.innerHTML = data.items.map(t => `
                <a href="/Reading/CommunityTranslation/${t.id}" class="translation-list-item d-flex align-items-center gap-3 p-3 rounded mb-2 text-decoration-none">
                    <div class="translation-avatar">
                        ${t.userAvatarUrl
                            ? `<img src="${t.userAvatarUrl}" class="rounded-circle" style="width:40px;height:40px;object-fit:cover;">`
                            : `<div class="avatar-placeholder rounded-circle d-flex align-items-center justify-content-center" style="width:40px;height:40px;background:#e9ecef;"><i class="bi bi-person text-secondary"></i></div>`}
                    </div>
                    <div class="flex-grow-1">
                        <div class="fw-semibold text-dark">${escapeHtml(t.userFullName)}</div>
                        <div class="d-flex align-items-center gap-3 text-muted small">
                            <span><i class="bi bi-hand-thumbs-up-fill text-primary"></i> ${t.likeCount}</span>
                            <span><i class="bi bi-hand-thumbs-down-fill text-danger"></i> ${t.dislikeCount}</span>
                            <span>${new Date(t.createdAtUtc).toLocaleDateString('vi-VN')}</span>
                        </div>
                    </div>
                    <i class="bi bi-chevron-right text-muted"></i>
                </a>
            `).join('');
        } catch {
            container.innerHTML = '<p class="text-muted small text-center">Lỗi tải dữ liệu.</p>';
        }
    }

    // ─── Toast helper ────────────────────────────────────────────────────────
    function showToast(message, type = 'info') {
        const container = document.querySelector('.toast-container') || createToastContainer();
        const toastId = 'toast-' + Date.now();
        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-bg-${type} border-0 show`;
        toast.id = toastId;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;
        container.appendChild(toast);
        setTimeout(() => toast.remove(), 4000);
    }

    function createToastContainer() {
        const c = document.createElement('div');
        c.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        c.style.zIndex = '9999';
        document.body.appendChild(c);
        return c;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }
});
