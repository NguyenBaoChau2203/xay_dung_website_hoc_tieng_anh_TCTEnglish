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
    const catalogUrl = createForm.getAttribute('data-catalog-url') || '';

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
                const code = payload && (payload.errorCode || payload.code) ? String(payload.errorCode || payload.code) : '';
                if (response.status === 401 || response.status === 403) {
                    setCreateMessage('is-error', (payload && payload.error) || availabilityMessage('access'));
                } else if (response.status === 429 && code !== 'circuit_open' && code !== 'rate_limited') {
                    const retryAfter = payload && payload.retryAfterSeconds
                        ? ' Hãy thử lại sau ' + payload.retryAfterSeconds + ' giây.'
                        : '';
                    setCreateMessage('is-error', (payload && payload.error) || ('Bạn đã dùng hết lượt tạo bài hôm nay.' + retryAfter));
                } else if (response.status === 429 || code === 'rate_limited') {
                    setCreateMessage('is-error', availabilityMessage('rate'));
                } else if (code === 'invalid_configuration' || code === 'authentication' || code === 'quota_exhausted') {
                    setCreateMessage('is-error', availabilityMessage('unavailable'));
                } else if (!payload) {
                    setCreateMessage('is-error', availabilityMessage('outage'));
                } else {
                    setCreateMessage('is-error', (payload && payload.error) || availabilityMessage('outage'));
                }
                showCatalogAlternative();
                return;
            }

            if (!payload || payload.success !== true || !payload.data) {
                setCreateMessage('is-error', 'Hệ thống trả về kết quả tạo bài không hợp lệ.');
                showCatalogAlternative();
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
            setCreateMessage('is-error', availabilityMessage('outage'));
            showCatalogAlternative();
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
