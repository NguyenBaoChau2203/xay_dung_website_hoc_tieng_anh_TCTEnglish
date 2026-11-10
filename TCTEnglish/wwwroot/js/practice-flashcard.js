/*
 * Lật thẻ (flashcard): flip, swipe, arrow keys, mark known / learning, term-first or meaning-first, shuffle, restart.
 * Progress is written only when "Tính vào ôn tập" is on (session.writer).
 */
(function () {
    'use strict';

    var P = window.TctPractice;
    var ui = P.ui;
    var core = P.core;
    var el = ui.el;
    var icon = ui.icon;

    var POS_LABEL = {
        noun: 'Danh từ', verb: 'Động từ', adj: 'Tính từ', adv: 'Trạng từ', pron: 'Đại từ', prep: 'Giới từ', conj: 'Liên từ',
        interj: 'Thán từ', det: 'Hạn định từ', phrasal_verb: 'Cụm động từ', idiom: 'Thành ngữ', phrase: 'Cụm từ', num: 'Số từ'
    };
    var SWIPE_DISTANCE = 100;
    var TAP_SLOP = 8;

    function run(session) {
        var config = session.config;
        var all = config.targets.slice();
        var state = { order: all.slice(), index: 0, flipped: false, meaningFirst: false, marks: {}, drag: null, suppressClick: false };

        // ------------------------------------------------------------- top bar slot
        var chipUnknown = el('span', 'tct-badge tct-badge--warning tp-chip');
        var chipKnown = el('span', 'tct-badge tct-badge--success tp-chip');
        // One toggle button drawn as a two-part segmented control; the active part is highlighted, the label stays the current order.
        var toggle = el('button', 'tp-order-toggle k-seg');
        toggle.type = 'button';
        var toggleFront = el('span', 'k-seg__item', 'Từ trước');
        var toggleBack = el('span', 'k-seg__item', 'Nghĩa trước');
        toggleFront.setAttribute('aria-hidden', 'true');
        toggleBack.setAttribute('aria-hidden', 'true');
        toggle.appendChild(toggleFront);
        toggle.appendChild(toggleBack);
        var shuffleBtn = el('button', 'btn btn-outline-primary tp-shuffle');
        shuffleBtn.type = 'button';
        shuffleBtn.setAttribute('aria-label', 'Xáo trộn thẻ');
        shuffleBtn.appendChild(icon('fa-solid fa-shuffle me-2'));
        shuffleBtn.appendChild(document.createTextNode('Xáo trộn'));
        session.setSlot([chipUnknown, chipKnown]);

        function chip(node, label, count) {
            node.textContent = '';
            node.appendChild(el('span', 'tp-chip__label', label + ': '));
            node.appendChild(el('span', 'tp-chip__count', String(count)));
        }

        function counts() {
            var known = 0;
            state.order.forEach(function (w) { if (state.marks[w.id] === 'known') { known += 1; } });
            return { known: known, unknown: state.order.length - known };
        }

        function updateBar() {
            var c = counts();
            chip(chipUnknown, 'Chưa thuộc', c.unknown);
            chip(chipKnown, 'Đã thuộc', c.known);
            toggle.setAttribute('aria-label', state.meaningFirst ? 'Nghĩa trước' : 'Từ trước');
            toggleFront.classList.toggle('is-active', !state.meaningFirst);
            toggleBack.classList.toggle('is-active', state.meaningFirst);
            toggle.setAttribute('aria-pressed', String(state.meaningFirst));
            session.setProgress(Math.min(state.index, state.order.length), state.order.length);
            session.setCounter(Math.min(state.index + 1, state.order.length) + '/' + state.order.length);
        }

        // ------------------------------------------------------------- card
        var stage = el('section', 'tp-flash');
        stage.setAttribute('aria-label', 'Thẻ ghi nhớ');
        var wrap = el('div', 'tp-flashcard-wrap');
        var hintLeft = el('span', 'tp-swipe-hint tp-swipe-hint--left', 'Chưa thuộc');
        var hintRight = el('span', 'tp-swipe-hint tp-swipe-hint--right', 'Đã thuộc');
        hintLeft.setAttribute('aria-hidden', 'true');
        hintRight.setAttribute('aria-hidden', 'true');
        var tag = el('span', 'tp-flash-tag');
        tag.hidden = true;
        var flipHint = el('p', 'tp-flash-hint');
        flipHint.setAttribute('aria-hidden', 'true');
        flipHint.appendChild(icon('fa-solid fa-rotate me-1'));
        flipHint.appendChild(document.createTextNode('Chạm để lật · vuốt trái “chưa thuộc”, vuốt phải “đã thuộc”'));
        var card = el('button', 'tp-flashcard');
        card.type = 'button';
        card.setAttribute('aria-label', 'Lật thẻ');
        var inner = el('span', 'tp-flashcard__inner');
        var front = el('span', 'tp-flashcard__face tp-flashcard__face--front');
        var back = el('span', 'tp-flashcard__face tp-flashcard__face--back');
        inner.appendChild(front);
        inner.appendChild(back);
        card.appendChild(inner);
        var speakHolder = el('span', 'tp-flash-speak');
        wrap.appendChild(hintLeft);
        wrap.appendChild(hintRight);
        wrap.appendChild(card);
        wrap.appendChild(tag);
        wrap.appendChild(speakHolder);
        wrap.appendChild(flipHint);
        var live = el('p', 'visually-hidden');
        live.setAttribute('aria-live', 'polite');

        var controls = el('div', 'tp-flash-controls');
        var prev = el('button', 'btn btn-outline-primary tct-btn-icon tp-flash-nav');
        prev.type = 'button';
        prev.setAttribute('aria-label', 'Thẻ trước');
        prev.appendChild(icon('fa-solid fa-chevron-left'));
        var next = el('button', 'btn btn-outline-primary tct-btn-icon tp-flash-nav');
        next.type = 'button';
        next.setAttribute('aria-label', 'Thẻ sau');
        next.appendChild(icon('fa-solid fa-chevron-right'));
        function markButton(cls, iconClass, label, keyHint) {
            var button = el('button', 'btn btn-lg ' + cls);
            button.type = 'button';
            button.appendChild(icon(iconClass + ' me-2'));
            button.appendChild(document.createTextNode(label));
            var kbd = el('kbd', 'k-kbd ms-2', keyHint);
            kbd.setAttribute('aria-hidden', 'true');
            button.appendChild(kbd);
            return button;
        }
        var unknownBtn = markButton('k-btn--mark-learning tp-mark tp-mark--unknown', 'fa-solid fa-rotate-left', 'Chưa thuộc', '1');
        var knownBtn = markButton('k-btn--mark-known tp-mark tp-mark--known', 'fa-solid fa-check', 'Đã thuộc', '2');
        controls.appendChild(prev);
        controls.appendChild(unknownBtn);
        controls.appendChild(knownBtn);
        controls.appendChild(next);

        var toolbar = el('div', 'tp-flash-toolbar');
        toolbar.appendChild(toggle);
        toolbar.appendChild(shuffleBtn);
        stage.appendChild(toolbar);
        stage.appendChild(wrap);
        stage.appendChild(live);
        stage.appendChild(controls);
        stage.appendChild(ui.keyHints(['Space lật', '←/→ chuyển thẻ', '1 chưa thuộc', '2 đã thuộc', 'R nghe']));

        function termFace(word) {
            var box = el('span', 'tp-flashcard__content');
            var v = ui.visual(word);
            if (v) { box.appendChild(v); }
            var term = el('span', 'tp-term k-display tct-break', word.term);
            term.lang = 'en';
            box.appendChild(term);
            if (word.phonetic) { box.appendChild(el('span', 'tct-phonetic', word.phonetic)); }
            return box;
        }

        function meaningFace(word) {
            var box = el('span', 'tp-flashcard__content');
            box.appendChild(el('span', 'tp-meaning tct-break', word.definition));
            if (word.meaningEn) { box.appendChild(el('span', 'tp-meaning-en', word.meaningEn)); }
            return box;
        }

        function exampleLine(word) {
            var ex = word.examples && word.examples[0];
            if (!ex) { return null; }
            var box = el('span', 'tp-flashcard__example');
            var text = el('span', 'tp-example', ex.text);
            text.lang = 'en';
            box.appendChild(text);
            if (ex.vi) { box.appendChild(el('span', 'tp-example-vi', ex.vi)); }
            return box;
        }

        function faceText(word, showingFront) {
            var termFirst = !state.meaningFirst;
            var termShown = termFirst === showingFront;
            return termShown ? word.term : word.definition;
        }

        function applyFlip() {
            card.classList.toggle('is-flipped', state.flipped);
            front.setAttribute('aria-hidden', String(state.flipped));
            back.setAttribute('aria-hidden', String(!state.flipped));
            var word = state.order[state.index];
            if (word) { live.textContent = faceText(word, !state.flipped); }
        }

        function renderCard() {
            var word = state.order[state.index];
            front.textContent = '';
            back.textContent = '';
            if (state.meaningFirst) {
                front.appendChild(meaningFace(word));
                back.appendChild(termFace(word));
            } else {
                front.appendChild(termFace(word));
                back.appendChild(meaningFace(word));
            }
            var example = exampleLine(word);
            if (example) { back.firstChild.appendChild(example); }
            speakHolder.textContent = '';
            speakHolder.appendChild(ui.speakButton(word.term));
            var tagParts = [];
            if (word.cefr) { tagParts.push(word.cefr); }
            if (word.partsOfSpeech && word.partsOfSpeech.length) { tagParts.push(POS_LABEL[word.partsOfSpeech[0]] || word.partsOfSpeech[0]); }
            tag.textContent = tagParts.join(' · ');
            tag.hidden = tagParts.length === 0;
            prev.disabled = state.index === 0;
            next.disabled = state.index >= state.order.length - 1;
            var mark = state.marks[word.id];
            knownBtn.setAttribute('aria-pressed', String(mark === 'known'));
            unknownBtn.setAttribute('aria-pressed', String(mark === 'unknown'));
            applyFlip();
            updateBar();
        }

        function flip() {
            state.flipped = !state.flipped;
            applyFlip();
        }

        function go(delta) {
            var target = state.index + delta;
            if (target < 0 || target >= state.order.length) { return; }
            state.index = target;
            state.flipped = false;
            renderCard();
        }

        function mark(known) {
            var word = state.order[state.index];
            if (!word) { return; }
            state.marks[word.id] = known ? 'known' : 'unknown';
            session.noteAnswer();
            session.writer.write(word.id, known);
            state.flipped = false;
            if (state.index >= state.order.length - 1) {
                showEnd();
                return;
            }
            state.index += 1;
            renderCard();
            card.focus({ preventScroll: true });
        }

        // ------------------------------------------------------------- swipe (pointer events)
        function setDrag(dx) {
            var progress = Math.max(-1, Math.min(1, dx / SWIPE_DISTANCE));
            wrap.style.setProperty('--tp-dx', dx + 'px');
            wrap.style.setProperty('--tp-rot', (dx / 25).toFixed(2) + 'deg');
            wrap.style.setProperty('--tp-left', String(Math.max(0, -progress)));
            wrap.style.setProperty('--tp-right', String(Math.max(0, progress)));
        }

        function clearDrag() {
            ['--tp-dx', '--tp-rot', '--tp-left', '--tp-right'].forEach(function (name) { wrap.style.removeProperty(name); });
            wrap.classList.remove('is-dragging');
        }

        card.addEventListener('pointerdown', function (event) {
            if (event.pointerType === 'mouse' && event.button !== 0) { return; }
            state.drag = { id: event.pointerId, x: event.clientX, dx: 0, moved: false };
            try { card.setPointerCapture(event.pointerId); } catch (e) { /* capture is best-effort */ }
        });
        card.addEventListener('pointermove', function (event) {
            var drag = state.drag;
            if (!drag || drag.id !== event.pointerId) { return; }
            drag.dx = event.clientX - drag.x;
            if (!drag.moved && Math.abs(drag.dx) > TAP_SLOP) { drag.moved = true; wrap.classList.add('is-dragging'); }
            if (drag.moved) { setDrag(drag.dx); }
        });
        function endDrag(event, cancelled) {
            var drag = state.drag;
            if (!drag || drag.id !== event.pointerId) { return; }
            state.drag = null;
            var dx = drag.dx;
            clearDrag();
            if (drag.moved) {
                state.suppressClick = true;
                setTimeout(function () { state.suppressClick = false; }, 0);
                if (!cancelled && Math.abs(dx) >= SWIPE_DISTANCE) { mark(dx > 0); }
            }
        }
        card.addEventListener('pointerup', function (event) { endDrag(event, false); });
        card.addEventListener('pointercancel', function (event) { endDrag(event, true); });
        card.addEventListener('click', function () {
            if (state.suppressClick) { return; }
            flip();
        });

        // ------------------------------------------------------------- controls and keys
        prev.addEventListener('click', function () { go(-1); });
        next.addEventListener('click', function () { go(1); });
        knownBtn.addEventListener('click', function () { mark(true); });
        unknownBtn.addEventListener('click', function () { mark(false); });
        toggle.addEventListener('click', function () {
            state.meaningFirst = !state.meaningFirst;
            state.flipped = false;
            renderCard();
        });
        shuffleBtn.addEventListener('click', function () {
            var done = state.order.slice(0, state.index);
            var rest = core.shuffle(state.order.slice(state.index));
            state.order = done.concat(rest);
            state.flipped = false;
            renderCard();
            session.toast('Đã xáo trộn các thẻ còn lại.');
        });

        ui.onKeys(function (event, key) {
            if (state.ended) { return false; }
            var onButton = event.target && /^(BUTTON|A|SUMMARY)$/.test(event.target.tagName);
            if ((key === ' ' || key === 'Enter') && !onButton) { flip(); return true; }
            if (key === 'ArrowUp' || key === 'ArrowDown') { flip(); return true; }
            if (key === 'ArrowLeft') { go(-1); return true; }
            if (key === 'ArrowRight') { go(1); return true; }
            if (key === '1') { mark(false); return true; }
            if (key === '2') { mark(true); return true; }
            if (key === 'r' || key === 'R') {
                var word = state.order[state.index];
                if (word) { ui.speak(word.term); }
                return true;
            }
            return false;
        });

        // ------------------------------------------------------------- end screen
        function restart(words) {
            state.order = words.slice();
            state.index = 0;
            state.flipped = false;
            state.ended = false;
            state.marks = {};
            session.finished = false;
            session.setSlot([chipUnknown, chipKnown]);
            session.render(stage);
            renderCard();
            card.focus({ preventScroll: true });
        }

        function showEnd() {
            state.ended = true;
            session.finish();
            var c = counts();
            var missing = state.order.filter(function (w) { return state.marks[w.id] !== 'known'; });
            var root = el('section', 'tp-summary');
            root.setAttribute('aria-label', 'Kết quả');
            var head = el('div', 'tp-summary__head');
            var percent = state.order.length ? Math.round(100 * c.known / state.order.length) : 0;
            head.appendChild(ui.ring(percent, 'đã thuộc'));
            var stars = el('div', 'tp-stars tp-pop');
            stars.setAttribute('role', 'img');
            var starCount = core.starsFor(c.known, state.order.length);
            stars.setAttribute('aria-label', starCount + ' sao');
            for (var i = 1; i <= 3; i++) { stars.appendChild(icon(i <= starCount ? 'fa-solid fa-star' : 'fa-solid fa-star tp-star--off')); }
            head.appendChild(stars);
            var title = el('h2', 'tp-summary__title', 'Hết thẻ rồi!');
            title.tabIndex = -1;
            head.appendChild(title);
            head.appendChild(el('p', 'm-0', c.unknown === 0 ? 'Bạn đã thuộc hết cả bộ thẻ này.' : 'Còn ' + c.unknown + ' thẻ cần học thêm.'));
            root.appendChild(head);

            var kpis = el('div', 'tp-kpis');
            [['Đã thuộc', String(c.known), 'success'], ['Chưa thuộc', String(c.unknown), 'warning'], ['Thời gian', core.formatDuration(session.elapsed()), 'info']].forEach(function (pair) {
                var kpi = el('div', 'tp-kpi k-soft tp-hue-' + pair[2]);
                kpi.appendChild(el('span', 'tp-kpi__value', pair[1]));
                kpi.appendChild(el('span', 'tp-kpi__label', pair[0]));
                kpis.appendChild(kpi);
            });
            root.appendChild(kpis);

            if (missing.length) {
                var section = el('section', 'tp-missed tp-missed--learning k-panel');
                var missedTitle = el('h3', 'tp-missed__title');
                missedTitle.appendChild(icon('fa-solid fa-rotate-left'));
                missedTitle.appendChild(document.createTextNode('Thẻ chưa thuộc'));
                section.appendChild(missedTitle);
                var list = el('ul', 'tp-missed__list');
                missing.forEach(function (w) {
                    var item = el('li', 'tp-missed__item');
                    var text = el('div', 'tp-missed__text');
                    var link = el('a', 'tp-missed__term', w.term);
                    link.href = '/Sets/Cards/' + w.id;
                    link.lang = 'en';
                    text.appendChild(link);
                    if (w.phonetic) { text.appendChild(el('span', 'tct-phonetic tp-missed__ipa', w.phonetic)); }
                    text.appendChild(el('span', 'tp-missed__def', w.definition));
                    item.appendChild(text);
                    item.appendChild(ui.speakButton(w.term));
                    list.appendChild(item);
                });
                section.appendChild(list);
                root.appendChild(section);
            }

            var actions = el('div', 'tp-summary__actions');
            if (missing.length) {
                var again = el('button', 'btn btn-lg btn-primary', 'Học tiếp ' + missing.length + ' thẻ chưa thuộc');
                again.type = 'button';
                again.addEventListener('click', function () { restart(missing); });
                actions.appendChild(again);
            }
            var whole = el('button', 'btn btn-lg ' + (missing.length ? 'btn-outline-primary' : 'btn-primary'), 'Học lại cả bộ');
            whole.type = 'button';
            whole.addEventListener('click', function () { restart(all); });
            actions.appendChild(whole);
            root.appendChild(actions);

            var links = el('div', 'tp-summary__links');
            if (missing.length) {
                var learn = el('a', 'tp-summary__link', 'Học bằng chế độ Học');
                learn.href = ui.learnLink(config, missing.slice(0, 50).map(function (w) { return w.id; }));
                links.appendChild(learn);
            }
            var done = el('a', 'tp-summary__link', 'Xong');
            done.href = config.exitUrl;
            links.appendChild(done);
            root.appendChild(links);

            session.setSlot([]);
            session.setProgress(state.order.length, state.order.length);
            session.setCounter('');
            session.render(root);
            title.focus();
        }

        session.render(stage);
        renderCard();
        card.focus({ preventScroll: true });
    }

    P.modes.flashcard = run;
}());
