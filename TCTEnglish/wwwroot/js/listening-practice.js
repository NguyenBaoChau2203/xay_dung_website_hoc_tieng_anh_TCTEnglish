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
    let currentSpeed      = 1;
    let transcriptPolling = null;
    let autoScrollEnabled = false;

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
                            onPlaybackEnded();
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
            onPlaybackEnded();
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

    // ── Speed control ─────────────────────────────────────────────
    document.querySelectorAll('.lp-speed-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const rate = parseFloat(this.dataset.rate);
            if (isNaN(rate)) return;

            currentSpeed = rate;
            document.querySelectorAll('.lp-speed-btn').forEach(b => b.classList.remove('lp-speed-active'));
            this.classList.add('lp-speed-active');

            if (ytPlayer && typeof ytPlayer.setPlaybackRate === 'function') {
                ytPlayer.setPlaybackRate(rate);
            }
            if (audioEl) {
                audioEl.playbackRate = rate;
            }
        });
    });

    // ══════════════════════════════════════════════════════════════
    // 4. LISTEN COUNTER
    // ══════════════════════════════════════════════════════════════
    let listenCount = 0;
    const listenCounterEl = document.getElementById('lp-listen-counter');
    const listenCountNum  = document.getElementById('lp-count-num');

    function onPlaybackEnded() {
        listenCount++;
        if (listenCountNum) listenCountNum.textContent = listenCount;
        if (listenCounterEl) {
            if (listenCount >= 3) {
                listenCounterEl.classList.add('lp-listened-enough');
                showToast('success', `Đã nghe ${listenCount} lần — Rất tốt! 👏`);
            } else {
                showToast('', `Đã nghe ${listenCount} lần — mục tiêu 3 lần 🎧`);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 5. A-B LOOP
    // ══════════════════════════════════════════════════════════════
    let abState    = 'off';   // 'off' | 'setA' | 'looping'
    let abPointA   = 0;
    let abPointB   = 0;
    let abInterval = null;
    const abBtn    = document.getElementById('btn-ab-loop');
    const abLabel  = document.getElementById('ab-btn-label');

    if (abBtn) {
        abBtn.addEventListener('click', function () {
            if (abState === 'off') {
                // → State: set A
                abPointA = getCurrentTime();
                abState  = 'setA';
                abBtn.dataset.state = 'setA';
                if (abLabel) abLabel.textContent = `A=${abPointA.toFixed(1)}s — nhấn để đặt B`;
                showToast('', `✅ Điểm A = ${abPointA.toFixed(1)}s`);

            } else if (abState === 'setA') {
                // → State: looping
                abPointB = getCurrentTime();
                if (abPointB <= abPointA) {
                    showToast('error', 'Điểm B phải sau điểm A!');
                    return;
                }
                abState = 'looping';
                abBtn.dataset.state = 'looping';
                if (abLabel) abLabel.textContent = `A-B (${abPointA.toFixed(1)}–${abPointB.toFixed(1)}s)`;
                seekTo(abPointA);

                // Start looping check
                abInterval = setInterval(() => {
                    const t = getCurrentTime();
                    if (t >= abPointB) {
                        seekTo(abPointA);
                    }
                }, 200);

                showToast('success', `🔁 Lặp ${abPointA.toFixed(1)}s → ${abPointB.toFixed(1)}s`);

            } else {
                // → State: off (reset)
                clearInterval(abInterval);
                abInterval = null;
                abState    = 'off';
                abPointA   = 0;
                abPointB   = 0;
                abBtn.dataset.state = 'off';
                if (abLabel) abLabel.textContent = 'A-B Loop';
                showToast('', 'A-B Loop đã tắt');
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 6. AUTO-SCROLL TOGGLE
    // ══════════════════════════════════════════════════════════════
    const autoScrollBtn = document.getElementById('btn-auto-scroll');

    if (autoScrollBtn) {
        autoScrollBtn.addEventListener('click', function () {
            autoScrollEnabled = !autoScrollEnabled;
            autoScrollBtn.classList.toggle('active', autoScrollEnabled);
            autoScrollBtn.innerHTML = autoScrollEnabled
                ? '<i class="fas fa-scroll"></i> Tự cuộn: Bật'
                : '<i class="fas fa-scroll"></i> Tự cuộn';
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 7. TAB SWITCHING
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
    // 8. MODE SELECTOR
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
            } else if (mode === 'shadowing' && !shadowingInitialized) {
                initShadowing();
            }
        });
    });

    // ══════════════════════════════════════════════════════════════
    // 9. TRANSCRIPT (MODE 1)
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
        let activeEl = null;
        transcriptLines.forEach(line => {
            const start = parseFloat(line.dataset.start);
            const end   = parseFloat(line.dataset.end);
            if (!isNaN(start) && !isNaN(end) && t >= start && t <= end) {
                line.classList.add('lp-line-active');
                activeEl = line;
            } else {
                line.classList.remove('lp-line-active');
            }
        });

        // Auto-scroll to active line
        if (autoScrollEnabled && activeEl) {
            activeEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
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
    // 10. DICTATION MODE (MODE 2)
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
        console.log('[LP] initDictation triggered');
        
        // Final fallback in case timing was weird
        if ((!transcriptData || transcriptData.length === 0) && window.LP_TRANSCRIPT && window.LP_TRANSCRIPT.length > 0) {
            console.log('[LP] transcriptData was empty but window.LP_TRANSCRIPT has data. Re-initializing...');
            transcriptData = window.LP_TRANSCRIPT.slice().sort((a, b) => a.orderIndex - b.orderIndex);
        }

        const container = document.getElementById('dictation-lines');
        if (!container) return;
        
        if (!transcriptData || transcriptData.length === 0) {
            console.warn('[LP] No transcript data for dictation. window.LP_TRANSCRIPT:', window.LP_TRANSCRIPT);
            container.innerHTML = '<p class="text-muted p-3">Không có nội dung transcript để luyện tập.</p>';
            return;
        }

        dictationInited = true;
        container.innerHTML = '';

        const speakerMap = new Map();
        let speakerIdx = 0;

        transcriptData.forEach((line, idx) => {
            if (!speakerMap.has(line.speaker)) {
                speakerMap.set(line.speaker, speakerIdx++);
            }
            const spClass = speakerMap.get(line.speaker) === 0 ? '' : 'speaker-b';

            const item = document.createElement('div');
            item.className = 'lp-dictation-item'; 
            item.innerHTML = `
                <div class="lp-dict-item-header">
                    <span class="lp-dict-num">${idx + 1}</span>
                    <span class="lp-dict-speaker-tag ${spClass}">${escHtml(line.speaker)}</span>
                    ${line.startTime != null
                        ? `<button class="lp-dict-listen-btn" data-start="${line.startTime}">
                               <i class="fas fa-play"></i> Nghe
                           </button>`
                        : ''}
                </div>
                <textarea class="lp-dict-textarea" rows="2" placeholder="Nhập những gì bạn nghe được…"></textarea>
                <div class="lp-dict-feedback" style="display:none; margin-top:10px; line-height:1.6"></div>
                <div class="lp-dict-answer" style="display:none; margin-top:5px; color:var(--lp-muted); font-size:0.9rem">
                    <strong>Đáp án:</strong> ${escHtml(line.text)}
                </div>
                <div style="margin-top:10px">
                    <button class="lp-dict-check-btn">Kiểm tra</button>
                </div>
            `;

            container.appendChild(item);

            const ta = item.querySelector('.lp-dict-textarea');
            const checkBtn = item.querySelector('.lp-dict-check-btn');
            const listenBtn = item.querySelector('.lp-dict-listen-btn');

            if (listenBtn) {
                listenBtn.addEventListener('click', () => seekTo(line.startTime));
            }

            if (ta) {
                ta.addEventListener('keydown', (e) => {
                    if (e.key === 'Tab') {
                        e.preventDefault();
                        if (line.startTime != null) seekTo(line.startTime);
                    } else if (e.key === 'Enter' && !e.shiftKey) {
                        e.preventDefault();
                        checkDictationLine(item, line);
                    }
                });
            }

            if (checkBtn) {
                checkBtn.addEventListener('click', () => checkDictationLine(item, line));
            }
        });

        console.log(`[LP] Generated ${transcriptData.length} dictation items`);
        updateDictRing(0);
    }

    function checkDictationLine(item, line) {
        if (item.classList.contains('lp-dict-done')) return;

        const ta = item.querySelector('.lp-dict-textarea');
        const feedbackEl = item.querySelector('.lp-dict-feedback');
        const answerEl = item.querySelector('.lp-dict-answer');
        const checkBtn = item.querySelector('.lp-dict-check-btn');

        if (!ta || !feedbackEl) return;

        const userText = ta.value.trim();
        const targetText = line.text;

        const userWords = userText.toLowerCase().replace(/[^a-z0-9'\s]/g, '').split(/\s+/).filter(Boolean);
        const targetWords = targetText.toLowerCase().replace(/[^a-z0-9'\s]/g, '').split(/\s+/).filter(Boolean);

        let correctInLine = 0;
        let nearInLine = 0;
        let wrongInLine = 0;

        let html = '';
        const maxLen = Math.max(userWords.length, targetWords.length);

        for (let i = 0; i < maxLen; i++) {
            const u = userWords[i] || '';
            const t = targetWords[i] || '';

            if (u === t && u !== '') {
                html += `<span class="lp-w-correct">${escHtml(u)}</span> `;
                correctInLine++;
            } else if (t !== '' && u !== '' && levenshtein(u, t) <= 2) {
                html += `<span class="lp-w-near" title="Đúng: ${escHtml(t)}">${escHtml(u)}</span> `;
                nearInLine++;
            } else if (t !== '') {
                html += `<span class="lp-w-wrong" title="Đúng: ${escHtml(t)}">${escHtml(u || '—')}</span> `;
                wrongInLine++;
            }
        }

        feedbackEl.innerHTML = html;
        feedbackEl.style.display = 'block';
        answerEl.style.display = 'block';
        item.classList.add('lp-dict-done');
        ta.disabled = true;
        if (checkBtn) checkBtn.disabled = true;

        dictCorrect += correctInLine;
        dictNear += nearInLine;
        dictWrong += wrongInLine;
        dictTotalWords += maxLen;

        const pct = dictTotalWords > 0 ? Math.round((dictCorrect + dictNear * 0.5) / dictTotalWords * 100) : 0;
        updateDictRing(pct);

        const totalItems = document.querySelectorAll('.lp-dictation-item').length;
        const doneItems  = document.querySelectorAll('.lp-dictation-item.lp-dict-done').length;
        if (doneItems === totalItems) {
            showDictSummary();
        }
    }

    function updateDictRing(pct) {
        const ringPctEl = document.getElementById('dict-ring-pct');
        const ringProg  = document.getElementById('dict-ring-progress');
        if (ringPctEl) ringPctEl.textContent = pct + '%';
        if (ringProg) {
            const circumference = 150.796;
            const offset = circumference - (pct / 100) * circumference;
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
    // 11. FILL-IN-THE-BLANKS MODE (MODE 3)
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
    // 12. SHADOWING MODE (MODE 4)
    // ══════════════════════════════════════════════════════════════
    let shadowingInitialized = false;
    let shadowIdx = 0;
    let shadowTextVisible = false;
    let shadowListenTimer = null;

    function initShadowing() {
        shadowingInitialized = true;
        shadowIdx = 0;
        shadowTextVisible = false;

        const totalEl = document.getElementById('shadow-total');
        if (totalEl) totalEl.textContent = transcriptData.length;

        renderShadowCard();
    }

    function renderShadowCard() {
        // Final fallback
        if ((!transcriptData || transcriptData.length === 0) && window.LP_TRANSCRIPT && window.LP_TRANSCRIPT.length > 0) {
            console.log('[LP] transcriptData was empty but window.LP_TRANSCRIPT has data. Re-initializing...');
            transcriptData = window.LP_TRANSCRIPT.slice().sort((a, b) => a.orderIndex - b.orderIndex);
        }

        if (!transcriptData || !transcriptData.length) {
            console.warn('[LP] No transcript data for shadowing');
            const card = document.getElementById('shadow-card');
            if (card) card.innerHTML = '<p class="text-muted p-3">Không có nội dung transcript để shadowing.</p>';
            return;
        }

        const line = transcriptData[shadowIdx];

        // Counter
        const idxDisplay = document.getElementById('shadow-idx-display');
        if (idxDisplay) idxDisplay.textContent = shadowIdx + 1;

        // Progress
        const pct = Math.round(shadowIdx / transcriptData.length * 100);
        const fill = document.getElementById('shadow-progress-fill');
        if (fill) fill.style.width = pct + '%';
        const pgLabel = document.getElementById('shadow-progress-label');
        if (pgLabel) pgLabel.textContent = pct + '%';

        // Speaker
        const speakerEl = document.getElementById('shadow-speaker');
        if (speakerEl) {
            speakerEl.textContent = line.speaker;
            speakerEl.className   = 'lp-shadow-speaker';
            // Detect 2nd speaker (simple approach: track unique speakers)
            const speakerSeen = {};
            let spIdx = 0;
            transcriptData.forEach(l => {
                if (speakerSeen[l.speaker] == null) speakerSeen[l.speaker] = spIdx++;
            });
            if (speakerSeen[line.speaker] > 0) speakerEl.classList.add('speaker-b');
        }

        // Reset text visibility
        shadowTextVisible = false;
        const textWrap = document.getElementById('shadow-text-wrap');
        const hiddenHint = document.getElementById('shadow-hidden-hint');
        if (textWrap)    textWrap.classList.remove('visible');
        if (hiddenHint)  hiddenHint.style.display = '';

        const shadowText = document.getElementById('shadow-text');
        const shadowVi   = document.getElementById('shadow-vi');
        if (shadowText) shadowText.textContent = line.text;
        if (shadowVi)   shadowVi.textContent   = line.vi ? '🇻🇳 ' + line.vi : '';

        // Status
        setStatus('Nhấn Nghe để bắt đầu', false);

        // Reset card state
        const card = document.getElementById('shadow-card');
        if (card) card.classList.remove('lp-shadow-listening');

        // Reset button states
        setShadowBtnState('idle');
    }

    function setStatus(msg, isListening) {
        const el = document.getElementById('shadow-status');
        if (!el) return;
        el.textContent = msg;
        el.className   = 'lp-shadow-status' + (isListening ? ' listening' : '');
    }

    function setShadowBtnState(state) {
        const listenBtn   = document.getElementById('btn-shadow-listen');
        const relistenBtn = document.getElementById('btn-shadow-relisten');
        const showBtn     = document.getElementById('btn-shadow-show-text');
        const nextBtn     = document.getElementById('btn-shadow-next');

        if (state === 'idle') {
            listenBtn.disabled   = false;
            relistenBtn.disabled = true;
            showBtn.disabled     = false;
            nextBtn.disabled     = shadowIdx >= transcriptData.length - 1;
        } else if (state === 'listening') {
            listenBtn.disabled   = true;
            relistenBtn.disabled = true;
            showBtn.disabled     = false;
            nextBtn.disabled     = true;
        } else if (state === 'done') {
            listenBtn.disabled   = false;
            relistenBtn.disabled = false;
            showBtn.disabled     = false;
            nextBtn.disabled     = shadowIdx >= transcriptData.length - 1;
        }
    }

    function shadowListen(line) {
        if (line.startTime == null) {
            setStatus('Câu này không có timestamps', false);
            return;
        }

        const card = document.getElementById('shadow-card');
        if (card) card.classList.add('lp-shadow-listening');
        setStatus('Đang phát…', true);
        setShadowBtnState('listening');

        seekTo(line.startTime);

        // Calculate duration
        const endTime  = line.endTime != null ? line.endTime : line.startTime + 5;
        const duration = ((endTime - line.startTime) / currentSpeed) * 1000 + 500;

        clearTimeout(shadowListenTimer);
        shadowListenTimer = setTimeout(() => {
            pausePlayer();
            if (card) card.classList.remove('lp-shadow-listening');
            setStatus('🎙️ Bây giờ hãy lặp lại!', false);
            setShadowBtnState('done');
        }, duration);
    }

    // Shadow: Listen button
    const btnShadowListen = document.getElementById('btn-shadow-listen');
    if (btnShadowListen) {
        btnShadowListen.addEventListener('click', () => {
            if (!transcriptData.length) return;
            shadowListen(transcriptData[shadowIdx]);
        });
    }

    // Shadow: Re-listen button
    const btnShadowRelisten = document.getElementById('btn-shadow-relisten');
    if (btnShadowRelisten) {
        btnShadowRelisten.addEventListener('click', () => {
            if (!transcriptData.length) return;
            shadowListen(transcriptData[shadowIdx]);
        });
    }

    // Shadow: Show text button
    const btnShadowShowText = document.getElementById('btn-shadow-show-text');
    if (btnShadowShowText) {
        btnShadowShowText.addEventListener('click', () => {
            shadowTextVisible = !shadowTextVisible;
            const textWrap   = document.getElementById('shadow-text-wrap');
            const hiddenHint = document.getElementById('shadow-hidden-hint');
            if (textWrap)    textWrap.classList.toggle('visible', shadowTextVisible);
            if (hiddenHint)  hiddenHint.style.display = shadowTextVisible ? 'none' : '';
            btnShadowShowText.innerHTML = shadowTextVisible
                ? '<i class="fas fa-eye-slash"></i> Ẩn text'
                : '<i class="fas fa-eye"></i> Hiện text';
        });
    }

    // Shadow: Next button
    const btnShadowNext = document.getElementById('btn-shadow-next');
    if (btnShadowNext) {
        btnShadowNext.addEventListener('click', () => {
            clearTimeout(shadowListenTimer);
            if (shadowIdx < transcriptData.length - 1) {
                shadowIdx++;
                renderShadowCard();
            } else {
                // All done
                const fill = document.getElementById('shadow-progress-fill');
                if (fill) fill.style.width = '100%';
                const pgLabel = document.getElementById('shadow-progress-label');
                if (pgLabel) pgLabel.textContent = '100%';
                showToast('success', '🎉 Đã hoàn thành Shadowing!');
                setShadowBtnState('idle');
                setStatus('✅ Hoàn thành tất cả', false);
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 13. QUIZ
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
