/**
 * Shared learner feedback: Clay toast cards + Bootstrap confirm modal.
 * window.TctFeedback.toast / .confirm — see docs/superpowers plan Class+Reading feedback.
 */
(function (global) {
    'use strict';

    var MAX_VISIBLE = 2;
    var COALESCE_MS = 1500;
    var DEFAULT_DELAY = {
        success: 3500,
        info: 3500,
        warning: 5000,
        danger: 6000
    };
    var ICONS = {
        success: 'fa-solid fa-circle-check',
        warning: 'fa-solid fa-triangle-exclamation',
        danger: 'fa-solid fa-circle-exclamation',
        info: 'fa-solid fa-circle-info'
    };

    var recentKey = '';
    var recentAt = 0;
    var confirmResolver = null;

    function normalizeType(type) {
        if (type === 'warning' || type === 'danger' || type === 'info') {
            return type;
        }
        return 'success';
    }

    function ensureHost() {
        var host = document.getElementById('tctFeedbackToastHost');
        if (host) {
            return host;
        }
        host = document.createElement('div');
        host.id = 'tctFeedbackToastHost';
        host.className = 'tct-feedback-toast-host toast-container';
        host.setAttribute('aria-live', 'polite');
        host.setAttribute('aria-atomic', 'false');
        document.body.appendChild(host);
        return host;
    }

    function trimStack(host) {
        var cards = host.querySelectorAll('.tct-feedback-toast');
        while (cards.length > MAX_VISIBLE) {
            var oldest = cards[0];
            if (oldest && oldest.parentNode) {
                oldest.parentNode.removeChild(oldest);
            }
            cards = host.querySelectorAll('.tct-feedback-toast');
        }
    }

    function toast(options) {
        options = options || {};
        var title = (options.title || '').trim();
        if (!title) {
            return { hide: function () { } };
        }

        var type = normalizeType(options.type);
        var message = (options.message || '').trim();
        var key = type + '|' + title + '|' + message;
        var now = Date.now();
        if (key === recentKey && now - recentAt < COALESCE_MS) {
            return { hide: function () { } };
        }
        recentKey = key;
        recentAt = now;

        var host = ensureHost();
        var el = document.createElement('div');
        el.className = 'tct-feedback-toast tct-feedback-toast--' + type;
        el.setAttribute('role', type === 'danger' || type === 'warning' ? 'alert' : 'status');
        el.setAttribute('aria-atomic', 'true');

        var iconWrap = document.createElement('span');
        iconWrap.className = 'tct-feedback-toast__icon';
        iconWrap.setAttribute('aria-hidden', 'true');
        var icon = document.createElement('i');
        icon.className = ICONS[type] || ICONS.info;
        iconWrap.appendChild(icon);
        el.appendChild(iconWrap);

        var body = document.createElement('div');
        body.className = 'tct-feedback-toast__body';
        var titleEl = document.createElement('p');
        titleEl.className = 'tct-feedback-toast__title';
        titleEl.textContent = title;
        body.appendChild(titleEl);
        if (message) {
            var msgEl = document.createElement('p');
            msgEl.className = 'tct-feedback-toast__message';
            msgEl.textContent = message;
            body.appendChild(msgEl);
        }

        var hasAction = options.actionLabel && typeof options.onAction === 'function';
        if (hasAction) {
            var actions = document.createElement('div');
            actions.className = 'tct-feedback-toast__actions';
            var actionBtn = document.createElement('button');
            actionBtn.type = 'button';
            actionBtn.className = 'tct-feedback-toast__action';
            actionBtn.textContent = options.actionLabel;
            actionBtn.addEventListener('click', function () {
                options.onAction();
                hide();
            });
            actions.appendChild(actionBtn);
            body.appendChild(actions);
        }
        el.appendChild(body);

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'tct-feedback-toast__close';
        close.setAttribute('aria-label', 'Đóng');
        close.innerHTML = '<i class="fa-solid fa-xmark" aria-hidden="true"></i>';
        el.appendChild(close);

        host.appendChild(el);
        trimStack(host);

        var timer = null;
        var delay = typeof options.delay === 'number'
            ? options.delay
            : (DEFAULT_DELAY[type] || DEFAULT_DELAY.success);
        var autohide = options.autohide !== false;

        function hide() {
            clearTimeout(timer);
            el.classList.remove('is-visible');
            window.setTimeout(function () {
                if (el.parentNode) {
                    el.parentNode.removeChild(el);
                }
            }, 260);
        }

        close.addEventListener('click', hide);

        requestAnimationFrame(function () {
            el.classList.add('is-visible');
        });

        if (autohide && delay > 0) {
            timer = window.setTimeout(hide, delay);
        }

        return { hide: hide };
    }

    function getConfirmEls() {
        return {
            modal: document.getElementById('tctFeedbackConfirm'),
            title: document.getElementById('tctFeedbackConfirmTitle'),
            message: document.getElementById('tctFeedbackConfirmMessage'),
            ok: document.getElementById('tctFeedbackConfirmOk'),
            cancel: document.getElementById('tctFeedbackConfirmCancel')
        };
    }

    function confirm(options) {
        options = options || {};
        var els = getConfirmEls();
        if (!els.modal || !window.bootstrap || !window.bootstrap.Modal) {
            var fallback = window.confirm(
                [options.title, options.message].filter(Boolean).join('\n')
            );
            return Promise.resolve(!!fallback);
        }

        return new Promise(function (resolve) {
            if (confirmResolver) {
                confirmResolver(false);
                confirmResolver = null;
            }
            confirmResolver = resolve;
            var settled = false;

            els.title.textContent = options.title || 'Xác nhận';
            els.message.textContent = options.message || '';
            els.ok.textContent = options.confirmLabel || 'Xác nhận';
            els.cancel.textContent = options.cancelLabel || 'Hủy';

            els.ok.classList.remove('btn-primary', 'btn-danger');
            els.ok.classList.add(options.danger ? 'btn-danger' : 'btn-primary');

            var instance = window.bootstrap.Modal.getOrCreateInstance(els.modal, {
                backdrop: true,
                keyboard: true,
                focus: true
            });

            function cleanup() {
                els.ok.removeEventListener('click', onOk);
                els.cancel.removeEventListener('click', onCancel);
                els.modal.removeEventListener('hidden.bs.modal', onHidden);
            }

            function finish(result) {
                if (settled) {
                    return;
                }
                settled = true;
                cleanup();
                if (confirmResolver === resolve) {
                    confirmResolver = null;
                }
                resolve(!!result);
            }

            function onOk() {
                finish(true);
                instance.hide();
            }

            function onCancel() {
                finish(false);
                instance.hide();
            }

            function onHidden() {
                finish(false);
            }

            els.ok.addEventListener('click', onOk);
            els.cancel.addEventListener('click', onCancel);
            els.modal.addEventListener('hidden.bs.modal', onHidden);
            instance.show();
        });
    }

    global.TctFeedback = {
        toast: toast,
        confirm: confirm
    };
})(window);
