/*
 * "Gợi ý thêm cùng chủ đề" on an onboarding deck: asks the server for the next 10 words of the deck's topics.
 * Uses TctSets.fetchJson (antiforgery header) and TctSets.toast from personal-sets-common.js, and a request id
 * from onboarding-core.js so a retry of the same click never adds twice. Server text goes through textContent only.
 */
(function () {
    'use strict';

    var card = document.querySelector('[data-tob-refill-card]');
    var core = window.TctOnboardingCore;
    var sets = window.TctSets;
    if (!card || !core || !sets) { return; }

    var button = card.querySelector('[data-tob-refill]');
    var status = card.querySelector('[data-tob-refill-status]');
    var text = card.querySelector('[data-tob-refill-text]');
    var setId = Number(card.getAttribute('data-set-id'));
    var setName = card.getAttribute('data-set-name') || '';
    var requestId = null;
    if (!button || !setId) { return; }

    function say(message) { if (status) { status.textContent = message; } }

    function showExhausted() {
        if (text) { text.textContent = 'Bạn đã có tất cả từ của chủ đề này 🎉'; }
        button.hidden = true;
        var thumbs = card.querySelector('.ts-refill__thumbs');
        if (thumbs) { thumbs.hidden = true; }
    }

    button.addEventListener('click', function () {
        if (button.disabled) { return; }
        button.disabled = true;
        say('');
        if (!requestId) { requestId = core.newRequestId(window.crypto); }

        sets.fetchJson('/api/onboarding/refill/' + setId, { method: 'POST', body: { requestId: requestId, count: 10 } })
            .then(function (data) {
                requestId = null;
                var added = data && data.added ? data.added : 0;
                if (added > 0) {
                    sets.toast('Đã thêm ' + added + ' từ vào "' + setName + '".');
                    window.setTimeout(function () { window.location.reload(); }, 900);
                    return;
                }
                showExhausted();
            })
            .catch(function (error) {
                button.disabled = false;
                if (error && error.status === 409) { requestId = null; }
                say((error && error.message) || 'Có lỗi xảy ra. Hãy thử lại.');
            });
    });
})();
