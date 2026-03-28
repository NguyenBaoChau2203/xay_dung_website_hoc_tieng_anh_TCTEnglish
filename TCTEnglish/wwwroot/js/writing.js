document.addEventListener('DOMContentLoaded', function () {
    const elPracticeRoot = document.querySelector('[data-writing-practice]');
    const elPayload = document.getElementById('writingPracticeData');
    const elProgressFill = document.getElementById('progressFill');
    const elProgressText = document.getElementById('progressText');
    const elSelectionNote = document.getElementById('selectionNote');
    const elSelectedSentenceCounter = document.getElementById('selectedSentenceCounter');
    const elSelectedSentenceText = document.getElementById('selectedSentenceText');
    const elInput = document.getElementById('answerInput');
    const elFeedbackBanner = document.getElementById('feedbackBanner');
    const elFeedbackIcon = document.getElementById('feedbackIcon');
    const elFeedbackTitle = document.getElementById('feedbackTitle');
    const elFeedbackText = document.getElementById('feedbackText');
    const elFeedbackSourceNote = document.getElementById('feedbackSourceNote');
    const elFeedbackSentenceMeta = document.getElementById('feedbackSentenceMeta');
    const elFeedbackSourceText = document.getElementById('feedbackSourceText');
    const elFeedbackSubmissionText = document.getElementById('feedbackSubmissionText');
    const elFeedbackReviewText = document.getElementById('feedbackReviewText');
    const elFeedbackReferenceText = document.getElementById('feedbackReferenceText');
    const elFeedbackHintText = document.getElementById('feedbackHintText');
    const elBtnPrevious = document.getElementById('btnPrevious');
    const elBtnNext = document.getElementById('btnNext');
    const elBtnHint = document.getElementById('btnHint');
    const elBtnSubmit = document.getElementById('btnSubmit');
    const sentenceItems = Array.from(document.querySelectorAll('[data-sentence-item]'));

    if (!elPracticeRoot || !elPayload || !elProgressFill || !elProgressText || !elSelectionNote ||
        !elSelectedSentenceCounter || !elSelectedSentenceText || !elInput ||
        !elFeedbackBanner || !elFeedbackIcon || !elFeedbackTitle || !elFeedbackText ||
        !elFeedbackSourceNote ||
        !elFeedbackSentenceMeta || !elFeedbackSourceText || !elFeedbackSubmissionText ||
        !elFeedbackReviewText || !elFeedbackReferenceText || !elFeedbackHintText ||
        !elBtnPrevious || !elBtnNext || !elBtnHint || !elBtnSubmit || sentenceItems.length === 0) {
        return;
    }

    const antiForgeryToken = elPracticeRoot.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const exerciseId = elPracticeRoot.getAttribute('data-exercise-id') || '';
    const hintUrl = elPracticeRoot.getAttribute('data-hint-url') || '';
    const evaluateUrl = elPracticeRoot.getAttribute('data-evaluate-url') || '';

    let sentencePayload = [];

    try {
        sentencePayload = JSON.parse(elPayload.textContent || '[]');
    } catch {
        return;
    }

    if (!Array.isArray(sentencePayload) || sentencePayload.length === 0) {
        return;
    }

    const orderedSentenceIds = sentencePayload.map(function (sentence) {
        return String(sentence.id);
    });

    const sentenceStateById = new Map(sentencePayload.map(function (sentence) {
        return [String(sentence.id), {
            id: sentence.id,
            number: sentence.number,
            vietnameseText: sentence.vietnameseText || '',
            placeholder: sentence.placeholder || 'Nhập câu tiếng Anh của bạn ở đây...',
            breakAfter: Boolean(sentence.breakAfter),
            draft: '',
            lastAttemptText: '',
            acceptedText: '',
            hasAccepted: false,
            lastHintTitle: '',
            lastHintText: '',
            lastEvaluation: null
        }];
    }));

    let activeSentenceId = orderedSentenceIds[0];
    let isSubmitting = false;

    function collapseWhitespace(value) {
        return String(value || '')
            .replace(/\s+/g, ' ')
            .trim();
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
        if (!sentence) {
            return false;
        }

        const draft = collapseWhitespace(sentence.draft);
        if (!draft) {
            return false;
        }

        if (sentence.hasAccepted) {
            return draft !== collapseWhitespace(sentence.acceptedText);
        }

        if (!sentence.lastAttemptText) {
            return true;
        }

        return draft !== collapseWhitespace(sentence.lastAttemptText);
    }

    function getDisplayedLineText(sentence) {
        return sentence && sentence.hasAccepted
            ? sentence.acceptedText
            : sentence.vietnameseText;
    }

    function getSentenceStatusLabel(sentence, isActive) {
        if (!sentence) {
            return 'Chờ làm';
        }

        if (sentence.hasAccepted && hasPendingChanges(sentence)) {
            return 'Đang sửa';
        }

        if (sentence.hasAccepted) {
            return 'Hoàn thành';
        }

        if (sentence.lastEvaluation && !sentence.lastEvaluation.passed) {
            return 'Làm lại';
        }

        if (isActive) {
            return 'Đang chọn';
        }

        if (hasDraft(sentence)) {
            return 'Bản nháp';
        }

        return 'Chờ làm';
    }

    function setSelectionNote(message, isWarning) {
        elSelectionNote.textContent = message;
        elSelectionNote.classList.toggle('is-warning', Boolean(isWarning));
    }

    function setBanner(type, title, text) {
        const classNames = ['is-info', 'is-hint', 'is-success', 'is-error', 'is-warning'];
        const iconMap = {
            info: 'fas fa-circle-info',
            hint: 'fas fa-lightbulb',
            success: 'fas fa-check-circle',
            error: 'fas fa-circle-xmark',
            warning: 'fas fa-triangle-exclamation'
        };

        elFeedbackBanner.classList.remove.apply(elFeedbackBanner.classList, classNames);
        elFeedbackBanner.classList.add(iconMap[type] ? `is-${type}` : 'is-info');
        elFeedbackIcon.className = iconMap[type] || iconMap.info;
        elFeedbackTitle.textContent = title;
        elFeedbackText.textContent = text;
    }

    function updateProgress() {
        const completedCount = Array.from(sentenceStateById.values()).filter(function (sentence) {
            return sentence.hasAccepted;
        }).length;
        const totalCount = orderedSentenceIds.length;
        const progressPercent = totalCount === 0 ? 0 : (completedCount / totalCount) * 100;

        elProgressFill.style.width = `${progressPercent}%`;
        elProgressText.textContent = `${completedCount}/${totalCount} câu đã xong`;
    }

    function renderSentenceList() {
        sentenceItems.forEach(function (item) {
            const sentenceId = item.getAttribute('data-sentence-id');
            const sentence = getSentenceById(sentenceId);
            const isActive = sentenceId === activeSentenceId;
            const elText = item.querySelector('[data-sentence-text]');
            const isEditing = Boolean(sentence) && hasPendingChanges(sentence);
            const isRetry = Boolean(sentence && sentence.lastEvaluation && !sentence.lastEvaluation.passed && !sentence.hasAccepted);

            if (!sentence || !elText) {
                return;
            }

            item.classList.toggle('is-active', isActive);
            item.classList.toggle('is-completed', sentence.hasAccepted && !isEditing);
            item.classList.toggle('is-editing', isEditing);
            item.classList.toggle('is-retry', isRetry);

            item.setAttribute(
                'aria-label',
                `Câu ${sentence.number}. ${getSentenceStatusLabel(sentence, isActive)}. ${sentence.hasAccepted ? 'Đang hiển thị tiếng Anh.' : 'Đang hiển thị tiếng Việt.'}`
            );

            elText.textContent = getDisplayedLineText(sentence);
        });
    }

    function syncButtons() {
        const activeSentence = getActiveSentence();
        const activeIndex = getSentenceIndex(activeSentenceId);
        const inputValue = collapseWhitespace(elInput.value);

        elBtnPrevious.disabled = !activeSentence || activeIndex <= 0 || isSubmitting;
        elBtnNext.disabled = !activeSentence || activeIndex === -1 || activeIndex >= orderedSentenceIds.length - 1 || isSubmitting;
        elBtnHint.disabled = !activeSentence || isSubmitting;
        elBtnSubmit.disabled = !activeSentence || inputValue.length === 0 || isSubmitting;
    }

    function renderActiveSentence() {
        const activeSentence = getActiveSentence();

        if (!activeSentence) {
            elSelectedSentenceCounter.textContent = 'Chưa chọn câu';
            elSelectedSentenceText.textContent = 'Chọn một câu để bắt đầu.';
            elInput.value = '';
            elInput.placeholder = 'Chọn câu rồi bắt đầu nhập...';
            syncButtons();
            return;
        }

        elSelectedSentenceCounter.textContent = `Câu ${activeSentence.number} / ${orderedSentenceIds.length}`;
        elSelectedSentenceText.textContent = activeSentence.vietnameseText;
        elInput.value = activeSentence.draft;
        elInput.placeholder = activeSentence.placeholder;

        syncButtons();
    }

    function getDefaultReviewText(sentence) {
        if (sentence && sentence.lastEvaluation) {
            const evaluation = sentence.lastEvaluation;

            if (!evaluation.usedAi) {
                const overallFeedback = evaluation.passed
                    ? 'Đánh giá nhanh của hệ thống đã chấp nhận câu này.'
                    : 'Câu này vẫn cần chỉnh trước khi thay thế câu tiếng Việt trong bài.';
                const meaningFeedback = evaluation.meaningFeedback || 'Hãy kiểm tra xem ý nghĩa đã bám sát câu gốc chưa.';
                const grammarFeedback = evaluation.grammarFeedback || 'Hãy kiểm tra lại dấu câu và cấu trúc câu.';
                const naturalnessFeedback = evaluation.naturalnessFeedback || 'Hãy giữ câu tiếng Anh ngắn gọn và tự nhiên.';
                const wordChoiceFeedback = evaluation.wordChoiceFeedback || 'Hãy dùng từ vựng sát với ý tiếng Việt.';

                return `Tổng quan: ${overallFeedback} Ý nghĩa: ${meaningFeedback} Ngữ pháp: ${grammarFeedback} Độ tự nhiên: ${naturalnessFeedback} Từ vựng: ${wordChoiceFeedback}`;
            }

            return evaluation.reviewText || 'Hệ thống đã có phản hồi cho câu này.';
        }

        return 'Hãy gửi câu đang chọn để nhận phản hồi từ hệ thống tại đây.';
    }

    function getDefaultRewriteText(sentence) {
        if (sentence && sentence.lastEvaluation) {
            return sentence.lastEvaluation.suggestedRewrite
                ? sentence.lastEvaluation.suggestedRewrite
                : 'Lần gửi này chưa có câu gợi ý chỉnh lại.';
        }

        return 'Nếu hệ thống gợi ý một câu viết tốt hơn, nội dung sẽ xuất hiện ở đây sau khi bạn gửi bài.';
    }

    function syncFeedbackSourceNote(sentence) {
        const evaluation = sentence && sentence.lastEvaluation
            ? sentence.lastEvaluation
            : null;

        if (!evaluation || evaluation.usedAi) {
            elFeedbackSourceNote.hidden = true;
            elFeedbackSourceNote.textContent = '';
            return;
        }

        elFeedbackSourceNote.hidden = false;
        elFeedbackSourceNote.textContent = evaluation.passed
            ? 'Phản hồi AI chi tiết hiện chưa khả dụng, nên kết quả đạt này đang dùng đánh giá nhanh từ hệ thống.'
            : 'Phản hồi AI chi tiết hiện chưa khả dụng, nên kết quả làm lại này đang dùng đánh giá nhanh từ hệ thống.';
    }

    function getEvaluationBannerCopy(evaluation) {
        if (!evaluation) {
            return {
                title: 'Câu đạt yêu cầu',
                text: 'Hệ thống đã chấp nhận câu này.'
            };
        }

        if (!evaluation.usedAi) {
            return evaluation.passed
                ? {
                    title: 'Câu đạt yêu cầu',
                    text: 'Đánh giá nhanh của hệ thống đã chấp nhận câu này, bạn có thể tiếp tục.'
                }
                : {
                    title: 'Hãy thử lại câu này',
                    text: 'Câu này chưa sẵn sàng để thay thế dòng tiếng Việt.'
                };
        }

        return {
            title: evaluation.summaryTitle || (evaluation.passed ? 'Câu đạt yêu cầu' : 'Hãy sửa lại câu này'),
            text: evaluation.summaryText || (evaluation.passed
                ? 'Hệ thống đã chấp nhận câu này.'
                : 'Hệ thống vẫn chưa thể chấp nhận câu này.')
        };
    }

    function renderFeedbackWorkspace(reason) {
        const activeSentence = getActiveSentence();

        if (!activeSentence) {
            setBanner('warning', 'Chưa có câu khả dụng', 'Hãy chọn một câu trong bài để tiếp tục.');
            syncFeedbackSourceNote(null);
            elFeedbackSentenceMeta.textContent = 'Chưa chọn câu';
            elFeedbackSourceText.textContent = 'Chọn một câu để bắt đầu.';
            elFeedbackSubmissionText.textContent = 'Hãy gửi câu đang chọn để hiện phần tiếng Anh của bạn ở đây.';
            elFeedbackReviewText.textContent = 'Hãy gửi câu đang chọn để nhận phản hồi từ hệ thống tại đây.';
            elFeedbackReferenceText.textContent = 'Nếu hệ thống gợi ý một câu viết tốt hơn, nội dung sẽ xuất hiện ở đây sau khi bạn gửi bài.';
            elFeedbackHintText.textContent = 'Hãy dùng Gợi ý nếu bạn muốn được nhắc nhanh cho câu hiện tại trước khi gửi bài.';
            return;
        }

        const currentEnglish = collapseWhitespace(activeSentence.draft)
            || activeSentence.lastAttemptText
            || (activeSentence.hasAccepted ? activeSentence.acceptedText : '');
        const evaluation = activeSentence.lastEvaluation;

        syncFeedbackSourceNote(activeSentence);
        elFeedbackSentenceMeta.textContent = `Câu ${activeSentence.number} / ${orderedSentenceIds.length}`;
        elFeedbackSourceText.textContent = activeSentence.vietnameseText;
        elFeedbackSubmissionText.textContent = currentEnglish || 'Hãy gửi câu đang chọn để hiện phần tiếng Anh của bạn ở đây.';
        elFeedbackReviewText.textContent = getDefaultReviewText(activeSentence);
        elFeedbackReferenceText.textContent = getDefaultRewriteText(activeSentence);
        elFeedbackHintText.textContent = activeSentence.lastHintText || 'Hãy dùng Gợi ý nếu bạn muốn được nhắc nhanh cho câu hiện tại trước khi gửi bài.';

        if (reason === 'hint') {
            setBanner(
                'hint',
                activeSentence.lastHintTitle || `Gợi ý cho câu ${activeSentence.number}`,
                activeSentence.lastHintText || 'Hãy xem gợi ý này rồi gửi câu của bạn để được nhận xét.'
            );
            return;
        }

        if (evaluation) {
            const bannerCopy = getEvaluationBannerCopy(evaluation);

            setBanner(
                evaluation.passed ? 'success' : 'warning',
                bannerCopy.title,
                bannerCopy.text
            );
            return;
        }

        if (hasDraft(activeSentence)) {
            setBanner(
                'info',
                'Bản nháp đã sẵn sàng',
                'Bản nháp này chỉ áp dụng cho câu đang chọn. Hãy gửi bài để hệ thống chấm.'
            );
            return;
        }

        setBanner(
            'info',
            'Mỗi lần một câu',
            'Hãy chỉ dịch câu đang chọn rồi nhấn Gửi bài để hệ thống chấm.'
        );
    }

    function updateUi(reason) {
        renderSentenceList();
        updateProgress();
        syncButtons();
        renderFeedbackWorkspace(reason);
    }

    function setActiveSentence(sentenceId, options) {
        const nextSentence = getSentenceById(sentenceId);
        if (!nextSentence) {
            return;
        }

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

    async function requestHint() {
        const activeSentence = getActiveSentence();
        if (!activeSentence) {
            return;
        }

        if (!hintUrl || !exerciseId) {
            activeSentence.lastHintTitle = `Gợi ý cho câu ${activeSentence.number}`;
            activeSentence.lastHintText = 'Bài này chưa được cấu hình gợi ý.';
            renderFeedbackWorkspace('hint');
            return;
        }

        try {
            const url = new URL(hintUrl, window.location.origin);
            url.searchParams.set('exerciseId', exerciseId);
            url.searchParams.set('sentenceId', activeSentence.id);

            const response = await fetch(url.toString(), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                activeSentence.lastHintTitle = `Gợi ý cho câu ${activeSentence.number}`;
                activeSentence.lastHintText = 'Không tải được gợi ý cho câu này.';
                renderFeedbackWorkspace('hint');
                return;
            }

            const hint = await response.json();
            activeSentence.lastHintTitle = hint && hint.hintTitle
                ? hint.hintTitle
                : `Gợi ý cho câu ${activeSentence.number}`;
            activeSentence.lastHintText = hint && hint.hintText
                ? hint.hintText
                : 'Câu này hiện chưa có gợi ý.';
            renderFeedbackWorkspace('hint');
        } catch {
            activeSentence.lastHintTitle = `Gợi ý cho câu ${activeSentence.number}`;
            activeSentence.lastHintText = 'Không tải được gợi ý cho câu này.';
            renderFeedbackWorkspace('hint');
        }
    }

    async function evaluateSentence(sentence, userAnswer) {
        const response = await fetch(evaluateUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiForgeryToken,
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                exerciseId: Number(exerciseId),
                sentenceId: sentence.id,
                userAnswer: userAnswer
            })
        });

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

    async function submitCurrentSentence() {
        const activeSentence = getActiveSentence();
        if (!activeSentence) {
            return;
        }

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
        elBtnSubmit.textContent = 'Đang chấm...';
        activeSentence.draft = trimmedDraft;
        syncButtons();

        try {
            const evaluation = await evaluateSentence(activeSentence, trimmedDraft);
            const bannerCopy = getEvaluationBannerCopy(evaluation);

            activeSentence.lastAttemptText = trimmedDraft;
            activeSentence.lastEvaluation = evaluation;

            if (evaluation.passed) {
                activeSentence.acceptedText = trimmedDraft;
                activeSentence.hasAccepted = true;
            }

            const currentIndex = getSentenceIndex(activeSentenceId);
            const hasNextSentence = currentIndex >= 0 && currentIndex < orderedSentenceIds.length - 1;

            if (evaluation.passed && evaluation.canAutoAdvance && hasNextSentence) {
                const nextSentenceId = orderedSentenceIds[currentIndex + 1];
                const nextSentence = getSentenceById(nextSentenceId);

                setSelectionNote(
                    `Câu ${activeSentence.number} đã đạt yêu cầu. Tiếp tục với câu ${nextSentence ? nextSentence.number : activeSentence.number + 1}.`,
                    false
                );

                setActiveSentence(nextSentenceId, {
                    focusInput: true,
                    reason: 'selection',
                    transitionBanner: {
                        type: 'success',
                        title: bannerCopy.title,
                        text: `Câu ${activeSentence.number} đã đạt. Bạn có thể tiếp tục với câu ${nextSentence ? nextSentence.number : activeSentence.number + 1}.`
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
            const message = error instanceof Error
                ? error.message
                : 'Hệ thống không thể chấm câu này.';

            setSelectionNote(message, true);
            setBanner('error', 'Chấm bài thất bại', message);
            elFeedbackReviewText.textContent = 'Hãy xử lý vấn đề ở trên rồi thử lại.';
        } finally {
            isSubmitting = false;
            elBtnSubmit.textContent = 'Gửi bài';
            syncButtons();
        }
    }

    sentenceItems.forEach(function (item) {
        item.addEventListener('click', function () {
            const sentenceId = item.getAttribute('data-sentence-id');
            setSelectionNote('Hiện chỉ có câu đang được tô sáng là có thể chỉnh sửa. Hãy gửi câu này khi bạn sẵn sàng.', false);
            setActiveSentence(sentenceId, { focusInput: true });
        });
    });

    elInput.addEventListener('input', function () {
        const activeSentence = getActiveSentence();
        if (!activeSentence) {
            return;
        }

        activeSentence.draft = elInput.value;
        setSelectionNote('Ô nhập này chỉ điều khiển câu đang chọn. Hãy gửi bài để hệ thống chấm.', false);
        updateUi('typing');
    });

    elInput.addEventListener('keydown', function (event) {
        if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
            event.preventDefault();
            submitCurrentSentence();
        }
    });

    elBtnPrevious.addEventListener('click', function () {
        const activeIndex = getSentenceIndex(activeSentenceId);
        if (activeIndex > 0) {
            setActiveSentence(orderedSentenceIds[activeIndex - 1], { focusInput: true });
        }
    });

    elBtnNext.addEventListener('click', function () {
        const activeIndex = getSentenceIndex(activeSentenceId);
        if (activeIndex >= 0 && activeIndex < orderedSentenceIds.length - 1) {
            setActiveSentence(orderedSentenceIds[activeIndex + 1], { focusInput: true });
        }
    });

    elBtnHint.addEventListener('click', function () {
        requestHint();
    });

    elBtnSubmit.addEventListener('click', function () {
        submitCurrentSentence();
    });

    renderActiveSentence();
    updateUi('selection');
    setSelectionNote('Chỉ có câu đang chọn là có thể chỉnh sửa. Một câu chỉ đổi sang tiếng Anh sau khi hệ thống chấp nhận.', false);
});
