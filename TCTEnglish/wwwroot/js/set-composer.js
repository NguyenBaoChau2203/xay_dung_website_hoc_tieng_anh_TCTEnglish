/* Add-words composer. The pure logic (TctSets.composeCore) is exported for Node tests; the DOM part runs on the add-words page. */
(function (root, factory) {
    'use strict';
    var core = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = core;
    } else {
        root.TctSets = root.TctSets || {};
        root.TctSets.composeCore = core;
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var POS_LABELS = {
        noun: 'Danh từ', verb: 'Động từ', adj: 'Tính từ', adv: 'Trạng từ', pron: 'Đại từ', prep: 'Giới từ', conj: 'Liên từ',
        interj: 'Thán từ', det: 'Hạn định từ', phrasal_verb: 'Cụm động từ', idiom: 'Thành ngữ', phrase: 'Cụm từ', num: 'Số từ'
    };

    function norm(value) { return String(value || '').trim().toLowerCase(); }

    function saveLabel(count) { return count > 1 ? 'Lưu ' + count + ' từ' : 'Lưu từ'; }

    function isBlank(card) {
        return !String(card.term || '').trim() && !String(card.definition || '').trim() && !(card.d && card.d.imageUrl);
    }

    function isReady(card) { return !!(String(card.term || '').trim() && String(card.definition || '').trim()); }

    /** Returns [{index, firstIndex}] for every card whose term repeats an earlier card (trim + lower-case). */
    function findInnerDuplicates(cards) {
        var first = {};
        var out = [];
        cards.forEach(function (card, index) {
            var key = norm(card.term);
            if (!key) { return; }
            if (Object.prototype.hasOwnProperty.call(first, key)) {
                out.push({ index: index, firstIndex: first[key] });
            } else {
                first[key] = index;
            }
        });
        return out;
    }

    function uniquePush(list, value, limit) {
        if (value && list.indexOf(value) === -1 && list.length < limit) { list.push(value); }
    }

    /**
     * Applies an enrich result to a card without overwriting what the learner typed:
     * a typed meaning (card.meaningTyped) and a typed IPA (card.readingTyped) always win.
     * selected: indexes of the chosen senses (default: the first).
     */
    function applyEnrich(card, result, selected) {
        var next = JSON.parse(JSON.stringify(card));
        next.d = next.d || {};
        var senses = (result && result.senses) || [];
        var chosen = (selected && selected.length ? selected : [0])
            .filter(function (i) { return senses[i]; })
            .map(function (i) { return senses[i]; });
        if (chosen.length === 0 && senses.length) { chosen = [senses[0]]; }

        if (!next.meaningTyped || !String(next.definition || '').trim()) {
            var meanings = chosen.map(function (s) { return s.meaningVi; }).filter(Boolean);
            if (meanings.length) { next.definition = meanings.join('; '); next.aiMeaning = true; }
        }

        if (!next.readingTyped || !String(next.d.phonetic || '').trim()) {
            if (result.ipaUs) { next.d.phonetic = result.ipaUs; }
        }

        if (result.ipaUk) { next.d.phoneticUk = result.ipaUk; }
        if (chosen[0] && chosen[0].meaningEn) { next.d.meaningEn = chosen[0].meaningEn; }

        var pos = [];
        chosen.forEach(function (s) { (s.pos || []).forEach(function (p) { uniquePush(pos, p, 6); }); });
        if (pos.length) { next.d.partsOfSpeech = pos; }

        var examples = [];
        chosen.forEach(function (s) {
            (s.examples || []).forEach(function (e) {
                if (e && e.text && examples.length < 6) { examples.push({ text: e.text, vi: e.vi || '' }); }
            });
        });
        if (examples.length) { next.d.examples = examples; }

        if (result.synonyms && result.synonyms.length) { next.d.synonyms = result.synonyms.slice(0, 10); }
        if (result.antonyms && result.antonyms.length) { next.d.antonyms = result.antonyms.slice(0, 10); }
        if (result.collocations && result.collocations.length) { next.d.collocations = result.collocations.slice(0, 8); }
        if (result.wordFamily && result.wordFamily.length) { next.d.wordFamily = result.wordFamily.slice(0, 8); }
        if (result.verbForms) { next.d.verbForms = result.verbForms; }
        if (result.plural) { next.d.plural = result.plural; }
        if (result.cefr) { next.d.cefr = result.cefr; }
        if (result.topicVi) { next.d.topic = result.topicVi; }
        if (result.emoji) { next.d.emoji = result.emoji; }
        if (result.imageQuery) { next.imageQuery = result.imageQuery; }
        next.d.aiGenerated = true;
        next.aiGenerated = true;
        return next;
    }

    /**
     * Merges one save-batch response into the card list. Inserted cards are removed; skipped/invalid cards stay
     * and carry saveError. Cards are matched by trimmed, lower-cased term.
     */
    function mergeBatchResults(cards, response) {
        var inserted = (response && response.inserted) || [];
        var skipped = ((response && response.skipped) || []).map(norm);
        var invalid = ((response && response.invalid) || []).map(norm);
        var insertedByTerm = {};
        inserted.forEach(function (item) {
            var key = norm(item.term);
            (insertedByTerm[key] = insertedByTerm[key] || []).push(item);
        });

        var remaining = [];
        var insertedItems = [];
        cards.forEach(function (card) {
            var key = norm(card.term);
            var hit = insertedByTerm[key] && insertedByTerm[key].shift();
            if (hit) { insertedItems.push(hit); return; }

            var copy = JSON.parse(JSON.stringify(card));
            var skipAt = skipped.indexOf(key);
            var invalidAt = invalid.indexOf(key);
            if (skipAt !== -1) { skipped.splice(skipAt, 1); copy.saveError = 'Đã có trong bộ này'; }
            else if (invalidAt !== -1) { invalid.splice(invalidAt, 1); copy.saveError = 'Dữ liệu chưa hợp lệ'; }
            remaining.push(copy);
        });
        return { remaining: remaining, insertedItems: insertedItems };
    }

    /** One-line summary under a card, e.g. "Đã điền bằng AI · 3 ví dụ · Tính từ · Cảm xúc · có ảnh". */
    function detailSummary(card) {
        var d = card.d || {};
        var parts = [];
        if (card.aiGenerated || d.aiGenerated) { parts.push('Đã điền bằng AI'); }
        if (d.examples && d.examples.length) { parts.push(d.examples.length + ' ví dụ'); }
        if (d.partsOfSpeech && d.partsOfSpeech.length) { parts.push(POS_LABELS[d.partsOfSpeech[0]] || d.partsOfSpeech[0]); }
        if (d.topic) { parts.push(d.topic); }
        if (d.imageUrl) { parts.push('có ảnh'); }
        return parts.join(' · ');
    }

    function saveStatus(cards, enrichProgress) {
        if (enrichProgress && enrichProgress.total > 0 && enrichProgress.done < enrichProgress.total) {
            return 'Đang điền ' + enrichProgress.done + '/' + enrichProgress.total + '…';
        }
        var filled = cards.filter(function (c) { return !isBlank(c); });
        var ready = filled.filter(isReady).length;
        var missing = filled.length - ready;
        var text = ready + ' thẻ sẵn sàng';
        if (missing > 0) { text += ' · ' + missing + ' thẻ thiếu nghĩa'; }
        return text;
    }

    function toInputDto(card) {
        var d = card.d || {};
        var verb = d.verbForms || {};
        var hasVerb = verb.past || verb.pastParticiple || verb.ing || verb.thirdPerson;
        return {
            term: String(card.term || '').trim(),
            definition: String(card.definition || '').trim(),
            phonetic: d.phonetic || null,
            phoneticUk: d.phoneticUk || null,
            meaningEn: d.meaningEn || null,
            partsOfSpeech: d.partsOfSpeech || [],
            topic: d.topic || null,
            emoji: d.emoji || null,
            cefr: d.cefr || null,
            personalNote: d.personalNote || null,
            imageUrl: d.imageUrl || null,
            imageSource: d.imageUrl && d.imageSource ? d.imageSource : null,
            examples: (d.examples || []).filter(function (e) { return e.text && e.text.trim(); }),
            synonyms: d.synonyms || [],
            antonyms: d.antonyms || [],
            collocations: (d.collocations || []).filter(function (c) { return c.text && c.text.trim(); }),
            wordFamily: (d.wordFamily || []).filter(function (w) { return w.text && w.text.trim(); }),
            verbForms: hasVerb ? verb : null,
            plural: d.plural || null,
            aiGenerated: !!(card.aiGenerated || d.aiGenerated)
        };
    }

    function chunk(list, size) {
        var out = [];
        for (var i = 0; i < list.length; i += size) { out.push(list.slice(i, i + size)); }
        return out;
    }

    return {
        saveLabel: saveLabel, isBlank: isBlank, isReady: isReady, findInnerDuplicates: findInnerDuplicates,
        applyEnrich: applyEnrich, mergeBatchResults: mergeBatchResults, detailSummary: detailSummary,
        saveStatus: saveStatus, toInputDto: toInputDto, chunk: chunk, norm: norm
    };
});

