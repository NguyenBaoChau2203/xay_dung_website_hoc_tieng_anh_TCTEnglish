/**
 * speaking.js — TCT Speaking Module
 * Part 1: Index Page — topic filtering, search, slider navigation
 * Part 2: Practice Page (v3) — YouTube IFrame API, sentence sync, speech AI
 */

// ════════════════════════════════════════════════════════════════════
//  PART 1 — INDEX PAGE: Topic Filter, Search & Slider
// ════════════════════════════════════════════════════════════════════
(function () {
    'use strict';

    // Guard: only run on the Index page (has filter buttons)
    const filterBtns = document.querySelectorAll('.topic-filter-btn');
    const levelBtns = document.querySelectorAll('.level-filter-btn');
    if (!filterBtns.length && !levelBtns.length) return;

    // ── DOM refs ─────────────────────────────────────────────────
    const searchInput = document.getElementById('vi-search');
    const videoCounter = document.getElementById('vi-video-count');
    const allCards = document.querySelectorAll('.vi-video-col');
    const levelSections = document.querySelectorAll('.vi-level-section');

    // ── State ────────────────────────────────────────────────────
    let activeTopic = 'all';
    let activeLevel = 'all';

    // ── TOPIC FILTER ─────────────────────────────────────────────
    filterBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            filterBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            activeTopic = btn.dataset.topic;
            if (searchInput) searchInput.value = '';
            applyFilters();
        });
    });

    // ── LEVEL FILTER ─────────────────────────────────────────────
    levelBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            levelBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            activeLevel = btn.dataset.level;
            if (searchInput) searchInput.value = '';
            applyFilters();
        });
    });

    // ── SEARCH ───────────────────────────────────────────────────
    if (searchInput) {
        searchInput.addEventListener('input', () => { applyFilters(); });

        document.addEventListener('keydown', e => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                searchInput.focus();
                searchInput.select();
            }
        });
    }

    // ── CORE FILTER & PAGINATION LOGIC ───────────────────────────
    const ITEMS_PER_PAGE = 6;

    function applyFilters() {
        const query = searchInput ? searchInput.value.toLowerCase().trim() : '';
        let totalVisibleCount = 0;

        levelSections.forEach(section => {
            const level = section.dataset.level;
            const cardsInLevel = section.querySelectorAll('.vi-video-col');
            let visibleCards = [];

            cardsInLevel.forEach(card => {
                const cardTopic = (card.dataset.topic || '').trim();
                const cardTitle = (card.dataset.title || '').trim();
                const cardLevel = (card.dataset.level || '').trim();

                const topicMatch = activeTopic === 'all' || cardTopic === activeTopic;
                const searchMatch = !query || cardTitle.includes(query);
                const levelMatch = activeLevel === 'all' || cardLevel === activeLevel;

                if (topicMatch && searchMatch && levelMatch) {
                    visibleCards.push(card);
                    totalVisibleCount++;
                } else {
                    card.classList.add('vi-card-hidden');
                }
            });

            if (visibleCards.length === 0) {
                section.classList.add('vi-section-hidden');
            } else {
                section.classList.remove('vi-section-hidden');
                renderPagination(level, visibleCards);
            }
        });

        if (videoCounter) {
            videoCounter.textContent = query || activeTopic !== 'all'
                ? `${totalVisibleCount} result${totalVisibleCount !== 1 ? 's' : ''}`
                : `${totalVisibleCount} videos`;
        }
    }

    function renderPagination(level, visibleCards) {
        const container = document.querySelector(`.numbered-pagination-container[data-level="${level}"]`);
        const track = document.getElementById(`track-${level}`);
        if (!container || !track) return;

        visibleCards.forEach(card => card.classList.remove('vi-card-hidden'));

        const totalItems = visibleCards.length;
        const totalPages = Math.ceil(totalItems / ITEMS_PER_PAGE);
        container.innerHTML = '';

        if (totalPages <= 1) return;

        for (let i = 1; i <= totalPages; i++) {
            const btn = document.createElement('button');
            btn.className = 'page-btn';
            btn.textContent = i;
            if (i === 1) btn.classList.add('active');

            btn.addEventListener('click', () => {
                container.querySelectorAll('.page-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                const firstCard = visibleCards[0];
                if (firstCard) {
                    const cardWidthWithGap = firstCard.offsetWidth + 16;
                    track.scrollTo({ left: (i - 1) * ITEMS_PER_PAGE * cardWidthWithGap, behavior: 'smooth' });
                }
            });

            container.appendChild(btn);
        }

        track.scrollTo({ left: 0, behavior: 'smooth' });
    }

    levelSections.forEach(section => {
        const level = section.dataset.level;
        const track = document.getElementById(`track-${level}`);
        if (track) {
            track.addEventListener('scroll', () => {
                const container = document.querySelector(`.numbered-pagination-container[data-level="${level}"]`);
                if (!container) return;
                const visibleCards = Array.from(track.querySelectorAll('.vi-video-col:not(.vi-card-hidden)'));
                if (visibleCards.length === 0) return;
                const cardWidthWithGap = visibleCards[0].offsetWidth + 16;
                const currentPage = Math.floor(track.scrollLeft / (cardWidthWithGap * ITEMS_PER_PAGE)) + 1;
                const buttons = container.querySelectorAll('.page-btn');
                buttons.forEach(b => b.classList.remove('active'));
                const activeBtn = buttons[currentPage - 1];
                if (activeBtn) activeBtn.classList.add('active');
            });
        }
    });

    applyFilters(); // ensure correct state on page load

})();


