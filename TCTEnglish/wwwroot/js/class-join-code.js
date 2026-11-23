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
                window.TctFeedback.toast({type: 'success', title: 'Đã gửi yêu cầu tham gia', message: result.message});
                window.bootstrap.Modal.getInstance(modal).hide();
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
