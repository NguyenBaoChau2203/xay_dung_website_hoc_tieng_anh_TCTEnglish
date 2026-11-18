/* Pure import parser for the personal set workspace.
   UMD: module.exports in Node (tests), window.TctSets.parseImport in the browser. English only. */
(function (root, factory) {
    'use strict';
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.TctSets = root.TctSets || {};
        root.TctSets.parseImport = factory().parseImport;
        root.TctSets.parseImportCore = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var MAX_VALID = 100;
    var MAX_TERM = 255;
    var MAX_MEANING = 1000;

    var TERM_ALIASES = ['term', 'từ', 'tu', 'word'];
    var COLUMN_ALIASES = {
        term: TERM_ALIASES,
        meaning: ['meaning_vi', 'meaning', 'nghĩa', 'nghia', 'vi'],
        phonetic: ['phonetic', 'reading', 'ipa', 'phiên âm', 'phien am'],
        example: ['example'],
        exampleVi: ['example_vi'],
        topic: ['topic']
    };

    // Characters that only occur in Vietnamese words (used to tell "- quả táo" from the list item "- apple").
    var VIETNAMESE_LETTER = /[ăâđêôơưàáảãạằắẳẵặầấẩẫậèéẻẽẹềếểễệìíỉĩịòóỏõọồốổỗộờớởỡợùúủũụừứửữựỳýỷỹỵ]/i;
    var LIST_MARKER = /^\s*(\d{1,3}[.)]\s+|[-*•·]\s+)/;

    function stripBom(text) {
        return text.charCodeAt(0) === 0xFEFF ? text.slice(1) : text;
    }

    /** RFC 4180 parser: quotes, doubled quotes and newlines inside cells. Returns [{cells, line}]. */
    function parseCsv(text, delimiter) {
        var records = [];
        var cells = [];
        var cell = '';
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;
        var i = 0;

        function endCell() { cells.push(cell); cell = ''; }
        function endRecord() {
            endCell();
            if (!(cells.length === 1 && cells[0].trim() === '')) { records.push({ cells: cells, line: recordLine }); }
            cells = [];
        }

        while (i < text.length) {
            var ch = text.charAt(i);
            if (inQuotes) {
                if (ch === '"') {
                    if (text.charAt(i + 1) === '"') { cell += '"'; i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                if (ch === '\n') { line++; }
                cell += ch; i++; continue;
            }
            if (ch === '"' && cell === '') { inQuotes = true; i++; continue; }
            if (ch === delimiter) { endCell(); i++; continue; }
            if (ch === '\r') { i++; continue; }
            if (ch === '\n') { endRecord(); line++; recordLine = line; i++; continue; }
            cell += ch; i++;
        }
        if (cell !== '' || cells.length > 0) { endRecord(); }
        return records;
    }

    function headerIndex(headers, aliases) {
        for (var i = 0; i < headers.length; i++) {
            if (aliases.indexOf(headers[i]) !== -1) { return i; }
        }
        return -1;
    }

    function detectCsv(text) {
        var firstLine = '';
        var lines = text.split('\n');
        for (var i = 0; i < lines.length; i++) {
            if (lines[i].trim() !== '') { firstLine = lines[i]; break; }
        }
        var delimiters = [',', ';', '\t'];
        for (var d = 0; d < delimiters.length; d++) {
            var parts = parseCsv(firstLine, delimiters[d]);
            if (parts.length === 0) { continue; }
            var headers = parts[0].cells.map(function (h) { return h.trim().toLowerCase(); });
            if (headers.length > 1 && headerIndex(headers, TERM_ALIASES) !== -1) {
                return { delimiter: delimiters[d], headers: headers };
            }
        }
        return null;
    }

    function unformula(value) {
        // The CSV export prefixes cells that start with = + - @ with an apostrophe; undo it on the way back in.
        return value.length > 1 && value.charAt(0) === '\'' && '=+-@'.indexOf(value.charAt(1)) !== -1 ? value.slice(1) : value;
    }

    /** Splits "term separator meaning" using the first separator found, in priority order. */
    function splitTermAndMeaning(text) {
        var tab = text.indexOf('\t');
        if (tab !== -1) { return { index: tab, length: 1 }; }

        var spaced = /\s[-–—]\s/.exec(text);
        if (spaced) { return { index: spaced.index + 1, length: 1 }; }

        var dash = /[–—]/.exec(text);
        if (dash) { return { index: dash.index, length: 1 }; }

        var colon = /[:：]/.exec(text);
        if (colon) { return { index: colon.index, length: 1 }; }

        var equals = /[=＝]/.exec(text);
        if (equals) { return { index: equals.index, length: 1 }; }
        return null;
    }

    function parseLineMode(text) {
        var out = [];
        var physical = text.split('\n');
        for (var i = 0; i < physical.length; i++) {
            var raw = physical[i].replace(/\r$/, '');
            var trimmed = raw.trim();
            if (trimmed === '' || trimmed.charAt(0) === '#') { continue; }

            var body = trimmed;
            var marker = LIST_MARKER.exec(body);
            var meaningOnly = false;
            if (marker) {
                var rest = body.slice(marker[0].length).trim();
                // "- quả táo": a dash followed by a Vietnamese phrase is a meaning with no term, not a list item.
                if (/^[-*•·]/.test(marker[0].trim()) && VIETNAMESE_LETTER.test(rest) && !splitTermAndMeaning(rest)) {
                    meaningOnly = true;
                }
                body = meaningOnly ? rest : rest;
            }

            var term = '';
            var meaning = '';
            if (meaningOnly) {
                meaning = body;
            } else {
                var split = splitTermAndMeaning(body);
                if (split) {
                    term = body.slice(0, split.index).trim();
                    meaning = body.slice(split.index + split.length).trim();
                } else {
                    term = body;
                }
            }

            out.push({ lineNo: i + 1, raw: raw, term: term, meaning: meaning, phonetic: '', example: '', exampleVi: '', topic: '' });
        }
        return out;
    }

    function parseCsvMode(text, detected) {
        var records = parseCsv(text, detected.delimiter);
        var headers = records.length ? records[0].cells.map(function (h) { return h.trim().toLowerCase(); }) : [];
        var index = {};
        Object.keys(COLUMN_ALIASES).forEach(function (key) { index[key] = headerIndex(headers, COLUMN_ALIASES[key]); });

        var out = [];
        for (var i = 1; i < records.length; i++) {
            var cells = records[i].cells;
            function cell(key) {
                var at = index[key];
                return at === -1 || at >= cells.length ? '' : unformula(cells[at].trim());
            }
            out.push({
                lineNo: records[i].line,
                raw: cells.join(detected.delimiter),
                term: cell('term'),
                meaning: cell('meaning'),
                phonetic: cell('phonetic'),
                example: cell('example'),
                exampleVi: cell('exampleVi'),
                topic: cell('topic')
            });
        }
        return out;
    }

    /**
     * parseImport(text, existingTermsLower) ->
     *   { lines: [{lineNo, raw, term, meaning, phonetic, example, exampleVi, topic, status, message}],
     *     ok, duplicates, errors, truncated }
     * status: 'ok' | 'duplicate' | 'error'.
     */
    function parseImport(text, existingTermsLower) {
        var normalized = stripBom(String(text === undefined || text === null ? '' : text)).replace(/\r\n?/g, '\n');
        var existing = {};
        (existingTermsLower || []).forEach(function (term) { existing[String(term).toLowerCase()] = true; });

        var detected = detectCsv(normalized);
        var lines = detected ? parseCsvMode(normalized, detected) : parseLineMode(normalized);

        var seen = {};
        var result = { lines: lines, ok: 0, duplicates: 0, errors: 0, truncated: false };
        lines.forEach(function (line) {
            var key = line.term.toLowerCase();
            if (line.term === '') {
                line.status = 'error'; line.message = 'Thiếu từ';
            } else if (line.term.length > MAX_TERM) {
                line.status = 'error'; line.message = 'Từ dài quá 255 ký tự';
            } else if (line.meaning.length > MAX_MEANING) {
                line.status = 'error'; line.message = 'Nghĩa dài quá 1000 ký tự';
            } else if (existing[key]) {
                line.status = 'duplicate'; line.message = 'Đã có trong bộ';
            } else if (seen[key]) {
                line.status = 'duplicate'; line.message = 'Trùng dòng phía trên';
            } else if (result.ok >= MAX_VALID) {
                line.status = 'error'; line.message = 'Quá 100 từ mỗi lần';
                result.truncated = true;
            } else {
                line.status = 'ok'; line.message = '';
                result.ok++;
            }

            if (line.term !== '' && line.status !== 'error') { seen[key] = true; }
            if (line.status === 'duplicate') { result.duplicates++; }
            if (line.status === 'error') { result.errors++; }
        });

        return result;
    }

    return { parseImport: parseImport, parseCsv: parseCsv, splitTermAndMeaning: splitTermAndMeaning, MAX_VALID: MAX_VALID };
});
