document.addEventListener('DOMContentLoaded', function () {
    const filterForm = document.querySelector('[data-writing-filter-form]');
    const filterSelects = document.querySelectorAll('[data-filter-select]');
    const createForm = document.querySelector('[data-writing-create-form]');
    const deleteForms = document.querySelectorAll('[data-writing-delete-form]');

    if (filterForm && filterSelects.length > 0) {
        filterSelects.forEach(function (select) {
            select.addEventListener('change', function () {
                filterForm.submit();
            });
        });
    }

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

    const sourceTextInput = createForm.querySelector('textarea[name="sourceText"]');
    const submitButton = createForm.querySelector('[data-writing-create-submit]');
    const submitText = createForm.querySelector('[data-writing-create-submit-text]');
    const messageBox = createForm.querySelector('[data-writing-create-message]');
    const antiForgeryToken = createForm.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const createUrl = createForm.getAttribute('data-create-url') || '';
    const practiceUrl = createForm.getAttribute('data-practice-url') || '';

    if (!sourceTextInput || !submitButton || !submitText || !messageBox || !antiForgeryToken || !createUrl) {
        return;
    }

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
    }

    function setSubmitting(isSubmitting) {
        submitButton.disabled = isSubmitting;
        submitText.textContent = isSubmitting
            ? 'Đang tạo bài viết...'
            : 'Tạo bài viết bằng AI';
    }

    function createIdempotencyKey() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') {
            return window.crypto.randomUUID();
        }

        return 'writing-' + Date.now() + '-' + Math.random().toString(16).slice(2);
    }

    function buildPracticeUrl(result) {
        if (!practiceUrl) {
            return '';
        }

        const nextUrl = new URL(practiceUrl, window.location.origin);
        nextUrl.searchParams.set('level', result.level || 'intermediate');
        nextUrl.searchParams.set('contentType', result.contentType || 'articles');
        nextUrl.searchParams.set('exerciseId', String(result.exerciseId));
        return nextUrl.pathname + nextUrl.search;
    }

    createForm.addEventListener('submit', async function (event) {
        event.preventDefault();

        const sourceText = sourceTextInput.value.trim();
        if (!sourceText) {
            setCreateMessage('is-error', 'Vui lòng dán bài viết trước khi gửi cho AI.');
            sourceTextInput.focus();
            return;
        }

        clearCreateMessage();
        setSubmitting(true);

        try {
            const response = await fetch(createUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    sourceText: sourceText,
                    idempotencyKey: createIdempotencyKey()
                })
            });

            let payload = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }

            if (!response.ok) {
                if (response.status === 429) {
                    const retryAfter = payload && payload.retryAfterSeconds
                        ? ' Hãy thử lại sau ' + payload.retryAfterSeconds + ' giây.'
                        : '';
                    setCreateMessage('is-error', (payload && payload.error) || ('Bạn đã dùng hết lượt tạo bài hôm nay.' + retryAfter));
                    return;
                }

                setCreateMessage('is-error', (payload && payload.error) || 'Không thể tạo bài viết bằng AI lúc này. Vui lòng thử lại.');
                return;
            }

            if (!payload || payload.success !== true || !payload.data) {
                setCreateMessage('is-error', 'Hệ thống trả về kết quả tạo bài không hợp lệ.');
                return;
            }

            setCreateMessage('is-success', 'Đã tạo bài viết thành công. Đang chuyển đến bài luyện tập...');

            const nextUrl = buildPracticeUrl(payload.data);
            if (!nextUrl) {
                window.location.reload();
                return;
            }

            window.location.assign(nextUrl);
        } catch {
            setCreateMessage('is-error', 'Kết nối tới dịch vụ AI đang gặp vấn đề. Vui lòng thử lại.');
        } finally {
            setSubmitting(false);
        }
    });

    sourceTextInput.addEventListener('input', function () {
        if (!messageBox.hidden) {
            clearCreateMessage();
        }
    });
});
