/*
 * /Home/Listening/Practice/{id}: player, steps, tabs, shortcuts and (in the sections below) the transcript, dictation,
 * fill-in, vocabulary and quiz behaviour. Needs window.TctSets (personal-sets-common.js) and
 * window.TctListening.core (listening-core.js). Lesson data comes from <script type="application/json" id="tlConfig">.
 * DOM is built with createElement and textContent only; server data is never turned into markup.
 */
(function () {
    'use strict';

    var core = window.TctListening && window.TctListening.core;
    var Sets = window.TctSets;
    var page = document.querySelector('.tl-page--practice');
    if (!core || !Sets || !page) { return; }

    var config = {};
    try { config = JSON.parse(document.getElementById('tlConfig').textContent) || {}; } catch (e) { config = {}; }
    var progress = config.progress || {};
    var lines = config.lines || [];
    var urls = config.urls || {};

    // ------------------------------------------------------------------ small helpers

    function byId(id) { return document.getElementById(id); }
    function setHidden(el, hidden) { if (el) { el.hidden = !!hidden; } }

    function setButtonContent(button, iconClass, text) {
        button.textContent = '';
        button.appendChild(Sets.icon(iconClass));
        button.appendChild(document.createTextNode(' '));
        button.appendChild(Sets.el('span', null, text));
    }

    function announce(id, text) {
        var el = byId(id);
        if (el) { el.textContent = ''; window.setTimeout(function () { el.textContent = text; }, 30); }
    }

    // ------------------------------------------------------------------ progress and steps

    var flags = {
        transcript: !!progress.transcriptCompleted,
        quiz: !!progress.quizCompleted,
        vocab: !!progress.vocabReviewed
    };
    var required = core.requiredSteps(config.steps);
    var saveWarned = false;

    /** POST the progress patch (guests skip). One retry; one toast per page visit when it still fails. */
    function saveProgress(patch) {
        if (!config.isAuthenticated) { return Promise.resolve(false); }
        function attempt() { return Sets.fetchJson(urls.save, { method: 'POST', body: patch }); }
        return attempt().catch(function () { return attempt(); }).then(function () { return true; }, function () {
            if (!saveWarned) {
                saveWarned = true;
                Sets.toast('Không lưu được tiến độ. Kết quả trên trang vẫn được giữ.', { type: 'danger' });
            }
            return false;
        });
    }

    function markTab(key) {
        var tab = document.querySelector('[data-tl-tab="' + key + '"]');
        if (!tab || tab.querySelector('.tl-tab__done')) { return; }
        tab.appendChild(Sets.icon('fa-solid fa-circle-check tl-tab__done'));
        tab.appendChild(Sets.el('span', 'visually-hidden', 'đã xong'));
    }

    function setStep(key, done) {
        flags[key] = !!done;
        var step = document.querySelector('.tl-step[data-step="' + key + '"]');
        if (step) {
            step.setAttribute('data-done', done ? 'true' : 'false');
            var icon = step.querySelector('.tl-step__icon');
            if (icon) { icon.className = (done ? 'fa-solid fa-circle-check' : 'fa-regular fa-circle') + ' tl-step__icon'; }
            var state = step.querySelector('.tl-step__state');
            if (state) { state.textContent = done ? 'đã xong' : 'chưa xong'; }
        }
        if (done) { markTab(key); }
        setHidden(byId('tlDone'), !core.isComplete(flags, required));
    }

    // ------------------------------------------------------------------ tabs

    var HASH = { transcript: 'loi-thoai', vocab: 'tu-vung', quiz: 'quiz' };
    var tabs = Array.prototype.slice.call(document.querySelectorAll('[data-tl-tab]'));
    var tablist = document.querySelector('.tl-tabs');

    function tabKey(tab) { return tab.getAttribute('data-tl-tab'); }

    function selectTab(key, options) {
        options = options || {};
        var found = false;
        tabs.forEach(function (tab) {
            var on = tabKey(tab) === key;
            found = found || on;
            tab.setAttribute('aria-selected', on ? 'true' : 'false');
            tab.tabIndex = on ? 0 : -1;
            setHidden(byId('tlPanel-' + tabKey(tab)), !on);
            if (on && options.focus) { tab.focus(); }
        });
        if (found && options.hash !== false) {
            try { window.history.replaceState(null, '', window.location.pathname + window.location.search + '#' + HASH[key]); } catch (e) { /* ignore */ }
        }
    }

    function keyFromHash() {
        var hash = window.location.hash.replace(/^#/, '');
        var key = Object.keys(HASH).filter(function (k) { return HASH[k] === hash; })[0];
        return key && document.querySelector('[data-tl-tab="' + key + '"]') ? key : null;
    }

    if (tablist) {
        tablist.addEventListener('click', function (event) {
            var tab = event.target.closest('[data-tl-tab]');
            if (tab) { selectTab(tabKey(tab)); }
        });
        tablist.addEventListener('keydown', function (event) {
            var current = tabs.indexOf(document.activeElement);
            if (current < 0) { return; }
            var next = -1;
            if (event.key === 'ArrowRight') { next = (current + 1) % tabs.length; }
            else if (event.key === 'ArrowLeft') { next = (current - 1 + tabs.length) % tabs.length; }
            else if (event.key === 'Home') { next = 0; }
            else if (event.key === 'End') { next = tabs.length - 1; }
            if (next >= 0) { event.preventDefault(); selectTab(tabKey(tabs[next]), { focus: true }); }
        });
    }

    Array.prototype.forEach.call(document.querySelectorAll('[data-tl-goto]'), function (button) {
        button.addEventListener('click', function () { selectTab(button.getAttribute('data-tl-goto'), { focus: true }); });
    });
    window.addEventListener('hashchange', function () { var key = keyFromHash(); if (key) { selectTab(key, { hash: false }); } });
    selectTab(keyFromHash() || 'transcript', { hash: false });

    // ------------------------------------------------------------------ player adapter (YouTube IFrame API or <audio>)

    function createPlayer() {
        var listeners = [];
        var api = { kind: 'none', ready: false };
        function emit(state) { listeners.forEach(function (fn) { fn(state); }); }
        api.onState = function (fn) { listeners.push(fn); };

        var audio = byId('tlAudio');
        var iframe = byId('tlYoutube');

        if (audio) {
            api.kind = 'audio';
            api.ready = true;
            api.play = function () { var p = audio.play(); if (p && p.catch) { p.catch(function () { /* autoplay refused */ }); } };
            api.pause = function () { audio.pause(); };
            api.toggle = function () { if (audio.paused) { api.play(); } else { api.pause(); } };
            api.seek = function (seconds) { audio.currentTime = Math.max(0, seconds); };
            api.time = function () { return audio.currentTime || 0; };
            api.rate = function (value) { if (value === undefined) { return audio.playbackRate; } audio.playbackRate = value; return value; };
            audio.addEventListener('play', function () { emit('playing'); });
            audio.addEventListener('pause', function () { emit('paused'); });
            audio.addEventListener('ended', function () { emit('ended'); });
        } else if (iframe && config.youtubeId) {
            api.kind = 'youtube';
            var yt = null;
            var notice = byId('tlPlayerNotice');
            var failTimer = window.setTimeout(function () { setHidden(notice, false); }, 8000);
            var fail = function () { window.clearTimeout(failTimer); setHidden(notice, false); };

            var start = function () {
                try {
                    yt = new window.YT.Player('tlYoutube', {
                        events: {
                            onReady: function () {
                                window.clearTimeout(failTimer);
                                setHidden(notice, true);
                                api.ready = true;
                                emit('ready');
                            },
                            onStateChange: function (event) {
                                var s = event.data;
                                if (s === 1) { emit('playing'); } else if (s === 2) { emit('paused'); } else if (s === 0) { emit('ended'); }
                            }
                        }
                    });
                } catch (e) { fail(); }
            };

            if (window.YT && window.YT.Player) {
                start();
            } else {
                window.onYouTubeIframeAPIReady = start;
                var tag = document.createElement('script');
                tag.src = 'https://www.youtube.com/iframe_api';
                tag.onerror = fail;
                document.head.appendChild(tag);
            }

            api.play = function () { if (yt && yt.playVideo) { yt.playVideo(); } };
            api.pause = function () { if (yt && yt.pauseVideo) { yt.pauseVideo(); } };
            api.toggle = function () {
                if (!yt || !yt.getPlayerState) { return; }
                if (yt.getPlayerState() === 1) { api.pause(); } else { api.play(); }
            };
            api.seek = function (seconds) { if (yt && yt.seekTo) { yt.seekTo(Math.max(0, seconds), true); } };
            api.time = function () { return yt && yt.getCurrentTime ? yt.getCurrentTime() || 0 : 0; };
            api.rate = function (value) {
                if (!yt || !yt.setPlaybackRate) { return 1; }
                if (value === undefined) { return yt.getPlaybackRate ? yt.getPlaybackRate() : 1; }
                yt.setPlaybackRate(value);
                return value;
            };
        } else {
            var none = function () { /* no media */ };
            api.play = api.pause = api.toggle = api.seek = none;
            api.time = function () { return 0; };
            api.rate = function () { return 1; };
        }
        return api;
    }

    var player = createPlayer();
    var playing = false;
    var playBtn = byId('tlPlay');
    var loopBtn = byId('tlLoop');
    var loopOn = false;

    function syncPlayButton() {
        if (!playBtn) { return; }
        playBtn.setAttribute('aria-label', playing ? 'Tạm dừng' : 'Phát');
        var icon = playBtn.querySelector('i');
        if (icon) { icon.className = playing ? 'fa-solid fa-pause' : 'fa-solid fa-play'; }
    }

    player.onState(function (state) {
        if (state === 'playing') { playing = true; } else if (state === 'paused' || state === 'ended') { playing = false; }
        syncPlayButton();
        if (state === 'ready') { applySpeed(); }
    });

    if (playBtn) { playBtn.addEventListener('click', function () { player.toggle(); }); }
    if (byId('tlBack')) { byId('tlBack').addEventListener('click', function () { player.seek(player.time() - 5); }); }
    if (byId('tlForward')) { byId('tlForward').addEventListener('click', function () { player.seek(player.time() + 5); }); }

    function setLoop(on) {
        loopOn = !!on;
        if (loopBtn) { loopBtn.setAttribute('aria-pressed', loopOn ? 'true' : 'false'); }
    }
    if (loopBtn) { loopBtn.addEventListener('click', function () { setLoop(!loopOn); }); }

    function applySpeed() {
        var checked = document.querySelector('input[name="tlSpeed"]:checked');
        if (checked) { player.rate(Number(checked.value)); }
    }
    Array.prototype.forEach.call(document.querySelectorAll('input[name="tlSpeed"]'), function (radio) {
        radio.addEventListener('change', applySpeed);
    });

    // ------------------------------------------------------------------ keyboard shortcuts

    // The transcript section fills these in; until then the keys simply do nothing.
    var lineApi = { prev: function () { }, next: function () { }, replay: function () { } };

    document.addEventListener('keydown', function (event) {
        if (event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented) { return; }
        var target = event.target;
        var tag = target && target.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (target && target.isContentEditable)) { return; }
        var onControl = tag === 'BUTTON' || tag === 'A' || tag === 'SUMMARY';
        var inGroup = target && target.closest && target.closest('[role="tablist"], [role="radiogroup"], audio, iframe');

        if (event.key === ' ') {
            if (onControl || inGroup) { return; }
            event.preventDefault();
            player.toggle();
        } else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
            if (inGroup) { return; }
            event.preventDefault();
            if (event.key === 'ArrowLeft') { lineApi.prev(); } else { lineApi.next(); }
        } else if (event.key === 'r' || event.key === 'R') {
            lineApi.replay();
        } else if (event.key === 'l' || event.key === 'L') {
            setLoop(!loopOn);
        }
    });

    // ------------------------------------------------------------------ transcript: modes, lines, active line

    var linesList = byId('tlLines');
    var linesWrap = byId('tlLinesWrap');
    var timed = [];
    var lastActive = -1;
    var segmentEnd = null;
    var rafId = null;
    var lastUserScroll = 0;
    var AUTOSCROLL_KEY = 'tct.listening.autoscroll';
    var SHOW_VI_KEY = 'tct.listening.showvi.' + config.lessonId;

    if (linesList) {
        Array.prototype.forEach.call(linesList.querySelectorAll('.tl-line'), function (li) {
            var start = li.getAttribute('data-start');
            if (start === null || start === '') { return; }
            timed.push({ start: Number(start), end: Number(li.getAttribute('data-end')), el: li, index: Number(li.getAttribute('data-i')) });
        });
        timed.sort(function (a, b) { return a.start - b.start; });
    }

    function timedPosition(order) {
        for (var i = 0; i < timed.length; i++) { if (timed[i].index === order) { return i; } }
        return -1;
    }

    function setActive(position) {
        if (position === lastActive || position < 0 || !timed[position]) { return; }
        if (lastActive >= 0 && timed[lastActive]) {
            timed[lastActive].el.classList.remove('is-active');
            timed[lastActive].el.removeAttribute('aria-current');
        }
        lastActive = position;
        var el = timed[position].el;
        el.classList.add('is-active');
        el.setAttribute('aria-current', 'true');
        if (autoScrollOn && Date.now() - lastUserScroll > 3000) { el.scrollIntoView({ block: 'nearest', behavior: 'auto' }); }
    }

    function tick() {
        rafId = null;
        var time = player.time();
        var position = core.lineIndexAt(timed, time);
        if (position >= 0) { setActive(position); }
        if (segmentEnd !== null && time >= segmentEnd) {
            segmentEnd = null;
            player.pause();
        } else if (loopOn && lastActive >= 0 && timed[lastActive] && time >= timed[lastActive].end - 0.12) {
            player.seek(timed[lastActive].start);
        }
        if (playing) { rafId = window.requestAnimationFrame(tick); }
    }

    function startTicking() { if (rafId === null && timed.length) { rafId = window.requestAnimationFrame(tick); } }

    player.onState(function (state) {
        if (state === 'playing') { startTicking(); }
        if (state === 'paused' || state === 'ended') {
            if (rafId !== null) { window.cancelAnimationFrame(rafId); rafId = null; }
            segmentEnd = null;
        }
    });

    /** Seek to a timed line and play it; with stopAtEnd the player pauses when the line ends. */
    function playTimed(position, stopAtEnd) {
        if (position < 0 || !timed[position]) { return; }
        setActive(position);
        segmentEnd = stopAtEnd ? timed[position].end : null;
        player.seek(timed[position].start);
        player.play();
        startTicking();
    }

    function currentPosition() {
        if (lastActive >= 0) { return lastActive; }
        return Math.max(0, core.lineIndexAt(timed, player.time()));
    }

    lineApi.prev = function () { if (timed.length) { playTimed(Math.max(0, currentPosition() - 1)); } };
    lineApi.next = function () { if (timed.length) { playTimed(Math.min(timed.length - 1, currentPosition() + 1)); } };
    lineApi.replay = function () { if (timed.length) { playTimed(currentPosition()); } };

    if (linesList) {
        linesList.addEventListener('click', function (event) {
            var seek = event.target.closest('.tl-line__seek');
            if (seek) {
                var order = Number(seek.closest('.tl-line').getAttribute('data-i'));
                playTimed(timedPosition(order));
                return;
            }
            var reveal = event.target.closest('.tl-line__reveal');
            if (reveal) {
                var li = reveal.closest('.tl-line');
                li.setAttribute('data-revealed', 'true');
                syncLineVisibility(li);
                var seekBtn = li.querySelector('.tl-line__seek');
                if (seekBtn) { seekBtn.focus(); }
            }
        });
    }

    // the learner's own scrolling wins for three seconds
    if (linesWrap) {
        ['wheel', 'touchmove', 'pointerdown', 'keydown'].forEach(function (name) {
            linesWrap.addEventListener(name, function () { lastUserScroll = Date.now(); }, { passive: true });
        });
    }

    // ------------------------------------------------------------------ transcript: toggles

    var viBtn = byId('tlToggleVi');
    var hideBtn = byId('tlToggleHide');
    var scrollBtn = byId('tlToggleScroll');
    var autoScrollOn = Sets.safeStorage.get(AUTOSCROLL_KEY) !== '0';

    function setPressed(button, on) { if (button) { button.setAttribute('aria-pressed', on ? 'true' : 'false'); } }

    function syncLineVisibility(li) {
        var masked = hideBtn && hideBtn.getAttribute('aria-pressed') === 'true' && li.getAttribute('data-revealed') !== 'true';
        var en = li.querySelector('.tl-line__en');
        var reveal = li.querySelector('.tl-line__reveal');
        var seek = li.querySelector('.tl-line__seek');
        var mask = li.querySelector('.tl-line__mask');
        if (en) { en.hidden = masked; }
        if (reveal) { reveal.hidden = !masked; }
        if (masked && seek && !mask) {
            mask = Sets.el('span', 'tl-line__mask', '••••••');
            mask.setAttribute('aria-hidden', 'true');
            seek.appendChild(mask);
        } else if (!masked && mask) {
            mask.remove();
        }
        if (masked && !seek && !mask && en) {
            var holder = Sets.el('span', 'tl-line__mask', '••••••');
            holder.setAttribute('aria-hidden', 'true');
            en.parentNode.insertBefore(holder, en);
        } else if (!masked) {
            var stray = li.querySelector('.tl-line__text > .tl-line__mask');
            if (stray) { stray.remove(); }
        }
    }

    function syncVi() {
        var on = viBtn && viBtn.getAttribute('aria-pressed') === 'true';
        Array.prototype.forEach.call(document.querySelectorAll('#tlLines .tl-line__vi'), function (el) { el.hidden = !on; });
    }

    if (viBtn) {
        viBtn.addEventListener('click', function () {
            if (viBtn.disabled) { return; }
            setPressed(viBtn, viBtn.getAttribute('aria-pressed') !== 'true');
            syncVi();
        });
    }
    if (hideBtn) {
        hideBtn.addEventListener('click', function () {
            setPressed(hideBtn, hideBtn.getAttribute('aria-pressed') !== 'true');
            Array.prototype.forEach.call(document.querySelectorAll('#tlLines .tl-line'), function (li) {
                li.removeAttribute('data-revealed');
                syncLineVisibility(li);
            });
        });
    }
    setPressed(scrollBtn, autoScrollOn);
    if (scrollBtn) {
        scrollBtn.addEventListener('click', function () {
            autoScrollOn = !autoScrollOn;
            setPressed(scrollBtn, autoScrollOn);
            Sets.safeStorage.set(AUTOSCROLL_KEY, autoScrollOn ? '1' : '0');
        });
    }

    // translate with AI (Premium); a page reload brings the saved translation in
    var translateBtn = byId('tlTranslate');
    if (translateBtn) {
        translateBtn.addEventListener('click', function () {
            Sets.pending(translateBtn, true, 'Đang dịch…');
            Sets.fetchJson(urls.translate, { method: 'POST' }).then(function () {
                Sets.toast('Đã dịch lời thoại.');
                Sets.safeStorage.set(SHOW_VI_KEY, '1');
                window.setTimeout(function () { window.location.reload(); }, 700);
            }).catch(function (error) {
                Sets.pending(translateBtn, false);
                var data = error && error.data;
                Sets.toast((data && data.message) || 'Không dịch được lời thoại. Vui lòng thử lại.', { type: 'danger' });
            });
        });
    }
    if (Sets.safeStorage.get(SHOW_VI_KEY) === '1') {
        Sets.safeStorage.remove(SHOW_VI_KEY);
        if (viBtn && !viBtn.disabled) { setPressed(viBtn, true); syncVi(); }
    }

    // ------------------------------------------------------------------ transcript: mode switch

    var modeHooks = {};
    var modeBuilt = {};

    function showMode(mode) {
        ['normal', 'dictation', 'fill'].forEach(function (name) { setHidden(byId('tlModePanel-' + name), name !== mode); });
        if (!modeBuilt[mode] && typeof modeHooks[mode] === 'function') { modeHooks[mode](); modeBuilt[mode] = true; }
    }

    Array.prototype.forEach.call(document.querySelectorAll('input[name="tlMode"]'), function (radio) {
        radio.addEventListener('change', function () { if (radio.checked) { showMode(radio.value); } });
    });

    // ------------------------------------------------------------------ transcript: mark as heard

    var markDoneBtn = byId('tlMarkDone');
    if (markDoneBtn) {
        markDoneBtn.addEventListener('click', function () {
            if (markDoneBtn.disabled) { return; }
            markDoneBtn.disabled = true;
            setButtonContent(markDoneBtn, 'fa-solid fa-circle-check', 'Đã nghe xong lời thoại');
            setStep('transcript', true);
            saveProgress({ transcriptCompleted: true });
        });
    }

    // ------------------------------------------------------------------ practice modes: shared helpers

    var TYPING_ATTRS = { lang: 'en', autocomplete: 'off', autocapitalize: 'none', autocorrect: 'off', spellcheck: 'false' };

    function applyTypingAttrs(input) {
        Object.keys(TYPING_ATTRS).forEach(function (name) { input.setAttribute(name, TYPING_ATTRS[name]); });
    }

    function playLine(order) {
        var position = timedPosition(order);
        if (position >= 0) { playTimed(position, true); }
    }

    function listenButton(order) {
        var button = Sets.el('button', 'btn btn-outline-primary btn-sm tl-listen');
        button.type = 'button';
        setButtonContent(button, 'fa-solid fa-play', 'Nghe câu này');
        if (timedPosition(order) < 0) {
            button.disabled = true;
            button.title = 'Câu này không có mốc thời gian';
        } else {
            button.addEventListener('click', function () { playLine(order); });
        }
        return button;
    }

    function srOnly(text) { return Sets.el('span', 'visually-hidden', text); }

    var RESULT_ICON = { correct: 'fa-solid fa-circle-check', near: 'fa-solid fa-circle-exclamation', wrong: 'fa-solid fa-circle-xmark' };

    function summaryTile(label, value, hue) {
        var tile = Sets.el('div', 'tl-summary__tile k-pillow tl-hue-' + hue);
        tile.appendChild(Sets.el('span', 'tl-summary__value', String(value)));
        tile.appendChild(Sets.el('span', 'tl-summary__label', label));
        return tile;
    }

    // ------------------------------------------------------------------ practice mode: dictation

    modeHooks.dictation = function () {
        var panel = byId('tlModePanel-dictation');
        var statuses = lines.map(function () { return null; });
        var feedback = [];
        var inputs = [];

        var head = Sets.el('div', 'tl-dict__head');
        head.appendChild(Sets.el('p', 'tl-dict__rule', 'Nghe từng câu rồi gõ lại.'));
        var ring = Sets.el('div', 'tl-ring');
        ring.setAttribute('role', 'img');
        var ringText = Sets.el('span', 'tl-ring__text', '0%');
        ring.appendChild(ringText);
        head.appendChild(ring);
        panel.appendChild(head);
        panel.appendChild(Sets.el('p', 'tl-hint', 'Enter: kiểm tra · Shift+Enter: xuống dòng · Alt+R: nghe lại câu · Tab: chuyển sang ô kế tiếp.'));

        var list = Sets.el('div', 'tl-dict__list');
        var summary = Sets.el('div', 'tl-summary k-panel');
        summary.hidden = true;
        summary.setAttribute('role', 'status');

        function refresh() {
            var checked = statuses.filter(Boolean).length;
            var percent = lines.length ? Math.round(checked * 100 / lines.length) : 0;
            ring.style.setProperty('--p', percent + '%');
            ring.setAttribute('aria-label', percent + '% số câu đã kiểm tra');
            ringText.textContent = percent + '%';
            if (lines.length && checked === lines.length) {
                var count = function (name) { return statuses.filter(function (s) { return s === name; }).length; };
                summary.textContent = '';
                var tiles = Sets.el('div', 'tl-summary__tiles');
                tiles.appendChild(summaryTile('Đúng', count('correct'), 'success'));
                tiles.appendChild(summaryTile('Gần đúng', count('near'), 'warning'));
                tiles.appendChild(summaryTile('Sai', count('wrong'), 'danger'));
                summary.appendChild(tiles);
                var again = Sets.el('button', 'btn btn-outline-primary');
                again.type = 'button';
                setButtonContent(again, 'fa-solid fa-rotate-right', 'Làm lại');
                again.addEventListener('click', reset);
                summary.appendChild(again);
                summary.hidden = false;
            } else {
                summary.hidden = true;
            }
        }

        function showResult(i, result, revealed) {
            var box = feedback[i];
            box.textContent = '';
            var status = revealed ? 'wrong' : result.status;
            box.className = 'tl-feedback tl-feedback--' + status;
            var title = Sets.el('p', 'tl-feedback__title');
            title.appendChild(Sets.icon(RESULT_ICON[status]));
            title.appendChild(document.createTextNode(' ' + (revealed ? 'Đáp án' : status === 'correct' ? 'Chính xác!' : status === 'near' ? 'Gần đúng' : 'Chưa đúng')));
            box.appendChild(title);
            if (!revealed) {
                var words = Sets.el('p', 'tl-feedback__words');
                words.setAttribute('lang', 'en');
                result.words.forEach(function (w, k) {
                    if (k) { words.appendChild(document.createTextNode(' ')); }
                    words.appendChild(Sets.el('span', 'tl-w tl-w--' + w.state, w.text));
                });
                box.appendChild(words);
                if (status !== 'correct') {
                    box.appendChild(Sets.el('p', 'tl-feedback__legend', 'Từ gạch chấm: gần đúng. Từ gạch liền: sai hoặc thiếu.'));
                }
            }
            var answer = Sets.el('p', 'tl-feedback__answer');
            answer.appendChild(document.createTextNode('Câu đúng: '));
            var sentence = Sets.el('span', null, lines[i].text);
            sentence.setAttribute('lang', 'en');
            answer.appendChild(sentence);
            box.appendChild(answer);
            statuses[i] = status;
            refresh();
        }

        function check(i) { showResult(i, core.gradeDictation(inputs[i].value, lines[i].text), false); }

        function reset() {
            inputs.forEach(function (input, i) { input.value = ''; feedback[i].textContent = ''; feedback[i].className = 'tl-feedback'; statuses[i] = null; });
            refresh();
            if (inputs[0]) { inputs[0].focus(); }
        }

        lines.forEach(function (line, i) {
            var card = Sets.el('div', 'tl-dcard k-panel');
            var top = Sets.el('div', 'tl-dcard__top');
            top.appendChild(Sets.el('span', 'tl-dcard__num', String(i + 1)));
            top.appendChild(Sets.el('span', 'tl-dcard__speaker tct-badge tct-badge--primary', line.speaker || ''));
            top.appendChild(listenButton(i));
            card.appendChild(top);

            var label = Sets.el('label', 'visually-hidden', 'Gõ lại câu ' + (i + 1));
            var input = document.createElement('textarea');
            input.className = 'form-control tl-dcard__input';
            input.id = 'tlDict-' + i;
            input.rows = 2;
            applyTypingAttrs(input);
            label.setAttribute('for', input.id);
            input.addEventListener('keydown', function (event) {
                if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); check(i); }
                else if (event.altKey && event.code === 'KeyR') { event.preventDefault(); playLine(i); }
            });
            inputs.push(input);
            card.appendChild(label);
            card.appendChild(input);

            var actions = Sets.el('div', 'tl-dcard__actions');
            var checkBtn = Sets.el('button', 'btn btn-primary btn-sm');
            checkBtn.type = 'button';
            checkBtn.textContent = 'Kiểm tra';
            checkBtn.addEventListener('click', function () { check(i); });
            var showBtn = Sets.el('button', 'btn btn-outline-primary btn-sm');
            showBtn.type = 'button';
            showBtn.textContent = 'Xem đáp án';
            showBtn.addEventListener('click', function () { showResult(i, null, true); });
            actions.appendChild(checkBtn);
            actions.appendChild(showBtn);
            card.appendChild(actions);

            var box = Sets.el('div', 'tl-feedback');
            box.setAttribute('aria-live', 'polite');
            feedback.push(box);
            card.appendChild(box);
            list.appendChild(card);
        });

        panel.appendChild(list);
        panel.appendChild(summary);
        refresh();
    };

    // ------------------------------------------------------------------ practice mode: fill in the blanks

    modeHooks.fill = function () {
        var panel = byId('tlModePanel-fill');
        var retry = 0;

        function render() {
            panel.textContent = '';
            var rng = core.mulberry32(Number(config.lessonId) * 1000 + retry);
            var blanks = [];

            panel.appendChild(Sets.el('p', 'tl-hint', 'Điền từ còn thiếu trong mỗi câu. Enter: kiểm tra ô này và sang ô kế tiếp.'));
            var list = Sets.el('div', 'tl-dict__list');
            var summary = Sets.el('div', 'tl-summary k-panel');
            summary.hidden = true;
            summary.setAttribute('role', 'status');

            function paint(blank, status) {
                blank.status = status;
                blank.wrap.className = 'tl-blank tl-blank--' + status;
                blank.mark.textContent = '';
                blank.mark.appendChild(Sets.icon(RESULT_ICON[status]));
                blank.mark.appendChild(srOnly(status === 'correct' ? 'Đúng' : status === 'near' ? 'Gần đúng' : 'Sai'));
                blank.answerEl.textContent = status === 'correct' ? '' : 'Đáp án: ' + blank.answer;
                blank.answerEl.hidden = status === 'correct';
            }

            function refreshSummary() {
                if (!blanks.length || blanks.some(function (b) { return !b.status; })) { summary.hidden = true; return; }
                var count = function (name) { return blanks.filter(function (b) { return b.status === name; }).length; };
                summary.textContent = '';
                var tiles = Sets.el('div', 'tl-summary__tiles');
                tiles.appendChild(summaryTile('Đúng', count('correct'), 'success'));
                tiles.appendChild(summaryTile('Gần đúng', count('near'), 'warning'));
                tiles.appendChild(summaryTile('Sai', count('wrong'), 'danger'));
                summary.appendChild(tiles);
                summary.hidden = false;
            }

            function checkBlank(blank) { paint(blank, core.gradeBlank(blank.input.value, blank.answer)); refreshSummary(); }

            lines.forEach(function (line, i) {
                var card = Sets.el('div', 'tl-dcard k-panel tl-fill__line');
                var top = Sets.el('div', 'tl-dcard__top');
                top.appendChild(Sets.el('span', 'tl-dcard__num', String(i + 1)));
                var sentence = Sets.el('p', 'tl-fill__sentence');
                sentence.setAttribute('lang', 'en');
                var picked = core.pickBlanks(line.text, rng);
                var k = 0;
                picked.parts.forEach(function (part) {
                    if (part.type === 'text') { sentence.appendChild(document.createTextNode(part.value)); return; }
                    k += 1;
                    var wrap = Sets.el('span', 'tl-blank');
                    var input = document.createElement('input');
                    input.type = 'text';
                    input.className = 'form-control tl-blank__input';
                    input.size = Math.max(3, part.answer.length + 1);
                    input.setAttribute('aria-label', 'Từ còn thiếu ' + k + ' của câu ' + (i + 1));
                    applyTypingAttrs(input);
                    var mark = Sets.el('span', 'tl-blank__mark');
                    var answerEl = Sets.el('span', 'tl-blank__answer');
                    answerEl.hidden = true;
                    wrap.appendChild(input);
                    wrap.appendChild(mark);
                    wrap.appendChild(answerEl);
                    var blank = { input: input, wrap: wrap, mark: mark, answerEl: answerEl, answer: part.answer, status: null };
                    blanks.push(blank);
                    input.addEventListener('keydown', function (event) {
                        if (event.key !== 'Enter' || event.isComposing) { return; }
                        event.preventDefault();
                        checkBlank(blank);
                        var next = blanks[blanks.indexOf(blank) + 1];
                        if (next) { next.input.focus(); }
                    });
                    sentence.appendChild(wrap);
                });
                top.appendChild(sentence);
                top.appendChild(listenButton(i));
                card.appendChild(top);
                list.appendChild(card);
            });

            panel.appendChild(list);

            if (!blanks.length) {
                panel.appendChild(Sets.el('p', 'tl-hint', 'Chưa chọn được từ nào để điền trong bài này.'));
            } else {
                var footer = Sets.el('div', 'tl-footer-row tl-fill__footer');
                var retryBtn = Sets.el('button', 'btn btn-outline-primary');
                retryBtn.type = 'button';
                setButtonContent(retryBtn, 'fa-solid fa-rotate-right', 'Làm lại');
                retryBtn.addEventListener('click', function () { retry += 1; render(); var first = panel.querySelector('.tl-blank__input'); if (first) { first.focus(); } });
                var allBtn = Sets.el('button', 'btn btn-primary');
                allBtn.type = 'button';
                setButtonContent(allBtn, 'fa-solid fa-check', 'Kiểm tra tất cả');
                allBtn.addEventListener('click', function () { blanks.forEach(function (b) { paint(b, core.gradeBlank(b.input.value, b.answer)); }); refreshSummary(); });
                footer.appendChild(retryBtn);
                footer.appendChild(allBtn);
                panel.appendChild(footer);
            }
            panel.appendChild(summary);
        }

        render();
    };

    // ------------------------------------------------------------------ vocabulary tab

    function swapBrokenImage(img) {
        if (!img || !img.parentNode || !img.classList.contains('tl-thumb')) { return; }
        var holder = Sets.el('span', 'tl-thumb--empty');
        holder.setAttribute('aria-hidden', 'true');
        holder.appendChild(Sets.icon('fa-solid fa-image'));
        img.parentNode.replaceChild(holder, img);
    }
    page.addEventListener('error', function (event) {
        if (event.target && event.target.tagName === 'IMG') { swapBrokenImage(event.target); }
    }, true);
    Array.prototype.forEach.call(page.querySelectorAll('img.tl-thumb'), function (img) {
        if (img.complete && img.naturalWidth === 0) { swapBrokenImage(img); }
    });

    if (Sets.canSpeak()) {
        Array.prototype.forEach.call(document.querySelectorAll('[data-tl-speak]'), function (button) {
            button.hidden = false;
            button.addEventListener('click', function () { Sets.speak(button.getAttribute('data-tl-speak')); });
        });
    }

    var vocabDoneBtn = byId('tlVocabDone');
    if (vocabDoneBtn) {
        vocabDoneBtn.addEventListener('click', function () {
            if (vocabDoneBtn.disabled) { return; }
            vocabDoneBtn.disabled = true;
            setButtonContent(vocabDoneBtn, 'fa-solid fa-circle-check', 'Đã học xong từ vựng');
            setStep('vocab', true);
            saveProgress({ vocabReviewed: true });
        });
    }

    // ------------------------------------------------------------------ quiz tab

    var quizForm = byId('tlQuizForm');
    var quizResult = byId('tlQuizResult');

    if (quizForm) {
        var fieldsets = Array.prototype.slice.call(quizForm.querySelectorAll('.tl-q'));
        var quizSubmit = byId('tlQuizSubmit');
        var quizCount = byId('tlQuizCount');
        var graded = false;

        var chosenFor = function (fs) { var r = fs.querySelector('input:checked'); return r ? r.value : null; };
        var answeredCount = function () { return fieldsets.filter(function (fs) { return chosenFor(fs); }).length; };

        function syncQuizCount() {
            if (quizCount) { quizCount.textContent = 'Đã chọn ' + answeredCount() + '/' + fieldsets.length; }
        }

        function clearMissing(fs) {
            fs.classList.remove('tl-q--missing');
            var note = fs.querySelector('.tl-q__missing');
            if (note) { note.hidden = true; }
        }

        quizForm.addEventListener('change', function (event) {
            var fs = event.target.closest('.tl-q');
            if (fs) { clearMissing(fs); }
            syncQuizCount();
        });

        function resultRing(percent, large) {
            var ring = Sets.el('div', 'tl-ring' + (large ? ' tl-ring--lg' : ''));
            ring.setAttribute('role', 'img');
            ring.setAttribute('aria-label', 'Điểm ' + percent + '%');
            ring.style.setProperty('--p', percent + '%');
            ring.appendChild(Sets.el('span', 'tl-ring__text', percent + '%'));
            return ring;
        }

        function resetQuiz() {
            graded = false;
            fieldsets.forEach(function (fs) {
                clearMissing(fs);
                Array.prototype.forEach.call(fs.querySelectorAll('input'), function (input) { input.checked = false; input.disabled = false; });
                Array.prototype.forEach.call(fs.querySelectorAll('.tl-option'), function (option) {
                    option.classList.remove('tl-option--correct', 'tl-option--wrong');
                    var mark = option.querySelector('.tl-option__mark');
                    if (mark) { mark.textContent = ''; }
                });
                var explain = fs.querySelector('.tl-q__explain');
                if (explain) { explain.textContent = ''; explain.hidden = true; }
            });
            if (quizResult) { quizResult.hidden = true; quizResult.textContent = ''; }
            if (quizSubmit) { quizSubmit.disabled = false; }
            syncQuizCount();
            var first = quizForm.querySelector('input');
            if (first) { first.focus(); }
        }

        function retryButton() {
            var button = Sets.el('button', 'btn btn-outline-primary');
            button.type = 'button';
            setButtonContent(button, 'fa-solid fa-rotate-right', 'Làm lại quiz');
            button.addEventListener('click', resetQuiz);
            return button;
        }

        function showStoredScore(score) {
            quizResult.textContent = '';
            quizResult.appendChild(resultRing(score, true));
            var body = Sets.el('div', 'tl-result__body');
            body.appendChild(Sets.el('p', 'tl-result__title', 'Điểm cao nhất: ' + score + '%'));
            body.appendChild(retryButton());
            quizResult.appendChild(body);
            quizResult.hidden = false;
        }

        function showResult(data) {
            graded = true;
            var byQuestion = {};
            (data.answers || []).forEach(function (a) { byQuestion[a.questionId] = a; });
            fieldsets.forEach(function (fs) {
                var answer = byQuestion[fs.getAttribute('data-qid')];
                Array.prototype.forEach.call(fs.querySelectorAll('input'), function (input) { input.disabled = true; });
                if (!answer) { return; }
                Array.prototype.forEach.call(fs.querySelectorAll('.tl-option'), function (option) {
                    var key = option.getAttribute('data-key');
                    var mark = option.querySelector('.tl-option__mark');
                    var text = null, icon = null, cls = null;
                    if (key === answer.correctAnswer) { cls = 'tl-option--correct'; icon = 'fa-solid fa-circle-check'; text = 'Đáp án đúng'; }
                    else if (key === answer.userAnswer) { cls = 'tl-option--wrong'; icon = 'fa-solid fa-circle-xmark'; text = 'Bạn chọn'; }
                    if (cls && mark) {
                        option.classList.add(cls);
                        mark.textContent = '';
                        mark.appendChild(Sets.icon(icon));
                        mark.appendChild(document.createTextNode(' ' + text));
                    }
                });
                var explain = fs.querySelector('.tl-q__explain');
                if (explain) {
                    if (answer.explanation) { explain.textContent = 'Giải thích: ' + answer.explanation; explain.hidden = false; }
                    else { explain.textContent = ''; explain.hidden = true; }
                }
            });

            quizResult.textContent = '';
            quizResult.appendChild(resultRing(data.scorePercent, true));
            var body = Sets.el('div', 'tl-result__body');
            body.appendChild(Sets.el('p', 'tl-result__title', 'Đúng ' + data.correctCount + '/' + data.totalQuestions));
            body.appendChild(Sets.el('p', 'tl-result__remark', data.passed ? 'Đạt! Bạn đã vượt qua bài quiz.' : 'Chưa đạt. Nghe lại rồi thử lần nữa nhé.'));
            if (data.bestScorePercent !== null && data.bestScorePercent !== undefined) {
                body.appendChild(Sets.el('p', 'tl-result__best', 'Điểm cao nhất: ' + data.bestScorePercent + '%'));
            }
            body.appendChild(retryButton());
            quizResult.appendChild(body);
            quizResult.hidden = false;
            quizResult.focus();
            if (data.bestScorePercent !== null && data.bestScorePercent !== undefined) { setStep('quiz', true); }
        }

        quizForm.addEventListener('submit', function (event) {
            event.preventDefault();
            if (!quizSubmit || quizSubmit.disabled) { return; }

            var missing = fieldsets.filter(function (fs) { return !chosenFor(fs); });
            if (missing.length) {
                missing.forEach(function (fs) {
                    fs.classList.add('tl-q--missing');
                    var note = fs.querySelector('.tl-q__missing');
                    if (note) { note.hidden = false; }
                });
                var firstInput = missing[0].querySelector('input');
                if (firstInput) { firstInput.focus(); }
                announce('tlQuizLive', 'Còn ' + missing.length + ' câu chưa trả lời.');
                return;
            }

            var answers = {};
            fieldsets.forEach(function (fs) { answers[fs.getAttribute('data-qid')] = chosenFor(fs); });
            quizSubmit.disabled = true;
            setButtonContent(quizSubmit, 'fa-solid fa-spinner fa-spin', 'Đang chấm…');
            Sets.fetchJson(urls.evaluate, { method: 'POST', body: { lessonId: config.lessonId, answers: answers } }).then(function (response) {
                setButtonContent(quizSubmit, 'fa-solid fa-paper-plane', 'Nộp bài');
                showResult(response && response.data ? response.data : {});
            }).catch(function () {
                quizSubmit.disabled = false;
                setButtonContent(quizSubmit, 'fa-solid fa-paper-plane', 'Nộp bài');
                Sets.toast('Không chấm được bài. Vui lòng thử lại.', { type: 'danger' });
            });
        });

        // a finished quiz shows the stored best score; the questions stay answerable again
        if (quizResult && progress.quizCompleted && progress.quizScore !== null && progress.quizScore !== undefined) {
            showStoredScore(progress.quizScore);
        }
        syncQuizCount();
    }

    // ------------------------------------------------------------------ quiz generation with AI (own lessons, or Admin on shared ones)

    var generateBtn = byId('tlGenerateQuiz');
    if (generateBtn) {
        generateBtn.addEventListener('click', function () {
            Sets.pending(generateBtn, true, 'Đang soạn câu hỏi…');
            var form = new FormData();
            form.append('lessonId', String(config.lessonId));
            Sets.fetchJson(urls.generateQuiz, { method: 'POST', body: form }).then(function () {
                Sets.toast('Đã tạo quiz.');
                window.setTimeout(function () { window.location.reload(); }, 600);
            }).catch(function (error) {
                Sets.pending(generateBtn, false);
                var data = error && error.data;
                Sets.toast((data && data.message) || 'Không tạo được quiz. Vui lòng thử lại.', { type: 'danger' });
            });
        });
    }
}());
