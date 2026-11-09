/*
 * Practice hub: pick a mode, a scope, a count, a word source, test options and the "Tính vào ôn tập" switch.
 * Defaults come from the last choices on this device, then ?set, then mode "learn". Stat lines and the
 * "Gợi ý cho bạn" badge read the device-local set stats.
 */
(function () {
    'use strict';

    var P = window.TctPractice;
    var core = P.core;
    var T = window.TctSets;
    var el = T.el;

    var form = document.getElementById('tpSetup');
    var cfgNode = document.getElementById('tpHubConfig');
    if (!form || !cfgNode) { return; }

    var cfg;
    try { cfg = JSON.parse(cfgNode.textContent); } catch (e) { return; }
    var setSelect = document.getElementById('tpSet');
    if (!setSelect) { return; }   // no words: the page shows an empty state instead of the setup panel

    function storage(name) { try { return window[name]; } catch (e) { return null; } }
    var store = core.createStore({ local: storage('localStorage'), session: storage('sessionStorage') });

    var SOURCES = [['all', 'Tất cả'], ['due', 'Đến hạn'], ['hard', 'Hay quên nhất'], ['recent', 'Mới thêm']];
    var cards = {};
    Array.prototype.forEach.call(form.querySelectorAll('[data-tp-card]'), function (card) { cards[card.getAttribute('data-tp-card')] = card; });

    var saved = store.loadSetup(cfg.userId) || {};
    var state = {
        mode: core.MODE_INFO[saved.mode] ? saved.mode : 'learn',
        scope: 'mine',
        n: typeof saved.n === 'number' ? saved.n : null,
        source: SOURCES.some(function (s) { return s[0] === saved.source; }) ? saved.source : 'all',
        srs: saved.srs === true,
        types: core.parseTestTypes(saved.types),
        ask: core.parseTestAsk(saved.ask)
    };
    var savedScope = typeof saved.scope === 'string' ? saved.scope : 'mine';
    function setExists(id) { return cfg.sets.some(function (s) { return String(s.id) === String(id); }); }
    if (cfg.preselectedSetId) { state.scope = String(cfg.preselectedSetId); }
    else if (savedScope === 'mine' || setExists(savedScope)) { state.scope = savedScope; }

    function currentSet() {
        for (var i = 0; i < cfg.sets.length; i++) { if (String(cfg.sets[i].id) === state.scope) { return cfg.sets[i]; } }
        return null;
    }

    function ensureCount() {
        var allowed = core.countsFor(state.mode);
        if (allowed.indexOf(state.n) < 0) { state.n = allowed[0]; }
    }

    // ------------------------------------------------------------- pills (radio / checkbox as buttons)
    function pill(type, name, value, label, checked, onChange, idPrefix) {
        var id = idPrefix + '-' + String(value);
        var wrap = el('span', 'tp-pill');
        var input = el('input', 'btn-check');
        input.type = type;
        input.name = name;
        input.value = value;
        input.id = id;
        input.checked = checked;
        input.addEventListener('change', function () { onChange(input); });
        var lab = el('label', 'btn btn-outline-primary', label);
        lab.htmlFor = id;
        wrap.appendChild(input);
        wrap.appendChild(lab);
        return { wrap: wrap, input: input };
    }

    function renderCounts() {
        ensureCount();
        var info = core.countUnit(state.mode);
        document.getElementById('tpCountLegend').textContent = info.legend;
        var box = document.getElementById('tpCountPills');
        box.textContent = '';
        core.countsFor(state.mode).forEach(function (n) {
            box.appendChild(pill('radio', 'n', String(n), n === 0 ? 'Cả bộ' : n + ' ' + info.unit, state.n === n, function () {
                state.n = n; renderSummary();
            }, 'tpN').wrap);
        });
    }

    function renderSources() {
        var box = document.getElementById('tpSourcePills');
        box.textContent = '';
        var set = currentSet();
        SOURCES.forEach(function (pair) {
            var label = pair[1];
            if (pair[0] === 'due' && set) { label += ' (' + set.dueCount + ')'; }
            box.appendChild(pill('radio', 'source', pair[0], label, state.source === pair[0], function () { state.source = pair[0]; renderSummary(); }, 'tpSrc').wrap);
        });
    }

    function renderTestOptions() {
        var isTest = state.mode === 'test';
        document.getElementById('tpTypesField').hidden = !isTest;
        document.getElementById('tpAskField').hidden = !isTest;
        if (!isTest) { return; }
        var types = document.getElementById('tpTypePills');
        types.textContent = '';
        core.TEST_TYPES.forEach(function (t) {
            var p = pill('checkbox', 'types', t, core.TEST_TYPE_LABEL[t], state.types.indexOf(t) >= 0, function (input) {
                if (!input.checked && state.types.length <= 1) { input.checked = true; return; }   // keep at least one type
                state.types = core.TEST_TYPES.filter(function (x) { return x === t ? input.checked : state.types.indexOf(x) >= 0; });
                renderSummary();
            }, 'tpType');
            types.appendChild(p.wrap);
        });
        var ask = document.getElementById('tpAskPills');
        ask.textContent = '';
        core.TEST_ASK.forEach(function (a) {
            ask.appendChild(pill('radio', 'ask', a, core.TEST_ASK_LABEL[a], state.ask === a, function () { state.ask = a; renderSummary(); }, 'tpAsk').wrap);
        });
    }

    function renderSrs() {
        var field = document.getElementById('tpSrsField');
        field.hidden = state.mode === 'match';   // Match never writes progress
        document.getElementById('tpSrs').checked = state.srs;
    }

    // ------------------------------------------------------------- stats and suggestion
    function formatBest(ms) { return core.formatMs(ms); }

    function renderStats() {
        var set = currentSet();
        var stats = set ? store.readStats(set.id) : {};
        var learn = typeof stats.learnPercent === 'number' ? Math.round(stats.learnPercent) : null;
        var test = typeof stats.testPercent === 'number' ? Math.round(stats.testPercent)
            : (typeof stats.quizPercent === 'number' ? Math.round(stats.quizPercent) : null);
        var pairs = state.mode === 'match' ? (state.n || 6) : 6;
        var best = set ? store.getMatchBest(cfg.userId, 'set:' + set.id, pairs) : null;
        var bestMs = best ? best.ms : (typeof stats.matchMs === 'number' && stats.matchMs > 0 ? stats.matchMs : null);

        function stat(mode, text, hue) {
            var node = form.querySelector('[data-tp-stat="' + mode + '"]');
            if (!node) { return; }
            node.className = 'tct-badge tp-stat ' + (hue ? 'tct-badge--' + hue : '');
            node.hidden = !text;
            node.textContent = text || '';
        }
        stat('learn', set && learn !== null ? 'Đã thuộc ' + learn + '%' : '', 'success');
        stat('test', set && test !== null ? 'Lần trước ' + test + '%' : '', 'primary');
        stat('match', set && bestMs !== null ? 'Kỷ lục ' + formatBest(bestMs) : '', 'warning');
        stat('flashcard', '', null);

        var suggested = set ? core.suggestMode({ learnPercent: learn, testPercent: test, matchMs: bestMs }) : null;
        Array.prototype.forEach.call(form.querySelectorAll('[data-tp-suggest]'), function (badge) {
            badge.hidden = badge.getAttribute('data-tp-suggest') !== suggested;
        });
    }

    // ------------------------------------------------------------- summary + submit
    function scopeName() {
        var set = currentSet();
        return set ? set.name : 'Tất cả bộ của tôi';
    }

    function renderSummary() {
        var info = core.countUnit(state.mode);
        var amount = state.n === 0 ? 'cả bộ' : state.n + ' ' + info.unit;
        document.getElementById('tpSummary').textContent = core.MODE_INFO[state.mode].label + ' · ' + scopeName() + ' · ' + amount;
    }

    function selectMode(mode) {
        state.mode = mode;
        Object.keys(cards).forEach(function (key) {
            cards[key].querySelector('input').checked = key === mode;
        });
        ensureCount();
        renderCounts();
        renderTestOptions();
        renderSrs();
        renderStats();
        renderSummary();
    }

    function buildUrl() {
        var params = [];
        params.push(state.scope === 'mine' ? 'scope=mine' : 'set=' + encodeURIComponent(state.scope));
        params.push('n=' + state.n);
        if (state.source !== 'all') { params.push('source=' + state.source); }
        if (state.srs && state.mode !== 'match') { params.push('srs=1'); }
        if (state.mode === 'test') {
            params.push('types=' + state.types.join(','));
            params.push('ask=' + state.ask);
        }
        params.push('from=hub');
        return '/Practice/' + state.mode + '?' + params.join('&');
    }

    Object.keys(cards).forEach(function (key) {
        cards[key].querySelector('input').addEventListener('change', function () { selectMode(key); });
    });

    setSelect.addEventListener('change', function () {
        state.scope = setSelect.value;
        renderSources();
        renderStats();
        renderSummary();
    });

    document.getElementById('tpSrs').addEventListener('change', function (event) { state.srs = event.target.checked; });

    form.addEventListener('submit', function (event) {
        event.preventDefault();
        store.saveSetup(cfg.userId, { mode: state.mode, scope: state.scope, n: state.n, source: state.source, srs: state.srs, types: state.types.join(','), ask: state.ask });
        window.location.href = buildUrl();
    });

    // ------------------------------------------------------------- boot
    setSelect.value = state.scope;
    if (setSelect.value !== state.scope) { state.scope = 'mine'; setSelect.value = 'mine'; }
    renderSources();
    selectMode(state.mode);
}());
