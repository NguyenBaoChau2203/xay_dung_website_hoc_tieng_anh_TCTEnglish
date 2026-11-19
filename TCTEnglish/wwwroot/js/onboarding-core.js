/*
 * First-run onboarding: pure logic (no DOM, no network). Loaded before onboarding.js and tested with node.
 * Limits mirror the server (OnboardingCatalog): goals 1-3, topics 3-8, level A1-B2, pace 5/10/15/20.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) { module.exports = factory(); }
    else { root.TctOnboardingCore = factory(); }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var MAX_GOALS = 3;
    var MIN_TOPICS = 3;
    var MAX_TOPICS = 8;
    var PREVIEW_FIRST = 12;
    var LEVELS = ['A1', 'A2', 'B1', 'B2'];
    var PACES = [5, 10, 15, 20];

    /**
     * Adds the key when it is not selected, removes it when it is. A new key beyond the limit is refused (never deselects
     * another one): the caller shows the hint when blocked is true. The order of selection is kept.
     */
    function toggleLimited(list, key, max) {
        var index = list.indexOf(key);
        if (index >= 0) {
            return { list: list.slice(0, index).concat(list.slice(index + 1)), blocked: false };
        }
        if (list.length >= max) {
            return { list: list.slice(), blocked: true };
        }
        return { list: list.concat([key]), blocked: false };
    }

    function defaultLevel(goals) {
        return goals.indexOf('beginner') >= 0 ? 'A1' : 'A2';
    }

    /** Days to meet every word at the chosen pace: ceil(total / pace). */
    function estimateDays(total, pace) {
        return pace > 0 ? Math.ceil(total / pace) : 0;
    }

    /** Whether the learner may leave this step forward. Steps: 1 goals, 2 topics, 3 level and pace. */
    function canContinue(step, state) {
        if (step === 1) { return state.goals.length >= 1 && state.goals.length <= MAX_GOALS; }
        if (step === 2) { return state.topics.length >= MIN_TOPICS && state.topics.length <= MAX_TOPICS; }
        if (step === 3) { return LEVELS.indexOf(state.level) >= 0 && PACES.indexOf(state.pace) >= 0; }
        return true;
    }

    /** The topics of a tab. "suggested" lists the topics that fit a chosen goal first, then the rest, each in catalogue order. */
    function tabTopics(topics, goals, tab) {
        function fits(topic) {
            return topic.goals.some(function (goal) { return goals.indexOf(goal) >= 0; });
        }
        if (tab === 'A') { return topics.filter(function (t) { return t.band === 'A1–A2'; }); }
        if (tab === 'B') { return topics.filter(function (t) { return t.band === 'B1–B2'; }); }
        if (tab === 'C') { return topics.filter(function (t) { return t.band === 'Giao tiếp'; }); }
        return topics.filter(fits).concat(topics.filter(function (t) { return !fits(t); }));
    }

    function isSuggested(topic, goals) {
        return topic.goals.some(function (goal) { return goals.indexOf(goal) >= 0; });
    }

    /** Cards of a deck that are kept: all minus the ones marked "Đã biết". known is an object keyed by card id. */
    function keptCards(deck, known) {
        return deck.cards.filter(function (card) { return !known[card.cardId]; });
    }

    /** Totals of the preview: decks that are switched on, their kept cards, the estimate and the footer sentence. */
    function previewTotals(decks, off, known, pace) {
        var on = decks.filter(function (deck) { return !off[deck.key]; });
        var total = 0;
        on.forEach(function (deck) { total += keptCards(deck, known).length; });
        var days = estimateDays(total, pace);
        return {
            decks: on.length,
            total: total,
            days: days,
            text: 'Tổng cộng ' + total + ' từ trong ' + on.length + ' bộ · khoảng ' + days + ' ngày với ' + pace + ' từ/ngày'
        };
    }

    /** At least one deck must stay on, and it must keep at least one card. */
    function canCreate(decks, off, known) {
        return decks.some(function (deck) { return !off[deck.key] && keptCards(deck, known).length > 0; });
    }

    /** The name for the greeting, as the dashboard shows it: the display name, never an e-mail, at most 40 characters. */
    function greetingName(fullName) {
        var text = (fullName || '').trim().replace(/\s+/g, ' ');
        if (!text || text.indexOf('@') >= 0) { return ''; }
        return text.length > 40 ? text.slice(0, 40).trim() : text;
    }

    function welcomeTitle(fullName) {
        var name = greetingName(fullName);
        return 'Chào mừng ' + (name || 'bạn') + ' đến với TCT English!';
    }

    /** A request id (RFC 4122 v4). Uses crypto.randomUUID when present, getRandomValues next, Math.random as the last resort. */
    function newRequestId(cryptoLike) {
        if (cryptoLike && typeof cryptoLike.randomUUID === 'function') { return cryptoLike.randomUUID(); }
        var bytes = new Array(16);
        for (var i = 0; i < 16; i++) { bytes[i] = Math.floor(Math.random() * 256); }
        if (cryptoLike && typeof cryptoLike.getRandomValues === 'function') {
            var arr = new Uint8Array(16);
            cryptoLike.getRandomValues(arr);
            bytes = Array.prototype.slice.call(arr);
        }
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        var hex = bytes.map(function (b) { return (b + 0x100).toString(16).slice(1); });
        return hex.slice(0, 4).join('') + '-' + hex.slice(4, 6).join('') + '-' + hex.slice(6, 8).join('') + '-' +
            hex.slice(8, 10).join('') + '-' + hex.slice(10, 16).join('');
    }

    function isRequestId(value) {
        return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value || '');
    }

    return {
        MAX_GOALS: MAX_GOALS,
        MIN_TOPICS: MIN_TOPICS,
        MAX_TOPICS: MAX_TOPICS,
        PREVIEW_FIRST: PREVIEW_FIRST,
        LEVELS: LEVELS,
        PACES: PACES,
        toggleLimited: toggleLimited,
        defaultLevel: defaultLevel,
        estimateDays: estimateDays,
        canContinue: canContinue,
        tabTopics: tabTopics,
        isSuggested: isSuggested,
        keptCards: keptCards,
        previewTotals: previewTotals,
        canCreate: canCreate,
        greetingName: greetingName,
        welcomeTitle: welcomeTitle,
        newRequestId: newRequestId,
        isRequestId: isRequestId
    };
});