// ════════════════════════════════════════════════════════════════════
//  PART 2 — PRACTICE PAGE (v3 — spk-* DOM)
// ════════════════════════════════════════════════════════════════════
/**
 * speaking.js — TCT Speaking Practice v3
 * Matches Practice.cshtml v3 using spk-* class conventions.
 * Features: YouTube IFrame API, sentence slider, replay, record (Web Speech API),
 *           SVG circular ring scores, mini bar scores, keyboard shortcuts, tab switching.
 */
(function (global) {
    'use strict';

    // ── Guard: only run on the Practice page ────────────────────────
    if (!global.SPK_VIDEO_ID) return;

    // ── Data from Razor ─────────────────────────────────────────────
    const VIDEO_ID = global.SPK_VIDEO_ID;
    const SENTENCES = global.SPK_SENTENCES || [];

    // ── DOM utilities ────────────────────────────────────────────────
    const $ = id => document.getElementById(id);
    const $$ = sel => document.querySelectorAll(sel);

    // ── DOM refs ─────────────────────────────────────────────────────
    const btnPrev = $('btn-prev');
    const btnNext = $('btn-next');
    const btnReplay = $('btn-replay');
    const btnRecord = $('btn-record');
    const recordIcon = $('record-icon');
    const recordLabel = $('record-label');
    const sentIndicator = $('sent-indicator');
    const transcriptEn = $('current-transcript-en');
    const transcriptVi = $('current-transcript-vi');
    const sliderTrack = $('sentence-scroll-container');
    const btnSliderPrev = $('btn-slider-prev');
    const btnSliderNext = $('btn-slider-next');
    const dicSliderTrack = $('dic-sentence-scroll-container');
    const btnDicSliderPrev = $('btn-dic-slider-prev');
    const btnDicSliderNext = $('btn-dic-slider-next');
    const btnHideText = $('btn-hide-text');
    const progBar = $('prog-bar');
    const progText = $('prog-text');
    const progPct = $('prog-pct');
    const toastEl = $('spk-toast');
    const tabPronunciation = $('tab-pronunciation');
    const tabDictation = $('tab-dictation');
    const panePronounciation = $('pronunciation-content');
    const paneDictation = $('dictation-content');
    const btnHideVideo = $('btn-dic-hide-video');
    const videoOverlay = $('video-overlay');
    const youtubeFrame = $('youtube-player');
    const dicInput = $('dic-input');
    const dicFeedback = $('dic-feedback');
    const dicSentIndicator = $('dic-sent-indicator');

    // ── State ────────────────────────────────────────────────────────
    let player = null;
    let activeSentIdx = -1;
    let isRecording = false;
    let isTextHidden = false;
    let isVideoHidden = false;
    let playbackTimer = null;
    let toastTimer = null;

    // ────────────────────────────────────────────────────────────────
    //  YOUTUBE IFRAME API
    // ────────────────────────────────────────────────────────────────
    function loadYouTubeAPI() {
        const tag = document.createElement('script');
        tag.src = 'https://www.youtube.com/iframe_api';
        document.head.appendChild(tag);
    }

    global.onYouTubeIframeAPIReady = function () {
        player = new YT.Player('youtube-player', {
            videoId: VIDEO_ID,
            playerVars: {
                rel: 0,
                modestbranding: 1,
                controls: 1,
                iv_load_policy: 3,
                cc_load_policy: 0,
                enablejsapi: 1
            },
            events: {
                onReady: onPlayerReady,
                onStateChange: onPlayerStateChange
            }
        });
    };

    function onPlayerReady() {
        updateProgressUI();
        if (SENTENCES.length > 0) selectSentence(0, false);
    }

    function onPlayerStateChange(evt) {
        if (evt.data === YT.PlayerState.PLAYING) {
            if (!playbackTimer) playbackTimer = setInterval(checkLoopBoundary, 120);
        } else {
            if (playbackTimer) { clearInterval(playbackTimer); playbackTimer = null; }
        }
    }

    function checkLoopBoundary() {
        if (activeSentIdx < 0 || !player || typeof player.getCurrentTime !== 'function') return;
        const s = SENTENCES[activeSentIdx];
        if (player.getCurrentTime() >= s.end) player.pauseVideo();
    }

    // ────────────────────────────────────────────────────────────────
    //  SENTENCE SELECTION
    // ────────────────────────────────────────────────────────────────
    function selectSentence(idx, autoPlay = true) {
        if (idx < 0 || idx >= SENTENCES.length) return;
        activeSentIdx = idx;
        const s = SENTENCES[idx];

        // Update slider buttons
        $$('.spk-sent-btn').forEach(btn => btn.classList.remove('spk-sent-btn--active'));
        const activeBtn = document.querySelector(`.spk-sent-btn[data-index="${idx}"]`);
        if (activeBtn) {
            activeBtn.classList.add('spk-sent-btn--active');
            activeBtn.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
        }
        // Sync dictation slider
        const activeDicBtn = document.querySelector(`.spk-sent-btn[data-dic-index="${idx}"]`);
        if (activeDicBtn) {
            activeDicBtn.classList.add('spk-sent-btn--active');
            activeDicBtn.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
        }

        // Update transcript
        if (transcriptEn) {
            transcriptEn.textContent = isTextHidden ? '••••••••••••••••' : s.text;
            transcriptEn.dataset.original = s.text;
        }
        if (transcriptVi) transcriptVi.textContent = s.vi || '';

        // Update indicator
        if (sentIndicator) sentIndicator.textContent = `(Câu hỏi ${idx + 1}/${SENTENCES.length})`;

        // Update dictation indicator
        if (dicSentIndicator) dicSentIndicator.textContent = `(Câu hỏi ${idx + 1}/${SENTENCES.length})`;

        // Reset dictation state
        if (dicInput) dicInput.value = '';
        renderDictationFeedback('', s.text);

        // Update score board from stored data
        updateScoreBoard(s);

        // YouTube seek
        if (player && typeof player.seekTo === 'function') {
            player.seekTo(s.start, true);
            if (autoPlay) player.playVideo();
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  SCORE BOARD — SVG rings + mini bars
    // ────────────────────────────────────────────────────────────────
    const SCORE_METRICS = [
        { val: 'totalScore', id: 'score-total', ringId: 'ring-total', barId: 'bar-total' },
        { val: 'accuracyScore', id: 'score-accuracy', ringId: 'ring-accuracy', barId: 'bar-accuracy' },
        { val: 'fluencyScore', id: 'score-fluency', ringId: 'ring-fluency', barId: 'bar-fluency' },
        { val: 'completenessScore', id: 'score-complete', ringId: 'ring-complete', barId: 'bar-complete' },
    ];

    function updateScoreBoard(sentence) {
        SCORE_METRICS.forEach(m => {
            const raw = sentence[m.val];
            const pct = (raw && raw > 0) ? Math.round(raw) : 0;

            const valEl = $(m.id);
            if (valEl) valEl.textContent = pct > 0 ? `${pct}` : '0';

            // SVG ring: stroke-dasharray = "filled, 100"
            const ringEl = $(m.ringId);
            if (ringEl) ringEl.setAttribute('stroke-dasharray', `${pct}, 100`);

            // Mini bar
            const barEl = $(m.barId);
            if (barEl) barEl.style.width = pct + '%';
        });
    }

    // ────────────────────────────────────────────────────────────────
    //  PROGRESS BAR (overall completion)
    // ────────────────────────────────────────────────────────────────
    function updateProgressUI() {
        const done = SENTENCES.filter(s => s.isPracticed).length;
        const total = SENTENCES.length;
        const pct = total > 0 ? Math.round((done / total) * 100) : 0;

        if (progText) progText.textContent = `${done} / ${total}`;
        if (progPct) progPct.textContent = `${pct}%`;
        if (progBar) {
            progBar.style.width = pct + '%';
            progBar.setAttribute('aria-valuenow', pct);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  RECORDING — Web Speech API
    // ────────────────────────────────────────────────────────────────
    const SpeechRecog = global.SpeechRecognition || global.webkitSpeechRecognition;
    let activeRecognition = null;

    function startRecording() {
        if (activeSentIdx === -1) {
            showToast('⚠️ Hãy chọn một câu trước!', 'warning');
            return;
        }

        if (!SpeechRecog) {
            showToast('⚠️ Trình duyệt chưa hỗ trợ ghi âm — hiển thị demo', 'error');
            simulateFeedback();
            return;
        }

        if (isRecording) {
            if (activeRecognition) activeRecognition.stop();
            return;
        }

        // Pause video during recording
        if (player && player.getPlayerState() === YT.PlayerState.PLAYING) {
            player.pauseVideo();
        }

        isRecording = true;
        setRecordUIState(true);

        const expected = SENTENCES[activeSentIdx]?.text || '';
        const rec = new SpeechRecog();
        rec.lang = 'en-US';
        rec.interimResults = true;
        rec.maxAlternatives = 3;
        activeRecognition = rec;

        rec.onresult = function (event) {
            let final = '';
            for (let i = event.resultIndex; i < event.results.length; i++) {
                if (event.results[i].isFinal) final += event.results[i][0].transcript;
            }
            if (final) {
                const sim = levenshteinSimilarity(normalizeText(final), normalizeText(expected));
                applyRecordingFeedback(sim, final);
            }
        };

        rec.onerror = function (evt) {
            const msgs = {
                'no-speech': 'Không phát hiện giọng nói — thử lại',
                'not-allowed': 'Truy cập mic bị từ chối',
                'audio-capture': 'Không tìm thấy mic'
            };
            showToast('⚠️ ' + (msgs[evt.error] || evt.error), 'error');
        };

        rec.onend = function () {
            activeRecognition = null;
            isRecording = false;
            setRecordUIState(false);
        };

        rec.start();
    }

    function setRecordUIState(recording) {
        if (!btnRecord) return;
        if (recording) {
            btnRecord.classList.add('is-recording');
            if (recordIcon) recordIcon.className = 'fas fa-stop-circle';
            if (recordLabel) recordLabel.textContent = 'Đang ghi âm...';
        } else {
            btnRecord.classList.remove('is-recording');
            if (recordIcon) recordIcon.className = 'fas fa-microphone';
            if (recordLabel) recordLabel.textContent = 'Kiểm tra phát âm';
        }
    }

    function setScoringUIState(scoring) {
        if (!btnRecord) return;
        if (scoring) {
            btnRecord.classList.add('is-scoring');
            btnRecord.disabled = true;
            if (recordIcon) recordIcon.className = 'fas fa-spinner fa-spin';
            if (recordLabel) recordLabel.textContent = 'Đang chấm điểm...';
        } else {
            btnRecord.classList.remove('is-scoring');
            btnRecord.disabled = false;
            if (recordIcon) recordIcon.className = 'fas fa-microphone';
            if (recordLabel) recordLabel.textContent = 'Kiểm tra phát âm';
        }
    }

    // ── API — Save progress to backend ─────────────────────────────
    async function saveSpeakingProgress(sentenceId, scores) {
        try {
            const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!tokenEl) { console.warn('Anti-forgery token not found'); return null; }

            const response = await fetch(`/api/speaking/${sentenceId}/progress`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': tokenEl.value
                },
                body: JSON.stringify(scores)
            });

            if (!response.ok) {
                const err = await response.json().catch(() => ({}));
                console.error('Save progress failed:', err);
                return null;
            }
            return await response.json();
        } catch (ex) {
            console.error('Save progress error:', ex);
            return null;
        }
    }

    async function applyRecordingFeedback(similarity, heard) {
        const pct = Math.round(similarity * 100);
        const s = SENTENCES[activeSentIdx];

        const scores = {
            totalScore: pct,
            accuracyScore: Math.min(100, Math.max(0, pct + Math.round(Math.random() * 10 - 5))),
            fluencyScore: Math.min(100, Math.max(0, pct + Math.round(Math.random() * 10 - 5))),
            completenessScore: Math.min(100, Math.max(0, pct + Math.round(Math.random() * 10 - 5)))
        };

        // Hiện trạng thái "Đang chấm điểm..."
        setScoringUIState(true);

        // Lưu lên server trước — chỉ cập nhật UI khi thành công
        const result = await saveSpeakingProgress(s.id, scores);

        // Tắt trạng thái chấm điểm
        setScoringUIState(false);

        if (result && result.success) {
            s.totalScore = scores.totalScore;
            s.accuracyScore = scores.accuracyScore;
            s.fluencyScore = scores.fluencyScore;
            s.completenessScore = scores.completenessScore;
            s.isPracticed = true;

            updateScoreBoard(s);
            updateProgressUI();
            markSentenceDone(activeSentIdx);

            if (similarity >= 0.8) showToast(`✅ Tuyệt vời! Độ chính xác: ${pct}%`, 'success');
            else showToast(`🔁 Thử lại! Bạn nói: "${heard}" (${pct}%)`, 'warning');
        } else {
            showToast('❌ Lỗi khi lưu điểm — thử lại sau', 'error');
        }
    }

    function simulateFeedback() {
        isRecording = true;
        setRecordUIState(true);
        setTimeout(async () => {
            isRecording = false;
            setRecordUIState(false);
            const s = SENTENCES[activeSentIdx];

            const scores = {
                totalScore: Math.floor(Math.random() * 20) + 78,
                accuracyScore: Math.floor(Math.random() * 20) + 78,
                fluencyScore: Math.floor(Math.random() * 20) + 78,
                completenessScore: Math.floor(Math.random() * 20) + 78
            };

            // Hiện trạng thái "Đang chấm điểm..."
            setScoringUIState(true);

            const result = await saveSpeakingProgress(s.id, scores);

            // Tắt trạng thái chấm điểm
            setScoringUIState(false);

            if (result && result.success) {
                s.totalScore = scores.totalScore;
                s.accuracyScore = scores.accuracyScore;
                s.fluencyScore = scores.fluencyScore;
                s.completenessScore = scores.completenessScore;
                s.isPracticed = true;
                updateScoreBoard(s);
                updateProgressUI();
                markSentenceDone(activeSentIdx);
                showToast(`✅ Demo: Điểm ${scores.totalScore}%`, 'success');
            } else {
                showToast('❌ Lỗi khi lưu điểm — thử lại sau', 'error');
            }
        }, 2500);
    }

    function markSentenceDone(idx) {
        const btn = document.querySelector(`.spk-sent-btn[data-index="${idx}"]`);
        if (!btn) return;
        btn.classList.add('spk-sent-btn--done');
        if (!btn.querySelector('.spk-done-check')) {
            const check = document.createElement('i');
            check.className = 'fas fa-check spk-done-check';
            btn.appendChild(check);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  HELPERS
    // ────────────────────────────────────────────────────────────────
    function normalizeText(s) {
        return s.toLowerCase().replace(/[^\w\s]/g, '').replace(/\s+/g, ' ').trim();
    }

    function levenshteinSimilarity(s1, s2) {
        if (s1 === s2) return 1.0;
        const a = s1.length > s2.length ? s1 : s2;
        const b = s1.length > s2.length ? s2 : s1;
        const maxLen = a.length;
        if (maxLen === 0) return 1.0;
        return (maxLen - editDist(a, b)) / maxLen;
    }

    function editDist(a, b) {
        const dp = Array.from({ length: a.length + 1 }, (_, i) => [i]);
        for (let j = 0; j <= b.length; j++) dp[0][j] = j;
        for (let i = 1; i <= a.length; i++) {
            for (let j = 1; j <= b.length; j++) {
                dp[i][j] = a[i - 1] === b[j - 1]
                    ? dp[i - 1][j - 1]
                    : 1 + Math.min(dp[i - 1][j], dp[i][j - 1], dp[i - 1][j - 1]);
            }
        }
        return dp[a.length][b.length];
    }

    // ────────────────────────────────────────────────────────────────
    //  DICTATION — Real-time character feedback
    // ────────────────────────────────────────────────────────────────
    function renderDictationFeedback(typedText, expectedText) {
        if (!dicFeedback) return;
        if (!expectedText) { dicFeedback.innerHTML = ''; return; }

        const words = expectedText.split(/\s+/);
        let globalIdx = 0;          // tracks position across full sentence
        const htmlParts = [];

        words.forEach((word, wIdx) => {
            // Determine word-level status
            const wordStart = globalIdx;
            const wordEnd = globalIdx + word.length;
            let wordClass = 'spk-dic-word';

            if (typedText.length >= wordEnd) {
                // All chars of this word have been typed — check if all correct
                let allCorrect = true;
                for (let c = 0; c < word.length; c++) {
                    if (typedText[wordStart + c]?.toLowerCase() !== word[c].toLowerCase()) {
                        allCorrect = false;
                        break;
                    }
                }
                wordClass += allCorrect ? ' spk-dic-word--done' : '';
            } else if (typedText.length > wordStart) {
                // Currently typing this word
                wordClass += ' spk-dic-word--active';
            }

            // Build character spans for this word
            let charSpans = '';
            for (let c = 0; c < word.length; c++) {
                const pos = globalIdx + c;
                if (pos >= typedText.length) {
                    // Not yet reached
                    charSpans += '<span class="char-untyped">*</span>';
                } else if (typedText[pos].toLowerCase() === word[c].toLowerCase()) {
                    // Correct — show the expected char (preserves original case)
                    charSpans += `<span class="char-correct">${escapeHtml(word[c])}</span>`;
                } else {
                    // Incorrect — show what the user typed
                    charSpans += `<span class="char-incorrect">${escapeHtml(typedText[pos])}</span>`;
                }
            }

            htmlParts.push(`<span class="${wordClass}">${charSpans}</span>`);
            globalIdx += word.length;

            // Account for the space between words in the typed text
            // (we skip the space character in comparison)
            if (wIdx < words.length - 1) {
                globalIdx++; // skip the space in expectedText
            }
        });

        dicFeedback.innerHTML = htmlParts.join('');
    }

    function escapeHtml(ch) {
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return map[ch] || ch;
    }

    // ── Toast notification ───────────────────────────────────────────
    function showToast(msg, type = 'info') {
        if (!toastEl) return;
        clearTimeout(toastTimer);
        toastEl.textContent = msg;
        toastEl.className = `spk-toast is-visible${type !== 'info' ? ' is-' + type : ''}`;
        toastTimer = setTimeout(() => { toastEl.className = 'spk-toast'; }, 3200);
    }

    // ────────────────────────────────────────────────────────────────
    //  EVENT WIRING
    // ────────────────────────────────────────────────────────────────
    function bindEvents() {

        // Prev / Next sentence
        btnPrev?.addEventListener('click', () => {
            if (activeSentIdx > 0) selectSentence(activeSentIdx - 1);
            else if (SENTENCES.length) selectSentence(0);
        });

        btnNext?.addEventListener('click', () => {
            if (activeSentIdx < SENTENCES.length - 1) selectSentence(activeSentIdx + 1);
            else if (activeSentIdx === -1 && SENTENCES.length) selectSentence(0);
        });

        // Replay
        btnReplay?.addEventListener('click', () => {
            if (activeSentIdx < 0) return;
            const s = SENTENCES[activeSentIdx];
            if (player && typeof player.seekTo === 'function') {
                player.seekTo(s.start, true);
                player.playVideo();
            }
        });

        // Record
        btnRecord?.addEventListener('click', startRecording);

        // Pronunciation slider arrows
        btnSliderPrev?.addEventListener('click', () => {
            sliderTrack?.scrollBy({ left: -160, behavior: 'smooth' });
        });
        btnSliderNext?.addEventListener('click', () => {
            sliderTrack?.scrollBy({ left: 160, behavior: 'smooth' });
        });

        // Dictation slider arrows
        btnDicSliderPrev?.addEventListener('click', () => {
            dicSliderTrack?.scrollBy({ left: -160, behavior: 'smooth' });
        });
        btnDicSliderNext?.addEventListener('click', () => {
            dicSliderTrack?.scrollBy({ left: 160, behavior: 'smooth' });
        });

        // Pronunciation sentence buttons
        $$('.spk-sent-btn[data-index]').forEach(btn => {
            btn.addEventListener('click', function () {
                selectSentence(parseInt(this.dataset.index, 10));
            });
        });

        // Dictation sentence buttons
        $$('.spk-sent-btn[data-dic-index]').forEach(btn => {
            btn.addEventListener('click', function () {
                selectSentence(parseInt(this.dataset.dicIndex, 10));
            });
        });

        // Dictation real-time feedback
        dicInput?.addEventListener('input', () => {
            if (activeSentIdx < 0) return;
            const expected = SENTENCES[activeSentIdx]?.text || '';
            renderDictationFeedback(dicInput.value, expected);
        });

        // Hide text toggle
        btnHideText?.addEventListener('click', () => {
            isTextHidden = !isTextHidden;
            if (btnHideText) btnHideText.textContent = isTextHidden ? 'Hiện' : 'Ẩn';
            if (transcriptEn) {
                transcriptEn.textContent = isTextHidden
                    ? '••••••••••••••••'
                    : (transcriptEn.dataset.original || '');
            }
        });

        // Hide video toggle — shows black overlay over iframe to block subtitles
        btnHideVideo?.addEventListener('click', () => {
            isVideoHidden = !isVideoHidden;
            if (videoOverlay) videoOverlay.classList.toggle('d-none', !isVideoHidden);
            if (btnHideVideo) {
                btnHideVideo.innerHTML = isVideoHidden
                    ? '<i class="fas fa-eye"></i> Hiện video'
                    : '<i class="fas fa-eye-slash"></i> Ẩn video';
            }
        });

        // Tab switching
        tabPronunciation?.addEventListener('click', function () {
            this.classList.add('spk-pill-active');
            tabDictation?.classList.remove('spk-pill-active');
            panePronounciation?.classList.remove('d-none');
            panePronounciation?.classList.add('d-flex');
            paneDictation?.classList.add('d-none');
            paneDictation?.classList.remove('d-flex');
        });

        tabDictation?.addEventListener('click', function () {
            this.classList.add('spk-pill-active');
            tabPronunciation?.classList.remove('spk-pill-active');
            paneDictation?.classList.remove('d-none');
            paneDictation?.classList.add('d-flex');
            panePronounciation?.classList.add('d-none');
            panePronounciation?.classList.remove('d-flex');
        });

        // Keyboard shortcuts
        document.addEventListener('keydown', onKeyDown);
    }

    function onKeyDown(e) {
        if (['INPUT', 'TEXTAREA'].includes(e.target.tagName)) return;

        switch (e.code) {
            case 'Space':
                e.preventDefault();
                if (player) {
                    if (player.getPlayerState() === YT.PlayerState.PLAYING) player.pauseVideo();
                    else player.playVideo();
                }
                break;
            case 'Enter':
                e.preventDefault();
                startRecording();
                break;
            case 'ArrowLeft':
                if (e.shiftKey) { e.preventDefault(); btnPrev?.click(); }
                break;
            case 'ArrowRight':
            case 'Tab':
                if (e.code === 'Tab') e.preventDefault();
                btnNext?.click();
                break;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  INIT
    // ────────────────────────────────────────────────────────────────
    function init() {
        bindEvents();
        loadYouTubeAPI();
        showToast('⌨️  Space=Play  Shift+←/→=Câu  Enter=Ghi âm', 'info');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window);