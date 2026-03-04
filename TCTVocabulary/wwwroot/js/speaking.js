/**
 * speaking.js — TCT Speaking Module
 * Part 1: Index Page — topic filtering, search, slider navigation
 * Part 2: Practice Page — YouTube IFrame API, sentence sync, speech AI
 */

// ════════════════════════════════════════════════════════════════════
//  PART 1 — INDEX PAGE: Topic Filter, Search & Slider
// ════════════════════════════════════════════════════════════════════
(function () {
    'use strict';

    // Guard: only run on the Index page (has filter buttons)
    const filterBtns = document.querySelectorAll('.topic-filter-btn');
    if (!filterBtns.length) return;

    // ── DOM refs ─────────────────────────────────────────────────
    const searchInput = document.getElementById('vi-search');
    const videoCounter = document.getElementById('vi-video-count');
    const allCards = document.querySelectorAll('.vi-video-col');
    const levelSections = document.querySelectorAll('.vi-level-section');

    // ── State ────────────────────────────────────────────────────
    let activeTopic = 'all';

    // ── TOPIC FILTER ─────────────────────────────────────────────
    filterBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            // Update active button
            filterBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            activeTopic = btn.dataset.topic;

            // Clear search when switching topics
            if (searchInput) {
                searchInput.value = '';
            }

            applyFilters();
        });
    });

    // ── SEARCH ───────────────────────────────────────────────────
    if (searchInput) {
        searchInput.addEventListener('input', () => {
            applyFilters();
        });

        // Ctrl+K / Cmd+K → focus search
        document.addEventListener('keydown', e => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                searchInput.focus();
                searchInput.select();
            }
        });
    }

    // ── CORE FILTER & PAGINATION LOGIC ─────────────────────────────
    const ITEMS_PER_PAGE = 6;

    function applyFilters() {
        const query = searchInput ? searchInput.value.toLowerCase().trim() : '';
        let totalVisibleCount = 0;

        levelSections.forEach(section => {
            const level = section.dataset.level;
            const cardsInLevel = section.querySelectorAll('.vi-video-col');
            let visibleCards = [];

            // 1. Determine which cards match the current filters
            cardsInLevel.forEach(card => {
                const cardTopic = (card.dataset.topic || '').trim();
                const cardTitle = (card.dataset.title || '').trim();

                const topicMatch = activeTopic === 'all' || cardTopic === activeTopic;
                const searchMatch = !query || cardTitle.includes(query);

                if (topicMatch && searchMatch) {
                    visibleCards.push(card);
                    totalVisibleCount++;
                } else {
                    // Hide non-matching right away
                    card.classList.add('vi-card-hidden');
                }
            });

            // 2. Hide section if empty, else paginate it
            if (visibleCards.length === 0) {
                section.classList.add('vi-section-hidden');
            } else {
                section.classList.remove('vi-section-hidden');
                renderPagination(level, visibleCards);
            }
        });

        // Update total counter
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

        // Reset all visible cards to be shown (in case they were hidden by the old grid logic)
        visibleCards.forEach(card => card.classList.remove('vi-card-hidden'));

        const totalItems = visibleCards.length;
        const totalPages = Math.ceil(totalItems / ITEMS_PER_PAGE);

        // Clear existing buttons
        container.innerHTML = '';

        // If 1 page or less, hide pagination
        if (totalPages <= 1) {
            return;
        }

        // Build pagination buttons
        for (let i = 1; i <= totalPages; i++) {
            const btn = document.createElement('button');
            btn.className = 'page-btn';
            btn.textContent = i;
            if (i === 1) btn.classList.add('active');

            btn.addEventListener('click', () => {
                // Update active button state
                container.querySelectorAll('.page-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');

                // Calculate scroll position based on 6 cards per page
                // We use the first visible card to estimate card width including gaps
                const firstCard = visibleCards[0];
                if (firstCard) {
                    const cardWidthWithGap = firstCard.offsetWidth + 16; // 16px is the gap from CSS
                    const scrollAmount = (i - 1) * ITEMS_PER_PAGE * cardWidthWithGap;
                    track.scrollTo({ left: scrollAmount, behavior: 'smooth' });
                }
            });

            container.appendChild(btn);
        }

        // Reset scroll to left when filters change
        track.scrollTo({ left: 0, behavior: 'smooth' });
    }

    // Optional: Update active pagination pill based on manual scrolling
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
                const scrollLeft = track.scrollLeft;

                // Determine current page based on scroll position
                const currentPage = Math.floor(scrollLeft / (cardWidthWithGap * ITEMS_PER_PAGE)) + 1;

                const buttons = container.querySelectorAll('.page-btn');
                buttons.forEach(b => b.classList.remove('active'));

                const activeBtn = buttons[currentPage - 1];
                if (activeBtn) activeBtn.classList.add('active');
            });
        }
    });

    // ── INIT ─────────────────────────────────────────────────────
    applyFilters(); // ensure correct state on page load

})();