/* ---------------------------------------------------------------------------------------------- DOM part */
(function () {
    'use strict';

    if (typeof document === 'undefined') { return; }
    var T = window.TctSets;
    var core = T && T.composeCore;
    var configNode = document.getElementById('tsComposeConfig');
    if (!T || !core || !configNode || !T.cardForm) { return; }

    var config;
    try { config = JSON.parse(configNode.textContent); } catch (e) { return; }

    var MAX = config.maxCards || 100;
    var uid = 0;
    var list = document.getElementById('ts-compose-list');
    var template = document.getElementById('tsComposeCardTpl');
    var addButton = document.getElementById('ts-add-card');
    var saveButton = document.getElementById('ts-save');
    var saveStatus = document.getElementById('ts-save-status');
    var targetSelect = document.getElementById('ts-target');
    var draftBar = document.getElementById('ts-draft-bar');

    var state = {
        cards: [], nodes: new Map(), targetId: config.setId, saving: false,
        enrich: { done: 0, total: 0 }, drawerCard: null, undoTimer: null
    };

    // ----------------------------------------------------------------- cards

    function newCard(partial) {
        var card = {
            uid: ++uid, term: '', definition: '', d: T.cardForm.emptyData(), meaningTyped: false, readingTyped: false,
            aiMeaning: false, aiGenerated: false, senses: null, selected: [0], didYouMean: null, enrichState: 'idle',
            enrichError: null, dupOther: [], saveError: null, touchedMeaning: false, lastEnrichedTerm: '', lastDupTerm: '',
            imageQuery: null, fromBatch: false, enrichTimer: null
        };
        return Object.assign(card, partial || {});
    }

    function addCard(partial, atIndex, focus) {
        if (state.cards.length >= MAX) { T.toast('Mỗi lần tối đa ' + MAX + ' từ.', { type: 'danger' }); return null; }
        var card = newCard(partial);
        var index = atIndex === undefined ? state.cards.length : atIndex;
        state.cards.splice(index, 0, card);
        var node = createNode(card);
        var reference = list.children[index] || null;
        list.insertBefore(node.root, reference);
        state.nodes.set(card.uid, node);
        renumber();
        refreshAll();
        if (focus) { node.refs.term.focus(); }
        return card;
    }

    function removeCard(card) {
        var index = state.cards.indexOf(card);
        if (index === -1) { return; }
        var snapshot = JSON.parse(JSON.stringify(Object.assign({}, card, { enrichTimer: null })));
        state.cards.splice(index, 1);
        var node = state.nodes.get(card.uid);
        if (node) { node.root.remove(); state.nodes.delete(card.uid); }
        if (state.cards.length === 0) { addCard(null, 0, false); }
        renumber();
        refreshAll();
        scheduleDraft();
        T.toast('Đã xóa thẻ', {
            actionLabel: 'Hoàn tác', delay: 6000,
            onAction: function () { addCard(snapshot, Math.min(index, state.cards.length), true); scheduleDraft(); }
        });
    }

    function createNode(card) {
        var fragment = template.content.cloneNode(true);
        var rootEl = fragment.querySelector('.ts-compose-item');
        var refs = {};
        rootEl.querySelectorAll('[data-ref]').forEach(function (el) { refs[el.getAttribute('data-ref')] = el; });
        var node = { root: rootEl, refs: refs };

        refs.term.addEventListener('input', function () {
            card.term = refs.term.value;
            card.saveError = null;
            scheduleAutoEnrich(card);
            refreshAll();
            scheduleDraft();
        });
        refs.term.addEventListener('blur', function () { checkServerDuplicates(card); });
        refs.term.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.ctrlKey && !event.metaKey) { event.preventDefault(); refs.meaning.focus(); }
        });
        refs.term.addEventListener('paste', function (event) {
            var text = (event.clipboardData || window.clipboardData).getData('text');
            if (text && /\r|\n/.test(text.trim())) {
                event.preventDefault();
                importText(text, card);
            }
        });

        refs.ipa.addEventListener('input', function () { card.d.phonetic = refs.ipa.value; card.readingTyped = refs.ipa.value.trim() !== ''; scheduleDraft(); });

        refs.meaning.addEventListener('input', function () {
            card.definition = refs.meaning.value;
            card.meaningTyped = true;
            card.aiMeaning = false;
            card.touchedMeaning = true;
            refreshAll();
            scheduleDraft();
        });
        refs.meaning.addEventListener('blur', function () { card.touchedMeaning = true; refreshCard(card); });
        refs.meaning.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.shiftKey && !event.ctrlKey && !event.metaKey) {
                var isLast = state.cards[state.cards.length - 1] === card;
                if (isLast && card.term.trim()) {
                    event.preventDefault();
                    addCard(null, undefined, true);
                }
            } else if (event.key === 'Tab' && !event.shiftKey) {
                var last = state.cards[state.cards.length - 1] === card;
                if (last && card.term.trim() && state.cards.length < MAX) {
                    event.preventDefault();
                    addCard(null, undefined, true);
                }
            }
        });

        refs.suggestBtn.addEventListener('click', function () {
            if (!config.isPremium) { openPremiumModal(); return; }
            enrichCard(card);
        });
        refs.removeBtn.addEventListener('click', function () { removeCard(card); });
        refs.detailBtn.addEventListener('click', function () { openDrawer(card, 'examples'); });
        refs.imageBtn.addEventListener('click', function () { openDrawer(card, 'image'); });
        refs.imageTile.addEventListener('click', function () { openDrawer(card, 'image'); });

        return node;
    }

    function openPremiumModal() {
        var el = document.getElementById('premiumLockedModal');
        if (el && window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(el).show(); }
    }

    function renumber() {
        state.cards.forEach(function (card, index) {
            var node = state.nodes.get(card.uid);
            if (!node) { return; }
            var n = index + 1;
            node.refs.num.textContent = String(n);
            node.refs.term.setAttribute('aria-label', 'Từ ' + n);
            node.refs.ipa.setAttribute('aria-label', 'Phiên âm ' + n);
            node.refs.meaning.setAttribute('aria-label', 'Nghĩa tiếng Việt ' + n);
            node.refs.imageBtn.setAttribute('aria-label', 'Ảnh cho thẻ ' + n);
            node.refs.detailBtn.setAttribute('aria-label', 'Chi tiết thẻ ' + n);
            node.refs.removeBtn.setAttribute('aria-label', 'Xóa thẻ ' + n);
            node.refs.removeBtn.hidden = state.cards.length <= 1;
        });
    }

    // ----------------------------------------------------------------- per-card UI

    function refreshCard(card) {
        var node = state.nodes.get(card.uid);
        if (!node) { return; }
        var r = node.refs;
        if (r.term.value !== card.term) { r.term.value = card.term; }
        if (r.meaning.value !== card.definition) { r.meaning.value = card.definition; }
        if (r.ipa.value !== (card.d.phonetic || '')) { r.ipa.value = card.d.phonetic || ''; }

        var ready = core.isReady(card);
        r.valid.hidden = !ready;
        node.root.querySelector('.ts-compose-card').classList.toggle('ts-compose-card--valid', ready);
        node.root.querySelector('.ts-compose-card').classList.toggle('ts-compose-card--error', !!card.saveError);

        r.spinner.hidden = card.enrichState !== 'loading';
        r.suggestBtn.disabled = card.enrichState === 'loading';
        r.suggestBtn.querySelector('i').className = (config.isPremium ? 'fa-solid fa-lightbulb' : 'fa-solid fa-crown') + ' me-1';

        var showMeaningError = card.touchedMeaning && card.term.trim() && !card.definition.trim();
        r.meaningError.hidden = !showMeaningError;

        r.enrichError.hidden = !card.enrichError;
        r.enrichError.textContent = card.enrichError || '';
        r.saveError.hidden = !card.saveError;
        r.saveError.textContent = card.saveError || '';

        // Senses (chips) when the model returned more than one.
        r.senses.textContent = '';
        if (card.senses && card.senses.length > 1) {
            r.senses.hidden = false;
            card.senses.forEach(function (sense, i) {
                var label = T.el('label', 'ts-sense');
                var box = document.createElement('input');
                box.type = 'checkbox';
                box.className = 'form-check-input';
                box.checked = card.selected.indexOf(i) !== -1;
                box.addEventListener('change', function () { toggleSense(card, i, box.checked); });
                label.appendChild(box);
                var pos = (sense.pos || []).map(function (p) { return T.posLabels[p] || p; }).join(', ');
                label.appendChild(T.el('span', null, (pos ? pos + ' · ' : '') + sense.meaningVi));
                r.senses.appendChild(label);
            });
            var all = T.el('button', 'btn btn-link btn-sm', 'Chọn tất cả');
            all.type = 'button';
            all.addEventListener('click', function () { card.selected = card.senses.map(function (_, i) { return i; }); reapplyEnrich(card); });
            r.senses.appendChild(all);
        } else {
            r.senses.hidden = true;
        }

        r.didYouMean.hidden = !card.didYouMean;
        r.didYouMean.textContent = '';
        if (card.didYouMean) {
            r.didYouMean.appendChild(document.createTextNode('Có phải bạn muốn gõ '));
            r.didYouMean.appendChild(T.el('strong', null, card.didYouMean));
            r.didYouMean.appendChild(document.createTextNode('? '));
            var apply = T.el('button', 'btn btn-link btn-sm p-0', 'Áp dụng');
            apply.type = 'button';
            apply.addEventListener('click', function () { card.term = card.didYouMean; card.didYouMean = null; refreshAll(); enrichCard(card); });
            r.didYouMean.appendChild(apply);
        }

        // Other-set duplicates (server) -> links.
        r.otherDup.textContent = '';
        r.otherDup.hidden = card.dupOther.length === 0;
        if (card.dupOther.length) {
            r.otherDup.appendChild(T.icon('fa-solid fa-triangle-exclamation me-1'));
            var here = card.dupOther.filter(function (h) { return h.setId === state.targetId; });
            r.otherDup.appendChild(document.createTextNode(here.length ? 'Đã có trong bộ này: ' : 'Đã có trong bộ '));
            card.dupOther.forEach(function (hit, i) {
                if (i > 0) { r.otherDup.appendChild(document.createTextNode(', ')); }
                var link = T.el('a', null, hit.setName);
                link.href = config.urls.cardBase + hit.cardId;
                r.otherDup.appendChild(link);
            });
        }

        var summary = core.detailSummary(card);
        r.summary.textContent = summary;
        r.summary.hidden = !summary;

        var tile = r.imageTile;
        tile.textContent = '';
        if (card.d.imageUrl) {
            var img = document.createElement('img');
            img.src = card.d.imageUrl; img.alt = '';
            tile.appendChild(img);
            r.imageBtn.querySelector('i').className = 'fa-solid fa-image';
        } else {
            tile.appendChild(T.el('span', null, 'Thêm ảnh'));
        }
        tile.setAttribute('aria-label', 'Ảnh cho thẻ ' + (state.cards.indexOf(card) + 1));
    }

    function refreshAll() {
        var dups = core.findInnerDuplicates(state.cards);
        var byIndex = {};
        dups.forEach(function (d) { byIndex[d.index] = d.firstIndex; });
        state.cards.forEach(function (card, index) {
            var node = state.nodes.get(card.uid);
            if (!node) { return; }
            var el = node.refs.innerDup;
            if (Object.prototype.hasOwnProperty.call(byIndex, index)) {
                el.hidden = false;
                el.querySelector('span').textContent = 'Trùng với thẻ số ' + (byIndex[index] + 1);
            } else {
                el.hidden = true;
            }
            refreshCard(card);
        });
        updateSaveBar();
        addButton.disabled = state.cards.length >= MAX;
        addButton.querySelector('span').textContent = state.cards.length >= MAX ? 'Tối đa ' + MAX + ' thẻ mỗi lần' : 'Thêm thẻ';
    }

    function updateSaveBar() {
        var filled = state.cards.filter(function (c) { return !core.isBlank(c); }).length;
        saveStatus.textContent = core.saveStatus(state.cards, state.enrich);
        saveButton.textContent = core.saveLabel(filled);
        saveButton.disabled = state.saving || filled === 0;
    }

    addButton.addEventListener('click', function () { addCard(null, undefined, true); });

    // ----------------------------------------------------------------- AI enrich

    function scheduleAutoEnrich(card) {
        clearTimeout(card.enrichTimer);
        if (!config.isPremium || card.fromBatch) { return; }
        var term = card.term.trim();
        if (term.length < 2 || core.norm(term) === core.norm(card.lastEnrichedTerm)) { return; }
        card.enrichTimer = setTimeout(function () { enrichCard(card); }, 900);
    }

    function enrichCard(card) {
        var term = card.term.trim();
        if (term.length < 1 || card.enrichState === 'loading') { return; }
        clearTimeout(card.enrichTimer);
        card.enrichState = 'loading';
        card.enrichError = null;
        card.lastEnrichedTerm = term;
        refreshCard(card);

        var body = { term: term };
        if (card.meaningTyped && card.definition.trim()) { body.meaningHint = card.definition.trim(); }
        T.fetchJson(config.urls.enrich, { method: 'POST', body: body }).then(function (result) {
            if (core.norm(card.term) !== core.norm(term)) { card.enrichState = 'idle'; refreshCard(card); return; } // term changed meanwhile
            card.enrichState = 'idle';
            if (result.isValidWord === false) {
                card.didYouMean = result.didYouMean || null;
                card.enrichError = card.didYouMean ? null : 'Không nhận ra từ này. Kiểm tra lại chính tả hoặc tự nhập nghĩa.';
            } else {
                card.didYouMean = null;
                card.senses = result.senses || [];
                card.selected = [0];
                card.lastResult = result;
                Object.assign(card, core.applyEnrich(card, result, card.selected), { enrichTimer: null });
            }
            refreshAll();
            scheduleDraft();
        }).catch(function (error) {
            card.enrichState = 'error';
            card.enrichError = error.message;
            refreshCard(card);
            if (error.status === 403 && error.data && error.data.premiumRequired) { openPremiumModal(); }
        });
    }

    function toggleSense(card, index, on) {
        var at = card.selected.indexOf(index);
        if (on && at === -1) { card.selected.push(index); } else if (!on && at !== -1) { card.selected.splice(at, 1); }
        if (card.selected.length === 0) { card.selected = [index]; }
        reapplyEnrich(card);
    }

    function reapplyEnrich(card) {
        if (!card.lastResult) { return; }
        card.meaningTyped = card.aiMeaning ? false : card.meaningTyped;
        Object.assign(card, core.applyEnrich(card, card.lastResult, card.selected.slice().sort()), { enrichTimer: null });
        refreshAll();
        scheduleDraft();
    }

    // ----------------------------------------------------------------- server duplicate check

    function checkServerDuplicates(card) {
        var term = card.term.trim();
        if (!term || core.norm(term) === core.norm(card.lastDupTerm)) { return; }
        card.lastDupTerm = term;
        T.fetchJson(config.urls.duplicates + '?term=' + encodeURIComponent(term)).then(function (hits) {
            if (core.norm(card.term) !== core.norm(term)) { return; }
            card.dupOther = hits || [];
            refreshCard(card);
        }).catch(function () { /* advisory only */ });
    }

    // ----------------------------------------------------------------- paste list

    var pasteText = document.getElementById('ts-paste-text');
    var pasteSplit = document.getElementById('ts-paste-split');
    var pasteErrors = document.getElementById('ts-paste-errors');

    pasteText.addEventListener('input', function () { pasteSplit.disabled = pasteText.value.trim() === ''; });
    pasteSplit.addEventListener('click', function () {
        if (importText(pasteText.value, null, true)) { pasteText.value = ''; pasteSplit.disabled = true; setMode('single'); }
    });

    function showLineErrors(lines) {
        pasteErrors.textContent = '';
        lines.filter(function (l) { return l.status !== 'ok'; }).forEach(function (line) {
            var item = T.el('li', 'text-danger');
            item.appendChild(T.icon('fa-solid fa-circle-exclamation me-1'));
            item.appendChild(document.createTextNode('Dòng ' + line.lineNo + ': ' + line.message + ' (' + line.raw + ')'));
            pasteErrors.appendChild(item);
        });
    }

    /** Splits pasted text into cards. replaceCard (optional) is replaced when empty, otherwise cards go after it. */
    function importText(text, replaceCard, fromPasteBox) {
        var result = T.parseImport(text, []);
        if (fromPasteBox) { showLineErrors(result.lines); }
        var usable = result.lines.filter(function (l) { return l.status === 'ok'; });
        if (usable.length === 0) {
            if (!fromPasteBox) { T.toast('Không tách được từ nào từ nội dung đã dán.', { type: 'danger' }); }
            return false;
        }

        var startIndex = state.cards.length;
        if (replaceCard) {
            var at = state.cards.indexOf(replaceCard);
            startIndex = at + 1;
            if (core.isBlank(replaceCard)) {
                state.nodes.get(replaceCard.uid).root.remove();
                state.nodes.delete(replaceCard.uid);
                state.cards.splice(at, 1);
                startIndex = at;
            }
        } else if (state.cards.length === 1 && core.isBlank(state.cards[0])) {
            state.nodes.get(state.cards[0].uid).root.remove();
            state.nodes.delete(state.cards[0].uid);
            state.cards.splice(0, 1);
            startIndex = 0;
        }

        var room = MAX - state.cards.length;
        if (usable.length > room) { T.toast('Mỗi lần tối đa ' + MAX + ' từ.', { type: 'danger' }); usable = usable.slice(0, Math.max(room, 0)); }

        var created = [];
        usable.forEach(function (line, i) {
            var d = T.cardForm.emptyData();
            if (line.phonetic) { d.phonetic = line.phonetic; }
            if (line.topic) { d.topic = line.topic; }
            if (line.example) { d.examples = [{ text: line.example, vi: line.exampleVi || '' }]; }
            created.push(addCard({
                term: line.term, definition: line.meaning, d: d, fromBatch: true,
                meaningTyped: !!line.meaning, readingTyped: !!line.phonetic, touchedMeaning: false
            }, startIndex + i, false));
        });

        if (state.cards.length === 0) { addCard(null, 0, false); }
        T.toast('Đã tách thành ' + created.length + ' thẻ');
        scheduleDraft();
        if (config.isPremium) { batchEnrich(created.filter(Boolean)); }
        return true;
    }

    function batchEnrich(cards) {
        var todo = cards.filter(function (c) { return c.term.trim(); });
        if (todo.length === 0) { return; }
        state.enrich = { done: 0, total: todo.length };
        updateSaveBar();
        var groups = core.chunk(todo, 10);
        var running = 0;
        var next = 0;

        function runNext() {
            while (running < 2 && next < groups.length) {
                (function (group) {
                    running++; next++;
                    T.fetchJson(config.urls.enrichBatch, {
                        method: 'POST',
                        body: { items: group.map(function (c) { return { term: c.term.trim(), meaningHint: c.meaningTyped && c.definition.trim() ? c.definition.trim() : null }; }) }
                    }).then(function (response) {
                        var failed = 0;
                        (response.results || []).forEach(function (item, i) {
                            var card = group[i];
                            if (!card) { return; }
                            if (item.ok && item.result && item.result.isValidWord !== false) {
                                card.senses = item.result.senses || [];
                                card.selected = [0];
                                card.lastResult = item.result;
                                card.lastEnrichedTerm = card.term.trim();
                                Object.assign(card, core.applyEnrich(card, item.result, [0]), { enrichTimer: null });
                            } else if (item.ok && item.result) {
                                card.didYouMean = item.result.didYouMean || null;
                            } else {
                                failed++;
                                card.enrichError = 'Chưa điền được gợi ý. Bạn có thể tự nhập nghĩa.';
                            }
                        });
                        if (failed > 0) { T.toast('Không điền được gợi ý cho ' + failed + ' từ.', { type: 'danger' }); }
                    }).catch(function (error) {
                        T.toast(error.message || 'Lỗi khi điền gợi ý.', { type: 'danger' });
                    }).then(function () {
                        running--;
                        state.enrich.done += group.length;
                        refreshAll();
                        scheduleDraft();
                        if (state.enrich.done >= state.enrich.total) { state.enrich = { done: 0, total: 0 }; updateSaveBar(); }
                        runNext();
                    });
                })(groups[next]);
            }
        }
        runNext();
    }

    // ----------------------------------------------------------------- mode switch / target

    function setMode(mode) {
        document.getElementById('ts-pane-single').hidden = mode !== 'single';
        document.getElementById('ts-pane-paste').hidden = mode !== 'paste';
        document.querySelectorAll('.ts-mode [data-ts-mode]').forEach(function (button) {
            var on = button.getAttribute('data-ts-mode') === mode;
            button.classList.toggle('active', on);
            button.setAttribute('aria-pressed', String(on));
        });
        if (mode === 'paste') { pasteText.focus(); }
    }

    document.querySelectorAll('.ts-mode [data-ts-mode]').forEach(function (button) {
        button.addEventListener('click', function () { setMode(button.getAttribute('data-ts-mode')); });
    });

    if (targetSelect) {
        targetSelect.addEventListener('change', function () {
            state.targetId = parseInt(targetSelect.value, 10);
            state.cards.forEach(function (c) { c.lastDupTerm = ''; });
        });
    }

    // ----------------------------------------------------------------- save

    function markInvalid(card, message) {
        card.saveError = message;
        refreshCard(card);
    }

    function save() {
        if (state.saving) { return; }
        var filled = state.cards.filter(function (c) { return !core.isBlank(c); });
        if (filled.length === 0) { return; }

        var firstInvalid = null;
        filled.forEach(function (card) {
            if (!core.isReady(card)) {
                card.touchedMeaning = true;
                card.saveError = !card.term.trim() ? 'Nhập từ' : null;
                if (!firstInvalid) { firstInvalid = card; }
            }
        });
        if (firstInvalid) {
            refreshAll();
            var node = state.nodes.get(firstInvalid.uid);
            if (node) { node.root.scrollIntoView({ behavior: 'smooth', block: 'center' }); (firstInvalid.term.trim() ? node.refs.meaning : node.refs.term).focus(); }
            T.toast('Kiểm tra lại các thẻ được đánh dấu.', { type: 'danger' });
            return;
        }

        state.saving = true;
        T.pending(saveButton, true);
        var totalSaved = 0;
        var lastInserted = null;
        var chunks = core.chunk(filled, config.chunk || 30);
        var targetId = state.targetId;

        function finish() {
            state.saving = false;
            T.pending(saveButton, false);
            refreshAll();
            scheduleDraft(true);
        }

        function step(index) {
            if (index >= chunks.length) {
                if (totalSaved > 0) {
                    if (totalSaved === 1 && lastInserted) {
                        T.toast('Đã lưu ' + lastInserted.term, { actionLabel: 'Xem', onAction: function () { window.location.href = config.urls.cardBase + lastInserted.id; } });
                    } else {
                        T.toast('Đã lưu ' + totalSaved + ' từ', { actionLabel: 'Xem bộ', onAction: function () { window.location.href = config.urls.setBase + targetId; } });
                    }
                }
                if (state.cards.every(core.isBlank)) {
                    resetToOneCard();
                    T.safeStorage.remove(draftKey());
                    draftBar.hidden = true;
                }
                finish();
                return;
            }

            var group = chunks[index];
            T.fetchJson(config.urls.batchBase + targetId + '/cards/batch', {
                method: 'POST', body: { cards: group.map(core.toInputDto) }
            }).then(function (response) {
                var merged = core.mergeBatchResults(group, response);
                totalSaved += merged.insertedItems.length;
                if (merged.insertedItems.length) { lastInserted = merged.insertedItems[merged.insertedItems.length - 1]; }
                // Keep cards not in this group, plus the unsaved leftovers of this group.
                var rest = state.cards.filter(function (c) { return group.indexOf(c) === -1; });
                var leftovers = merged.remaining.map(function (copy) {
                    var original = group.find(function (c) { return c.uid === copy.uid; });
                    return Object.assign(original, { saveError: copy.saveError });
                });
                replaceCards(rest.concat(leftovers).sort(function (a, b) { return a.uid - b.uid; }));
                step(index + 1);
            }).catch(function (error) {
                T.toast(error.message || 'Không lưu được. Các thẻ chưa lưu vẫn được giữ lại.', { type: 'danger' });
                finish();
            });
        }

        step(0);
    }

    function replaceCards(cards) {
        state.nodes.forEach(function (node) { node.root.remove(); });
        state.nodes.clear();
        state.cards = cards;
        if (state.cards.length === 0) { state.cards.push(newCard()); }
        state.cards.forEach(function (card, i) {
            var node = createNode(card);
            state.nodes.set(card.uid, node);
            list.appendChild(node.root);
        });
        renumber();
        refreshAll();
    }

    function resetToOneCard() {
        replaceCards([newCard()]);
        var first = state.nodes.get(state.cards[0].uid);
        if (first) { first.refs.term.focus(); }
    }

    saveButton.addEventListener('click', save);
    document.addEventListener('keydown', function (event) {
        if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); save(); }
    });

    // ----------------------------------------------------------------- draft

    function draftKey() { return 'tct:compose:' + config.userId + ':' + config.setId; }
    var draftTimer = null;

    function scheduleDraft(now) {
        clearTimeout(draftTimer);
        var run = function () {
            var filled = state.cards.filter(function (c) { return !core.isBlank(c); }).slice(0, MAX);
            if (filled.length === 0) { T.safeStorage.remove(draftKey()); return; }
            var payload = filled.map(function (c) {
                return { term: c.term, definition: c.definition, d: c.d, meaningTyped: c.meaningTyped, readingTyped: c.readingTyped, aiGenerated: c.aiGenerated };
            });
            T.safeStorage.set(draftKey(), JSON.stringify(payload));
        };
        if (now) { run(); } else { draftTimer = setTimeout(run, 800); }
    }

    function restoreDraft() {
        var raw = T.safeStorage.get(draftKey());
        if (!raw) { return false; }
        var saved;
        try { saved = JSON.parse(raw); } catch (e) { return false; }
        if (!Array.isArray(saved) || saved.length === 0) { return false; }
        var cards = saved.slice(0, MAX).map(function (item) {
            return newCard({
                term: String(item.term || ''), definition: String(item.definition || ''),
                d: Object.assign(T.cardForm.emptyData(), item.d || {}),
                meaningTyped: !!item.meaningTyped, readingTyped: !!item.readingTyped, aiGenerated: !!item.aiGenerated, fromBatch: true
            });
        });
        replaceCards(cards);
        document.getElementById('ts-draft-text').textContent = 'Đã khôi phục ' + cards.length + ' thẻ chưa lưu.';
        draftBar.hidden = false;
        return true;
    }

    document.getElementById('ts-draft-clear').addEventListener('click', function () {
        T.safeStorage.remove(draftKey());
        draftBar.hidden = true;
        resetToOneCard();
    });

    // ----------------------------------------------------------------- details drawer

    var drawerEl = document.getElementById('tsCardDetails');
    var discardEl = document.getElementById('tsDiscardDetails');
    var drawerForm = T.cardForm.mount(drawerEl.querySelector('[data-ts-details]'), {
        isPremium: config.isPremium, urls: { images: config.urls.images, cardImage: config.urls.cardImage }
    });
    var allowDrawerClose = false;
    var offcanvas = window.bootstrap ? new window.bootstrap.Offcanvas(drawerEl) : null;

    function openDrawer(card, tab) {
        state.drawerCard = card;
        document.getElementById('tsCardDetailsTitle').textContent = card.term.trim() || 'Thẻ ' + (state.cards.indexOf(card) + 1);
        drawerForm.setData(card.d);
        drawerForm.showTab(tab || 'examples');
        allowDrawerClose = false;
        if (offcanvas) { offcanvas.show(); }
    }

    document.getElementById('ts-drawer-done').addEventListener('click', function () {
        var card = state.drawerCard;
        if (card) {
            card.d = drawerForm.getData();
            card.readingTyped = card.readingTyped || !!(card.d.phonetic && card.d.phonetic.trim());
            refreshAll();
            scheduleDraft();
        }
        drawerForm.markClean();
        allowDrawerClose = true;
        if (offcanvas) { offcanvas.hide(); }
    });

    function requestClose() {
        if (drawerForm.isDirty()) {
            if (window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(discardEl).show(); }
        } else if (offcanvas) {
            allowDrawerClose = true;
            offcanvas.hide();
        }
    }

    document.getElementById('ts-drawer-close').addEventListener('click', requestClose);
    drawerEl.addEventListener('hide.bs.offcanvas', function (event) {
        if (!allowDrawerClose && drawerForm.isDirty()) {
            event.preventDefault();
            if (window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(discardEl).show(); }
        }
    });
    document.getElementById('ts-discard-confirm').addEventListener('click', function () {
        if (window.bootstrap) { window.bootstrap.Modal.getOrCreateInstance(discardEl).hide(); }
        drawerForm.markClean();
        allowDrawerClose = true;
        if (offcanvas) { offcanvas.hide(); }
    });

    // ----------------------------------------------------------------- boot

    addCard(null, 0, false);
    var restored = restoreDraft();

    if (config.initialTerm) {
        var target = state.cards.slice().reverse().find(core.isBlank);
        var card = target || addCard(null, undefined, false);
        if (card) {
            card.term = config.initialTerm;
            refreshAll();
            var node = state.nodes.get(card.uid);
            if (node) { node.refs.meaning.focus(); }
            checkServerDuplicates(card);
        }
    } else if (!restored) {
        var first = state.nodes.get(state.cards[0].uid);
        if (first) { first.refs.term.focus(); }
    }

    refreshAll();
})();
