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

    // ------------------------------------------------------------------ sections below are added phase by phase
}());
