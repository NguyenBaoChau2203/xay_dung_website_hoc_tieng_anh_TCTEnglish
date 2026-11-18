/* Bulk import page: paste/CSV -> preview -> (optional AI enrich) -> add in chunks, with retry. */
(function () {
    'use strict';

    var T = window.TctSets;
    var core = T && T.composeCore;
    var configNode = document.getElementById('tsImportConfig');
    if (!T || !core || !T.parseImport || !configNode) { return; }

    var config;
    try { config = JSON.parse(configNode.textContent); } catch (e) { return; }

    var MAX_FILE = 500 * 1024;
    var $ = function (id) { return document.getElementById(id); };
    var els = {
        input: $('ts-step-input'), preview: $('ts-step-preview'), run: $('ts-step-run'),
        text: $('ts-import-text'), file: $('ts-import-file'), previewBtn: $('ts-import-preview'),
        summary: $('ts-preview-summary'), rows: $('ts-preview-rows'), back: $('ts-preview-back'),
        runAi: $('ts-run-ai'), runPlain: $('ts-run-plain'), hint: $('ts-import-premium-hint'),
        progress: $('ts-progress'), bar: $('ts-progress-bar'), list: $('ts-run-list'), done: $('ts-run-done'),
        retry: $('ts-run-retry'), retryCount: $('ts-retry-count'), again: $('ts-run-again'), runTitle: $('ts-run-title')
    };

    var existing = config.existing.slice();
    var parsed = null;
    var jobs = [];

    function show(step) {
        els.input.hidden = step !== 'input';
        els.preview.hidden = step !== 'preview';
        els.run.hidden = step !== 'run';
    }

    els.text.addEventListener('input', function () { els.previewBtn.disabled = els.text.value.trim() === ''; });

    els.file.addEventListener('change', function () {
        var file = els.file.files && els.file.files[0];
        if (!file) { return; }
        if (file.size > MAX_FILE) { T.toast('File quá lớn (tối đa 500 KB).', { type: 'danger' }); els.file.value = ''; return; }
        var reader = new FileReader();
        reader.onload = function () {
            els.text.value = String(reader.result || '');
            els.previewBtn.disabled = els.text.value.trim() === '';
            els.file.value = '';
        };
        reader.onerror = function () { T.toast('Không đọc được file.', { type: 'danger' }); };
        reader.readAsText(file, 'utf-8');
    });

    // ------------------------------------------------------------------ preview

    function badgeFor(line) {
        if (line.status === 'ok') {
            return T.el('span', 'tct-badge tct-badge--success', 'Sẵn sàng');
        }
        if (line.status === 'duplicate') {
            return T.el('span', 'tct-badge tct-badge--warning', 'Trùng: ' + line.message.replace(/^Trùng\s*/, ''));
        }
        return T.el('span', 'tct-badge tct-badge--danger', 'Lỗi: ' + line.message);
    }

    function renderPreview() {
        parsed = T.parseImport(els.text.value, existing);
        els.rows.textContent = '';
        parsed.lines.forEach(function (line, i) {
            var tr = document.createElement('tr');
            tr.appendChild(T.el('td', null, String(i + 1)));
            tr.appendChild(T.el('td', 'tct-break', line.term));
            var meaning = T.el('td', 'tct-break', line.meaning);
            if (line.status === 'ok' && !line.meaning) {
                meaning.appendChild(T.el('span', 'tct-text-muted', 'Thiếu nghĩa — cần AI hoặc tự nhập'));
            }
            tr.appendChild(meaning);
            var status = document.createElement('td');
            status.appendChild(badgeFor(line));
            tr.appendChild(status);
            els.rows.appendChild(tr);
        });

        var summary = parsed.ok + ' từ sẵn sàng · ' + parsed.duplicates + ' trùng · ' + parsed.errors + ' lỗi';
        els.summary.textContent = summary;

        var withMeaning = parsed.lines.filter(function (l) { return l.status === 'ok' && l.meaning; }).length;
        els.runAi.hidden = !config.isPremium;
        els.runAi.disabled = parsed.ok === 0;
        els.runPlain.disabled = withMeaning === 0;
        els.runPlain.textContent = config.isPremium ? 'Thêm không cần AI' : 'Thêm ' + withMeaning + ' từ có nghĩa';
        els.hint.hidden = config.isPremium;
        show('preview');
        if (parsed.lines.length === 0) { els.summary.textContent = 'Không tìm thấy dòng nào để nhập.'; }
    }

    els.previewBtn.addEventListener('click', renderPreview);
    els.back.addEventListener('click', function () { show('input'); els.text.focus(); });

    // ------------------------------------------------------------------ run

    function buildJobs(useAi) {
        return parsed.lines.filter(function (l) { return l.status === 'ok' && (useAi || l.meaning); }).map(function (line) {
            return { line: line, state: 'pending', error: null, node: null, stateNode: null };
        });
    }

    function renderJobs() {
        els.list.textContent = '';
        jobs.forEach(function (job) {
            var li = T.el('li', 'ts-run-item');
            li.appendChild(T.el('span', 'tct-break ts-run-term', job.line.term));
            job.stateNode = T.el('span', 'ts-run-state');
            li.appendChild(job.stateNode);
            job.node = li;
            els.list.appendChild(li);
            paint(job);
        });
    }

    function paint(job) {
        var node = job.stateNode;
        node.textContent = '';
        if (job.state === 'pending') { node.className = 'ts-run-state tct-text-muted'; node.textContent = 'Đang chờ'; }
        else if (job.state === 'working') {
            node.className = 'ts-run-state';
            var spinner = T.el('span', 'spinner-border spinner-border-sm me-1');
            spinner.setAttribute('role', 'presentation');
            node.appendChild(spinner);
            node.appendChild(document.createTextNode('Đang xử lý'));
        } else if (job.state === 'added') {
            node.className = 'ts-run-state text-success';
            node.appendChild(T.icon('fa-solid fa-circle-check me-1'));
            node.appendChild(document.createTextNode('Đã thêm'));
        } else if (job.state === 'skipped') {
            node.className = 'ts-run-state tct-text-muted';
            node.textContent = 'Bỏ qua: ' + (job.error || 'Đã có trong bộ');
        } else {
            node.className = 'ts-run-state text-danger';
            node.appendChild(T.icon('fa-solid fa-circle-exclamation me-1'));
            node.appendChild(document.createTextNode(job.error || 'Lỗi'));
        }
    }

    function setProgress() {
        var finished = jobs.filter(function (j) { return j.state !== 'pending' && j.state !== 'working'; }).length;
        var percent = jobs.length ? Math.round(100 * finished / jobs.length) : 100;
        els.bar.style.setProperty('--p', percent + '%');
        els.progress.setAttribute('aria-valuenow', String(percent));
        var saved = jobs.filter(function (j) { return j.state === 'added'; }).length;
        els.runTitle.textContent = finished >= jobs.length ? 'Đã thêm ' + saved + '/' + jobs.length + ' từ' : 'Đang điền và thêm từ…';
    }

    function cardFor(job, enrichResult) {
        var line = job.line;
        var base = {
            term: line.term, definition: line.meaning, meaningTyped: !!line.meaning, readingTyped: !!line.phonetic,
            d: {
                phonetic: line.phonetic || '', topic: line.topic || '',
                examples: line.example ? [{ text: line.example, vi: line.exampleVi || '' }] : []
            }
        };
        return enrichResult ? core.applyEnrich(base, enrichResult, [0]) : base;
    }

    function runGroup(group, useAi) {
        group.forEach(function (job) { job.state = 'working'; paint(job); });

        var enrichStep = useAi
            ? T.fetchJson(config.urls.enrichBatch, {
                method: 'POST',
                body: { items: group.map(function (j) { return { term: j.line.term, meaningHint: j.line.meaning || null }; }) }
            }).then(function (response) { return response.results || []; })
                .catch(function (error) { T.toast(error.message || 'Lỗi khi điền gợi ý.', { type: 'danger' }); return []; })
            : Promise.resolve([]);

        return enrichStep.then(function (results) {
            var sendable = [];
            group.forEach(function (job, i) {
                var item = results[i];
                var enriched = item && item.ok && item.result && item.result.isValidWord !== false ? item.result : null;
                var card = cardFor(job, enriched);
                if (!card.definition || !String(card.definition).trim()) {
                    job.state = 'error';
                    job.error = useAi ? 'Không điền được nghĩa, hãy tự nhập' : 'Thiếu nghĩa';
                    paint(job);
                    return;
                }
                sendable.push({ job: job, dto: core.toInputDto(card) });
            });
            if (sendable.length === 0) { return null; }

            return T.fetchJson(config.urls.batch, { method: 'POST', body: { cards: sendable.map(function (s) { return s.dto; }) } })
                .then(function (response) {
                    var insertedTerms = (response.inserted || []).map(function (i) { return core.norm(i.term); });
                    sendable.forEach(function (s) {
                        var key = core.norm(s.dto.term);
                        var at = insertedTerms.indexOf(key);
                        if (at !== -1) {
                            insertedTerms.splice(at, 1);
                            s.job.state = 'added';
                            existing.push(key); // a retry can never duplicate it
                        } else if ((response.skipped || []).map(core.norm).indexOf(key) !== -1) {
                            s.job.state = 'skipped'; s.job.error = 'Đã có trong bộ';
                        } else {
                            s.job.state = 'error'; s.job.error = 'Dữ liệu chưa hợp lệ';
                        }
                        paint(s.job);
                    });
                    return null;
                });
        }).catch(function (error) {
            group.forEach(function (job) {
                if (job.state === 'working') { job.state = 'error'; job.error = error.message || 'Lỗi mạng'; paint(job); }
            });
        });
    }

    function runJobs(targets, useAi) {
        show('run');
        els.done.hidden = true;
        els.retry.hidden = true;
        targets.forEach(function (job) { job.state = 'pending'; job.error = null; });
        if (targets === jobs) { renderJobs(); } else { targets.forEach(paint); }
        setProgress();

        var groups = core.chunk(targets, 10);
        var next = 0;
        var running = 0;

        function pump() {
            while (running < 2 && next < groups.length) {
                (function (group) {
                    running++; next++;
                    runGroup(group, useAi).then(function () {
                        running--;
                        setProgress();
                        pump();
                    });
                })(groups[next]);
            }
            if (running === 0 && next >= groups.length) { finishRun(useAi); }
        }
        pump();
    }

    function finishRun(useAi) {
        setProgress();
        var failed = jobs.filter(function (j) { return j.state === 'error'; });
        els.done.hidden = false;
        els.retry.hidden = failed.length === 0;
        els.retryCount.textContent = String(failed.length);
        els.retry.onclick = function () { runJobs(failed, useAi); };
        var saved = jobs.filter(function (j) { return j.state === 'added'; }).length;
        if (saved > 0) { T.toast('Đã thêm ' + saved + ' từ'); }
    }

    els.runAi.addEventListener('click', function () { jobs = buildJobs(true); runJobs(jobs, true); });
    els.runPlain.addEventListener('click', function () { jobs = buildJobs(false); runJobs(jobs, false); });
    els.again.addEventListener('click', function () { els.text.value = ''; els.previewBtn.disabled = true; show('input'); els.text.focus(); });
})();
