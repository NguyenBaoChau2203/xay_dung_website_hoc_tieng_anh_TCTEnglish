/*
 * Listening: pure logic (no DOM, no network). UMD: module.exports under Node, window.TctListening.core in the browser.
 * Every random step takes an injectable rng (default Math.random); mulberry32(seed) gives a deterministic one.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.TctListening = root.TctListening || {};
        root.TctListening.core = factory();
    }
}(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var LEVELS = ['A1', 'A2', 'B1', 'B2', 'C1'];
    var STATUSES = ['new', 'progress', 'done'];
    var STOP_WORDS = ['the', 'a', 'an', 'and', 'or', 'but', 'to', 'of', 'in', 'on', 'at', 'is', 'are', 'was', 'were', 'be',
        'it', 'i', 'you', 'he', 'she', 'we', 'they'];
    var NEAR_MIN_LETTERS = 4;
    var QUERY_MAX = 80;
    var TOPIC_MAX = 100;

    // ------------------------------------------------------------------ text helpers

    function mulberry32(seed) {
        var a = seed >>> 0;
        return function () {
            a = (a + 0x6D2B79F5) >>> 0;
            var t = a;
            t = Math.imul(t ^ (t >>> 15), t | 1);
            t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
            return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
        };
    }

    /** NFC, lower-case, curly apostrophes to ', punctuation removed except inner apostrophes and hyphens, spaces collapsed. */
    function normalize(text) {
        var s = String(text == null ? '' : text).normalize('NFC').toLowerCase();
        s = s.replace(/[‘’ʼ]/g, "'");
        s = s.replace(/[^\p{L}\p{N}'\-\s]/gu, ' ');
        return s.split(/\s+/)
            .map(function (w) { return w.replace(/^['\-]+|['\-]+$/g, ''); })
            .filter(Boolean)
            .join(' ');
    }

    function tokenize(text) {
        var n = normalize(text);
        return n ? n.split(' ') : [];
    }

    function levenshtein(a, b) {
        a = String(a); b = String(b);
        if (a === b) return 0;
        if (!a.length) return b.length;
        if (!b.length) return a.length;
        var prev = [];
        var i, j;
        for (j = 0; j <= b.length; j++) prev[j] = j;
        for (i = 1; i <= a.length; i++) {
            var cur = [i];
            for (j = 1; j <= b.length; j++) {
                cur[j] = Math.min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (a.charAt(i - 1) === b.charAt(j - 1) ? 0 : 1));
            }
            prev = cur;
        }
        return prev[b.length];
    }

    function isNear(typed, expected) {
        return expected.length >= NEAR_MIN_LETTERS && levenshtein(typed, expected) <= 1;
    }

    /** Answer words with their display text (original case, edge punctuation stripped) and their normalized form. */
    function answerTokens(answer) {
        var out = [];
        String(answer == null ? '' : answer).split(/\s+/).forEach(function (piece) {
            var shown = piece.replace(/^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$/gu, '');
            var norm = normalize(piece);
            if (!norm) return;
            var parts = norm.split(' ');
            if (parts.length === 1) { out.push({ text: shown || parts[0], norm: parts[0] }); return; }
            parts.forEach(function (p) { out.push({ text: p, norm: p }); });
        });
        return out;
    }

    // ------------------------------------------------------------------ dictation

    /** Longest-common-subsequence matches between two token arrays: [[answerIndex, typedIndex], ...] ascending. */
    function lcsPairs(ans, typed) {
        var n = ans.length, m = typed.length, i, j;
        var table = [];
        for (i = 0; i <= n; i++) { table[i] = []; for (j = 0; j <= m; j++) table[i][j] = 0; }
        for (i = n - 1; i >= 0; i--) {
            for (j = m - 1; j >= 0; j--) {
                table[i][j] = ans[i] === typed[j] ? table[i + 1][j + 1] + 1 : Math.max(table[i + 1][j], table[i][j + 1]);
            }
        }
        var pairs = [];
        i = 0; j = 0;
        while (i < n && j < m) {
            if (ans[i] === typed[j]) { pairs.push([i, j]); i++; j++; }
            else if (table[i + 1][j] >= table[i][j + 1]) i++;
            else j++;
        }
        return pairs;
    }

    /**
     * Word-level grading of a dictated sentence.
     * Returns { status: 'correct'|'near'|'wrong', score: 0-1, extra, words: [{ text, state: 'ok'|'near'|'wrong'|'missing' }] }.
     * score = exact words / answer words. Extra typed words keep a perfect score from being "correct" (status becomes "near").
     */
    function gradeDictation(input, answer) {
        var expected = answerTokens(answer);
        var ans = expected.map(function (t) { return t.norm; });
        var typed = tokenize(input);

        if (!ans.length) {
            return { status: typed.length ? 'wrong' : 'correct', score: typed.length ? 0 : 1, extra: typed.length, words: [] };
        }
        if (!typed.length) {
            return { status: 'wrong', score: 0, extra: 0, words: expected.map(function (t) { return { text: t.text, state: 'missing' }; }) };
        }

        var states = ans.map(function () { return null; });
        var usedTyped = typed.map(function () { return false; });
        var pairs = lcsPairs(ans, typed);
        pairs.forEach(function (p) { states[p[0]] = 'ok'; usedTyped[p[1]] = true; });

        // Walk the gaps between exact matches: near matches first, then wrong/missing in order.
        var anchors = [[-1, -1]].concat(pairs).concat([[ans.length, typed.length]]);
        for (var g = 0; g < anchors.length - 1; g++) {
            var aFrom = anchors[g][0] + 1, aTo = anchors[g + 1][0];
            var tFrom = anchors[g][1] + 1, tTo = anchors[g + 1][1];
            var a, t;
            for (a = aFrom; a < aTo; a++) {
                for (t = tFrom; t < tTo; t++) {
                    if (!usedTyped[t] && isNear(typed[t], ans[a])) { states[a] = 'near'; usedTyped[t] = true; break; }
                }
            }
            for (a = aFrom; a < aTo; a++) {
                if (states[a]) continue;
                var free = -1;
                for (t = tFrom; t < tTo; t++) { if (!usedTyped[t]) { free = t; break; } }
                if (free >= 0) { states[a] = 'wrong'; usedTyped[free] = true; } else { states[a] = 'missing'; }
            }
        }

        var ok = states.filter(function (s) { return s === 'ok'; }).length;
        var extra = usedTyped.filter(function (u) { return !u; }).length;
        var score = ok / ans.length;
        var status = score === 1 ? (extra ? 'near' : 'correct') : (score >= 0.8 ? 'near' : 'wrong');
        return {
            status: status,
            score: score,
            extra: extra,
            words: expected.map(function (tok, i) { return { text: tok.text, state: states[i] }; })
        };
    }

    // ------------------------------------------------------------------ fill in the blanks

    function wordMatches(text) {
        var re = /[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*/gu;
        var out = [], m;
        while ((m = re.exec(text)) !== null) out.push({ word: m[0], start: m.index, end: m.index + m[0].length });
        return out;
    }

    function letterCount(word) {
        var m = word.match(/\p{L}/gu);
        return m ? m.length : 0;
    }

    /**
     * Chooses the words to blank in one line. About one blank per 5 words (1-4), only words of 3+ letters that are not
     * stop words, never two neighbouring words. Returns { parts: [{ type: 'text'|'blank', value, answer? }], blanks }.
     */
    function pickBlanks(text, rng, opts) {
        rng = rng || Math.random;
        opts = opts || {};
        var perWords = opts.perWords || 5, minBlanks = opts.minBlanks == null ? 1 : opts.minBlanks, maxBlanks = opts.maxBlanks || 4;
        var source = String(text == null ? '' : text);
        var words = wordMatches(source);

        var eligible = [];
        words.forEach(function (w, i) {
            if (letterCount(w.word) >= 3 && STOP_WORDS.indexOf(normalize(w.word)) < 0) eligible.push(i);
        });

        var target = Math.max(minBlanks, Math.min(maxBlanks, Math.round(words.length / perWords)));
        var order = eligible.slice();
        for (var i = order.length - 1; i > 0; i--) {
            var j = Math.floor(rng() * (i + 1));
            var tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        var chosen = [];
        order.forEach(function (idx) {
            if (chosen.length >= target) return;
            if (chosen.indexOf(idx - 1) >= 0 || chosen.indexOf(idx + 1) >= 0) return;
            chosen.push(idx);
        });
        chosen.sort(function (a, b) { return a - b; });

        var parts = [], cursor = 0;
        chosen.forEach(function (idx) {
            var w = words[idx];
            if (w.start > cursor) parts.push({ type: 'text', value: source.slice(cursor, w.start) });
            parts.push({ type: 'blank', value: w.word, answer: w.word });
            cursor = w.end;
        });
        if (cursor < source.length || !parts.length) parts.push({ type: 'text', value: source.slice(cursor) });
        return { parts: parts, blanks: chosen.length };
    }

    /** 'correct' when equal after normalize, 'near' for a one-letter slip in a 4+ letter word, otherwise 'wrong'. */
    function gradeBlank(input, answer) {
        var typed = normalize(input), expected = normalize(answer);
        if (!typed) return 'wrong';
        if (typed === expected) return 'correct';
        return isNear(typed, expected) ? 'near' : 'wrong';
    }

    // ------------------------------------------------------------------ lesson list

    function fold(text) {
        return String(text == null ? '' : text).normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
    }

    function isAll(value) { return value == null || value === '' || value === 'all'; }

    /** lessons = [{ id, title, topic, level, status }]. Returns the matching ids in input order. */
    function filterLessons(lessons, filters) {
        filters = filters || {};
        var needle = fold(String(filters.q == null ? '' : filters.q).trim());
        return (lessons || []).filter(function (l) {
            if (!isAll(filters.level) && l.level !== filters.level) return false;
            if (!isAll(filters.topic) && l.topic !== filters.topic) return false;
            if (!isAll(filters.status) && l.status !== filters.status) return false;
            if (needle && fold(l.title).indexOf(needle) < 0 && fold(l.topic).indexOf(needle) < 0) return false;
            return true;
        }).map(function (l) { return l.id; });
    }

    function paginate(ids, shown) {
        var list = ids || [];
        var n = Math.max(0, Math.floor(Number(shown) || 0));
        return { visible: list.slice(0, n), hasMore: list.length > n };
    }

    // ------------------------------------------------------------------ player

    /**
     * lines = [{ start, end }] sorted by start, all with a numeric start. Returns the index whose range contains time
     * (start <= time < end, where a missing end means the next line's start), -1 before the first line, in a gap or
     * after the last line.
     */
    function lineIndexAt(lines, time) {
        var list = lines || [];
        var t = Number(time);
        if (!list.length || !isFinite(t)) return -1;
        var lo = 0, hi = list.length - 1, found = -1;
        while (lo <= hi) {
            var mid = (lo + hi) >> 1;
            if (list[mid].start <= t) { found = mid; lo = mid + 1; } else { hi = mid - 1; }
        }
        if (found < 0) return -1;
        var line = list[found];
        var end = line.end != null ? line.end : (found + 1 < list.length ? list[found + 1].start : Infinity);
        return t < end ? found : -1;
    }

    function formatClock(seconds) {
        var s = Math.floor(Number(seconds));
        if (!isFinite(s) || s < 0) s = 0;
        var h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), sec = s % 60;
        var ss = (sec < 10 ? '0' : '') + sec;
        if (h > 0) return h + ':' + (m < 10 ? '0' : '') + m + ':' + ss;
        return m + ':' + ss;
    }

    // ------------------------------------------------------------------ completion (mirrors ListeningService.IsLessonComplete)

    /** counts = { transcript, quiz, vocab } -> the required step keys, in the order the page shows them. */
    function requiredSteps(counts) {
        var c = counts || {};
        var out = [];
        if (c.transcript > 0) out.push('transcript');
        if (c.vocab > 0) out.push('vocab');
        if (c.quiz > 0) out.push('quiz');
        return out;
    }

    /** flags = { transcript, quiz, vocab } booleans. Complete when every required step is done (and there is one). */
    function isComplete(flags, required) {
        var f = flags || {};
        var req = required || [];
        return req.length > 0 && req.every(function (k) { return !!f[k]; });
    }

    // ------------------------------------------------------------------ URL state of the index filters

    /** search = "?level=B1&topic=Daily&status=done&q=abc". Unknown or invalid values fall back to "all" (q to ""). */
    function parseFilterQuery(search, knownTopics) {
        var params = new URLSearchParams(String(search == null ? '' : search).replace(/^\?/, ''));
        var level = String(params.get('level') || '').toUpperCase();
        var status = String(params.get('status') || '').toLowerCase();
        var topic = String(params.get('topic') || '').slice(0, TOPIC_MAX);
        var q = String(params.get('q') || '').trim().slice(0, QUERY_MAX);
        if (knownTopics && topic && knownTopics.indexOf(topic) < 0) topic = '';
        return {
            level: LEVELS.indexOf(level) >= 0 ? level : 'all',
            topic: topic || 'all',
            status: STATUSES.indexOf(status) >= 0 ? status : 'all',
            q: q
        };
    }

    /** Only non-default values are written: "" when nothing is filtered, otherwise "?level=..&topic=..". */
    function buildFilterQuery(state) {
        var s = state || {};
        var params = new URLSearchParams();
        if (!isAll(s.level)) params.set('level', s.level);
        if (!isAll(s.topic)) params.set('topic', s.topic);
        if (!isAll(s.status)) params.set('status', s.status);
        var q = String(s.q == null ? '' : s.q).trim();
        if (q) params.set('q', q);
        var text = params.toString();
        return text ? '?' + text : '';
    }

    return {
        LEVELS: LEVELS, STATUSES: STATUSES, STOP_WORDS: STOP_WORDS,
        mulberry32: mulberry32, normalize: normalize, tokenize: tokenize, levenshtein: levenshtein,
        gradeDictation: gradeDictation, pickBlanks: pickBlanks, gradeBlank: gradeBlank,
        filterLessons: filterLessons, paginate: paginate,
        lineIndexAt: lineIndexAt, formatClock: formatClock,
        requiredSteps: requiredSteps, isComplete: isComplete,
        parseFilterQuery: parseFilterQuery, buildFilterQuery: buildFilterQuery
    };
}));
