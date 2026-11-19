/*
 * Word lookup for /Home/Listening/Practice/{id}: a popover under the tapped word on laptops and a bottom sheet at <= 767 px.
 * Exposes window.TctListening.word.open(button, { term, key, line }); listening-practice.js calls it for transcript and
 * subtitle words. Needs window.TctSets (personal-sets-common.js). DOM is built with createElement and textContent only;
 * server strings are never turned into markup.
 *
 * Server contract (ListeningApiController): GET api/listening/word?lessonId&term -> { source: lesson|dictionary, word,
 * definition, example, phonetic } or { source: 'locked', lineTranslationOnly } (Standard and guests, D1);
 * POST api/listening/word/save { lessonId, term, definition, example } -> { saved, duplicate?, setUrl }.
 */
(function () {
    'use strict';

    var Sets = window.TctSets;
    var root = window.TctListening = window.TctListening || {};
    var page = document.querySelector('.tl-page--practice');
    if (!Sets || !page) { return; }

    var config = {};
    try { config = JSON.parse(document.getElementById('tlConfig').textContent) || {}; } catch (e) { config = {}; }
    var urls = config.urls || {};
    var cache = new Map();      // lower-case term -> lookup result (one fetch per word per page visit)
    var sheetQuery = window.matchMedia ? window.matchMedia('(max-width: 767.98px)') : null;

    var pop = null;             // { box, backdrop, word, phonetic, speak, body, actions }
    var state = null;           // { trigger, term, ctx, result, token }
    var lastSetUrl = null;

    function isSheet() { return !!(sheetQuery && sheetQuery.matches); }

    // ------------------------------------------------------------------ DOM (built once)

    function build() {
        var backdrop = Sets.el('div', 'tl-pop-backdrop');
        backdrop.hidden = true;
        backdrop.addEventListener('click', close);

        var box = Sets.el('div', 'tl-pop');
        box.id = 'tlWordPop';
        box.setAttribute('role', 'dialog');
        box.setAttribute('aria-modal', 'false');
        box.setAttribute('aria-labelledby', 'tlWordPopTitle');
        box.hidden = true;
        box.tabIndex = -1;

        box.appendChild(Sets.el('span', 'tl-pop__handle')).setAttribute('aria-hidden', 'true');

        var head = Sets.el('div', 'tl-pop__head');
        var titleWrap = Sets.el('div', 'tl-pop__title-wrap');
        var word = Sets.el('h3', 'tl-pop__word');
        word.id = 'tlWordPopTitle';
        word.setAttribute('lang', 'en');
        var phonetic = Sets.el('span', 'tl-pop__phonetic');
        titleWrap.appendChild(word);
        titleWrap.appendChild(phonetic);
        var speak = Sets.el('button', 'btn tct-btn-icon tl-pop__speak');
        speak.type = 'button';
        speak.setAttribute('aria-label', 'Phát âm');
        speak.appendChild(Sets.icon('fa-solid fa-volume-high'));
        speak.hidden = !Sets.canSpeak();
        speak.addEventListener('click', function () { if (state) { Sets.speak(state.term); } });
        var closeBtn = Sets.el('button', 'btn tct-btn-icon tl-pop__close');
        closeBtn.type = 'button';
        closeBtn.setAttribute('aria-label', 'Đóng');
        closeBtn.appendChild(Sets.icon('fa-solid fa-xmark'));
        closeBtn.addEventListener('click', close);
        head.appendChild(titleWrap);
        head.appendChild(speak);
        head.appendChild(closeBtn);

        var body = Sets.el('div', 'tl-pop__body');
        body.setAttribute('aria-live', 'polite');
        var actions = Sets.el('div', 'tl-pop__actions');

        box.appendChild(head);
        box.appendChild(body);
        box.appendChild(actions);
        // inside the page wrapper so the --tct-* tokens and .tl-page rules apply
        page.appendChild(backdrop);
        page.appendChild(box);

        box.addEventListener('keydown', onKeydown);
        pop = { box: box, backdrop: backdrop, word: word, phonetic: phonetic, speak: speak, body: body, actions: actions };
    }

    function paragraph(className, text) { return Sets.el('p', className, text); }

    // ------------------------------------------------------------------ content states

    function showLoading() {
        pop.body.textContent = '';
        var skeleton = Sets.el('div', 'tl-pop__skeleton');
        skeleton.setAttribute('aria-hidden', 'true');
        skeleton.appendChild(Sets.el('span', 'tl-pop__bar'));
        skeleton.appendChild(Sets.el('span', 'tl-pop__bar tl-pop__bar--short'));
        pop.body.appendChild(skeleton);
        pop.body.appendChild(Sets.el('p', 'visually-hidden', 'Đang tra từ…'));
        pop.body.appendChild(paragraph('tl-pop__muted', 'Đang tra từ…'));
        pop.actions.textContent = '';
    }

    function showError(message) {
        pop.body.textContent = '';
        pop.body.appendChild(paragraph('tl-pop__error', message));
        pop.actions.textContent = '';
        var retry = Sets.el('button', 'btn btn-outline-primary btn-sm', 'Thử lại');
        retry.type = 'button';
        retry.addEventListener('click', function () { if (state) { lookup(state); } });
        pop.actions.appendChild(retry);
    }

    function showLocked(ctx) {
        pop.body.textContent = '';
        var line = ctx && ctx.line;
        if (line && line.vi) {
            pop.body.appendChild(paragraph('tl-pop__label', 'Nghĩa của cả câu'));
            pop.body.appendChild(paragraph('tl-pop__def', line.vi));
        }
        pop.body.appendChild(paragraph('tl-pop__muted', 'Tra mọi từ là tính năng Premium.'));
        pop.actions.textContent = '';
        var upgrade = Sets.el('a', 'btn btn-primary btn-sm', 'Nâng cấp Premium');
        upgrade.href = urls.premium || '/Account/Premium';
        pop.actions.appendChild(upgrade);
    }

    function showResult(result) {
        pop.word.textContent = result.word || state.term;
        pop.phonetic.textContent = result.phonetic || '';
        pop.phonetic.hidden = !result.phonetic;
        pop.body.textContent = '';
        pop.body.appendChild(paragraph('tl-pop__def', result.definition || ''));
        if (result.example) {
            var example = paragraph('tl-pop__example', result.example);
            example.setAttribute('lang', 'en');
            pop.body.appendChild(example);
        }
        var note = Sets.el('p', 'tl-pop__note');
        note.hidden = true;
        note.id = 'tlWordPopNote';
        pop.body.appendChild(note);
        renderActions(result, note);
    }

    function renderActions(result, note) {
        pop.actions.textContent = '';
        var save = Sets.el('button', 'btn btn-primary btn-sm');
        save.type = 'button';
        save.appendChild(Sets.icon('fa-regular fa-bookmark'));
        save.appendChild(document.createTextNode(' Lưu vào Bộ thẻ'));
        save.addEventListener('click', function () { saveCurrent(result, save, note); });
        pop.actions.appendChild(save);
        if (lastSetUrl || config.wordSetUrl) { addSetLink(lastSetUrl || config.wordSetUrl); }
    }

    function addSetLink(url) {
        if (!url || pop.actions.querySelector('.tl-pop__setlink')) { return; }
        var link = Sets.el('a', 'btn btn-outline-primary btn-sm tl-pop__setlink', 'Mở bộ thẻ');
        link.href = url;
        pop.actions.appendChild(link);
    }

    // ------------------------------------------------------------------ lookup and save

    function lookup(current) {
        var key = current.term.toLowerCase();
        // a lesson-vocabulary word (a phrase such as "to go" included) is already on the page: free for everyone, no request
        if (current.ctx && current.ctx.vocab && current.ctx.vocab.definition) {
            present(current, { source: 'lesson', word: current.ctx.vocab.word, definition: current.ctx.vocab.definition, example: current.ctx.vocab.example || '', phonetic: null });
            return;
        }
        showLoading();
        if (cache.has(key)) { present(current, cache.get(key)); return; }
        var token = {};
        current.token = token;
        var url = (urls.word || '/api/listening/word') + '?lessonId=' + encodeURIComponent(config.lessonId) + '&term=' + encodeURIComponent(current.term);
        Sets.fetchJson(url).then(function (result) {
            if (!result) { throw new Error('Chưa tra được từ này. Thử lại sau.'); }
            cache.set(key, result);
            if (state && state.token === token) { present(current, result); }
        }).catch(function (error) {
            if (state && state.token === token) { showError((error && error.message) || 'Chưa tra được từ này. Thử lại sau.'); }
        });
    }

    function present(current, result) {
        if (result.source === 'locked') {
            pop.word.textContent = result.word || current.term;
            pop.phonetic.hidden = true;
            showLocked(current.ctx);
        } else {
            showResult(result);
        }
        place();
    }

    function saveCurrent(result, button, note) {
        function say(text, bad) { note.hidden = false; note.textContent = text; note.className = 'tl-pop__note' + (bad ? ' tl-pop__note--bad' : ''); }
        if (!config.isAuthenticated) {
            note.hidden = false;
            note.className = 'tl-pop__note';
            note.textContent = 'Đăng nhập để lưu từ vào Bộ thẻ. ';
            var login = Sets.el('a', null, 'Đăng nhập');
            login.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
            note.appendChild(login);
            return;
        }
        Sets.pending(button, true, 'Đang lưu…');
        Sets.fetchJson(urls.wordSave || '/api/listening/word/save', {
            method: 'POST',
            body: { lessonId: config.lessonId, term: result.word || state.term, definition: result.definition || '', example: result.example || '' }
        }).then(function (saved) {
            Sets.pending(button, false);
            if (saved && saved.setUrl) { lastSetUrl = saved.setUrl; addSetLink(saved.setUrl); }
            if (saved && saved.duplicate) { say('Từ này đã có trong bộ thẻ.'); return; }
            say('Đã lưu.');
            Sets.toast('Đã lưu "' + (result.word || state.term) + '" vào bộ "Từ vựng Luyện nghe".');
        }).catch(function (error) {
            Sets.pending(button, false);
            say((error && error.message) || 'Chưa lưu được từ này. Thử lại sau.', true);
        });
    }

    // ------------------------------------------------------------------ placement, focus, closing

    function place() {
        if (!pop || pop.box.hidden) { return; }
        var box = pop.box;
        if (isSheet()) { box.style.removeProperty('top'); box.style.removeProperty('left'); box.classList.add('tl-pop--sheet'); return; }
        box.classList.remove('tl-pop--sheet');
        if (!state || !document.body.contains(state.trigger)) { return; }
        var rect = state.trigger.getBoundingClientRect();
        var width = box.offsetWidth || 300;
        var height = box.offsetHeight || 160;
        var gap = 8;
        var left = Math.min(Math.max(8, rect.left), window.innerWidth - width - 8);
        var below = rect.bottom + gap;
        var top = below + height > window.innerHeight - 8 && rect.top - gap - height > 8 ? rect.top - gap - height : below;
        box.style.left = Math.round(left) + 'px';
        box.style.top = Math.round(Math.max(8, top)) + 'px';
    }

    function focusables() {
        return Array.prototype.filter.call(pop.box.querySelectorAll('a[href], button:not([disabled])'), function (el) { return !el.hidden && el.offsetParent !== null; });
    }

    function onKeydown(event) {
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(); return; }
        if (event.key !== 'Tab' || !isSheet()) { return; }
        // the bottom sheet traps focus (backdrop behind it); the laptop popover lets Tab leave
        var items = focusables();
        if (!items.length) { event.preventDefault(); pop.box.focus(); return; }
        var first = items[0], last = items[items.length - 1];
        if (event.shiftKey && (document.activeElement === first || document.activeElement === pop.box)) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }

    function onDocumentPointer(event) {
        if (!pop || pop.box.hidden) { return; }
        if (pop.box.contains(event.target)) { return; }
        if (state && state.trigger && state.trigger.contains(event.target)) { return; }
        close(false);
    }

    function close(restoreFocus) {
        if (!pop || pop.box.hidden) { return; }
        pop.box.hidden = true;
        pop.backdrop.hidden = true;
        document.body.classList.remove('tl-pop-open');
        var trigger = state && state.trigger;
        state = null;
        if (restoreFocus !== false && trigger && document.body.contains(trigger)) { trigger.focus(); }
    }

    function open(button, ctx) {
        if (!button || !ctx || !ctx.term) { return; }
        if (!pop) { build(); }
        var same = state && state.trigger === button && !pop.box.hidden;
        if (same) { close(); return; }
        state = { trigger: button, term: ctx.term, ctx: ctx, token: null };
        pop.word.textContent = ctx.term;
        pop.phonetic.hidden = true;
        pop.box.hidden = false;
        pop.box.classList.toggle('tl-pop--sheet', isSheet());
        pop.backdrop.hidden = !isSheet();
        if (isSheet()) { document.body.classList.add('tl-pop-open'); }
        showLoading();
        place();
        pop.box.focus();
        lookup(state);
    }

    document.addEventListener('mousedown', onDocumentPointer);
    document.addEventListener('touchstart', onDocumentPointer, { passive: true });
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);

    root.word = { open: open, close: close };
}());
