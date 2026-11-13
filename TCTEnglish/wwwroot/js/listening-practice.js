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

    // ------------------------------------------------------------------ sections below are added phase by phase
}());
