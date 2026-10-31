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
    const noResultsEl = document.getElementById('vi-no-results');

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

        if (noResultsEl) {
            noResultsEl.style.display = totalVisibleCount === 0 ? '' : 'none';
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
    const TEST_MODE = !!global.TCT_SPEAKING_TEST_MODE;
    /** Browser Web Speech is preview-only until L4 server assessment. */
    const BROWSER_SCORE_PREVIEW_ONLY = true;

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
    const btnDicHint = $('btn-dic-hint');
    const btnDicPlay = $('btn-dic-play');
    const dicPlayIcon = $('dic-play-icon');
    const dicPlayLabel = $('dic-play-label');
    const btnDicNext = $('btn-dic-next');
    const btnDicPlaySlow = $('btn-dic-play-slow') || $('btn-dic-slow-play');

    // ── State ────────────────────────────────────────────────────────
    let player = null;
    let activeSentIdx = -1;
    let isRecording = false;
    let isTextHidden = false;
    let isVideoHidden = false;
    let playbackTimer = null;
    let toastTimer = null;
    let recordTimeout = null;
    let isDictationAnswerRevealed = false;
    let recordingEpoch = 0;
    let activeRecordingSnapshot = null;
    let activeAssessController = null;

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
            setDictationPlayState(true);
        } else {
            if (playbackTimer) { clearInterval(playbackTimer); playbackTimer = null; }
            if (evt.data === YT.PlayerState.PAUSED || evt.data === YT.PlayerState.ENDED) {
                setDictationPlayState(false);
            }
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
    function abortActiveRecognition(reason) {
        recordingEpoch += 1;
        activeRecordingSnapshot = null;
        if (recordTimeout) {
            clearTimeout(recordTimeout);
            recordTimeout = null;
        }
        if (activeAssessController) {
            try { activeAssessController.abort(); } catch (_) { /* ignore */ }
            activeAssessController = null;
        }
        if (activeRecognition) {
            try {
                if (typeof activeRecognition.abort === 'function') activeRecognition.abort();
                else activeRecognition.stop();
            } catch (_) { /* ignore */ }
            activeRecognition = null;
        }
        const recorder = activePcmRecorder;
        activePcmRecorder = null;
        if (recorder && typeof recorder.dispose === 'function') {
            try { recorder.dispose(); } catch (_) { /* ignore */ }
        }
        isRecording = false;
        isScoring = false;
        setRecordUIState(false);
        setScoringUIState(false);
        if (reason === 'sentence-change') {
            setAssessmentStatus('Đã hủy ghi âm vì đổi câu — kết quả cũ không được gắn sang câu mới.');
        }
    }

    function selectSentence(idx, autoPlay = true) {
        if (idx < 0 || idx >= SENTENCES.length) return;

        if (isRecording || activeRecognition || activePcmRecorder || activeAssessController) {
            abortActiveRecognition(activeSentIdx !== idx ? 'sentence-change' : 'reselect');
        }

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
        isDictationAnswerRevealed = false;
        btnDicHint?.classList.remove('is-active');
        setDictationPlayState(false);
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

    function setAssessmentStatus(message) {
        const el = $('spk-assessment-status');
        if (el) el.textContent = message || '';
    }

    function formatScoreDisplay(value, { unavailable = false } = {}) {
        if (unavailable) return '—';
        const pct = (value && value > 0) ? Math.round(value) : 0;
        return pct > 0 ? `${pct}` : '0';
    }

    function updateScoreBoard(sentence, previewMatchPct = null) {
        const hasStored = !!(sentence && sentence.isPracticed && sentence.totalScore > 0);
        const isPreview = previewMatchPct != null;

        SCORE_METRICS.forEach(m => {
            let display;
            let barPct = 0;
            let unavailable = false;

            if (isPreview) {
                if (m.val === 'totalScore' || m.val === 'accuracyScore') {
                    barPct = Math.max(0, Math.min(100, Math.round(previewMatchPct)));
                    display = String(barPct);
                } else {
                    // Fluency / completeness are not measured by edit-distance preview.
                    unavailable = true;
                    display = '—';
                    barPct = 0;
                }
            } else if (hasStored) {
                const raw = sentence[m.val];
                barPct = (raw && raw > 0) ? Math.round(raw) : 0;
                display = formatScoreDisplay(raw);
            } else {
                display = '0';
                barPct = 0;
            }

            const valEl = $(m.id);
            if (valEl) {
                valEl.textContent = display;
                valEl.title = unavailable ? 'Chưa đánh giá' : '';
            }

            const ringEl = $(m.ringId);
            if (ringEl) ringEl.setAttribute('stroke-dasharray', `${barPct}, 100`);

            const barEl = $(m.barId);
            if (barEl) barEl.style.width = barPct + '%';
        });

        if (isPreview) {
            setAssessmentStatus('Xem trước khớp lời nói — chưa lưu tiến độ / XP (chờ chấm phía máy chủ).');
        } else if (hasStored) {
            setAssessmentStatus('');
        } else {
            setAssessmentStatus('Chưa có điểm đã lưu cho câu này.');
        }
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
    //  RECORDING — server WAV assess (preferred) / Web Speech preview
    // ────────────────────────────────────────────────────────────────
    const SpeechRecog = global.SpeechRecognition || global.webkitSpeechRecognition;
    let activeRecognition = null;
    let activePcmRecorder = null;
    let isScoring = false;
    let currentInterim = '';

    function startRecording() {
        if (activeSentIdx === -1) {
            showToast('⚠️ Hãy chọn một câu trước!', 'warning');
            return;
        }

        if (isScoring) return;

        if (isRecording) {
            if (activePcmRecorder) {
                finishPcmRecording();
                return;
            }
            if (activeRecognition) activeRecognition.stop();
            return;
        }

        // Pause video during recording
        if (player && typeof player.getPlayerState === 'function'
            && global.YT && player.getPlayerState() === global.YT.PlayerState.PLAYING) {
            player.pauseVideo();
        }

        const sentence = SENTENCES[activeSentIdx];
        if (!sentence) return;

        const epoch = ++recordingEpoch;
        const snapshot = {
            epoch,
            sentenceId: sentence.id,
            sentenceIndex: activeSentIdx,
            expectedText: sentence.text || ''
        };
        activeRecordingSnapshot = snapshot;

        if (global.TctSpeechRecorder && global.TctSpeechRecorder.isSupported()) {
            startPcmRecording(snapshot);
            return;
        }

        if (!SpeechRecog) {
            showToast('⚠️ Trình duyệt chưa hỗ trợ thu âm. Bạn vẫn có thể nghe và luyện; chưa lưu điểm.', 'error');
            setAssessmentStatus('Thu âm không khả dụng — chưa đánh giá, không lưu tiến độ.');
            updateScoreBoard(SENTENCES[activeSentIdx], null);
            const fluencyEl = $('score-fluency');
            const completeEl = $('score-complete');
            if (fluencyEl) { fluencyEl.textContent = '—'; fluencyEl.title = 'Chưa đánh giá'; }
            if (completeEl) { completeEl.textContent = '—'; completeEl.title = 'Chưa đánh giá'; }
            return;
        }

        isRecording = true;
        setRecordUIState(true);
        currentInterim = '';
        setAssessmentStatus('Đang ghi âm — Web Speech chỉ xem trước, chưa lưu XP (cần thu PCM + ASR máy chủ).');

        // Accumulate ALL final transcript fragments across multiple result events
        let accumulatedFinal = '';

        const rec = new SpeechRecog();
        rec.lang = 'en-US';
        rec.interimResults = true;
        rec.continuous = true;
        rec.maxAlternatives = 3;
        activeRecognition = rec;

        // Auto-stop after 15 seconds to prevent infinite recording
        recordTimeout = setTimeout(() => {
            if (activeRecognition && activeRecordingSnapshot && activeRecordingSnapshot.epoch === epoch) {
                console.log('⏱️ Auto-stop after 15s timeout');
                activeRecognition.stop();
            }
        }, 15000);

        rec.onresult = function (event) {
            if (!activeRecordingSnapshot || activeRecordingSnapshot.epoch !== epoch) return;

            let latestFinal = '';
            let interim = '';
            for (let i = event.resultIndex; i < event.results.length; i++) {
                if (event.results[i].isFinal) {
                    latestFinal += event.results[i][0].transcript;
                } else {
                    interim += event.results[i][0].transcript;
                }
            }

            if (latestFinal) {
                accumulatedFinal += latestFinal;
                currentInterim = '';
                console.log('✅ Final fragment:', latestFinal, '| Total:', accumulatedFinal);
            }

            if (interim) {
                currentInterim = interim;
                if (recordLabel) {
                    let display = interim.length > 25 ? '...' + interim.substring(interim.length - 25) : interim;
                    recordLabel.textContent = `Nghe: ${display}`;
                }
            }
        };

        rec.onsoundstart = function () {
            if (!activeRecordingSnapshot || activeRecordingSnapshot.epoch !== epoch) return;
            console.log('🎙️ Đã bắt được âm thanh (sound start)');
            if (recordLabel && !currentInterim && !accumulatedFinal) {
                recordLabel.textContent = 'Đang ghi âm (có tiếng)...';
            }
        };

        rec.onspeechstart = function () {
            console.log('🗣️ Đã phát hiện giọng nói (speech start)');
        };

        rec.onnomatch = function () {
            console.warn('❌ nomatch: có giọng nói nhưng không nhận diện được');
        };

        rec.onerror = function (evt) {
            console.error('🔴 SpeechRecognition error:', evt.error);
            // 'aborted' fires when we call .stop()/.abort() — not a real error
            if (evt.error === 'aborted') return;
            const msgs = {
                'no-speech': 'Không phát hiện giọng nói — thử lại',
                'not-allowed': 'Truy cập mic bị từ chối — kiểm tra quyền mic',
                'audio-capture': 'Không tìm thấy mic — kiểm tra kết nối',
                'network': 'Lỗi mạng — kiểm tra kết nối Internet'
            };
            showToast('⚠️ ' + (msgs[evt.error] || 'Lỗi ghi âm: ' + evt.error), 'error');
            setAssessmentStatus('Không có kết quả nhận dạng — chưa lưu tiến độ.');
            // Do not POST / invent scores on recognition errors.
        };

        rec.onend = function () {
            if (recordTimeout) {
                clearTimeout(recordTimeout);
                recordTimeout = null;
            }
            activeRecognition = null;
            isRecording = false;

            const bestTranscript = accumulatedFinal || currentInterim;
            currentInterim = '';

            console.log('🏁 Recording ended. bestTranscript:', bestTranscript);

            const snapshotStillValid = activeRecordingSnapshot
                && activeRecordingSnapshot.epoch === epoch
                && SENTENCES[activeSentIdx]
                && SENTENCES[activeSentIdx].id === snapshot.sentenceId;

            activeRecordingSnapshot = null;

            if (!snapshotStillValid) {
                setRecordUIState(false);
                return;
            }

            if (!isScoring && bestTranscript) {
                if (recordLabel) recordLabel.textContent = 'Đang phân tích...';
                const sim = levenshteinSimilarity(
                    normalizeText(bestTranscript),
                    normalizeText(snapshot.expectedText)
                );
                applyRecordingFeedback(sim, bestTranscript, snapshot);
            } else if (!isScoring) {
                showToast('⚠️ Không nhận diện được giọng nói — hãy nói rõ hơn hoặc kiểm tra mic!', 'warning');
                setAssessmentStatus('Không nhận được lời nói — chưa đánh giá, chưa lưu tiến độ.');
                setRecordUIState(false);
            }
        };

        try {
            rec.start();
            console.log('🟢 SpeechRecognition started (continuous mode)');
        } catch (e) {
            console.error('Failed to start SpeechRecognition:', e);
            showToast('❌ Không thể bật ghi âm — thử tải lại trang (F5)', 'error');
            isRecording = false;
            activeRecordingSnapshot = null;
            if (recordTimeout) {
                clearTimeout(recordTimeout);
                recordTimeout = null;
            }
            setRecordUIState(false);
        }
    }

    function setRecordUIState(recording) {
        if (!btnRecord) return;
        if (recording) {
            btnRecord.classList.add('is-recording');
            if (recordIcon) {
                recordIcon.className = 'material-symbols-outlined';
                recordIcon.textContent = 'stop_circle';
            }
            if (recordLabel) recordLabel.textContent = 'Đang ghi âm...';
        } else {
            btnRecord.classList.remove('is-recording');
            if (recordIcon) {
                recordIcon.className = 'material-symbols-outlined';
                recordIcon.textContent = 'mic';
            }
            if (recordLabel) recordLabel.textContent = 'Kiểm tra phát âm';
        }
    }

    function setScoringUIState(scoring) {
        if (!btnRecord) return;
        if (scoring) {
            btnRecord.classList.add('is-scoring');
            btnRecord.disabled = true;
            if (recordIcon) {
                recordIcon.className = 'fas fa-spinner fa-spin';
                recordIcon.textContent = '';
            }
            if (recordLabel) recordLabel.textContent = 'Đang chấm điểm...';
        } else {
            btnRecord.classList.remove('is-scoring');
            btnRecord.disabled = false;
            if (recordIcon) {
                recordIcon.className = 'material-symbols-outlined';
                recordIcon.textContent = 'mic';
            }
            if (recordLabel) recordLabel.textContent = 'Kiểm tra phát âm';
        }
    }

    async function startPcmRecording(snapshot) {
        const recorder = global.TctSpeechRecorder.create({
            onComplete: function (blob) {
                if (!blob) return;
                if (recordingEpoch !== snapshot.epoch) return;
                const current = activeRecordingSnapshot;
                if (!current || current.epoch !== snapshot.epoch || current.sentenceId !== snapshot.sentenceId) {
                    return;
                }
                isRecording = false;
                setRecordUIState(false);
                assessAndCommitFromWav(snapshot, blob);
            },
            onError: function () {
                if (recordingEpoch !== snapshot.epoch) return;
                setAssessmentStatus('Thu âm lỗi — chưa lưu tiến độ.');
            }
        });
        activePcmRecorder = recorder;
        isRecording = true;
        setRecordUIState(true);
        setAssessmentStatus('Đang thu PCM — sẽ gửi máy chủ chấm (không dùng điểm trình duyệt).');
        try {
            await recorder.start();
            if (recordingEpoch !== snapshot.epoch || activePcmRecorder !== recorder) {
                return;
            }
        } catch (ex) {
            console.error(ex);
            if (activePcmRecorder === recorder) {
                activePcmRecorder = null;
            }
            try { recorder.dispose(); } catch (_) { /* ignore */ }
            if (recordingEpoch !== snapshot.epoch) {
                return;
            }
            isRecording = false;
            activeRecordingSnapshot = null;
            setRecordUIState(false);
            showToast('Không mở được micro — kiểm tra quyền mic/HTTPS.', 'error');
            setAssessmentStatus('Mic bị từ chối hoặc không khả dụng — chưa lưu tiến độ.');
        }
    }

    async function finishPcmRecording() {
        const recorder = activePcmRecorder;
        isRecording = false;
        if (!recorder) {
            setRecordUIState(false);
            return;
        }

        try {
            await recorder.stop();
        } catch (ex) {
            console.error(ex);
            showToast('Bản thu không hợp lệ.', 'error');
            setAssessmentStatus('Audio không hợp lệ — chưa lưu tiến độ.');
            setRecordUIState(false);
        }
    }

    async function assessAndCommitFromWav(snapshot, wavBlob) {
        if (recordingEpoch !== snapshot.epoch) {
            setRecordUIState(false);
            return;
        }
        const s = SENTENCES.find(item => item.id === snapshot.sentenceId) || SENTENCES[activeSentIdx];
        if (!s || s.id !== snapshot.sentenceId) {
            setRecordUIState(false);
            return;
        }

        const controller = new AbortController();
        activeAssessController = controller;
        isScoring = true;
        setScoringUIState(true);
        setAssessmentStatus('Đang gửi máy chủ nhận dạng...');

        try {
            const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!tokenEl) {
                showToast('Thiếu anti-forgery token.', 'error');
                return;
            }

            const form = new FormData();
            form.append('audio', wavBlob, 'speech.wav');
            const assessResponse = await fetch(`/api/speaking/${snapshot.sentenceId}/assess`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': tokenEl.value },
                body: form,
                signal: controller.signal
            });
            if (recordingEpoch !== snapshot.epoch || activeSentIdx < 0 || SENTENCES[activeSentIdx]?.id !== snapshot.sentenceId) {
                return;
            }
            const assessBody = await assessResponse.json().catch(() => ({}));
            if (recordingEpoch !== snapshot.epoch) return;
            if (!assessResponse.ok || !assessBody.success) {
                const code = assessBody.code || '';
                showToast(assessBody.error || 'ASR máy chủ chưa sẵn sàng.', 'warning');
                setAssessmentStatus(assessBody.error || 'Engine chưa sẵn sàng — nghe/thu được nhưng chưa lưu XP.');
                return;
            }

            if (!assessBody.canCommitProgress || !assessBody.assessmentId) {
                showToast(assessBody.message || 'Chưa nhận được lời nói.', 'warning');
                setAssessmentStatus(assessBody.message || 'Chưa nhận được lời nói — chưa lưu tiến độ.');
                updateScoreBoard(s, null);
                return;
            }

            updateScoreBoard(s, Math.round(assessBody.textMatchScore || 0));
            const progress = await saveSpeakingProgress(snapshot.sentenceId, {
                assessmentId: assessBody.assessmentId
            }, controller.signal);
            if (recordingEpoch !== snapshot.epoch || SENTENCES[activeSentIdx]?.id !== snapshot.sentenceId) {
                return;
            }
            if (!progress || !progress.success) {
                showToast('Đã chấm nhưng chưa lưu được tiến độ.', 'warning');
                setAssessmentStatus('Assessment hợp lệ nhưng commit tiến độ thất bại.');
                return;
            }

            s.isPracticed = true;
            s.totalScore = progress.totalScore;
            s.accuracyScore = progress.accuracyScore;
            s.fluencyScore = progress.fluencyScore;
            s.completenessScore = progress.completenessScore;
            updateScoreBoard(s, null);
            updateProgressUI();
            markSentenceDone(snapshot.sentenceIndex);
            setAssessmentStatus('Đã lưu mức khớp lời nói (server). Fluency/prosody chưa đo.');
            showToast(`Đã lưu ~${Math.round(progress.totalScore)}% khớp lời nói`, 'success');
            if (progress.firstTimeVideoCompletion && progress.xpEarned > 0) {
                showToast(`Hoàn thành bài — +${progress.xpEarned} XP`, 'success');
            }
        } catch (ex) {
            if (ex && ex.name === 'AbortError') {
                return;
            }
            console.error(ex);
            if (recordingEpoch !== snapshot.epoch) return;
            showToast('Lỗi khi gửi bản thu.', 'error');
            setAssessmentStatus('Lỗi mạng/máy chủ — chưa lưu tiến độ.');
        } finally {
            if (activeAssessController === controller) {
                activeAssessController = null;
            }
            if (recordingEpoch === snapshot.epoch) {
                isScoring = false;
                setScoringUIState(false);
                setRecordUIState(false);
            }
        }
    }

    // ── API — Save progress (assessmentId required) ─
    async function saveSpeakingProgress(sentenceId, payload, signal) {
        try {
            const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!tokenEl) { console.warn('Anti-forgery token not found'); return null; }

            const response = await fetch(`/api/speaking/${sentenceId}/progress`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': tokenEl.value
                },
                body: JSON.stringify(payload),
                signal
            });

            if (!response.ok) {
                const err = await response.json().catch(() => ({}));
                console.error('Save progress failed:', err);
                return null;
            }
            return await response.json();
        } catch (ex) {
            if (ex && ex.name === 'AbortError') return null;
            console.error('Save progress error:', ex);
            return null;
        }
    }

    /**
     * Browser recognition feedback remains preview-only (not a score source).
     */
    async function applyRecordingFeedback(similarity, heard, snapshot) {
        const pct = Math.round(similarity * 100);
        const s = SENTENCES[activeSentIdx];

        if (!s || !snapshot || s.id !== snapshot.sentenceId) {
            setRecordUIState(false);
            return;
        }

        isScoring = true;
        setScoringUIState(true);

        updateScoreBoard(s, pct);
        isScoring = false;
        setScoringUIState(false);
        setRecordUIState(false);

        if (similarity >= 0.8) {
            showToast(`👀 Xem trước: khớp ~${pct}% — chưa lưu tiến độ`, 'success');
        } else {
            showToast(`👀 Xem trước: "${heard}" (~${pct}%) — chưa lưu tiến độ`, 'warning');
        }
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

    function escapeHtmlText(text) {
        return (text || '').replace(/[&<>"']/g, ch => escapeHtml(ch));
    }

    function setDictationPlayState(isPlaying) {
        if (!btnDicPlay) return;
        btnDicPlay.classList.toggle('is-playing', isPlaying);
        if (dicPlayIcon) {
            dicPlayIcon.className = 'material-symbols-outlined';
            dicPlayIcon.textContent = isPlaying ? 'pause' : 'play_arrow';
        }
        if (dicPlayLabel) dicPlayLabel.textContent = isPlaying ? 'Tạm dừng' : 'Phát lại';
    }

    function replayCurrentSentence(rate = 1) {
        if (activeSentIdx < 0 || !player || typeof player.seekTo !== 'function') return;
        const s = SENTENCES[activeSentIdx];
        if (!s) return;

        if (typeof player.setPlaybackRate === 'function') {
            player.setPlaybackRate(rate);
        }

        player.seekTo(s.start, true);
        player.playVideo();
    }

    function revealDictationAnswer() {
        if (activeSentIdx < 0) {
            showToast('⚠️ Hãy chọn một câu trước!', 'warning');
            return;
        }

        const expectedText = SENTENCES[activeSentIdx]?.text || '';
        if (!expectedText || !dicFeedback) return;

        if (!isDictationAnswerRevealed) {
            isDictationAnswerRevealed = true;
            btnDicHint?.classList.add('is-active');
            renderDictationFeedback(expectedText, expectedText);
            return;
        }

        isDictationAnswerRevealed = false;
        btnDicHint?.classList.remove('is-active');
        renderDictationFeedback(dicInput?.value || '', expectedText);
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
            replayCurrentSentence(1);
        });

        // Dictation support buttons
        btnDicHint?.addEventListener('click', revealDictationAnswer);

        btnDicPlay?.addEventListener('click', () => {
            if (!player) return;
            if (player.getPlayerState() === YT.PlayerState.PLAYING) {
                player.pauseVideo();
                return;
            }
            replayCurrentSentence(1);
        });

        btnDicPlaySlow?.addEventListener('click', () => {
            replayCurrentSentence(0.5);
        });

        btnDicNext?.addEventListener('click', () => {
            if (activeSentIdx < SENTENCES.length - 1) selectSentence(activeSentIdx + 1);
            else if (activeSentIdx === -1 && SENTENCES.length) selectSentence(0);
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
            if (isDictationAnswerRevealed) return;
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
    //  INIT / DISPOSE
    // ────────────────────────────────────────────────────────────────
    function dispose() {
        abortActiveRecognition('dispose');
        if (playbackTimer) {
            clearInterval(playbackTimer);
            playbackTimer = null;
        }
        if (toastTimer) {
            clearTimeout(toastTimer);
            toastTimer = null;
        }
        document.removeEventListener('keydown', onKeyDown);
        isScoring = false;
    }

    function init() {
        bindEvents();
        if (TEST_MODE) {
            if (SENTENCES.length > 0) selectSentence(0, false);
            return;
        }
        loadYouTubeAPI();
        showToast('⌨️  Space=Play  Shift+←/→=Câu  Enter=Ghi âm', 'info');
    }

    global.TctSpeakingPractice = {
        startRecording,
        selectSentence,
        dispose,
        applyRecordingFeedback,
        isPreviewOnly: BROWSER_SCORE_PREVIEW_ONLY,
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window);


// ════════════════════════════════════════════════════════════════════
//  PART 3 — IMPORT PANEL & BÀI NÓI CỦA TÔI (Index page only)
// ════════════════════════════════════════════════════════════════════
(function () {
    'use strict';

    // Guard: only run on Index page (import submit button must exist)
    const submitBtn = document.getElementById('mip-submit-btn');
    if (!submitBtn) return;

    const urlInput      = document.getElementById('mip-url-input');
    const subtitleInput = document.getElementById('mip-subtitle-input');
    const feedback      = document.getElementById('mip-feedback');
    const btnLabel      = document.getElementById('mip-btn-label');
    const btnIcon       = document.getElementById('mip-btn-icon');
    const myVideosGrid  = document.getElementById('my-videos-grid');
    const myEmptyState  = document.getElementById('my-empty-state');
    const mySecCount    = document.getElementById('my-sec-count');
    const loadingCard   = document.getElementById('mip-loading-card');

    // ── Anti-forgery token ─────────────────────────────────────────
    function getToken() {
        const t = document.querySelector('input[name="__RequestVerificationToken"]');
        return t ? t.value : '';
    }

    // ── Feedback helpers ───────────────────────────────────────────
    function showFeedback(innerHtml, cls) {
        if (!feedback) return;
        feedback.innerHTML = `<span class="${cls}">${innerHtml}</span>`;
    }

    function clearFeedback() {
        if (feedback) feedback.innerHTML = '';
    }

    function escHtml(str) {
        return (str || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    // ── Loading state ──────────────────────────────────────────────
    function setLoading(loading) {
        if (submitBtn)  submitBtn.disabled  = loading;
        if (urlInput)   urlInput.disabled   = loading;

        if (loading) {
            if (btnLabel) btnLabel.textContent = 'Đang xử lý...';
            if (btnIcon) {
                btnIcon.className = 'fas fa-spinner fa-spin';
                btnIcon.textContent = '';
            }

            // Show skeleton card in my-videos grid
            if (loadingCard && myVideosGrid) {
                loadingCard.classList.remove('d-none');
                myVideosGrid.insertBefore(loadingCard, myVideosGrid.firstChild);
            }
        } else {
            if (btnLabel) btnLabel.textContent = 'Thêm video';
            if (btnIcon) {
                btnIcon.className = 'material-symbols-outlined';
                btnIcon.textContent = 'add_circle';
            }

            if (loadingCard) loadingCard.classList.add('d-none');
        }
    }

    // ── My-videos section count ────────────────────────────────────
    function updateCount() {
        if (!mySecCount || !myVideosGrid) return;
        const realCards = myVideosGrid.querySelectorAll(
            '.my-video-card:not(.my-video-card--skeleton)'
        );
        const n = realCards.length;
        mySecCount.textContent = n + ' bài';

        if (myEmptyState) {
            myEmptyState.style.display = n === 0 ? '' : 'none';
        }
    }

    const testCfg = window.TctSpeakingImportTest || {};
    const initialDelayMs = Number.isFinite(testCfg.initialDelayMs) ? testCfg.initialDelayMs : 5000;
    const maxDelayMs = Number.isFinite(testCfg.maxDelayMs) ? testCfg.maxDelayMs : 30000;
    const giveUpMs = Number.isFinite(testCfg.giveUpMs) ? testCfg.giveUpMs : 5 * 60 * 1000;
    const reloadDelayMs = Number.isFinite(testCfg.reloadDelayMs) ? testCfg.reloadDelayMs : 800;
    let activeWatch = 0;

    function sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    function rememberJob(jobId) {
        try { sessionStorage.setItem('tct-speaking-import-job', String(jobId)); } catch (_) { /* ignore */ }
    }

    function statusLabel(status) {
        if (status === 'queued') return 'Đang chờ xử lý';
        if (status === 'running') return 'Đang tạo bài';
        if (status === 'succeeded') return 'Đã xong';
        if (status === 'failed') return 'Thất bại';
        if (status === 'cancelled') return 'Đã hủy';
        return status || 'Đang theo dõi';
    }

    async function watchJob(jobId, statusUrl) {
        const watchId = ++activeWatch;
        const started = Date.now();
        let delayMs = initialDelayMs;
        rememberJob(jobId);
        showFeedback(
            '<i class="fas fa-spinner fa-spin"></i> ' + escHtml('Đã nhận job ' + jobId + '. Đang theo dõi trạng thái.'),
            'mip-feedback-success'
        );

        while (watchId === activeWatch && Date.now() - started < giveUpMs) {
            const hidden = typeof document.hidden === 'boolean' && document.hidden;
            const offline = typeof navigator !== 'undefined' && navigator.onLine === false;
            if (hidden || offline) {
                showFeedback(
                    '<i class="fas fa-pause-circle"></i> ' + escHtml('Tạm dừng theo dõi job ' + jobId + ' khi ẩn trang hoặc mất mạng. Job trên máy chủ vẫn chạy.'),
                    'mip-feedback-error'
                );
                await sleep(delayMs);
                continue;
            }

            await sleep(delayMs);
            if (watchId !== activeWatch) return;
            let res;
            let data = {};
            try {
                res = await fetch(statusUrl, { headers: { 'Accept': 'application/json' } });
                data = await res.json().catch(() => ({}));
            } catch (_) {
                delayMs = Math.min(maxDelayMs, Math.round(delayMs * 1.5) || initialDelayMs);
                continue;
            }

            if (!res.ok) {
                showFeedback(
                    '<i class="fas fa-exclamation-circle"></i> ' + escHtml(data.error || 'Không đọc được trạng thái job.'),
                    'mip-feedback-error'
                );
                setLoading(false);
                return;
            }

            const status = data.status;
            if (status === 'succeeded' && data.videoId) {
                showFeedback(
                    '<i class="fas fa-check-circle"></i> Đã tạo bài. Đang tải lại trang...',
                    'mip-feedback-success'
                );
                try { sessionStorage.removeItem('tct-speaking-import-job'); } catch (_) { /* ignore */ }
                setTimeout(() => window.location.reload(), reloadDelayMs);
                return;
            }

            if (status === 'failed' || status === 'cancelled') {
                showFeedback(
                    '<i class="fas fa-exclamation-circle"></i> '
                    + escHtml((data.error || statusLabel(status)) + ' Bạn có thể gửi lại cùng URL.'),
                    'mip-feedback-error'
                );
                setLoading(false);
                return;
            }

            showFeedback(
                '<i class="fas fa-spinner fa-spin"></i> ' + escHtml(statusLabel(status) + ' — job ' + jobId),
                'mip-feedback-success'
            );
            delayMs = Math.min(maxDelayMs, Math.round(delayMs * 1.5) || maxDelayMs);
        }

        if (watchId !== activeWatch) return;
        showFeedback(
            '<i class="fas fa-clock"></i> '
            + escHtml('Đã theo dõi 5 phút. Job ' + jobId + ' vẫn có thể đang chạy. Mở lại trang hoặc bấm Thêm video để theo dõi tiếp. Không hủy job.'),
            'mip-feedback-error'
        );
        setLoading(false);
        window.TctSpeakingImport.lastJobId = jobId;
    }

    async function submitImport() {
        const url = (urlInput ? urlInput.value : '').trim();
        if (!url) {
            showFeedback(
                '<i class="fas fa-exclamation-circle"></i> Vui lòng nhập YouTube URL.',
                'mip-feedback-error'
            );
            if (urlInput) urlInput.focus();
            return;
        }

        clearFeedback();
        setLoading(true);
        try {
            const formData = new FormData();
            formData.append('youtubeUrl', url);
            formData.append('__RequestVerificationToken', getToken());
            const file = subtitleInput && subtitleInput.files && subtitleInput.files[0];
            const endpoint = file ? '/api/speaking/imports/subtitles' : '/api/speaking/imports';
            if (file) formData.append('subtitle', file, file.name || 'subtitles.vtt');

            const res = await fetch(endpoint, { method: 'POST', body: formData });
            const data = await res.json().catch(() => ({}));
            if (res.status === 202 && data.jobId && data.statusUrl) {
                if (urlInput) urlInput.value = '';
                if (subtitleInput) subtitleInput.value = '';
                await watchJob(data.jobId, data.statusUrl);
                return;
            }

            if (data.code === 'premium_required') {
                showFeedback(
                    '<i class="fas fa-crown"></i> '
                    + escHtml(data.error || 'Tính năng này yêu cầu tài khoản Premium.')
                    + ' <a href="/Account/Premium" title="Nâng cấp Premium">Nâng cấp ngay →</a>',
                    'mip-feedback-upgrade'
                );
                setLoading(false);
                return;
            }

            showFeedback(
                '<i class="fas fa-exclamation-circle"></i> '
                + escHtml(data.error || 'Đã xảy ra lỗi — vui lòng thử lại.'),
                'mip-feedback-error'
            );
        } catch (_err) {
            showFeedback(
                '<i class="fas fa-exclamation-circle"></i> Lỗi kết nối — vui lòng thử lại sau.',
                'mip-feedback-error'
            );
        }

        setLoading(false);
    }

    submitBtn.addEventListener('click', () => { submitImport(); });

    window.TctSpeakingImport = {
        submit: submitImport,
        resume(jobId) {
            const id = jobId || window.TctSpeakingImport.lastJobId;
            if (!id) return;
            setLoading(true);
            watchJob(id, '/api/speaking/imports/' + id);
        }
    };

    // Allow Enter key in URL input to trigger submit
    if (urlInput) {
        urlInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); submitBtn.click(); }
        });
    }

    // ── Delete confirmation ────────────────────────────────────────
    let pendingDeleteId   = null;
    let pendingDeleteCard = null;

    const deleteModalEl = document.getElementById('deleteVideoModal');
    const deleteModal   = deleteModalEl && typeof bootstrap !== 'undefined'
        ? new bootstrap.Modal(deleteModalEl)
        : null;
    const confirmBtn    = document.getElementById('delete-modal-confirm');
    const modalTitle    = document.getElementById('delete-modal-title');

    async function executeDelete() {
        if (!pendingDeleteId) return;

        const formData = new FormData();
        formData.append('id', pendingDeleteId);
        formData.append('__RequestVerificationToken', getToken());

        try {
            const res = await fetch('/Speaking/My/Delete', { method: 'POST', body: formData });

            if (res.ok) {
                if (deleteModal) deleteModal.hide();

                // Animate card removal
                const card = pendingDeleteCard;
                if (card) {
                    card.style.transition = 'opacity 0.28s ease, transform 0.28s ease';
                    card.style.opacity    = '0';
                    card.style.transform  = 'scale(0.94)';
                    setTimeout(() => { card.remove(); updateCount(); }, 300);
                } else {
                    updateCount();
                }
            }
        } catch (err) {
            console.error('[Speaking] Delete error:', err);
        }

        pendingDeleteId   = null;
        pendingDeleteCard = null;
    }

    function bindDeleteButton(btn) {
        if (btn._deleteHandlerBound) return;
        btn._deleteHandlerBound = true;

        btn.addEventListener('click', () => {
            pendingDeleteId   = btn.dataset.id;
            pendingDeleteCard = btn.closest('.my-video-card');

            if (modalTitle) {
                modalTitle.textContent = btn.dataset.title || 'video này';
            }

            if (deleteModal) {
                deleteModal.show();
            } else {
                // Fallback: native confirm (Bootstrap not yet loaded)
                if (window.confirm('Xóa "' + (btn.dataset.title || 'video này') + '"?')) {
                    executeDelete();
                } else {
                    pendingDeleteId   = null;
                    pendingDeleteCard = null;
                }
            }
        });
    }

    // Bind existing delete buttons on page load
    document.querySelectorAll('.my-btn-delete').forEach(bindDeleteButton);

    // Wire up modal confirm button
    if (confirmBtn) {
        confirmBtn.addEventListener('click', executeDelete);
    }

    // ── Initial count sync ─────────────────────────────────────────
    updateCount();

})();
