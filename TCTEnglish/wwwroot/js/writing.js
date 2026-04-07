/**
 * writing.js – Practice UI logic (Phase 4 Overhaul)
 *
 * New in this version:
 *  - Browser draft persistence via localStorage (keyed per exercise + sentence)
 *  - Explicit UI states: loading, rate-limit (429), session-expired (401), fallback
 *  - Loading spinner on submit button
 *  - Debounced draft-saved indicator
 *  - Ctrl+Enter submit shortcut
 *  - All DOM IDs preserved for compatibility
 */

document.addEventListener('DOMContentLoaded', function () {

    /* ── DOM refs ─────────────────────────────────────────────────────────── */
    const elPracticeRoot            = document.querySelector('[data-writing-practice]');
    const elPayload                 = document.getElementById('writingPracticeData');
    const elProgressFill            = document.getElementById('progressFill');
    const elProgressText            = document.getElementById('progressText');
    const elSelectionNote           = document.getElementById('selectionNote');
    const elSelectedSentenceCounter = document.getElementById('selectedSentenceCounter');
    const elSelectedSentenceText    = document.getElementById('selectedSentenceText');
    const elInput                   = document.getElementById('answerInput');
    const elFeedbackBanner          = document.getElementById('feedbackBanner');
    const elFeedbackIcon            = document.getElementById('feedbackIcon');
    const elFeedbackTitle           = document.getElementById('feedbackTitle');
    const elFeedbackText            = document.getElementById('feedbackText');
    const elFeedbackSourceNote      = document.getElementById('feedbackSourceNote');
    const elSessionExpiredPrompt    = document.getElementById('sessionExpiredPrompt');
    const elBtnReload               = document.getElementById('btnReload');
    const elFeedbackSentenceMeta    = document.getElementById('feedbackSentenceMeta');
    const elFeedbackSourceText      = document.getElementById('feedbackSourceText');
    const elFeedbackSubmissionText  = document.getElementById('feedbackSubmissionText');
    const elFeedbackReviewText      = document.getElementById('feedbackReviewText');
    const elFeedbackReferenceText   = document.getElementById('feedbackReferenceText');
    const elFeedbackHintText        = document.getElementById('feedbackHintText');
    const elBtnPrevious             = document.getElementById('btnPrevious');
    const elBtnNext                 = document.getElementById('btnNext');
    const elBtnHint                 = document.getElementById('btnHint');
    const elBtnSubmit               = document.getElementById('btnSubmit');
    const elBtnSubmitText           = document.getElementById('btnSubmitText');
    const elDraftIndicator          = document.getElementById('draftIndicator');
    const sentenceItems             = Array.from(document.querySelectorAll('[data-sentence-item]'));

    /* ── Guard: bail if any required element is missing ───────────────────── */
    const requiredEls = [
        elPracticeRoot, elPayload, elProgressFill, elProgressText,
        elSelectionNote, elSelectedSentenceCounter, elSelectedSentenceText,
        elInput, elFeedbackBanner, elFeedbackIcon, elFeedbackTitle, elFeedbackText,
        elFeedbackSourceNote, elFeedbackSentenceMeta, elFeedbackSourceText,
        elFeedbackSubmissionText, elFeedbackReviewText, elFeedbackReferenceText,
        elFeedbackHintText, elBtnPrevious, elBtnNext, elBtnHint, elBtnSubmit, elBtnSubmitText
    ];

    if (requiredEls.some(function (el) { return !el; }) || sentenceItems.length === 0) {
        return;
    }

    /* ── Config from data attributes ──────────────────────────────────────── */
    const antiForgeryToken = elPracticeRoot.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const exerciseId       = elPracticeRoot.getAttribute('data-exercise-id') || '';
    const resumeSentenceId = elPracticeRoot.getAttribute('data-resume-sentence-id') || '';
    const hintUrl          = elPracticeRoot.getAttribute('data-hint-url')    || '';
    const evaluateUrl      = elPracticeRoot.getAttribute('data-evaluate-url') || '';

    /* ── Parse sentence payload ───────────────────────────────────────────── */
    let sentencePayload = [];

    try {
        sentencePayload = JSON.parse(elPayload.textContent || '[]');
    } catch {
        return;
    }

    if (!Array.isArray(sentencePayload) || sentencePayload.length === 0) {
        return;
    }

    const orderedSentenceIds = sentencePayload.map(function (s) { return String(s.id); });

    /* ── localStorage helpers ─────────────────────────────────────────────── */
    const DRAFT_NS = 'wp_draft_ex' + exerciseId + '_';

    function draftKey(sentenceId) {
        return DRAFT_NS + String(sentenceId);
    }

    function loadDraft(sentenceId) {
        try {
            return localStorage.getItem(draftKey(sentenceId)) || '';
        } catch {
            return '';
        }
    }

    function saveDraft(sentenceId, text) {
        try {
            if (text) {
                localStorage.setItem(draftKey(sentenceId), text);
            } else {
                localStorage.removeItem(draftKey(sentenceId));
            }
        } catch { /* storage full or blocked – silent */ }
    }

    function clearDraft(sentenceId) {
        try {
            localStorage.removeItem(draftKey(sentenceId));
        } catch { /* silent */ }
    }

    /* ── Sentence state ───────────────────────────────────────────────────── */
    const sentenceStateById = new Map(sentencePayload.map(function (s) {
        const restoredDraft = loadDraft(s.id);
        const persistedAttempt = collapseWhitespace(s.lastSubmittedAnswer || '');
        const persistedEvaluation = normalizeEvaluationSnapshot(s.lastEvaluation);
        const lastEvaluationPassed = typeof s.lastEvaluationPassed === 'boolean'
            ? s.lastEvaluationPassed
            : (persistedEvaluation ? Boolean(persistedEvaluation.passed) : null);

        return [String(s.id), {
            id:              s.id,
            number:          s.number,
            vietnameseText:  s.vietnameseText || '',
            placeholder:     s.placeholder   || 'Nhập câu tiếng Anh của bạn ở đây...',
            breakAfter:      Boolean(s.breakAfter),
            draft:           restoredDraft || persistedAttempt,
            attemptCount:    Number(s.attemptCount || 0),
            lastAttemptText: persistedAttempt,
            acceptedText:    collapseWhitespace(s.acceptedAnswer || ''),
            hasAccepted:     Boolean(s.hasAccepted),
            lastEvaluationPassed: lastEvaluationPassed,
            lastHintTitle:   '',
            lastHintText:    '',
            lastEvaluation:  persistedEvaluation
        }];
    }));

    let activeSentenceId = orderedSentenceIds.includes(String(resumeSentenceId))
        ? String(resumeSentenceId)
        : orderedSentenceIds[0];
    let isSubmitting     = false;

    /* ── Draft-saved indicator (debounced) ───────────────────────────────── */
    let draftIndicatorTimer = null;

    function showDraftSaved() {
        if (!elDraftIndicator) { return; }
        elDraftIndicator.classList.add('is-visible');
        clearTimeout(draftIndicatorTimer);
        draftIndicatorTimer = setTimeout(function () {
            elDraftIndicator.classList.remove('is-visible');
        }, 2000);
    }

    /* ── Utility ──────────────────────────────────────────────────────────── */
    function collapseWhitespace(value) {
        return String(value || '').replace(/\s+/g, ' ').trim();
    }

    function normalizeEvaluationSnapshot(value) {
        if (!value || typeof value !== 'object') {
            return null;
        }

        return {
            passed: Boolean(value.passed),
            usedAi: Boolean(value.usedAi),
            evaluationSource: collapseWhitespace(value.evaluationSource || ''),
            summaryTitle: collapseWhitespace(value.summaryTitle || ''),
            summaryText: collapseWhitespace(value.summaryText || ''),
            reviewText: String(value.reviewText || '').trim(),
            meaningFeedback: collapseWhitespace(value.meaningFeedback || ''),
            grammarFeedback: collapseWhitespace(value.grammarFeedback || ''),
            naturalnessFeedback: collapseWhitespace(value.naturalnessFeedback || ''),
            wordChoiceFeedback: collapseWhitespace(value.wordChoiceFeedback || ''),
            suggestedRewrite: String(value.suggestedRewrite || '').trim()
        };
    }

    function getSentenceById(sentenceId) {
        return sentenceStateById.get(String(sentenceId)) || null;
    }

    function getActiveSentence() {
        return getSentenceById(activeSentenceId);
    }

    function getSentenceIndex(sentenceId) {
        return orderedSentenceIds.indexOf(String(sentenceId));
    }

    function hasDraft(sentence) {
        return collapseWhitespace(sentence && sentence.draft).length > 0;
    }

    function hasPendingChanges(sentence) {
        if (!sentence) { return false; }
        const draft = collapseWhitespace(sentence.draft);
        if (!draft)   { return false; }

        if (sentence.hasAccepted) {
            return draft !== collapseWhitespace(sentence.acceptedText);
        }

        if (!sentence.lastAttemptText) { return true; }

        return draft !== collapseWhitespace(sentence.lastAttemptText);
    }

    function getDisplayedLineText(sentence) {
        return sentence && sentence.hasAccepted
            ? sentence.acceptedText
            : sentence.vietnameseText;
    }

    function getSentenceStatusLabel(sentence, isActive) {
        if (!sentence)                                                    { return 'Chờ làm';   }
        if (sentence.hasAccepted && hasPendingChanges(sentence))         { return 'Đang sửa';  }
        if (sentence.hasAccepted)                                        { return 'Hoàn thành'; }
        if (sentence.lastEvaluationPassed === false)                     { return 'Làm lại';   }
        if (isActive)                                                     { return 'Đang chọn'; }
        if (hasDraft(sentence))                                          { return 'Bản nháp';  }
        return 'Chờ làm';
    }

    /* ── Banner ──────────────────────────────────────────────────────────── */
    const BANNER_CLASSES = ['is-info', 'is-hint', 'is-success', 'is-warning',
                            'is-error', 'is-loading', 'is-rate-limit', 'is-session-expired'];

    const ICON_MAP = {
        info:            'fas fa-circle-info',
        hint:            'fas fa-lightbulb',
        success:         'fas fa-check-circle',
        error:           'fas fa-circle-xmark',
        warning:         'fas fa-triangle-exclamation',
        loading:         'fas fa-spinner fa-spin',
        'rate-limit':    'fas fa-clock',
        'session-expired': 'fas fa-lock'
    };

    function setBanner(type, title, text) {
        elFeedbackBanner.classList.remove.apply(elFeedbackBanner.classList, BANNER_CLASSES);
        elFeedbackBanner.classList.add('is-' + (ICON_MAP[type] ? type : 'info'));
        elFeedbackIcon.className = ICON_MAP[type] || ICON_MAP.info;
        elFeedbackTitle.textContent = title;
        elFeedbackText.textContent  = text;
    }

    function setBannerLoading() {
        elFeedbackBanner.classList.remove.apply(elFeedbackBanner.classList, BANNER_CLASSES);
        elFeedbackBanner.classList.add('is-loading');
        elFeedbackIcon.className = 'writing-spinner';
        elFeedbackTitle.textContent = 'Đang chấm bài…';
        elFeedbackText.textContent  = 'Hệ thống đang kiểm tra câu của bạn, vui lòng chờ.';
    }

    /* ── Session-expired prompt ─────────────────────────────────────────── */
    function showSessionExpiredPrompt() {
        if (elSessionExpiredPrompt) {
            elSessionExpiredPrompt.hidden = false;
        }
    }

    function hideSessionExpiredPrompt() {
        if (elSessionExpiredPrompt) {
            elSessionExpiredPrompt.hidden = true;
        }
    }

    if (elBtnReload) {
        elBtnReload.addEventListener('click', function () {
            window.location.reload();
        });
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */
    function setSelectionNote(message, isWarning) {
        elSelectionNote.textContent = message;
        elSelectionNote.classList.toggle('is-warning', Boolean(isWarning));
    }

    function updateProgress() {
        const completed = Array.from(sentenceStateById.values()).filter(function (s) {
            return s.hasAccepted;
        }).length;
        const total   = orderedSentenceIds.length;
        const percent = total === 0 ? 0 : (completed / total) * 100;

        elProgressFill.style.width = percent + '%';
        elProgressText.textContent = completed + '/' + total + ' câu đã xong';
    }

    function renderSentenceList() {
        sentenceItems.forEach(function (item) {
            const sentenceId = item.getAttribute('data-sentence-id');
            const sentence   = getSentenceById(sentenceId);
            const isActive   = sentenceId === activeSentenceId;
            const elText     = item.querySelector('[data-sentence-text]');
            const isEditing  = Boolean(sentence) && hasPendingChanges(sentence);
            const isRetry    = Boolean(sentence && sentence.lastEvaluationPassed === false && !sentence.hasAccepted);

            if (!sentence || !elText) { return; }

            item.classList.toggle('is-active',    isActive);
            item.classList.toggle('is-completed', sentence.hasAccepted && !isEditing);
            item.classList.toggle('is-editing',   isEditing);
            item.classList.toggle('is-retry',     isRetry);
            item.setAttribute(
                'aria-label',
                'Câu ' + sentence.number + '. ' + getSentenceStatusLabel(sentence, isActive) + '. ' +
                (sentence.hasAccepted ? 'Đang hiển thị tiếng Anh.' : 'Đang hiển thị tiếng Việt.')
            );

            elText.textContent = getDisplayedLineText(sentence);
        });
    }

    function syncButtons() {
        const active      = getActiveSentence();
        const activeIndex = getSentenceIndex(activeSentenceId);
        const inputValue  = collapseWhitespace(elInput.value);

        elBtnPrevious.disabled = !active || activeIndex <= 0                                             || isSubmitting;
        elBtnNext.disabled     = !active || activeIndex === -1 || activeIndex >= orderedSentenceIds.length - 1 || isSubmitting;
        elBtnHint.disabled     = !active                                                                  || isSubmitting;
        elBtnSubmit.disabled   = !active || inputValue.length === 0                                       || isSubmitting;
    }

    function renderActiveSentence() {
        const active = getActiveSentence();

        if (!active) {
            elSelectedSentenceCounter.textContent = 'Chưa chọn câu';
            elSelectedSentenceText.textContent    = 'Chọn một câu bên trên để bắt đầu.';
            elInput.value                         = '';
            elInput.placeholder                   = 'Chọn câu rồi bắt đầu nhập...';
            syncButtons();
            return;
        }

        elSelectedSentenceCounter.textContent = 'Câu ' + active.number + ' / ' + orderedSentenceIds.length;
        elSelectedSentenceText.textContent    = active.vietnameseText;
        elInput.value                         = active.draft;
        elInput.placeholder                   = active.placeholder;

        syncButtons();
    }

    /* ── Feedback review / rewrite text builders ─────────────────────────── */
    function getDefaultReviewText(sentence) {
        if (sentence && sentence.lastEvaluation) {
            const ev = sentence.lastEvaluation;

            if (!ev.usedAi) {
                const overall     = ev.passed
                    ? 'Đánh giá nhanh của hệ thống đã chấp nhận câu này.'
                    : 'Câu này vẫn cần chỉnh trước khi thay thế câu tiếng Việt trong bài.';
                const meaning     = ev.meaningFeedback     || 'Hãy kiểm tra xem ý nghĩa đã bám sát câu gốc chưa.';
                const grammar     = ev.grammarFeedback     || 'Hãy kiểm tra lại dấu câu và cấu trúc câu.';
                const naturalness = ev.naturalnessFeedback || 'Hãy giữ câu tiếng Anh ngắn gọn và tự nhiên.';
                const wordChoice  = ev.wordChoiceFeedback  || 'Hãy dùng từ vựng sát với ý tiếng Việt.';

                return 'Tổng quan: ' + overall +
                       '\nÝ nghĩa: '  + meaning +
                       '\nNgữ pháp: ' + grammar +
                       '\nĐộ tự nhiên: ' + naturalness +
                       '\nTừ vựng: '  + wordChoice;
            }

            return ev.reviewText || (ev.passed
                ? 'Câu này đã được chấp nhận.'
                : 'Câu này cần chỉnh thêm. Hãy xem gợi ý bên dưới.');
        }

        return 'Hãy gửi câu đang chọn để nhận phản hồi chi tiết tại đây.';
    }

    function getDefaultRewriteText(sentence) {
        if (sentence && sentence.lastEvaluation) {
            return sentence.lastEvaluation.suggestedRewrite
                ? sentence.lastEvaluation.suggestedRewrite
                : 'Lần gửi này chưa có câu gợi ý chỉnh lại.';
        }

        return 'Nếu hệ thống có câu gợi ý, nội dung sẽ xuất hiện tại đây sau khi bạn gửi bài.';
    }

    function syncFeedbackSourceNote(sentence) {
        const ev = sentence && sentence.lastEvaluation ? sentence.lastEvaluation : null;

        if (!ev || ev.usedAi) {
            elFeedbackSourceNote.hidden      = true;
            elFeedbackSourceNote.textContent = '';
            return;
        }

        // Rule-based fallback was used (not AI) – label this honestly
        elFeedbackSourceNote.hidden      = false;
        elFeedbackSourceNote.textContent = ev.passed
            ? 'Hệ thống đã dùng đánh giá tự động (không dùng AI). Câu này đủ điều kiện để tiếp tục.'
            : 'Hệ thống đã dùng đánh giá tự động (không dùng AI). Hãy chỉnh sửa và gửi lại câu này.';
    }

    function getEvaluationBannerCopy(evaluation) {
        if (!evaluation) {
            return { title: 'Câu đạt yêu cầu', text: 'Hệ thống đã chấp nhận câu này.' };
        }

        if (!evaluation.usedAi) {
            return evaluation.passed
                ? { title: 'Câu đạt yêu cầu',      text: 'Đánh giá nhanh của hệ thống đã chấp nhận câu này, bạn có thể tiếp tục.' }
                : { title: 'Hãy thử lại câu này',   text: 'Câu này chưa sẵn sàng để thay thế dòng tiếng Việt.' };
        }

        return {
            title: evaluation.summaryTitle || (evaluation.passed ? 'Câu đạt yêu cầu' : 'Hãy sửa lại câu này'),
            text:  evaluation.summaryText  || (evaluation.passed
                ? 'Hệ thống đã chấp nhận câu này.'
                : 'Hệ thống vẫn chưa thể chấp nhận câu này.')
        };
    }

    /* ── renderFeedbackWorkspace ──────────────────────────────────────────── */
    function renderFeedbackWorkspace(reason) {
        const active = getActiveSentence();

        hideSessionExpiredPrompt();

        if (!active) {
            setBanner('warning', 'Chưa có câu khả dụng', 'Hãy chọn một câu trong bài để tiếp tục.');
            syncFeedbackSourceNote(null);
            elFeedbackSentenceMeta.textContent    = 'Chưa chọn câu';
            elFeedbackSourceText.textContent      = 'Chọn một câu để bắt đầu.';
            elFeedbackSubmissionText.textContent  = 'Hãy gửi câu đang chọn để hiện câu tiếng Anh tại đây.';
            elFeedbackReviewText.textContent      = 'Hãy gửi câu đang chọn để nhận phản hồi chi tiết tại đây.';
            elFeedbackReferenceText.textContent   = 'Câu gợi ý chỉnh lại sẽ xuất hiện tại đây nếu câu của bạn cần sửa.';
            elFeedbackHintText.textContent        = 'Nhấn nút Gợi ý để hiện bản dịch tiếng Anh của câu đang chọn.';
            return;
        }

        const currentEnglish = collapseWhitespace(active.draft)
            || active.lastAttemptText
            || (active.hasAccepted ? active.acceptedText : '');
        const evaluation = active.lastEvaluation;

        syncFeedbackSourceNote(active);
        elFeedbackSentenceMeta.textContent   = 'Câu ' + active.number + ' / ' + orderedSentenceIds.length;
        elFeedbackSourceText.textContent     = active.vietnameseText;
        elFeedbackSubmissionText.textContent = currentEnglish || 'Hãy gửi câu đang chọn để hiện câu tiếng Anh tại đây.';
        elFeedbackReviewText.textContent     = getDefaultReviewText(active);
        elFeedbackReferenceText.textContent  = getDefaultRewriteText(active);
        elFeedbackHintText.textContent       = active.lastHintText || 'Nhấn nút Gợi ý để hiện bản dịch tiếng Anh của câu đang chọn.';

        if (reason === 'hint') {
            setBanner(
                'hint',
                active.lastHintTitle || 'Bản dịch tham khảo – Câu ' + active.number,
                active.lastHintText  || 'Hãy xem bản dịch này rồi gửi câu của bạn để được nhận xét.'
            );
            return;
        }

        if (evaluation) {
            const copy = getEvaluationBannerCopy(evaluation);
            setBanner(evaluation.passed ? 'success' : 'warning', copy.title, copy.text);
            return;
        }

        if (hasDraft(active)) {
            setBanner('info', 'Bản nháp đã sẵn sàng', 'Hãy gửi bài để hệ thống chấm câu đang chọn.');
            return;
        }

        setBanner('info', 'Mỗi lần một câu', 'Hãy dịch câu đang chọn rồi nhấn Gửi bài để nhận phản hồi.');
    }

    /* ── updateUi ─────────────────────────────────────────────────────────── */
    function updateUi(reason) {
        renderSentenceList();
        updateProgress();
        syncButtons();
        renderFeedbackWorkspace(reason);
    }

    /* ── setActiveSentence ────────────────────────────────────────────────── */
    function setActiveSentence(sentenceId, options) {
        const next = getSentenceById(sentenceId);
        if (!next) { return; }

        activeSentenceId = String(sentenceId);
        renderActiveSentence();
        updateUi(options && options.reason ? options.reason : 'selection');

        if (options && options.transitionBanner) {
            setBanner(
                options.transitionBanner.type,
                options.transitionBanner.title,
                options.transitionBanner.text
            );
        }

        if (options && options.focusInput) {
            elInput.focus();
        }
    }

    /* ── requestHint ──────────────────────────────────────────────────────── */
    async function requestHint() {
        const active = getActiveSentence();
        if (!active) { return; }

        if (!hintUrl || !exerciseId) {
            active.lastHintTitle = 'Gợi ý cho câu ' + active.number;
            active.lastHintText  = 'Bài này chưa được cấu hình gợi ý.';
            renderFeedbackWorkspace('hint');
            return;
        }

        elBtnHint.disabled = true;

        try {
            const url = new URL(hintUrl, window.location.origin);
            url.searchParams.set('exerciseId', exerciseId);
            url.searchParams.set('sentenceId', active.id);

            const response = await fetch(url.toString(), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (response.status === 401 || response.status === 403) {
                active.lastHintTitle = 'Gợi ý cho câu ' + active.number;
                active.lastHintText  = 'Phiên đăng nhập đã hết hạn. Hãy tải lại trang.';
                setBanner('session-expired', 'Phiên đăng nhập hết hạn', 'Bạn cần đăng nhập lại để tiếp tục.');
                showSessionExpiredPrompt();
                return;
            }

            if (response.status === 429) {
                active.lastHintTitle = 'Gợi ý cho câu ' + active.number;
                active.lastHintText  = 'Bạn đang gửi quá nhiều yêu cầu. Hãy thử lại sau vài giây.';
                setBanner('rate-limit', 'Quá nhiều yêu cầu', 'Hãy đợi một chút rồi thử lại.');
                return;
            }

            if (!response.ok) {
                active.lastHintTitle = 'Gợi ý cho câu ' + active.number;
                active.lastHintText  = 'Không tải được gợi ý cho câu này.';
                renderFeedbackWorkspace('hint');
                return;
            }

            const hint = await response.json();
            active.lastHintTitle = hint && hint.hintTitle ? hint.hintTitle : 'Gợi ý cho câu ' + active.number;
            active.lastHintText  = hint && hint.hintText  ? hint.hintText  : 'Câu này hiện chưa có gợi ý.';
            renderFeedbackWorkspace('hint');

        } catch {
            active.lastHintTitle = 'Gợi ý cho câu ' + active.number;
            active.lastHintText  = 'Không tải được gợi ý cho câu này.';
            renderFeedbackWorkspace('hint');
        } finally {
            syncButtons();
        }
    }

    /* ── evaluateSentence (raw HTTP call) ─────────────────────────────────── */
    async function evaluateSentence(sentence, userAnswer) {
        const response = await fetch(evaluateUrl, {
            method:  'POST',
            headers: {
                'Content-Type':             'application/json',
                'RequestVerificationToken': antiForgeryToken,
                'X-Requested-With':         'XMLHttpRequest'
            },
            body: JSON.stringify({
                exerciseId: Number(exerciseId),
                sentenceId: sentence.id,
                userAnswer: userAnswer
            })
        });

        // Surface special HTTP status codes before trying to parse JSON
        if (response.status === 401 || response.status === 403) {
            const err = new Error('SESSION_EXPIRED');
            err.code  = 'SESSION_EXPIRED';
            throw err;
        }

        if (response.status === 429) {
            let retryAfter = response.headers.get('Retry-After');
            const err = new Error('RATE_LIMITED');
            err.code       = 'RATE_LIMITED';
            err.retryAfter = retryAfter ? parseInt(retryAfter, 10) : null;
            throw err;
        }

        let payload = null;

        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (!response.ok) {
            throw new Error(payload && payload.error ? payload.error : 'Hệ thống không thể chấm câu này.');
        }

        if (!payload || payload.success !== true || !payload.data) {
            throw new Error('Hệ thống trả về kết quả chấm không hợp lệ.');
        }

        return payload.data;
    }

    /* ── submitCurrentSentence ────────────────────────────────────────────── */
    async function submitCurrentSentence() {
        const active = getActiveSentence();
        if (!active) { return; }

        const trimmedDraft = collapseWhitespace(elInput.value);

        if (!trimmedDraft) {
            setSelectionNote('Hãy nhập một câu tiếng Anh trước khi gửi bài.', true);
            setBanner('warning', 'Cần nhập câu', 'Hãy nhập câu đang chọn trước khi nhấn Gửi bài.');
            elFeedbackReviewText.textContent = 'Khu vực nhận xét sẽ hiển thị sau khi bạn gửi một câu.';
            syncButtons();
            return;
        }

        if (!evaluateUrl || !antiForgeryToken) {
            setSelectionNote('Trang này chưa sẵn sàng để chấm bài.', true);
            setBanner('error', 'Chưa thể chấm bài', 'Thiếu endpoint chấm bài hoặc mã chống giả mạo.');
            return;
        }

        isSubmitting = true;
        active.draft = trimmedDraft;
        elBtnSubmitText.textContent = 'Đang chấm…';
        setBannerLoading();
        syncButtons();

        try {
            const evaluation = await evaluateSentence(active, trimmedDraft);
            const copy       = getEvaluationBannerCopy(evaluation);

            active.lastAttemptText = trimmedDraft;
            active.attemptCount    = Number(active.attemptCount || 0) + 1;
            active.lastEvaluation  = evaluation;
            active.lastEvaluationPassed = Boolean(evaluation.passed);
            hideSessionExpiredPrompt();

            if (evaluation.passed) {
                active.acceptedText = trimmedDraft;
                active.hasAccepted  = true;
                clearDraft(active.id);       // remove persisted draft once accepted
            }

            const currentIndex  = getSentenceIndex(activeSentenceId);
            const hasNext       = currentIndex >= 0 && currentIndex < orderedSentenceIds.length - 1;

            if (evaluation.passed && evaluation.canAutoAdvance && hasNext) {
                const nextId       = orderedSentenceIds[currentIndex + 1];
                const nextSentence = getSentenceById(nextId);

                setSelectionNote(
                    'Câu ' + active.number + ' đã đạt yêu cầu. Tiếp tục với câu ' +
                    (nextSentence ? nextSentence.number : active.number + 1) + '.',
                    false
                );

                setActiveSentence(nextId, {
                    focusInput: true,
                    reason: 'selection',
                    transitionBanner: {
                        type:  'success',
                        title: copy.title,
                        text:  'Câu ' + active.number + ' đã đạt. Bạn có thể tiếp tục với câu ' +
                               (nextSentence ? nextSentence.number : active.number + 1) + '.'
                    }
                });

                return;
            }

            if (evaluation.passed) {
                setSelectionNote('Câu này đã đạt yêu cầu. Bạn có thể tiếp tục khi sẵn sàng.', false);
            } else {
                setSelectionNote('Câu này vẫn cần chỉnh thêm trước khi thay thế câu tiếng Việt trong bài.', true);
            }

            renderActiveSentence();
            updateUi('submitted');

        } catch (error) {
            // Handle typed errors
            if (error && error.code === 'SESSION_EXPIRED') {
                setSelectionNote('Phiên đăng nhập đã hết hạn. Hãy tải lại trang.', true);
                setBanner('session-expired', 'Phiên đăng nhập hết hạn', 'Bạn cần đăng nhập lại để tiếp tục.');
                showSessionExpiredPrompt();
                elFeedbackReviewText.textContent = 'Hãy tải lại trang rồi đăng nhập lại.';
                return;
            }

            if (error && error.code === 'RATE_LIMITED') {
                const wait = error.retryAfter ? ' Vui lòng đợi ' + error.retryAfter + ' giây.' : '';
                setSelectionNote('Bạn đang gửi quá nhiều yêu cầu.' + wait, true);
                setBanner('rate-limit', 'Quá nhiều yêu cầu', 'Hãy đợi một chút rồi thử lại.' + wait);
                elFeedbackReviewText.textContent = 'Hãy đợi một lúc rồi gửi lại.';
                return;
            }

            const message = error instanceof Error
                ? error.message
                : 'Hệ thống không thể chấm câu này.';

            setSelectionNote(message, true);
            setBanner('error', 'Chấm bài thất bại', message);
            elFeedbackReviewText.textContent = 'Hãy xử lý vấn đề ở trên rồi thử lại.';

        } finally {
            isSubmitting = false;
            elBtnSubmitText.textContent = 'Gửi bài';
            syncButtons();
        }
    }

    /* ── Event listeners ──────────────────────────────────────────────────── */

    // Sentence item click in full-text
    sentenceItems.forEach(function (item) {
        item.addEventListener('click', function () {
            const sentenceId = item.getAttribute('data-sentence-id');
            setSelectionNote('Hiện chỉ có câu đang được tô sáng là có thể chỉnh sửa. Hãy gửi câu này khi bạn sẵn sàng.', false);
            setActiveSentence(sentenceId, { focusInput: true });
        });
    });

    // Textarea input – persist draft
    elInput.addEventListener('input', function () {
        const active = getActiveSentence();
        if (!active) { return; }

        active.draft = elInput.value;
        saveDraft(active.id, active.draft);
        showDraftSaved();
        setSelectionNote('Ô nhập này chỉ điều khiển câu đang chọn. Hãy gửi bài để hệ thống chấm.', false);
        updateUi('typing');
    });

    // Ctrl+Enter shortcut
    elInput.addEventListener('keydown', function (event) {
        if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
            event.preventDefault();
            submitCurrentSentence();
        }
    });

    // Navigation buttons
    elBtnPrevious.addEventListener('click', function () {
        const idx = getSentenceIndex(activeSentenceId);
        if (idx > 0) {
            setActiveSentence(orderedSentenceIds[idx - 1], { focusInput: true });
        }
    });

    elBtnNext.addEventListener('click', function () {
        const idx = getSentenceIndex(activeSentenceId);
        if (idx >= 0 && idx < orderedSentenceIds.length - 1) {
            setActiveSentence(orderedSentenceIds[idx + 1], { focusInput: true });
        }
    });

    elBtnHint.addEventListener('click', function () { requestHint(); });
    elBtnSubmit.addEventListener('click', function () { submitCurrentSentence(); });

    /* ── Initial render ───────────────────────────────────────────────────── */
    renderActiveSentence();
    updateUi('selection');

    // If the first sentence has a restored draft, let the user know
    const firstState = getSentenceById(orderedSentenceIds[0]);
    if (firstState && hasDraft(firstState)) {
        setSelectionNote('Bản nháp trước của bạn đã được khôi phục. Hãy tiếp tục hoặc chỉnh lại.', false);
    } else {
        setSelectionNote('Nhấn vào câu trên để chọn, rồi dịch và gửi bài. Nhấn Ctrl+Enter để gửi nhanh.', false);
    }
});
