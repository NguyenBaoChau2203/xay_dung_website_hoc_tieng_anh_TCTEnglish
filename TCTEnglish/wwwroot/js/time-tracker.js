/**
 * Activity tracker. Heartbeats go to the authenticated checkpoint API.
 * The antiforgery token stays in a header or form body, never the query string.
 */
(function () {
    'use strict';

    var scriptEl = document.currentScript || document.querySelector('script[data-feature]');
    var featureName = scriptEl ? scriptEl.getAttribute('data-feature') : null;
    var modeName = scriptEl ? scriptEl.getAttribute('data-mode') : null;
    var session = null;
    var pendingStart = false;
    var generation = 0;
    var lastInteraction = Date.now();
    var timer = null;

    function token() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function post(url, body) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token()
            },
            body: JSON.stringify(body),
            keepalive: true
        });
    }

    function noteInteraction() {
        if (session && Date.now() - lastInteraction >= 90000) stop();
        lastInteraction = Date.now();
        if (document.visibilityState === 'visible' && !session && !pendingStart) {
            start();
        }
    }

    function sample() {
        if (!session) return;
        var now = Date.now();
        var until = Math.min(now, lastInteraction + 90000);
        session.activeMilliseconds += Math.max(0, until - session.lastSample);
        session.lastSample = now;
    }

    function flush(endSession) {
        if (!session) return;
        var current = session;
        current.sequence += 1;
        current.lastFlush = Date.now();
        post('/api/LearningApi/activity-checkpoint', {
            sessionId: current.id,
            sequence: current.sequence,
            cumulativeActiveSeconds: Math.floor(current.activeMilliseconds / 1000),
            leaseToken: current.leaseToken,
            visible: true,
            endSession: Boolean(endSession)
        }).then(function (response) {
            if (!response.ok && session === current) {
                generation += 1;
                if (timer) window.clearInterval(timer);
                timer = null;
                session = null;
            }
        }).catch(function () { /* A later cumulative checkpoint can cover a lost response. */ });
    }

    function tick() {
        if (!session) return;
        sample();
        if (document.visibilityState !== 'visible' || Date.now() - lastInteraction >= 90000) {
            stop();
        } else if (Date.now() - session.lastFlush >= 30000) {
            flush(false);
        }
    }

    function start() {
        if (session || pendingStart || !featureName || !token() || document.visibilityState !== 'visible') return;
        pendingStart = true;
        var expectedGeneration = ++generation;
        post('/api/LearningApi/activity-session', { feature: featureName, mode: modeName })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (payload) {
                if (expectedGeneration !== generation) return;
                pendingStart = false;
                if (!payload || document.visibilityState !== 'visible') return;
                session = {
                    id: payload.sessionId, leaseToken: payload.leaseToken, sequence: 0,
                    activeMilliseconds: 0, lastSample: Date.now(), lastFlush: Date.now()
                };
                timer = window.setInterval(tick, 1000);
            })
            .catch(function () { if (expectedGeneration === generation) pendingStart = false; });
    }

    function stop() {
        generation += 1;
        pendingStart = false;
        if (timer) window.clearInterval(timer);
        timer = null;
        if (session) { sample(); flush(true); }
        session = null;
    }

    ['pointerdown', 'keydown', 'touchstart'].forEach(function (name) {
        document.addEventListener(name, noteInteraction, { passive: true });
    });
    document.addEventListener('play', function (event) {
        if (event.isTrusted) noteInteraction();
    }, true);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'hidden') stop();
    });
    window.addEventListener('pagehide', stop);
    window.addEventListener('pageshow', function (event) { if (event.persisted) stop(); });

    window.TimeTracker = {
        start: function (feature) {
            featureName = feature;
            stop();
            noteInteraction();
        },
        stop: stop
    };
})();
