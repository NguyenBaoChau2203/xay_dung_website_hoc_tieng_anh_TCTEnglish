(() => {
    const modal = document.getElementById('joinByCodeModal');
    const form = modal?.querySelector('form');
    if (!form) return;

    const code = document.getElementById('joinCodeInput');
    const passwordWrap = document.getElementById('joinCodePasswordWrap');
    const password = document.getElementById('joinCodePassword');
    const feedback = document.getElementById('joinCodeError');
    const submit = document.getElementById('confirmJoinByCodeBtn');

    function showMessage(message) {
        feedback.textContent = message;
        feedback.hidden = !message;
    }

    modal.addEventListener('show.bs.modal', () => {
        form.reset();
        passwordWrap.hidden = true;
        password.disabled = true;
        showMessage('');
    });

    modal.addEventListener('shown.bs.modal', () => code.focus());

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (submit.disabled) return;

        if (!code.value.trim()) {
            showMessage('Vui lòng nhập mã lớp.');
            code.focus();
            return;
        }

        submit.disabled = true;
        showMessage('');
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new URLSearchParams(new FormData(form))
            });
            const text = await response.text();
            if (!response.ok) {
                const message = response.headers.get('Content-Type')?.startsWith('text/plain')
                    ? text
                    : '';
                showMessage(message || 'Không tham gia được.');
                return;
            }

            const result = JSON.parse(text);
            if (result.status === 'needsPassword') {
                passwordWrap.hidden = false;
                password.disabled = false;
                password.focus();
                showMessage(result.message);
            } else if (result.status === 'pending') {
                const pendingToast = {
                    type: 'success',
                    title: 'Đã gửi yêu cầu tham gia',
                    message: result.message || 'Vui lòng chờ trưởng nhóm phê duyệt.'
                };
                if (window.TctFeedback && typeof window.TctFeedback.toast === 'function') {
                    window.TctFeedback.toast(pendingToast);
                } else {
                    // Clay toast host without window.alert
                    let host = document.getElementById('tctFeedbackToastHost');
                    if (!host) {
                        host = document.createElement('div');
                        host.id = 'tctFeedbackToastHost';
                        host.className = 'tct-feedback-toast-host toast-container';
                        host.setAttribute('aria-live', 'polite');
                        document.body.appendChild(host);
                    }
                    const el = document.createElement('div');
                    el.className = 'tct-feedback-toast tct-feedback-toast--success is-visible';
                    el.setAttribute('role', 'status');
                    el.innerHTML = '<div class="tct-feedback-toast__body"><p class="tct-feedback-toast__title"></p><p class="tct-feedback-toast__message"></p></div>';
                    el.querySelector('.tct-feedback-toast__title').textContent = pendingToast.title;
                    el.querySelector('.tct-feedback-toast__message').textContent = pendingToast.message;
                    host.appendChild(el);
                    window.setTimeout(() => el.remove(), 3500);
                }
                window.bootstrap.Modal.getInstance(modal)?.hide();
            } else if (result.redirectUrl) {
                window.location.assign(result.redirectUrl);
            }
        } catch {
            showMessage('Không kết nối được máy chủ.');
        } finally {
            submit.disabled = false;
        }
    });
})();
