/*
 * Vocabulary home: client-side course filters (search, level, status).
 * The state lives in the URL (?q=&level=&status=) and is written with history.replaceState.
 * Reads the server-rendered cards (data-level / data-status / data-name); it never builds markup from data.
 */
(function () {
    'use strict';

    var core = typeof TctVocabulary !== 'undefined' && TctVocabulary.core;
    var cards = Array.prototype.slice.call(document.querySelectorAll('[data-tv-course]'));
    if (!core || cards.length === 0) { return; }

    var search = document.getElementById('tvSearch');
    var status = document.getElementById('tvResultStatus');
    var noMatch = document.getElementById('tvNoMatch');
    var clear = document.getElementById('tvClear');
    var collections = Array.prototype.slice.call(document.querySelectorAll('[data-tv-collection]'));
    var groups = {};
    Array.prototype.forEach.call(document.querySelectorAll('[data-tv-filter]'), function (group) {
        groups[group.getAttribute('data-tv-filter')] = group;
    });

    function allowed(name) {
        var group = groups[name];
        if (!group) { return ['']; }
        return Array.prototype.map.call(group.querySelectorAll('[data-value]'), function (b) { return b.getAttribute('data-value'); });
    }

    var items = cards.map(function (el) {
        return {
            el: el,
            li: el.closest('li') || el,
            level: el.getAttribute('data-level') || '',
            status: el.getAttribute('data-status') || '',
            name: el.getAttribute('data-name') || ''
        };
    });

    var state = { q: '', level: '', status: '' };

    function readUrl() {
        var params = new URLSearchParams(location.search);
        var q = (params.get('q') || '').slice(0, 80);
        var level = params.get('level') || '';
        var st = params.get('status') || '';
        state.q = q;
        state.level = allowed('level').indexOf(level) >= 0 ? level : '';
        state.status = allowed('status').indexOf(st) >= 0 ? st : '';
    }

    function writeUrl() {
        var params = new URLSearchParams();
        if (state.q) { params.set('q', state.q); }
        if (state.level) { params.set('level', state.level); }
        if (state.status) { params.set('status', state.status); }
        var query = params.toString();
        var url = location.pathname + (query ? '?' + query : '') + location.hash;
        try { history.replaceState(null, '', url); } catch (e) { /* the filters still work without URL state */ }
    }

    function syncControls() {
        if (search && search.value !== state.q) { search.value = state.q; }
        ['level', 'status'].forEach(function (name) {
            var group = groups[name];
            if (!group) { return; }
            Array.prototype.forEach.call(group.querySelectorAll('[data-value]'), function (button) {
                button.setAttribute('aria-pressed', button.getAttribute('data-value') === state[name] ? 'true' : 'false');
            });
        });
    }

    function apply() {
        var visible = core.filterCourses(items, { q: state.q, level: state.level, status: state.status });
        var shown = {};
        visible.forEach(function (item) { shown[items.indexOf(item)] = true; });
        items.forEach(function (item, index) { item.li.hidden = !shown[index]; });

        collections.forEach(function (section) {
            var lis = Array.prototype.slice.call(section.querySelectorAll('li'));
            var count = lis.filter(function (li) { return !li.hidden; }).length;
            var counter = section.querySelector('[data-tv-count]');
            if (counter) { counter.textContent = count + ' khóa'; }
            section.hidden = lis.length > 0 && count === 0;
        });

        var none = visible.length === 0;
        if (noMatch) { noMatch.hidden = !none; }
        if (status) { status.textContent = none ? 'Không có khóa học phù hợp.' : 'Hiển thị ' + visible.length + ' khóa học.'; }
        syncControls();
        writeUrl();
    }

    if (search) {
        search.addEventListener('input', function () {
            state.q = search.value.slice(0, 80);
            apply();
        });
    }

    Object.keys(groups).forEach(function (name) {
        groups[name].addEventListener('click', function (event) {
            var button = event.target.closest('[data-value]');
            if (!button || !groups[name].contains(button)) { return; }
            state[name] = button.getAttribute('data-value') || '';
            apply();
        });
    });

    if (clear) {
        clear.addEventListener('click', function () {
            state.q = '';
            state.level = '';
            state.status = '';
            apply();
            if (search) { search.focus(); }
        });
    }

    readUrl();
    apply();
}());
