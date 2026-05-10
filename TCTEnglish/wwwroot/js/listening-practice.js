/**
 * listening-practice.js
 * Listening Practice Page — Client-side logic
 *
 * Features:
 *  - YouTube IFrame API (load, play, pause, seekTo, getCurrentTime)
 *  - HTML5 <audio> integration
 *  - Playback speed control (0.5x, 0.75x, 1x, 1.25x, 1.5x)
 *  - A-B Loop (3-state toggle)
 *  - Listen counter (tracks completed plays)
 *  - Auto-scroll to active transcript line
 *  - Per-line replay button
 *  - Tab switching (Transcript / Quiz / Vocab)
 *  - 4 Practice Modes: Normal, Dictation, Fill-in-Blanks, Shadowing
 *  - Quiz: collect answers → POST /Home/Listening/EvaluateQuiz
 *  - Progress auto-save: POST /Home/Listening/SaveProgress/{lessonId}
 *  - Anti-CSRF: token read from hidden input
 */
(function () {
    'use strict';

    // ══════════════════════════════════════════════════════════════
    // 1. DATA — injected by Razor into window constants
    // ══════════════════════════════════════════════════════════════
    console.log('[LP] Booting script. window.LP_TRANSCRIPT:', window.LP_TRANSCRIPT);
    
    const lessonId        = window.LP_LESSON_ID;
    const youtubeId       = window.LP_YOUTUBE_ID;
    
    // Evaluate lazily or once at start
    let transcriptData  = (window.LP_TRANSCRIPT || []).slice().sort((a, b) => a.orderIndex - b.orderIndex);
    const quizData        = window.LP_QUIZ || [];
    const isAuthenticated = window.LP_AUTH === true;
    const isPremiumUser   = window.LP_IS_PREMIUM === true;

    console.log('[LP] transcriptData initialized. Length:', transcriptData.length);

    // ══════════════════════════════════════════════════════════════
    // 2. ANTI-CSRF TOKEN
    // ══════════════════════════════════════════════════════════════
    function getCsrfToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    // ══════════════════════════════════════════════════════════════
    // 3. PLAYER — YouTube IFrame API or HTML5 Audio
    // ══════════════════════════════════════════════════════════════
    let ytPlayer          = null;
    let ytReady            = false;
    let audioEl           = document.getElementById('lp-audio');
    let transcriptPolling = null;

    // ── YouTube IFrame API ────────────────────────────────────────
    if (youtubeId) {
        window.onYouTubeIframeAPIReady = function () {
            new YT.Player('youtube-player', {
                events: {
                    onReady: function (event) {
                        ytPlayer = event.target;        // true player instance with seekTo/playVideo
                        window.ytPlayer = event.target; // expose globally
                        ytReady = true;
                        startTranscriptHighlight();
                    },
                    onStateChange: function (e) {
                        if (e.data === YT.PlayerState.PLAYING) {
                            startTranscriptHighlight();
                        } else if (e.data === YT.PlayerState.PAUSED) {
                            stopTranscriptHighlight();
                        } else if (e.data === YT.PlayerState.ENDED) {
                            stopTranscriptHighlight();
                        }
                    }
                }
            });
        };

        const tag = document.createElement('script');
        tag.src = 'https://www.youtube.com/iframe_api';
        document.head.appendChild(tag);
    }

    // ── HTML5 Audio ───────────────────────────────────────────────
    if (audioEl) {
        audioEl.addEventListener('play',  startTranscriptHighlight);
        audioEl.addEventListener('pause', stopTranscriptHighlight);
        audioEl.addEventListener('ended', function () {
            stopTranscriptHighlight();
        });
    }

    // ── Seek helpers ──────────────────────────────────────────────
    function seekTo(seconds) {
        console.log('[LP] seekTo called:', seconds, 'ytReady:', ytReady, 'ytPlayer:', ytPlayer, 'audioEl:', audioEl);
        const yt = ytReady ? (ytPlayer || window.ytPlayer) : null;
        if (yt && typeof yt.seekTo === 'function') {
            console.log('[LP] Using YouTube player to seek');
            yt.seekTo(seconds, true);
            yt.playVideo();
        } else if (audioEl) {
            console.log('[LP] Using HTML5 audio to seek');
            audioEl.currentTime = seconds;
            audioEl.play().catch(() => {});
        } else {
            console.warn('[LP] seekTo: no player available!');
        }
    }
    window.seekTo = seekTo; // Expose for dynamically generated HTML

    function getCurrentTime() {
        if (ytPlayer && typeof ytPlayer.getCurrentTime === 'function') {
            return ytPlayer.getCurrentTime();
        }
        if (audioEl) return audioEl.currentTime;
        return 0;
    }

    function pausePlayer() {
        if (ytPlayer && typeof ytPlayer.pauseVideo === 'function') {
            ytPlayer.pauseVideo();
        } else if (audioEl) {
            audioEl.pause();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 4. TAB SWITCHING
    // ══════════════════════════════════════════════════════════════
    const tabBtns   = document.querySelectorAll('.lp-tab-btn');
    const tabPanels = document.querySelectorAll('.lp-tab-panel');

    tabBtns.forEach(btn => {
        btn.addEventListener('click', function () {
            const target = this.dataset.tab;
            tabBtns.forEach(b => b.classList.remove('lp-tab-active'));
            tabPanels.forEach(p => p.classList.remove('lp-panel-active'));

            this.classList.add('lp-tab-active');
            const panel = document.getElementById('panel-' + target);
            if (panel) panel.classList.add('lp-panel-active');
        });
    });

    // ══════════════════════════════════════════════════════════════
    // 5. MODE SELECTOR
    // ══════════════════════════════════════════════════════════════
    const modeCards  = document.querySelectorAll('.lp-mode-card');
    const modePanels = document.querySelectorAll('.lp-mode-panel');

    modeCards.forEach(card => {
        card.addEventListener('click', function () {
            const mode = this.dataset.mode;

            modeCards.forEach(c => c.classList.remove('active'));
            modePanels.forEach(p => p.classList.remove('lp-mode-active'));

            this.classList.add('active');
            const panel = document.getElementById('mode-' + mode);
            if (panel) panel.classList.add('lp-mode-active');

            // Initialize on first enter
            if (mode === 'dictation' && !dictationInited) {
                initDictation();
            } else if (mode === 'fillin' && !fillinInited) {
                initFillIn();
            }
        });
    });

    // ══════════════════════════════════════════════════════════════
    // 6. TRANSCRIPT (MODE 1)
    // ══════════════════════════════════════════════════════════════
    const transcriptLines = document.querySelectorAll('.lp-transcript-line');
    console.log('[LP] Found transcript lines in DOM:', transcriptLines.length);
    if (transcriptLines.length > 0) {
        const firstLine = transcriptLines[0];
        console.log('[LP] First line data-start:', firstLine.dataset.start, 'parsed:', parseFloat(firstLine.dataset.start));
    }

    // ── Click to seek ─────────────────────────────────────────────
    transcriptLines.forEach(line => {
        const start = parseFloat(line.dataset.start);
        if (!isNaN(start)) {
            line.addEventListener('click', function (e) {
                if (e.target.closest('.lp-line-vi-btn')) return;
                if (e.target.closest('.lp-line-replay-btn')) return;
                seekTo(start);
            });
        }

        // Vietnamese toggle
        const viBtn = line.querySelector('.lp-line-vi-btn');
        if (viBtn) {
            viBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                const viEl = line.querySelector('.lp-line-vi');
                if (!viEl) return;
                const visible = viEl.classList.toggle('lp-vi-visible');
                viBtn.textContent = visible ? 'Ẩn dịch' : 'Xem dịch';
            });
        }

        // Per-line replay button
        const replayBtn = line.querySelector('.lp-line-replay-btn');
        if (replayBtn) {
            replayBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                const s = parseFloat(line.dataset.start);
                if (!isNaN(s)) seekTo(s);
            });
        }
    });

    // ── Highlight current line during playback ────────────────────
    function highlightCurrentLine() {
        const t = getCurrentTime();
        transcriptLines.forEach(line => {
            const start = parseFloat(line.dataset.start);
            const end   = parseFloat(line.dataset.end);
            if (!isNaN(start) && !isNaN(end) && t >= start && t <= end) {
                line.classList.add('lp-line-active');
            } else {
                line.classList.remove('lp-line-active');
            }
        });
    }

    function startTranscriptHighlight() {
        if (transcriptPolling) return;
        transcriptPolling = setInterval(highlightCurrentLine, 300);
    }

    function stopTranscriptHighlight() {
        clearInterval(transcriptPolling);
        transcriptPolling = null;
        transcriptLines.forEach(l => l.classList.remove('lp-line-active'));
    }

    // ── Toggle all Vietnamese ─────────────────────────────────────
    const toggleAllViBtn = document.getElementById('btn-toggle-all-vi');
    let allViVisible = false;

    if (toggleAllViBtn) {
        toggleAllViBtn.addEventListener('click', async function () {
            // Check if translations exist
            const anyVi = Array.from(document.querySelectorAll('.lp-line-vi')).some(el => el.textContent.trim().length > 0);
            
            if (!anyVi) {
                if (!isPremiumUser) {
                    showToast('warning', 'Vui lòng nâng cấp Premium để sử dụng tính năng dịch bằng AI.');
                    return;
                }

                toggleAllViBtn.disabled = true;
                toggleAllViBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang dịch bằng AI...';

                try {
                    const resp = await fetch(`/Home/Listening/Translate/${lessonId}`, {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': getCsrfToken() }
                    });

                    if (resp.ok) {
                        showToast('success', 'Đã dịch xong! Đang tải lại...');
                        setTimeout(() => location.reload(), 1000);
                        return;
                    } else {
                        const err = await resp.json();
                        showToast('error', err.message || 'Lỗi dịch thuật.');
                    }
                } catch (e) {
                    console.error(e);
                    showToast('error', 'Lỗi kết nối khi dịch bài.');
                }

                toggleAllViBtn.disabled = false;
                toggleAllViBtn.innerHTML = '<i class="fas fa-language"></i> Xem tất cả bản dịch';
                return;
            }

            // Normal toggle behavior
            allViVisible = !allViVisible;
            document.querySelectorAll('.lp-line-vi').forEach(vi => {
                vi.classList.toggle('lp-vi-visible', allViVisible);
            });
            document.querySelectorAll('.lp-line-vi-btn').forEach(btn => {
                btn.textContent = allViVisible ? 'Ẩn dịch' : 'Xem dịch';
            });
            toggleAllViBtn.innerHTML = allViVisible
                ? '<i class="fas fa-eye-slash"></i> Ẩn tất cả'
                : '<i class="fas fa-language"></i> Xem tất cả bản dịch';
        });
    }

    // ── Mark transcript as read ───────────────────────────────────
    const markReadBtn = document.getElementById('btn-mark-transcript-read');

    if (markReadBtn) {
        markReadBtn.addEventListener('click', async function () {
            await saveProgress({ transcriptCompleted: true });
            markReadBtn.disabled = true;
            markReadBtn.innerHTML = '<i class="fas fa-check-circle"></i> Đã đánh dấu hoàn thành';
            updateProgressDot('dot-transcript');
            showToast('success', 'Đã lưu tiến độ Transcript! 🎉');
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 7. DICTATION MODE (MODE 2)
    // ══════════════════════════════════════════════════════════════
    let dictationInited = false;
    let dictTotalWords  = 0;
    let dictCorrect     = 0;
    let dictNear        = 0;
    let dictWrong       = 0;

    function levenshtein(a, b) {
        const m = a.length, n = b.length;
        const dp = Array.from({ length: m + 1 }, () => new Array(n + 1).fill(0));
        for (let i = 0; i <= m; i++) dp[i][0] = i;
        for (let j = 0; j <= n; j++) dp[0][j] = j;
        for (let i = 1; i <= m; i++) {
            for (let j = 1; j <= n; j++) {
                dp[i][j] = a[i-1] === b[j-1] ? dp[i-1][j-1] : 1 + Math.min(dp[i-1][j], dp[i][j-1], dp[i-1][j-1]);
            }
        }
        return dp[m][n];
    }

    function initDictation() {
        if ((!transcriptData || transcriptData.length === 0) && window.LP_TRANSCRIPT && window.LP_TRANSCRIPT.length > 0) {
            transcriptData = window.LP_TRANSCRIPT.slice().sort((a, b) => a.orderIndex - b.orderIndex);
        }
        const container = document.getElementById('dictation-lines');
        if (!container) return;
        if (!transcriptData || transcriptData.length === 0) {
            container.innerHTML = '<p class="text-muted p-3">Không có nội dung transcript để luyện tập.</p>';
            return;
        }
        dictationInited = true;
        container.innerHTML = '';
        const speakerMap = new Map();
        let speakerIdx = 0;
        transcriptData.forEach((line, idx) => {
            if (!speakerMap.has(line.speaker)) speakerMap.set(line.speaker, speakerIdx++);
            const spClass = speakerMap.get(line.speaker) === 0 ? '' : 'speaker-b';
            const item = document.createElement('div');
            item.className = 'lp-dictation-item';
            item.innerHTML = `
                <div class="lp-dict-item-header">
                    <span class="lp-dict-num">${idx + 1}</span>
                    <span class="lp-dict-speaker-tag ${spClass}">${escHtml(line.speaker)}</span>
                    ${line.startTime != null ? `<button class="lp-dict-listen-btn" data-start="${line.startTime}"><i class="fas fa-play"></i> Nghe</button>` : ''}
                </div>
                <textarea class="lp-dict-textarea" rows="2" placeholder="Nhập những gì bạn nghe được…"></textarea>
                <div class="lp-dict-feedback" style="display:none; margin-top:10px; line-height:1.6"></div>
                <div class="lp-dict-answer" style="display:none; margin-top:5px; color:var(--lp-muted); font-size:0.9rem">
                    <strong>Đáp án:</strong> ${escHtml(line.text)}
                </div>
                <div style="margin-top:10px"><button class="lp-dict-check-btn">Kiểm tra</button></div>
            `;
            container.appendChild(item);
            const ta = item.querySelector('.lp-dict-textarea');
            const checkBtn = item.querySelector('.lp-dict-check-btn');
            const listenBtn = item.querySelector('.lp-dict-listen-btn');
            if (listenBtn) listenBtn.addEventListener('click', () => seekTo(line.startTime));
            if (ta) {
                ta.addEventListener('keydown', (e) => {
                    if (e.key === 'Tab') { e.preventDefault(); if (line.startTime != null) seekTo(line.startTime); }
                    else if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); checkDictationLine(item, line); }
                });
            }
            if (checkBtn) checkBtn.addEventListener('click', () => checkDictationLine(item, line));
        });
        updateDictRing(0);
    }

    function checkDictationLine(item, line) {
        if (item.classList.contains('lp-dict-done')) return;
        const ta = item.querySelector('.lp-dict-textarea');
        const feedbackEl = item.querySelector('.lp-dict-feedback');
        const answerEl = item.querySelector('.lp-dict-answer');
        const checkBtn = item.querySelector('.lp-dict-check-btn');
        if (!ta || !feedbackEl) return;
        const userWords   = ta.value.trim().toLowerCase().replace(/[^a-z0-9'\s]/g, '').split(/\s+/).filter(Boolean);
        const targetWords = line.text.toLowerCase().replace(/[^a-z0-9'\s]/g, '').split(/\s+/).filter(Boolean);
        let correctInLine = 0, nearInLine = 0, wrongInLine = 0, html = '';
        const maxLen = Math.max(userWords.length, targetWords.length);
        for (let i = 0; i < maxLen; i++) {
            const u = userWords[i] || '', t = targetWords[i] || '';
            if (u === t && u !== '') { html += `<span class="lp-w-correct">${escHtml(u)}</span> `; correctInLine++; }
            else if (t !== '' && u !== '' && levenshtein(u, t) <= 2) { html += `<span class="lp-w-near" title="Đúng: ${escHtml(t)}">${escHtml(u)}</span> `; nearInLine++; }
            else if (t !== '') { html += `<span class="lp-w-wrong" title="Đúng: ${escHtml(t)}">${escHtml(u || '—')}</span> `; wrongInLine++; }
        }
        feedbackEl.innerHTML = html; feedbackEl.style.display = 'block';
        answerEl.style.display = 'block';
        item.classList.add('lp-dict-done'); ta.disabled = true;
        if (checkBtn) checkBtn.disabled = true;
        dictCorrect += correctInLine; dictNear += nearInLine; dictWrong += wrongInLine; dictTotalWords += maxLen;
        const pct = dictTotalWords > 0 ? Math.round((dictCorrect + dictNear * 0.5) / dictTotalWords * 100) : 0;
        updateDictRing(pct);
        const totalItems = document.querySelectorAll('.lp-dictation-item').length;
        const doneItems  = document.querySelectorAll('.lp-dictation-item.lp-dict-done').length;
        if (doneItems === totalItems) showDictSummary();
    }

    function updateDictRing(pct) {
        const ringPctEl = document.getElementById('dict-ring-pct');
        const ringProg  = document.getElementById('dict-ring-progress');
        if (ringPctEl) ringPctEl.textContent = pct + '%';
        if (ringProg) {
            const offset = 150.796 - (pct / 100) * 150.796;
            ringProg.style.strokeDashoffset = offset;
            ringProg.style.stroke = pct >= 70 ? 'var(--lp-success)' : pct >= 40 ? 'var(--lp-warning)' : 'var(--lp-error)';
        }
    }

    function showDictSummary() {
        const summaryEl = document.getElementById('dict-summary');
        if (!summaryEl) return;
        document.getElementById('dict-stat-correct').textContent = dictCorrect;
        document.getElementById('dict-stat-near').textContent    = dictNear;
        document.getElementById('dict-stat-wrong').textContent   = dictWrong;
        summaryEl.classList.add('show');
        showToast('success', '✨ Hoàn thành Chính tả!');
    }

    const dictRetryBtn = document.getElementById('btn-dict-retry');
    if (dictRetryBtn) {
        dictRetryBtn.addEventListener('click', () => {
            const summaryEl = document.getElementById('dict-summary');
            if (summaryEl) summaryEl.classList.remove('show');
            dictationInited = false;
            initDictation();
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 8. FILL-IN-THE-BLANKS MODE (MODE 3)
    // ══════════════════════════════════════════════════════════════
    let fillinInited = false;
    let fillinBlanks = []; // Array of {id, answer}

    function pickBlankIndices(words) {
        const skipWords = new Set(['a', 'an', 'the', 'is', 'am', 'are', 'was', 'were', 'be', 'to', 'of', 'in', 'on', 'at', 'it', 'i', 'we', 'he', 'she', 'my', 'me', 'do', 'did', 'and', 'or', 'but', 'so', 'if', 'no', 'not', 'yes']);
        const candidates = [];
        words.forEach((w, i) => {
            const clean = w.replace(/[.,!?;:'"()]/g, '').toLowerCase();
            if (clean.length >= 3 && !skipWords.has(clean)) candidates.push(i);
        });
        if (!candidates.length) return [];
        const shuffled = candidates.sort(() => Math.random() - 0.5);
        const count = Math.min(Math.max(1, Math.floor(candidates.length * 0.3)), 3);
        return shuffled.slice(0, count).sort((a, b) => a - b);
    }

    function initFillIn() {
        console.log('[LP] initFillIn triggered');
        
        // Final fallback
        if ((!transcriptData || transcriptData.length === 0) && window.LP_TRANSCRIPT && window.LP_TRANSCRIPT.length > 0) {
            console.log('[LP] transcriptData was empty but window.LP_TRANSCRIPT has data. Re-initializing...');
            transcriptData = window.LP_TRANSCRIPT.slice().sort((a, b) => a.orderIndex - b.orderIndex);
        }

        const container = document.getElementById('fillin-lines');
        const footer    = document.getElementById('fillin-footer');
        if (!container) return;

        if (!transcriptData || transcriptData.length === 0) {
            console.warn('[LP] No transcript data for fill-in. window.LP_TRANSCRIPT:', window.LP_TRANSCRIPT);
            container.innerHTML = '<p class="text-muted p-3">Không có nội dung transcript để luyện tập.</p>';
            return;
        }

        fillinInited = true;
        fillinBlanks = [];
        container.innerHTML = '';

        const speakerMap = new Map();
        let speakerIdx = 0;

        transcriptData.forEach((line, lineIdx) => {
            if (!speakerMap.has(line.speaker)) {
                speakerMap.set(line.speaker, speakerIdx++);
            }
            const spClass = speakerMap.get(line.speaker) === 0 ? '' : 'speaker-b';

            const words = line.text.trim().split(/\s+/);
            if (words.length < 3) return;

            const blankIndices = pickBlankIndices(words);
            if (blankIndices.length === 0) return;

            let lineHtml = '';
            words.forEach((w, wordIdx) => {
                if (blankIndices.includes(wordIdx)) {
                    const clean = w.replace(/[.,!?;:'"()]/g, '');
                    const punct = w.substring(w.toLowerCase().indexOf(clean.toLowerCase()) + clean.length);
                    const wid   = `fill-${lineIdx}-${wordIdx}`;
                    
                    fillinBlanks.push({ id: wid, answer: clean });

                    lineHtml += `<input class="lp-fillin-blank" 
                                        id="${wid}" 
                                        data-answer="${escHtml(clean)}"
                                        style="width:${Math.max(clean.length * 10 + 20, 50)}px"
                                        autocomplete="off"
                                 />${escHtml(punct)} `;
                } else {
                    lineHtml += escHtml(w) + ' ';
                }
            });

            const item = document.createElement('div');
            item.className = 'lp-dictation-item';
            item.innerHTML = `
                <div class="lp-dict-item-header">
                    <span class="lp-dict-num">${lineIdx + 1}</span>
                    <span class="lp-dict-speaker-tag ${spClass}">${escHtml(line.speaker)}</span>
                    ${line.startTime != null ? `<button class="lp-dict-listen-btn" data-start="${line.startTime}"><i class="fas fa-play"></i> Nghe</button>` : ''}
                </div>
                <div class="lp-fillin-text" style="line-height:2.2; margin-top:15px; margin-bottom:15px;">${lineHtml}</div>
                <div class="lp-dict-answer" style="display:none; margin-top:5px; color:var(--lp-muted); font-size:0.9rem">
                    <strong>Đáp án:</strong> ${escHtml(line.text)}
                </div>
                <div style="margin-top:10px"><button class="lp-fillin-check-btn lp-dict-check-btn">Kiểm tra</button></div>
            `;
            container.appendChild(item);

            // Bind click to play button
            const playBtn = item.querySelector('.lp-dict-listen-btn');
            if (playBtn) {
                playBtn.addEventListener('click', function () {
                    seekTo(parseFloat(this.dataset.start));
                });
            }

            // Bind real-time check for each blank
            item.querySelectorAll('.lp-fillin-blank').forEach(input => {
                input.addEventListener('keydown', function(e) {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        checkSingleBlank(this);
                    }
                });
                input.addEventListener('blur', function() {
                    if (this.value.trim()) checkSingleBlank(this);
                });
            });

            // Bind check button
            const checkBtn = item.querySelector('.lp-fillin-check-btn');
            if (checkBtn) {
                checkBtn.addEventListener('click', () => {
                    item.querySelectorAll('.lp-fillin-blank').forEach(input => {
                        checkSingleBlank(input);
                    });
                    const answerEl = item.querySelector('.lp-dict-answer');
                    if (answerEl) answerEl.style.display = 'block';
                    checkBtn.disabled = true;
                });
            }
        });

        // Reset ring
        const ringPctEl = document.getElementById('fillin-ring-pct');
        const ringProg  = document.getElementById('fillin-ring-progress');
        if (ringPctEl) ringPctEl.textContent = '0%';
        if (ringProg) {
            ringProg.style.strokeDashoffset = 150.796;
            ringProg.style.stroke = 'var(--lp-error)';
        }

        if (footer) footer.style.display = 'flex';

        const progressText = document.getElementById('fillin-progress-text');
        if (progressText) progressText.textContent = '';

        console.log(`[LP] Generated fill-in with ${fillinBlanks.length} blanks`);
    }

    function checkSingleBlank(input) {
        if (input.disabled) return;
        const answer = input.dataset.answer;
        const val = input.value.trim();
        if (val.toLowerCase() === answer.toLowerCase()) {
            input.classList.remove('wrong');
            input.classList.add('correct');
            input.disabled = true;
            // Remove answer span if correct after being wrong
            const next = input.nextElementSibling;
            if (next && next.classList.contains('lp-fillin-answer')) next.remove();
        } else {
            input.classList.remove('correct');
            input.classList.add('wrong');
            // Show correct answer beside if not already shown
            if (!input.nextElementSibling || !input.nextElementSibling.classList.contains('lp-fillin-answer')) {
                const span = document.createElement('span');
                span.className = 'lp-fillin-answer';
                span.textContent = answer;
                input.parentNode.insertBefore(span, input.nextSibling);
            }
        }
        updateFillinProgress();
    }

    function updateFillinProgress() {
        const inputs = document.querySelectorAll('#fillin-lines .lp-fillin-blank');
        let correct = 0;
        let filled = 0;
        inputs.forEach(i => {
            if (i.classList.contains('correct')) correct++;
            if (i.value.trim()) filled++;
        });
        
        const total = fillinBlanks.length;
        const pct = total > 0 ? Math.round((correct / total) * 100) : 0;
        
        const ringPctEl = document.getElementById('fillin-ring-pct');
        const ringProg  = document.getElementById('fillin-ring-progress');
        if (ringPctEl) ringPctEl.textContent = pct + '%';
        if (ringProg) {
            const offset = 150.796 - (pct / 100) * 150.796;
            ringProg.style.strokeDashoffset = offset;
            ringProg.style.stroke = pct >= 70 ? 'var(--lp-success)' : pct >= 40 ? 'var(--lp-warning)' : 'var(--lp-error)';
        }

        const progressText = document.getElementById('fillin-progress-text');
        if (progressText) progressText.textContent = correct + ' / ' + total + ' từ đúng';
    }

    // Check all button
    const fillinCheckBtn = document.getElementById('btn-fillin-check');
    const fillinRetryBtn = document.getElementById('btn-fillin-retry');

    if (fillinCheckBtn) {
        fillinCheckBtn.onclick = function () {
            let correct = 0;
            fillinBlanks.forEach(function (b) {
                const input = document.getElementById(b.id);
                if (!input) return;
                const val = input.value.trim();
                // Compare case-insensitive
                if (val.toLowerCase() === b.answer.toLowerCase()) {
                    input.classList.remove('wrong');
                    input.classList.add('correct');
                    correct++;
                    const next = input.nextElementSibling;
                    if (next && next.classList.contains('lp-fillin-answer')) next.remove();
                } else {
                    input.classList.remove('correct');
                    input.classList.add('wrong');
                    input.title = 'Đáp án: ' + b.answer;
                    if (!input.nextElementSibling || !input.nextElementSibling.classList.contains('lp-fillin-answer')) {
                        const span = document.createElement('span');
                        span.className = 'lp-fillin-answer';
                        span.textContent = b.answer;
                        input.parentNode.insertBefore(span, input.nextSibling);
                    }
                }
                input.disabled = true;
            });

            const total = fillinBlanks.length;
            const pct = total > 0 ? Math.round((correct / total) * 100) : 0;
            
            const progressText = document.getElementById('fillin-progress-text');
            if (progressText) progressText.textContent = correct + ' / ' + total + ' từ đúng';
            
            // Show summary
            const summaryEl = document.getElementById('fillin-summary');
            if (summaryEl) {
                document.getElementById('fillin-stat-correct').textContent = correct;
                document.getElementById('fillin-stat-wrong').textContent = total - correct;
                summaryEl.classList.add('show');
            }

            if (fillinRetryBtn) fillinRetryBtn.style.display = 'inline-flex';
            fillinCheckBtn.disabled = true;

            showToast(pct >= 70 ? 'success' : 'error', 'Kết quả: ' + correct + '/' + total + ' (' + pct + '%)');
        };
    }

    if (fillinRetryBtn) {
        fillinRetryBtn.onclick = function () {
            fillinInited = false;
            fillinRetryBtn.style.display = 'none';
            if (fillinCheckBtn) fillinCheckBtn.disabled = false;
            initFillIn();
        };
    }

    const retrySummaryBtn = document.getElementById('btn-fillin-retry-summary');
    if (retrySummaryBtn) {
        retrySummaryBtn.onclick = function () {
            const summaryEl = document.getElementById('fillin-summary');
            if (summaryEl) summaryEl.classList.remove('show');
            fillinInited = false;
            if (fillinCheckBtn) fillinCheckBtn.disabled = false;
            initFillIn();
        };
    }

    // ══════════════════════════════════════════════════════════════
    // 8. QUIZ
    // ══════════════════════════════════════════════════════════════
    const submitBtn   = document.getElementById('btn-submit-quiz');
    const resultPanel = document.getElementById('quiz-result-panel');
    let quizSubmitted = false;

    if (submitBtn) {
        submitBtn.addEventListener('click', async function () {
            if (quizSubmitted) return;

            const answers = {};
            let allAnswered = true;

            document.querySelectorAll('.lp-quiz-item').forEach(item => {
                const qId    = parseInt(item.dataset.questionId);
                const chosen = item.querySelector('input[type="radio"]:checked');
                if (!chosen) {
                    allAnswered = false;
                    item.style.outline = '2px solid var(--lp-error)';
                    setTimeout(() => { item.style.outline = ''; }, 2000);
                } else {
                    answers[qId] = chosen.value;
                }
            });

            if (!allAnswered) {
                showToast('error', 'Hãy trả lời tất cả các câu hỏi!');
                return;
            }

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang chấm...';

            try {
                const response = await fetch('/Home/Listening/EvaluateQuiz', {
                    method: 'POST',
                    headers: {
                        'Content-Type':            'application/json',
                        'RequestVerificationToken': getCsrfToken()
                    },
                    body: JSON.stringify({ lessonId: lessonId, answers: answers })
                });

                if (!response.ok) throw new Error('Server error');

                const json = await response.json();
                if (!json.success) throw new Error('Evaluate failed');

                quizSubmitted = true;
                displayQuizResult(json.data);
                await saveProgress({ quizCompleted: true, quizScore: json.data.scorePercent });

            } catch (err) {
                console.error(err);
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fas fa-paper-plane"></i> Nộp bài';
                showToast('error', 'Có lỗi xảy ra, vui lòng thử lại.');
            }
        });
    }

    function displayQuizResult(result) {
        result.answers.forEach(ans => {
            const item = document.querySelector(`.lp-quiz-item[data-question-id="${ans.questionId}"]`);
            if (!item) return;

            item.classList.add(ans.isCorrect ? 'correct' : 'wrong');
            item.querySelectorAll('input[type="radio"]').forEach(r => r.disabled = true);

            item.querySelectorAll('.lp-option-label').forEach(label => {
                const radio = label.querySelector('input[type="radio"]');
                if (!radio) return;
                if (radio.value === ans.correctAnswer) {
                    label.classList.add('lp-opt-correct');
                } else if (radio.value === ans.userAnswer && !ans.isCorrect) {
                    label.classList.add('lp-opt-wrong');
                }
            });

            const expEl = item.querySelector('.lp-explanation');
            if (expEl) {
                expEl.innerHTML = `<strong>Giải thích:</strong> ${ans.explanation || 'Không có giải thích.'}`;
                expEl.className = 'lp-explanation show ' + (ans.isCorrect ? 'correct' : 'wrong');
            }
        });

        submitBtn.innerHTML = '<i class="fas fa-check-circle"></i> Đã nộp bài';
        submitBtn.disabled  = true;

        if (resultPanel) {
            const passed = result.passed;
            resultPanel.className = 'lp-result-panel show ' + (passed ? 'lp-result-pass' : 'lp-result-fail');

            document.getElementById('result-emoji').textContent = passed ? '🎉' : '😅';
            document.getElementById('result-score').textContent = `${result.correctCount} / ${result.totalQuestions}`;
            document.getElementById('result-pct').textContent   = `${result.scorePercent}%`;
            document.getElementById('result-msg').textContent   = passed
                ? 'Xuất sắc! Bạn đã vượt qua bài kiểm tra.'
                : 'Chưa đạt. Hãy nghe lại và thử lại nhé!';

            resultPanel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }

        updateProgressDot('dot-quiz');
        showToast('success', `Điểm của bạn: ${result.scorePercent}%`);
    }

    // ── Retry Quiz ──────────────────────────────────────────────
    const retryQuizBtn = document.getElementById('btn-retry-quiz');
    if (retryQuizBtn) {
        retryQuizBtn.addEventListener('click', () => {
            quizSubmitted = false;
            
            // Reset items
            document.querySelectorAll('.lp-quiz-item').forEach(item => {
                item.classList.remove('correct', 'wrong');
                item.querySelectorAll('input[type="radio"]').forEach(r => {
                    r.disabled = false;
                    r.checked  = false;
                });
                item.querySelectorAll('.lp-option-label').forEach(label => {
                    label.classList.remove('lp-opt-correct', 'lp-opt-wrong');
                });
                const expEl = item.querySelector('.lp-explanation');
                if (expEl) {
                    expEl.classList.remove('show', 'correct', 'wrong');
                    expEl.innerHTML = '';
                }
            });

            // Reset footer & results
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fas fa-paper-plane"></i> Nộp bài';
            }
            if (resultPanel) {
                resultPanel.classList.remove('show', 'lp-result-pass', 'lp-result-fail');
            }

            // Scroll to top of quiz
            const quizList = document.querySelector('.lp-quiz-list');
            if (quizList) quizList.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 15. PROGRESS SAVE
    // ══════════════════════════════════════════════════════════════
    async function saveProgress(dto) {
        if (!isAuthenticated) return;
        try {
            const response = await fetch(`/Home/Listening/SaveProgress/${lessonId}`, {
                method: 'POST',
                headers: {
                    'Content-Type':            'application/json',
                    'RequestVerificationToken': getCsrfToken()
                },
                body: JSON.stringify(dto)
            });
            if (!response.ok) throw new Error('Save failed');
        } catch (err) {
            console.warn('Progress save error:', err);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 16. PROGRESS INDICATORS
    // ══════════════════════════════════════════════════════════════
    function updateProgressDot(id) {
        const dot = document.getElementById(id);
        if (dot) dot.classList.add('done');
    }

    // ══════════════════════════════════════════════════════════════
    // 17. TOAST
    // ══════════════════════════════════════════════════════════════
    const toastEl   = document.getElementById('lp-toast');
    let  toastTimer = null;

    function showToast(type, msg) {
        if (!toastEl) return;
        clearTimeout(toastTimer);
        toastEl.className = `lp-toast${type ? ' lp-toast-' + type : ''} lp-toast-show`;
        toastEl.innerHTML = msg;
        toastTimer = setTimeout(() => {
            toastEl.classList.remove('lp-toast-show');
        }, 3500);
    }

    // ══════════════════════════════════════════════════════════════
    // 18. UTILITIES
    // ══════════════════════════════════════════════════════════════
    function escHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // ══════════════════════════════════════════════════════════════
    // 19. SPEED CONTROL
    // ══════════════════════════════════════════════════════════════
    const speedBtns = document.querySelectorAll('.lp-speed-btn:not(.lp-speed-locked):not([disabled])');
    speedBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const rate = parseFloat(btn.dataset.rate);
            if (!rate) return;

            // YouTube
            if (window.ytPlayer && typeof window.ytPlayer.setPlaybackRate === 'function') {
                window.ytPlayer.setPlaybackRate(rate);
            } 
            // HTML5 Audio
            else if (audioEl) {
                audioEl.playbackRate = rate;
            }

            // Update UI
            document.querySelectorAll('.lp-speed-btn').forEach(b => b.classList.remove('lp-speed-active'));
            btn.classList.add('lp-speed-active');
            
            console.log('[LP] Playback rate set to:', rate);
        });
    });

    // ══════════════════════════════════════════════════════════════
    // 20. AI QUIZ GENERATOR
    // ══════════════════════════════════════════════════════════════
    const btnGenQuiz = document.getElementById('btnGenerateQuiz');
    if (btnGenQuiz) {
        btnGenQuiz.addEventListener('click', async () => {
            const lessonId = btnGenQuiz.dataset.lessonId;
            const token = getCsrfToken();
            
            btnGenQuiz.disabled = true;
            btnGenQuiz.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang biên soạn...';

            try {
                const formData = new FormData();
                formData.append('lessonId', lessonId);
                if (token) formData.append('__RequestVerificationToken', token);

                const resp = await fetch('/Home/Listening/My/GenerateQuiz', {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': token },
                    body: formData
                });

                if (resp.ok) {
                    location.reload(); 
                } else {
                    let errorMessage = 'Không thể tạo bài tập.';
                    try {
                        const data = await resp.json();
                        errorMessage = data.message || data.error || errorMessage;
                    } catch (e) {
                         // Fallback to text reading if JSON is malformed
                         const textResponse = await resp.text();
                         console.warn("Could not parse JSON response from Server, body was:", textResponse);
                    }
                    
                    alert(errorMessage);
                    btnGenQuiz.disabled = false;
                    btnGenQuiz.innerHTML = '<i class="fas fa-magic"></i> Tạo quiz bằng AI';
                }
            } catch (err) {
                console.error(err);
                alert('Lỗi kết nối. Vui lòng thử lại.');
                btnGenQuiz.disabled = false;
                btnGenQuiz.innerHTML = '<i class="fas fa-magic"></i> Tạo quiz bằng AI';
            }
        });
    }

})();
