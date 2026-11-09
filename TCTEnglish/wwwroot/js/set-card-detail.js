/* Card detail page: speech buttons and the per-viewer "suspend from review" toggle. */
(function () {
    'use strict';

    var T = window.TctSets;
    var configNode = document.getElementById('tsCardConfig');
    if (!T || !configNode) { return; }

    var config;
    try { config = JSON.parse(configNode.textContent); } catch (e) { return; }

    // Speech buttons are hidden until the browser proves it can speak.
    document.querySelectorAll('[data-ts-speak]').forEach(function (button) {
        if (!T.canSpeak()) { return; }
        button.hidden = false;
        button.addEventListener('click', function () { T.speak(button.getAttribute('data-ts-speak')); });
    });

    var toggle = document.getElementById('ts-suspend');
    if (!toggle) { return; }
    var label = document.getElementById('ts-suspend-label');
    var suspended = !!config.suspended;

    toggle.addEventListener('click', function () {
        var wanted = !suspended;
        toggle.disabled = true;
        toggle.setAttribute('aria-busy', 'true');
        T.fetchJson(config.suspendUrl, { method: 'POST', body: { suspended: wanted } }).then(function (result) {
            suspended = !!(result && result.suspended);
            toggle.setAttribute('aria-pressed', String(suspended));
            label.textContent = suspended ? 'Bỏ tạm ẩn' : 'Tạm ẩn khỏi ôn tập';
            T.toast(suspended ? 'Đã tạm ẩn ' + config.term : 'Đã bỏ tạm ẩn ' + config.term);
        }).catch(function (error) {
            T.toast(error.message, { type: 'danger' });
        }).then(function () {
            toggle.disabled = false;
            toggle.removeAttribute('aria-busy');
        });
    });
})();
