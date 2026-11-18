/* Edit-card page: mounts the shared detail fields, AI suggest (never overwrites typed text) and a duplicate warning. */
(function () {
    'use strict';

    var T = window.TctSets;
    var core = T && T.composeCore;
    var configNode = document.getElementById('tsEditCardConfig');
    if (!T || !core || !T.cardForm || !configNode) { return; }

    var config;
    try { config = JSON.parse(configNode.textContent); } catch (e) { return; }

    var form = document.getElementById('ts-edit-card-form');
    var termInput = document.getElementById('ec-term');
    var meaningInput = document.getElementById('ec-definition');
    var details = T.cardForm.mount(form.querySelector('[data-ts-details]'), {
        named: true, isPremium: config.isPremium, urls: config.urls
    });
    details.setData(config.initial || {});

    var dupNote = document.getElementById('ec-dup');
    var enrichError = document.getElementById('ec-enrich-error');
    var lastDupTerm = termInput.value.trim();

    // A server-side validation error inside a hidden tab opens that tab.
    var firstError = form.querySelector('.is-invalid');
    if (firstError) { firstError.focus(); }

    function showDuplicates(hits) {
        dupNote.textContent = '';
        dupNote.hidden = !hits || hits.length === 0;
        if (dupNote.hidden) { return; }
        dupNote.appendChild(T.icon('fa-solid fa-triangle-exclamation me-1'));
        dupNote.appendChild(document.createTextNode('Đã có trong bộ '));
        hits.forEach(function (hit, i) {
            if (i > 0) { dupNote.appendChild(document.createTextNode(', ')); }
            var link = T.el('a', null, hit.setName);
            link.href = config.urls.cardBase + hit.cardId;
            dupNote.appendChild(link);
        });
    }

    termInput.addEventListener('blur', function () {
        var term = termInput.value.trim();
        if (!term || core.norm(term) === core.norm(lastDupTerm)) { return; }
        lastDupTerm = term;
        T.fetchJson(config.urls.duplicates + '?term=' + encodeURIComponent(term) + '&excludeCardId=' + config.cardId)
            .then(showDuplicates).catch(function () { /* advisory only */ });
    });

    var suggest = document.getElementById('ec-suggest');
    if (suggest) {
        suggest.addEventListener('click', function () {
            var term = termInput.value.trim();
            if (!term) { termInput.focus(); return; }
            enrichError.hidden = true;
            T.pending(suggest, true, 'Đang gợi ý…');
            var typedMeaning = meaningInput.value.trim();
            T.fetchJson(config.urls.enrich, {
                method: 'POST',
                body: { term: term, meaningHint: typedMeaning || null }
            }).then(function (result) {
                if (result.isValidWord === false) {
                    enrichError.textContent = result.didYouMean ? 'Có phải bạn muốn gõ “' + result.didYouMean + '”?' : 'Không nhận ra từ này.';
                    enrichError.hidden = false;
                    return;
                }
                var current = details.getData();
                var merged = core.applyEnrich({
                    term: term, definition: typedMeaning, meaningTyped: typedMeaning !== '', readingTyped: !!(current.phonetic && current.phonetic.trim()), d: current
                }, result, [0]);
                if (!typedMeaning) { meaningInput.value = merged.definition || ''; }
                merged.d.aiGenerated = true;
                details.setData(merged.d);
                details.markClean();
                T.toast('Đã điền gợi ý. Kiểm tra lại trước khi lưu.');
            }).catch(function (error) {
                enrichError.textContent = error.message;
                enrichError.hidden = false;
            }).then(function () { T.pending(suggest, false); });
        });
    }

    form.addEventListener('submit', function (event) {
        if (!form.checkValidity()) {
            event.preventDefault();
            details.focusFirstInvalid();
            if (!form.querySelector(':invalid')) { return; }
            var invalid = form.querySelector(':invalid');
            if (invalid) { invalid.focus(); }
        }
    });
})();
