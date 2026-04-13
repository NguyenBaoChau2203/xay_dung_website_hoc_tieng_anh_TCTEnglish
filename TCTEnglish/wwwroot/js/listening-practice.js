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
    let audioEl           = document.getElementById('lp-audio');
    let transcriptPolling = null;

    // ── YouTube IFrame API ────────────────────────────────────────
    if (youtubeId) {
        window.onYouTubeIframeAPIReady = function () {
            ytPlayer = new YT.Player('youtube-player', {
                events: {
                    onReady: function () {
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
        if (ytPlayer && typeof ytPlayer.seekTo === 'function') {
            ytPlayer.seekTo(seconds, true);
            ytPlayer.playVideo();
        } else if (audioEl) {
            audioEl.currentTime = seconds;
            audioEl.play().catch(() => {});
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
        toggleAllViBtn.addEventListener('click', function () {
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
            item.className = 'lp-fillin-item';
            item.innerHTML = `
                <div class="lp-fillin-item-header">
                    <span class="lp-fillin-num">${lineIdx + 1}</span>
                    <span class="lp-fillin-speaker ${spClass}">${escHtml(line.speaker)}</span>
                    ${line.startTime != null ? `<button class="lp-fillin-play-btn" data-start="${line.startTime}"><i class="fas fa-play"></i></button>` : ''}
                </div>
                <div class="lp-fillin-text" style="line-height:2.2">${lineHtml}</div>
            `;
            container.appendChild(item);

            // Bind click to play button
            const playBtn = item.querySelector('.lp-fillin-play-btn');
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
        });

        const totalCountEl = document.getElementById('fillin-total-count');
        if (totalCountEl) totalCountEl.textContent = fillinBlanks.length;
        
        const correctCountEl = document.getElementById('fillin-correct-count');
        if (correctCountEl) correctCountEl.textContent = '0';

        if (footer) footer.style.display = 'flex';

        // Re-init bar
        const bar = document.getElementById('fillin-progress-fill');
        if (bar) bar.style.width = '0%';
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
        const pct = total > 0 ? Math.round((filled / total) * 100) : 0;
        const bar = document.getElementById('fillin-progress-fill');
        if (bar) bar.style.width = pct + '%';

        const progressText = document.getElementById('fillin-progress-text');
        if (progressText) progressText.textContent = correct + ' / ' + total + ' từ đúng';
        
        const correctCount = document.getElementById('fillin-correct-count');
        if (correctCount) correctCount.textContent = correct;
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
            
            const bar = document.getElementById('fillin-progress-fill');
            if (bar) bar.style.width = pct + '%';

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
            if (expEl) expEl.classList.add('show');
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

    // ══════════════════════════════════════════════════════════════
    // 14. VOCABULARY DONE
    // ══════════════════════════════════════════════════════════════
    const vocabDoneBtn = document.getElementById('btn-vocab-done');

    if (vocabDoneBtn) {
        vocabDoneBtn.addEventListener('click', async function () {
            await saveProgress({ vocabReviewed: true });
            vocabDoneBtn.disabled = true;
            vocabDoneBtn.innerHTML = '<i class="fas fa-check-circle"></i> Đã ôn từ vựng';
            updateProgressDot('dot-vocab');
            showToast('success', 'Đã lưu tiến độ Từ vựng! 📚');
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

})();
