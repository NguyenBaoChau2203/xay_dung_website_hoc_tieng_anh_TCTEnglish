/*
 * Vocabulary course page: ARIA tabs (lessons / all words), word filters, example disclosure and speech.
 * Server-rendered markup only; nothing is built from data with HTML strings.
 * Tabs: roving tabindex, arrows / Home / End, state in the URL hash (#bai-hoc, #tat-ca-tu).
 */
(function () {
    'use strict';

    var page = document.querySelector('.tv-page--course');
    if (!page) { return; }

    // ------------------------------------------------------------------ tabs
    var tabs = Array.prototype.slice.call(page.querySelectorAll('[role="tab"]'));
    var byHash = {};
    tabs.forEach(function (tab) { byHash[tab.getAttribute('data-hash')] = tab; });

    function panelOf(tab) { return document.getElementById(tab.getAttribute('aria-controls')); }

    function selectTab(tab, focus, writeHash) {
        tabs.forEach(function (other) {
            var on = other === tab;
            other.setAttribute('aria-selected', on ? 'true' : 'false');
            other.tabIndex = on ? 0 : -1;
            var panel = panelOf(other);
            if (panel) { panel.hidden = !on; }
        });
        page.setAttribute('data-active-tab', tab.getAttribute('data-tv-tab'));
        if (focus) { tab.focus(); }
        if (writeHash) {
            try { history.replaceState(null, '', location.pathname + location.search + '#' + tab.getAttribute('data-hash')); } catch (e) { /* hash is optional */ }
        }
    }

    tabs.forEach(function (tab, index) {
        tab.addEventListener('click', function () { selectTab(tab, false, true); });
        tab.addEventListener('keydown', function (event) {
            var target = null;
            if (event.key === 'ArrowRight') { target = tabs[(index + 1) % tabs.length]; }
            else if (event.key === 'ArrowLeft') { target = tabs[(index - 1 + tabs.length) % tabs.length]; }
            else if (event.key === 'Home') { target = tabs[0]; }
            else if (event.key === 'End') { target = tabs[tabs.length - 1]; }
            if (target) {
                event.preventDefault();
                selectTab(target, true, true);
            }
        });
    });

    var fromHash = byHash[(location.hash || '').replace(/^#/, '')];
    if (fromHash) { selectTab(fromHash, false, false); }

    // ------------------------------------------------------------------ words
    var core = typeof TctVocabulary !== 'undefined' && TctVocabulary.core;
    var rows = Array.prototype.slice.call(page.querySelectorAll('[data-tv-word]'));
    var search = document.getElementById('tvWordSearch');
    var status = document.getElementById('tvWordStatus');
    var noMatch = document.getElementById('tvWordNoMatch');
    var table = document.getElementById('tvWordTable');
    var stateGroup = page.querySelector('[data-tv-filter="state"]');
    var chip = document.getElementById('tvTopicChip');
    var chipRemove = document.getElementById('tvTopicRemove');

    if (core && rows.length > 0) {
        var items = rows.map(function (el) {
            return {
                el: el,
                term: el.getAttribute('data-term') || '',
                definition: el.getAttribute('data-definition') || '',
                state: el.getAttribute('data-state') || '',
                due: el.getAttribute('data-due') === 'true',
                lesson: Number(el.getAttribute('data-lesson')) || 0,
                topic: el.getAttribute('data-topic') || ''
            };
        });
        var filter = { q: '', state: '', topic: (page.getAttribute('data-topic-filter') || '').trim() };

        var apply = function () {
            var visible = core.filterWords(items, filter);
            var shown = {};
            visible.forEach(function (item) { shown[items.indexOf(item)] = true; });
            items.forEach(function (item, index) { item.el.hidden = !shown[index]; });
            var none = visible.length === 0;
            if (noMatch) { noMatch.hidden = !none; }
            if (table) { table.hidden = none; }
            if (status) { status.textContent = none ? 'Không có từ phù hợp.' : 'Hiển thị ' + visible.length + ' từ.'; }
            if (stateGroup) {
                Array.prototype.forEach.call(stateGroup.querySelectorAll('[data-value]'), function (button) {
                    button.setAttribute('aria-pressed', button.getAttribute('data-value') === filter.state ? 'true' : 'false');
                });
            }
        };

        if (search) {
            search.addEventListener('input', function () {
                filter.q = search.value.slice(0, 80);
                apply();
            });
        }

        if (stateGroup) {
            stateGroup.addEventListener('click', function (event) {
                var button = event.target.closest('[data-value]');
                if (!button) { return; }
                filter.state = button.getAttribute('data-value') || '';
                apply();
            });
        }

        if (chipRemove) {
            chipRemove.addEventListener('click', function () {
                filter.topic = '';
                if (chip) { chip.hidden = true; }
                var params = new URLSearchParams(location.search);
                params.delete('topic');
                try { history.replaceState(null, '', location.pathname + '?' + params.toString() + location.hash); } catch (e) { /* optional */ }
                apply();
                if (search) { search.focus(); }
            });
        }

        apply();
    }

    // ------------------------------------------------------------------ example disclosure
    page.addEventListener('click', function (event) {
        var toggle = event.target.closest('[data-tv-example]');
        if (!toggle) { return; }
        var panel = document.getElementById(toggle.getAttribute('aria-controls'));
        if (!panel) { return; }
        var open = toggle.getAttribute('aria-expanded') !== 'true';
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        toggle.textContent = open ? 'Ẩn ví dụ' : 'Xem ví dụ';
        panel.hidden = !open;
    });

    // ------------------------------------------------------------------ speech (only when the browser can speak)
    var tct = typeof TctSets !== 'undefined' ? TctSets : null;
    var canSpeak = !!(tct && tct.canSpeak && tct.canSpeak());
    Array.prototype.forEach.call(page.querySelectorAll('[data-tv-speak]'), function (button) {
        if (!canSpeak) { return; }
        button.hidden = false;
        button.addEventListener('click', function () { tct.speak(button.getAttribute('data-tv-speak')); });
    });
}());