// ════════════════════════════════════════════════════════════════════
//  PART 2 — PRACTICE PAGE
// ════════════════════════════════════════════════════════════════════
/**
 * speaking.js — TCT Speaking Practice Module
 * Encapsulates: YouTube IFrame API bridge, sentence sync,
 *               slow-motion, repeat/loop, Web Speech AI feedback, toast.
 * Constraint: no global pollution; safe to co-exist with other pages.
 */
(function (global) {
    'use strict';

    // ── Guard: only run on the Practice page ────────────────────────
    if (!global.SPK_VIDEO_ID) return;

    // ── Data from Razor ─────────────────────────────────────────────
    const VIDEO_ID = global.SPK_VIDEO_ID;
    const SENTENCES = global.SPK_SENTENCES || [];   // [{id,start,end,text,vi}]

    // ── DOM refs ─────────────────────────────────────────────────────
    const $ = id => document.getElementById(id);
    const $$ = sel => document.querySelectorAll(sel);

    const btnPlayPause = $('btn-play-pause');
    const iconPlayPause = $('icon-play-pause');
    const btnRepeat = $('btn-repeat');
    const btnSlow = $('btn-slow');
    const btnPrev = $('btn-prev-sentence');
    const btnNext = $('btn-next-sentence');
    const btnToggleViAll = $('btn-toggle-vi-all');
    const sentenceList = $('prac-sentence-list');
    const progressFill = $('prac-progress-fill');
    const currentTimeEl = $('prac-current-time');
    const totalTimeEl = $('prac-total-time');
    const sentIdxEl = $('prac-sent-idx');
    const modeChipEl = $('prac-mode-chip');
    const toast = $('spk-toast');

    // ── State ────────────────────────────────────────────────────────
    let player = null;
    let activeSentIdx = -1;
    let isLooping = false;
    let isSlowMotion = false;
    let viAllVisible = false;
    let pollTimer = null;
    let toastTimer = null;
    const viVisible = {};  // per-sentence VI toggle state

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
                controls: 1,    // keep native controls visible
                iv_load_policy: 3,
                cc_load_policy: 0
            },
            events: {
                onReady: onPlayerReady,
                onStateChange: onPlayerStateChange
            }
        });
    };

    function onPlayerReady() {
        // Kick off polling
        pollTimer = setInterval(onTick, 150);

        // Set total duration label once it's available
        setTimeout(() => {
            const dur = player.getDuration();
            if (dur) totalTimeEl.textContent = fmtTime(dur);
        }, 1500);
    }

    function onPlayerStateChange(evt) {
        const playing = evt.data === YT.PlayerState.PLAYING;
        iconPlayPause.className = playing ? 'fas fa-pause' : 'fas fa-play';
    }

    // ────────────────────────────────────────────────────────────────
    //  TICK — sync sentence highlight + progress bar
    // ────────────────────────────────────────────────────────────────
    function onTick() {
        if (!player || typeof player.getCurrentTime !== 'function') return;
        const state = player.getPlayerState();
        if (state !== YT.PlayerState.PLAYING) return;

        const t = player.getCurrentTime();
        const dur = player.getDuration() || 1;

        // Progress bar
        progressFill.style.width = ((t / dur) * 100).toFixed(2) + '%';
        currentTimeEl.textContent = fmtTime(t);

        // Loop: if active sentence ended, seek back
        if (isLooping && activeSentIdx >= 0) {
            const s = SENTENCES[activeSentIdx];
            if (t >= s.end) {
                player.seekTo(s.start, true);
                return;
            }
        }

        // Find active sentence
        const idx = SENTENCES.findIndex(s => t >= s.start && t < s.end);
        if (idx !== -1 && idx !== activeSentIdx) {
            setActiveSentence(idx, false);
        }
    }

    function setActiveSentence(idx, scrollOnly = false) {
        // Remove old highlight
        if (activeSentIdx >= 0) {
            const prev = sentenceList.querySelector(`[data-index="${activeSentIdx}"]`);
            if (prev) prev.classList.remove('prac-active');
        }

        activeSentIdx = idx;
        sentIdxEl.textContent = idx + 1;

        const el = sentenceList.querySelector(`[data-index="${idx}"]`);
        if (!el) return;

        el.classList.add('prac-active');
        el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

        // Update loop boundaries
        if (isLooping) {
            // Loop already handles via activeSentIdx
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  CONTROL HANDLERS
    // ────────────────────────────────────────────────────────────────
    function togglePlayPause() {
        if (!player) return;
        if (player.getPlayerState() === YT.PlayerState.PLAYING) {
            player.pauseVideo();
        } else {
            player.playVideo();
        }
    }

    function toggleRepeat() {
        isLooping = !isLooping;
        btnRepeat.classList.toggle('prac-ctrl-active', isLooping);
        modeChipEl.textContent = isLooping ? '🔁 Looping' : '';
        showToast(isLooping ? 'Loop ON — repeating current sentence' : 'Loop OFF');
    }

    function toggleSlow() {
        isSlowMotion = !isSlowMotion;
        btnSlow.classList.toggle('prac-ctrl-active', isSlowMotion);
        if (player && typeof player.setPlaybackRate === 'function') {
            player.setPlaybackRate(isSlowMotion ? 0.75 : 1);
        }
        showToast(isSlowMotion ? 'Slow Motion: 0.75×' : 'Normal Speed: 1×');
    }

    function seekToSentence(idx) {
        if (!SENTENCES[idx]) return;
        if (player && typeof player.seekTo === 'function') {
            player.seekTo(SENTENCES[idx].start, true);
            player.playVideo();
        }
        setActiveSentence(idx);
    }

    function goPrev() {
        const target = Math.max(0, activeSentIdx > 0 ? activeSentIdx - 1 : 0);
        seekToSentence(target);
    }

    function goNext() {
        const target = Math.min(SENTENCES.length - 1, activeSentIdx + 1);
        seekToSentence(target);
    }

    // ────────────────────────────────────────────────────────────────
    //  VIETNAMESE TOGGLE
    // ────────────────────────────────────────────────────────────────
    function toggleViSingle(idx) {
        viVisible[idx] = !viVisible[idx];
        const el = $(`sent-vi-${idx}`);
        if (el) el.classList.toggle('prac-vi-visible', viVisible[idx]);
    }

    function toggleViAll() {
        viAllVisible = !viAllVisible;
        btnToggleViAll.classList.toggle('prac-ctrl-active', viAllVisible);
        SENTENCES.forEach((_, i) => {
            viVisible[i] = viAllVisible;
            const el = $(`sent-vi-${i}`);
            if (el) el.classList.toggle('prac-vi-visible', viAllVisible);
        });
        showToast(viAllVisible ? 'Vietnamese shown' : 'Vietnamese hidden');
    }

    // ────────────────────────────────────────────────────────────────
    //  WEB SPEECH API — Record & Compare
    // ────────────────────────────────────────────────────────────────
    const SpeechRecog = global.SpeechRecognition || global.webkitSpeechRecognition;
    let activeRecognition = null;

    function startRecording(idx, micBtn) {
        if (!SpeechRecog) {
            showToast('⚠️ Speech recognition requires Chrome or Edge', 'error');
            return;
        }

        // Prevent double-click start
        if (activeRecognition) {
            activeRecognition.stop();
            activeRecognition = null;
            return;
        }

        // Pause video while listening
        if (player && player.getPlayerState() === YT.PlayerState.PLAYING) {
            player.pauseVideo();
        }

        const sentEl = sentenceList.querySelector(`[data-index="${idx}"]`);
        const enEl = $(`sent-en-${idx}`);
        const expected = SENTENCES[idx]?.text || '';

        // Recording visual state
        micBtn.classList.add('prac-mic-recording');
        micBtn.innerHTML = '<i class="fas fa-stop-circle"></i>';

        const rec = new SpeechRecog();
        rec.lang = 'en-US';
        rec.interimResults = true;
        rec.maxAlternatives = 3;
        activeRecognition = rec;

        let interimShown = false;

        rec.onresult = function (event) {
            let interim = '';
            let final = '';
            for (let i = event.resultIndex; i < event.results.length; i++) {
                const t = event.results[i][0].transcript;
                if (event.results[i].isFinal) final += t;
                else interim += t;
            }

            // Show interim in a subtle style
            if (!final && interim && !interimShown) {
                enEl.dataset.original = enEl.textContent;
                interimShown = true;
            }

            if (final) {
                const sim = levenshteinSimilarity(
                    normalizeText(final),
                    normalizeText(expected)
                );
                applyFeedback(idx, sim, final);
            }
        };

        rec.onerror = function (evt) {
            const msgs = {
                'no-speech': 'No speech detected — try again',
                'not-allowed': 'Microphone access denied',
                'network': 'Network error during recognition',
                'audio-capture': 'No microphone found'
            };
            showToast('⚠️ ' + (msgs[evt.error] || evt.error), 'error');
        };

        rec.onend = function () {
            activeRecognition = null;
            micBtn.classList.remove('prac-mic-recording');
            micBtn.innerHTML = '<i class="fas fa-microphone"></i>';
        };

        rec.start();
    }

    function applyFeedback(idx, similarity, heard) {
        const pct = Math.round(similarity * 100);
        const enEl = $(`sent-en-${idx}`);
        const barEl = $(`acc-bar-${idx}`);
        const fillEl = $(`acc-fill-${idx}`);
        const pctEl = $(`acc-pct-${idx}`);
        const sentEl = sentenceList.querySelector(`[data-index="${idx}"]`);

        // Remove previous result classes
        sentEl.classList.remove('prac-result-ok', 'prac-result-fail');
        enEl.classList.remove('prac-text-ok', 'prac-text-fail');

        if (similarity >= 0.8) {
            sentEl.classList.add('prac-result-ok');
            enEl.classList.add('prac-text-ok');
            showToast(`✅ Great! Accuracy: ${pct}%`, 'success');
        } else {
            sentEl.classList.add('prac-result-fail');
            enEl.classList.add('prac-text-fail');
            showToast(`🔁 Try again! You said: "${heard}" (${pct}%)`, 'retry');
        }

        // Accuracy bar
        if (barEl && fillEl && pctEl) {
            barEl.style.display = 'flex';
            fillEl.style.width = pct + '%';
            fillEl.style.background = similarity >= 0.8 ? '#10b981' : '#ef4444';
            pctEl.textContent = pct + '%';
            pctEl.style.color = similarity >= 0.8 ? '#10b981' : '#ef4444';
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  HELPERS
    // ────────────────────────────────────────────────────────────────
    function normalizeText(s) {
        return s.toLowerCase()
            .replace(/[^\w\s]/g, '')
            .replace(/\s+/g, ' ')
            .trim();
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

    function fmtTime(secs) {
        const m = Math.floor(secs / 60);
        const s = Math.floor(secs % 60);
        return m + ':' + String(s).padStart(2, '0');
    }

    // ── Toast notification ───────────────────────────────────────────
    function showToast(msg, type = 'info') {
        if (!toast) return;
        clearTimeout(toastTimer);
        toast.textContent = msg;
        toast.className = `spk-toast spk-toast-${type} spk-toast-show`;
        toastTimer = setTimeout(() => {
            toast.className = 'spk-toast';
        }, 3000);
    }

    // ────────────────────────────────────────────────────────────────
    //  EVENT WIRING
    // ────────────────────────────────────────────────────────────────
    function bindEvents() {
        btnPlayPause?.addEventListener('click', togglePlayPause);
        btnRepeat?.addEventListener('click', toggleRepeat);
        btnSlow?.addEventListener('click', toggleSlow);
        btnPrev?.addEventListener('click', goPrev);
        btnNext?.addEventListener('click', goNext);
        btnToggleViAll?.addEventListener('click', toggleViAll);

        // Keyboard shortcuts
        document.addEventListener('keydown', onKeyDown);

        // Sentence-row clicks
        $$('.prac-sentence').forEach(row => {
            const idx = parseInt(row.dataset.index, 10);

            // Click row → seek
            row.addEventListener('click', e => {
                if (e.target.closest('.prac-sent-actions')) return;
                seekToSentence(idx);
            });
        });

        // Listen buttons
        $$('.prac-listen-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                e.stopPropagation();
                const start = parseFloat(btn.dataset.start);
                if (player && !isNaN(start)) {
                    player.seekTo(start, true);
                    player.playVideo();
                }
            });
        });

        // Record buttons
        $$('.prac-mic-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                e.stopPropagation();
                const idx = parseInt(btn.dataset.index, 10);
                startRecording(idx, btn);
            });
        });

        // Per-sentence VI toggle
        $$('.prac-vi-toggle').forEach(btn => {
            btn.addEventListener('click', e => {
                e.stopPropagation();
                const idx = parseInt(btn.dataset.index, 10);
                toggleViSingle(idx);
            });
        });
    }

    function onKeyDown(e) {
        // Ignore if typing in an input/textarea
        if (['INPUT', 'TEXTAREA'].includes(e.target.tagName)) return;

        switch (e.code) {
            case 'Space':
                e.preventDefault();
                togglePlayPause();
                break;
            case 'ArrowLeft':
                e.preventDefault();
                goPrev();
                break;
            case 'ArrowRight':
                e.preventDefault();
                goNext();
                break;
            case 'KeyR':
                toggleRepeat();
                break;
            case 'KeyS':
                toggleSlow();
                break;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  INIT
    // ────────────────────────────────────────────────────────────────
    function init() {
        bindEvents();
        loadYouTubeAPI();

        // Pre-hide all VI translations
        SENTENCES.forEach((_, i) => {
            const el = $(`sent-vi-${i}`);
            if (el) el.classList.remove('prac-vi-visible');
        });

        showToast('⌨️  Space=Play  ←→=Jump  R=Loop  S=Slow', 'info');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window);
