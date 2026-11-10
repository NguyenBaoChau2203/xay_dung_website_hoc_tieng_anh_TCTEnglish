/**
 * TCT English - Account Auth & Recovery UI (AU01 - AU04)
 * Owned script for Login, Register, Forgot Password, and Reset Password views.
 */
(function (global) {
    'use strict';

    function $(id) {
        return document.getElementById(id);
    }

    function setPending(btn, busy, text) {
        if (!btn) return;
        if (!btn.dataset.origText) {
            btn.dataset.origText = btn.innerHTML;
        }
        btn.disabled = busy;
        if (busy) {
            btn.setAttribute('aria-busy', 'true');
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' + text;
        } else {
            if (typeof btn.removeAttribute === 'function') {
                btn.removeAttribute('aria-busy');
            } else if (btn.attributes) {
                delete btn.attributes['aria-busy'];
            }
            if (btn.dataset.origText) {
                btn.innerHTML = btn.dataset.origText;
            }
        }
    }

    function setNativePending(e, form, submitBtn, text) {
        if (e) e._authHandled = true;
        form._inFlight = true;
        setPending(submitBtn, true, text);
        // A validator may cancel after this handler without starting a request.
        Promise.resolve().then(function () {
            if (e && e.defaultPrevented) {
                form._inFlight = false;
                setPending(submitBtn, false);
            }
        });
    }

    function switchTab(tabName) {
        var isReg = tabName === 'register';
        var tabLog = $('tab-login');
        var tabReg = $('tab-register');
        var formLog = $('form-login');
        var formReg = $('form-register');
        var heading = $('auth-heading');
        var desc = $('auth-description');
        var card = document.querySelector('.auth-card');

        if (card && card.setAttribute) {
            card.setAttribute('data-active-tab', isReg ? 'register' : 'login');
        }

        if (tabLog) {
            tabLog.setAttribute('aria-selected', String(!isReg));
            tabLog.tabIndex = isReg ? -1 : 0;
            if (tabLog.classList) tabLog.classList.toggle('active', !isReg);
        }
        if (tabReg) {
            tabReg.setAttribute('aria-selected', String(isReg));
            tabReg.tabIndex = isReg ? 0 : -1;
            if (tabReg.classList) tabReg.classList.toggle('active', isReg);
        }

        if (formLog) {
            formLog.hidden = isReg;
            formLog.style.display = isReg ? 'none' : '';
        }
        if (formReg) {
            formReg.hidden = !isReg;
            formReg.style.display = isReg ? '' : 'none';
        }

        if (heading) {
            heading.textContent = isReg ? 'Tạo tài khoản' : 'Đăng nhập';
            if (heading.classList) {
                heading.classList.remove('auth-header-animate');
                void heading.offsetWidth;
                heading.classList.add('auth-header-animate');
            }
        }
        if (desc) {
            desc.textContent = isReg ? 'Tạo tài khoản để bắt đầu học.' : 'Tiếp tục học với TCT English.';
            if (desc.classList) {
                desc.classList.remove('auth-header-animate');
                void desc.offsetWidth;
                desc.classList.add('auth-header-animate');
            }
        }
    }

    function togglePassword(inputId, btn) {
        var input = typeof inputId === 'string' ? $(inputId) : inputId;
        if (!input) return;
        var willShow = input.type === 'password';
        input.type = willShow ? 'text' : 'password';

        if (btn) {
            btn.setAttribute('aria-pressed', String(willShow));
            var currentLabel = btn.getAttribute('aria-label') || '';
            if (willShow) {
                btn.setAttribute('aria-label', currentLabel.replace('Hiện', 'Ẩn'));
            } else {
                btn.setAttribute('aria-label', currentLabel.replace('Ẩn', 'Hiện'));
            }
            var icon = btn.querySelector('i');
            if (icon) {
                icon.className = willShow ? 'fa-solid fa-eye-slash' : 'fa-solid fa-eye';
            }
        }
    }

    function showLoginError(msg) {
        var errorBox = $('login-error-msg');
        if (!errorBox) {
            var form = $('form-login');
            if (!form) return;
            errorBox = document.createElement('div');
            errorBox.id = 'login-error-msg';
            errorBox.className = 'alert alert-danger';
            errorBox.setAttribute('role', 'alert');
            errorBox.tabIndex = -1;
            form.appendChild(errorBox);
        }

        if (typeof errorBox.replaceChildren === 'function') {
            errorBox.replaceChildren();
        } else {
            errorBox.innerHTML = '';
        }
        var icon = document.createElement('i');
        icon.className = 'fa-solid fa-circle-exclamation me-2';
        icon.setAttribute('aria-hidden', 'true');
        errorBox.appendChild(icon);
        var textSpan = document.createElement('span');
        textSpan.textContent = msg;
        errorBox.appendChild(textSpan);
        errorBox.hidden = false;
        errorBox.style.display = '';
        if (typeof errorBox.focus === 'function') {
            errorBox.focus();
        }
    }

    var activeNavDestination = '';
    var navRecoveryTimerId = null;

    function safeSetTimeout(fn, ms) {
        if (typeof window !== 'undefined' && typeof window.setTimeout === 'function') {
            return window.setTimeout(fn, ms);
        }
        if (typeof setTimeout === 'function') {
            return setTimeout(fn, ms);
        }
        return null;
    }

    function safeClearTimeout(id) {
        if (!id) return;
        if (typeof window !== 'undefined' && typeof window.clearTimeout === 'function') {
            window.clearTimeout(id);
            return;
        }
        if (typeof clearTimeout === 'function') {
            clearTimeout(id);
        }
    }

    function hideLoaderOverlay() {
        if (navRecoveryTimerId) {
            safeClearTimeout(navRecoveryTimerId);
            navRecoveryTimerId = null;
        }
        var overlay = $('loader-overlay');
        if (overlay) {
            overlay.hidden = true;
            if (overlay.style) {
                overlay.style.display = 'none';
            }
        }
        var recoveryBox = $('loader-recovery-actions');
        if (recoveryBox && recoveryBox.classList) {
            recoveryBox.classList.add('d-none');
        }
    }

    function showLoaderOverlay(title, subtitle, destination) {
        var overlay = $('loader-overlay');
        if (!overlay) return;

        activeNavDestination = destination || '';
        if (navRecoveryTimerId) {
            safeClearTimeout(navRecoveryTimerId);
            navRecoveryTimerId = null;
        }

        var titleEl = $('loading-title');
        if (titleEl && title) {
            titleEl.textContent = title;
        }
        var fillEl = $('loading-fill');
        if (fillEl && subtitle) {
            fillEl.textContent = subtitle;
        }

        var recoveryBox = $('loader-recovery-actions');
        if (recoveryBox && recoveryBox.classList) {
            recoveryBox.classList.add('d-none');
        }

        overlay.hidden = false;
        if (overlay.style) {
            overlay.style.display = 'flex';
        }

        navRecoveryTimerId = safeSetTimeout(function () {
            if (overlay && !overlay.hidden && recoveryBox && recoveryBox.classList) {
                recoveryBox.classList.remove('d-none');
            }
        }, 8000);
    }

    function showLoaderAndRedirect(targetUrl) {
        var destination = targetUrl || ($('form-login') && $('form-login').dataset.homeUrl) || '/Home/Index';
        showLoaderOverlay('Đang mở trang học tập…', 'Đang chuẩn bị không gian học tập của bạn.', destination);
        window.location.href = destination;
    }

    function handleExternalLogin(url) {
        showLoaderOverlay('Đang kết nối dịch vụ xác thực…', 'Chuyển tiếp đến cổng đăng nhập an toàn.', url);
        window.location.href = url;
    }

    function handleLogin(e) {
        if (e && e.defaultPrevented) return;
        if (e && typeof e.preventDefault === 'function') {
            e.preventDefault();
        }

        var form = $('form-login');
        var submitBtn = $('btn-login-submit');
        if (!form || !submitBtn) return;
        if (submitBtn.disabled || form._inFlight) return;

        form._inFlight = true;
        setPending(submitBtn, true, 'Đang đăng nhập…');

        var errorBox = $('login-error-msg');
        if (errorBox) {
            errorBox.hidden = true;
            errorBox.style.display = 'none';
        }
        var blockedAlert = $('blocked-alert');
        if (blockedAlert) {
            blockedAlert.hidden = true;
            blockedAlert.style.display = 'none';
        }

        var formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData
        })
        .then(function (res) {
            if (!res.ok) throw new Error('HTTP ' + res.status);
            return res.json();
        })
        .then(function (data) {
            form._inFlight = false;
            if (data && data.success) {
                showLoaderAndRedirect(form.dataset.homeUrl);
            } else if (data && data.blocked) {
                setPending(submitBtn, false);
                window.location.reload();
            } else {
                setPending(submitBtn, false);
                var message = (data && data.message) ? data.message : 'Email hoặc mật khẩu không đúng.';
                showLoginError(message);
            }
        })
        .catch(function () {
            form._inFlight = false;
            setPending(submitBtn, false);
            showLoginError('Không thể kết nối. Vui lòng thử lại.');
        });
    }

    function handleRegister(e) {
        if (e && e.defaultPrevented) return;
        var form = $('form-register');
        var terms = $('terms');
        var submitBtn = $('btn-register-submit');

        if (!terms || !terms.checked) {
            if (e && typeof e.preventDefault === 'function') {
                e.preventDefault();
            }
            if (global.Swal) {
                global.Swal.fire({
                    icon: 'warning',
                    title: 'Điều khoản dịch vụ',
                    text: 'Vui lòng đồng ý với điều khoản dịch vụ để tiếp tục.',
                    confirmButtonColor: 'var(--tct-primary)',
                    confirmButtonText: 'Đã hiểu'
                });
            } else {
                alert('Vui lòng đồng ý với điều khoản dịch vụ để tiếp tục.');
            }
            return;
        }

        if (!form || !submitBtn) return;
        if (submitBtn.disabled || form._inFlight) {
            if (e && typeof e.preventDefault === 'function') {
                e.preventDefault();
            }
            return;
        }

        setNativePending(e, form, submitBtn, 'Đang đăng ký…');
    }

    function handleForgotSubmit(e) {
        if (e && e.defaultPrevented) return;
        var form = $('form-forgot-password');
        var submitBtn = $('btn-forgot-submit');
        if (!form || !submitBtn) return;
        if (e && e._authHandled) return;

        if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
            return;
        }

        if (submitBtn.disabled || form._inFlight) {
            if (e && typeof e.preventDefault === 'function') {
                e.preventDefault();
            }
            return;
        }

        setNativePending(e, form, submitBtn, 'Đang xử lý…');
    }

    function handleResetSubmit(e) {
        if (e && e.defaultPrevented) return;
        var form = $('form-reset-password');
        var submitBtn = $('btn-reset-submit');
        var newPwd = $('NewPassword') || $('reset-new-password');
        var confirmPwd = $('ConfirmPassword') || $('reset-confirm-password');
        var mismatch = $('reset-mismatch');
        if (!form || !submitBtn) return;
        if (e && e._authHandled) return;

        if (newPwd && confirmPwd && newPwd.value !== confirmPwd.value) {
            if (e && typeof e.preventDefault === 'function') {
                e.preventDefault();
            }
            if (mismatch) {
                mismatch.hidden = false;
                mismatch.style.display = '';
            }
            if (typeof confirmPwd.focus === 'function') {
                confirmPwd.focus();
            }
            return;
        }

        if (mismatch) {
            mismatch.hidden = true;
            mismatch.style.display = 'none';
        }

        if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
            return;
        }

        if (submitBtn && (submitBtn.disabled || form._inFlight)) {
            if (e && typeof e.preventDefault === 'function') {
                e.preventDefault();
            }
            return;
        }

        if (submitBtn) {
            setNativePending(e, form, submitBtn, 'Đang xử lý…');
        }
    }

    function initAuthEvents() {
        var root = document.querySelector('.account-auth-ui');
        if (!root || root.dataset.authInitialized === 'true') return;
        root.dataset.authInitialized = 'true';

        // Tabs
        var tabLog = $('tab-login');
        var tabReg = $('tab-register');
        if (tabLog) {
            tabLog.addEventListener('click', function () { switchTab('login'); });
        }
        if (tabReg) {
            tabReg.addEventListener('click', function () { switchTab('register'); });
        }

        [tabLog, tabReg].forEach(function (tab) {
            if (!tab) return;
            tab.addEventListener('keydown', function (e) {
                if (e.key === 'ArrowRight' || e.key === 'ArrowLeft' || e.key === 'Home' || e.key === 'End') {
                    e.preventDefault();
                    var next = (e.key === 'Home') ? 'login' :
                               (e.key === 'End') ? 'register' :
                               (tab.id === 'tab-login') ? 'register' : 'login';
                    switchTab(next);
                    var nextTab = $(next === 'login' ? 'tab-login' : 'tab-register');
                    if (nextTab && typeof nextTab.focus === 'function') {
                        nextTab.focus();
                    }
                }
            });
        });

        // Click delegate for toggles, tabs, and external links
        document.addEventListener('click', function (e) {
            var toggle = e.target.closest('[data-password]');
            if (toggle) {
                e.preventDefault();
                togglePassword(toggle.dataset.password, toggle);
                return;
            }

            var authTabBtn = e.target.closest('[data-auth-tab]');
            if (authTabBtn) {
                e.preventDefault();
                switchTab(authTabBtn.dataset.authTab);
                var targetTab = $(authTabBtn.dataset.authTab === 'register' ? 'tab-register' : 'tab-login');
                if (targetTab && typeof targetTab.focus === 'function') {
                    targetTab.focus();
                }
                return;
            }
        });

        // Form submits
        var formLog = $('form-login');
        if (formLog) {
            formLog.addEventListener('submit', handleLogin);
        }

        var formReg = $('form-register');
        if (formReg) {
            formReg.addEventListener('submit', handleRegister);
        }

        var formForgot = $('form-forgot-password');
        if (formForgot) {
            formForgot.addEventListener('submit', handleForgotSubmit);
        }

        var formReset = $('form-reset-password');
        if (formReset) {
            formReset.addEventListener('submit', handleResetSubmit);
        }

        // Loader recovery buttons
        var btnLoaderRetry = $('btn-loader-retry');
        if (btnLoaderRetry) {
            btnLoaderRetry.addEventListener('click', function () {
                if (activeNavDestination) {
                    window.location.href = activeNavDestination;
                }
            });
        }

        var btnLoaderCancel = $('btn-loader-cancel');
        if (btnLoaderCancel) {
            btnLoaderCancel.addEventListener('click', function () {
                hideLoaderOverlay();
                if (formLog) formLog._inFlight = false;
                setPending($('btn-login-submit'), false);
            });
        }

        // Restore pending and hide loader on pageshow (e.g., bfcache navigation)
        window.addEventListener('pageshow', function () {
            hideLoaderOverlay();
            if (formLog) formLog._inFlight = false;
            if (formReg) formReg._inFlight = false;
            if (formForgot) formForgot._inFlight = false;
            if (formReset) formReset._inFlight = false;
            setPending($('btn-login-submit'), false);
            setPending($('btn-register-submit'), false);
            setPending($('btn-forgot-submit'), false);
            setPending($('btn-reset-submit'), false);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAuthEvents);
    } else {
        initAuthEvents();
    }

    // Expose compatibility window aliases
    global.switchTab = switchTab;
    global.togglePassword = togglePassword;
    global.handleLogin = handleLogin;
    global.handleRegister = handleRegister;
    global.handleExternalLogin = handleExternalLogin;
    global.showLoginError = showLoginError;
    global.showLoaderAndRedirect = showLoaderAndRedirect;
    global.hideLoaderOverlay = hideLoaderOverlay;
    global.handleForgotSubmit = handleForgotSubmit;
    global.handleResetSubmit = handleResetSubmit;

})(typeof window !== 'undefined' ? window : globalThis);
