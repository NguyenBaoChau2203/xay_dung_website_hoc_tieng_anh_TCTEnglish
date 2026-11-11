/* Shared helpers for the personal set workspace (window.TctSets). Vanilla JS, no dependencies. */
(function () {
    'use strict';

    var TctSets = window.TctSets = window.TctSets || {};

    TctSets.posLabels = {
        noun: 'Danh từ', verb: 'Động từ', adj: 'Tính từ', adv: 'Trạng từ', pron: 'Đại từ', prep: 'Giới từ',
        conj: 'Liên từ', interj: 'Thán từ', det: 'Hạn định từ', phrasal_verb: 'Cụm động từ', idiom: 'Thành ngữ',
        phrase: 'Cụm từ', num: 'Số từ'
    };

    TctSets.topics = [
        'Gia đình', 'Nhà cửa', 'Ăn uống', 'Mua sắm', 'Du lịch', 'Giao thông', 'Công việc', 'Kinh doanh',
        'Học tập', 'Công nghệ', 'Sức khỏe', 'Thể thao', 'Thiên nhiên', 'Thời tiết', 'Động vật', 'Cảm xúc',
        'Tính cách', 'Ngoại hình', 'Thời gian', 'Xã hội', 'Văn hóa – Giải trí', 'Giao tiếp hằng ngày', 'Khác'
    ];

    function antiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    /**
     * JSON request with the antiforgery header. Resolves with the parsed body (or null);
     * rejects with an Error carrying status, data and a user-facing message.
     */
    TctSets.fetchJson = function (url, options) {
        options = options || {};
        var headers = { Accept: 'application/json', RequestVerificationToken: antiForgeryToken() };
        var init = { method: options.method || 'GET', headers: headers, credentials: 'same-origin', signal: options.signal };
        if (options.body !== undefined && !(options.body instanceof FormData)) {
            headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(options.body);
        } else if (options.body instanceof FormData) {
            init.body = options.body;
        }

        return fetch(url, init).then(function (response) {
            return response.text().then(function (text) {
                var data = null;
                if (text) {
                    try { data = JSON.parse(text); } catch (e) { data = null; }
                }
                if (!response.ok) {
                    var message = (data && data.error) || (response.status === 404
                        ? 'Không tìm thấy dữ liệu.'
                        : response.status === 403 ? 'Bạn không có quyền dùng tính năng này.' : 'Có lỗi xảy ra, thử lại sau.');
                    var error = new Error(message);
                    error.status = response.status;
                    error.data = data;
                    throw error;
                }
                return data;
            });
        });
    };

    TctSets.debounce = function (fn, wait) {
        var timer = null;
        return function () {
            var args = arguments, self = this;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(self, args); }, wait);
        };
    };

    TctSets.speak = function (text) {
        if (!('speechSynthesis' in window) || !text) { return; }
        try {
            window.speechSynthesis.cancel();
            var utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = 'en-US';
            window.speechSynthesis.speak(utterance);
        } catch (e) { /* speech is best-effort */ }
    };

    TctSets.canSpeak = function () { return 'speechSynthesis' in window; };

    /** disabled + aria-busy + spinner; restores the original content when turned off. */
    TctSets.pending = function (button, on, label) {
        if (!button) { return; }
        if (on) {
            if (button.getAttribute('aria-busy') === 'true') { return; }
            button.dataset.tsOriginal = button.innerHTML;
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
            button.textContent = '';
            var spinner = document.createElement('span');
            spinner.className = 'spinner-border spinner-border-sm me-2';
            spinner.setAttribute('role', 'presentation');
            button.appendChild(spinner);
            button.appendChild(document.createTextNode(label || button.getAttribute('data-ts-pending') || 'Đang xử lý…'));
        } else {
            if (button.dataset.tsOriginal !== undefined) {
                button.innerHTML = button.dataset.tsOriginal;
                delete button.dataset.tsOriginal;
            }
            button.disabled = false;
            button.removeAttribute('aria-busy');
        }
    };

    /**
     * Bootstrap toast in #tsToastHost. options: { type: 'success'|'danger', actionLabel, onAction, delay }.
     * Falls back to a polite live region when Bootstrap's Toast is unavailable.
     */
    TctSets.toast = function (message, options) {
        options = options || {};
        var host = document.getElementById('tsToastHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'tsToastHost';
            host.className = 'toast-container ts-toast-host';
            host.setAttribute('aria-live', 'polite');
            document.body.appendChild(host);
        }

        var type = options.type === 'danger' ? 'danger' : 'success';
        var el = document.createElement('div');
        el.className = 'toast ts-toast ts-toast--' + type;
        el.setAttribute('role', type === 'danger' ? 'alert' : 'status');
        el.setAttribute('aria-atomic', 'true');

        var row = document.createElement('div');
        row.className = 'd-flex align-items-center';
        var body = document.createElement('div');
        body.className = 'toast-body';
        body.textContent = message;
        row.appendChild(body);

        if (options.actionLabel && typeof options.onAction === 'function') {
            var action = document.createElement('button');
            action.type = 'button';
            action.className = 'btn btn-sm btn-link ts-toast-action';
            action.textContent = options.actionLabel;
            action.addEventListener('click', function () {
                options.onAction();
                hide();
            });
            row.appendChild(action);
        }

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close me-2 m-auto';
        close.setAttribute('aria-label', 'Đóng');
        row.appendChild(close);
        el.appendChild(row);
        host.appendChild(el);

        var instance = null;
        var timer = null;
        function hide() {
            if (instance) { instance.hide(); } else { el.remove(); }
            clearTimeout(timer);
        }
        close.addEventListener('click', hide);

        if (window.bootstrap && window.bootstrap.Toast) {
            instance = new window.bootstrap.Toast(el, { delay: options.delay || 4000, autohide: true });
            el.addEventListener('hidden.bs.toast', function () { el.remove(); });
            instance.show();
        } else {
            el.classList.add('show');
            timer = setTimeout(hide, options.delay || 4000);
        }
        return { hide: hide };
    };

    TctSets.safeStorage = {
        get: function (key) { try { return window.localStorage.getItem(key); } catch (e) { return null; } },
        set: function (key, value) { try { window.localStorage.setItem(key, value); return true; } catch (e) { return false; } },
        remove: function (key) { try { window.localStorage.removeItem(key); } catch (e) { /* ignore */ } }
    };

    /** Creates an element with optional class and text (never innerHTML with user data). */
    TctSets.el = function (tag, className, text) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        return node;
    };

    TctSets.icon = function (classes) {
        var i = document.createElement('i');
        i.className = classes;
        i.setAttribute('aria-hidden', 'true');
        return i;
    };

    function bindGenericBehaviors() {
        // Pending state for submit buttons that opt in with data-ts-pending.
        document.addEventListener('submit', function (event) {
            var form = event.target;
            if (!(form instanceof HTMLFormElement) || event.defaultPrevented) { return; }
            var button = event.submitter && event.submitter.hasAttribute('data-ts-pending')
                ? event.submitter
                : form.querySelector('button[type="submit"][data-ts-pending]');
            if (button) {
                setTimeout(function () { TctSets.pending(button, true); }, 0);
            }
        });

        // Type-the-name confirmation for destructive forms.
        document.querySelectorAll('[data-ts-confirm-form]').forEach(function (form) {
            var input = form.querySelector('[data-ts-confirm-input]');
            var submit = form.querySelector('[data-ts-confirm-submit]');
            if (!input || !submit) { return; }
            var expected = (input.getAttribute('data-ts-confirm-value') || '').trim();
            function sync() { submit.disabled = expected.length === 0 || input.value.trim() !== expected; }
            input.addEventListener('input', sync);
            sync();
        });

        // Live cover preview on the create/settings form.
        var source = document.querySelector('[data-ts-preview-source]');
        var target = document.querySelector('[data-ts-preview-target]');
        if (source && target) {
            source.addEventListener('input', function () {
                var value = source.value.trim();
                target.textContent = value ? Array.from(value)[0].toUpperCase() : '?';
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindGenericBehaviors);
    } else {
        bindGenericBehaviors();
    }
})();
