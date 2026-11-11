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
    const levelSections = document.querySelectorAll('.vi-level-section');
    const noResultsEl = document.getElementById('vi-no-results');

    // ── State ────────────────────────────────────────────────────
    const initialActiveLevelBtn = document.querySelector('.level-filter-btn.spk-pill-btn--active');
    let activeLevel = (initialActiveLevelBtn && initialActiveLevelBtn.dataset.level) || 'all';

    const initialActiveTopicBtn = document.querySelector('.topic-filter-btn.spk-chip--active');
    let activeTopic = (initialActiveTopicBtn && initialActiveTopicBtn.dataset.topic) || 'all';

    // Navigational anchors query the complete server catalog, including unloaded
    // pages. Only non-navigational buttons may narrow the cards already loaded.
    filterBtns.forEach(btn => {
        if (btn.tagName === 'A') return;
        btn.addEventListener('click', () => {
            filterBtns.forEach(b => b.classList.remove('active', 'spk-chip--active'));
            btn.classList.add('active', 'spk-chip--active');
            activeTopic = btn.dataset.topic || 'all';
            applyFilters();
        });
    });

    levelBtns.forEach(btn => {
        if (btn.tagName === 'A') return;
        btn.addEventListener('click', () => {
            levelBtns.forEach(b => b.classList.remove('active', 'spk-pill-btn--active'));
            btn.classList.add('active', 'spk-pill-btn--active');
            activeLevel = btn.dataset.level || 'all';
            applyFilters();
        });
    });

    // ── SEARCH (server query on Enter; live filter still narrows current page) ─
    if (searchInput) {
        searchInput.addEventListener('input', () => { applyFilters(); });
        searchInput.addEventListener('keydown', e => {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            const params = new URLSearchParams(window.location.search);
            const q = searchInput.value.trim();
            if (q) params.set('search', q); else params.delete('search');
            params.delete('page');
            const qs = params.toString();
            window.location.href = '/Speaking' + (qs ? ('?' + qs) : '');
        });

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
                : `${videoCounter.dataset.totalCount || totalVisibleCount} videos`;
        }

        const libraryBadge = document.querySelector('.spk-library-count-badge');
        if (libraryBadge) {
            libraryBadge.textContent = `${totalVisibleCount} bài học`;
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

        if (totalPages <= 1) {
            container.classList.add('d-none');
            return;
        }
        container.classList.remove('d-none');

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

    function appendPublicCard(track, item) {
        if (!track || !item || track.querySelector(`[data-video-id="${item.id}"]`)) return;
        const col = document.createElement('div');
        col.className = 'vi-video-col';
        col.dataset.title = (item.title || '').toLowerCase();
        col.dataset.topic = item.topic || '';
        col.dataset.level = item.level || '';
        col.dataset.videoId = String(item.id);
        const thumb = item.thumbnailUrl
            || `https://img.youtube.com/vi/${item.youtubeId}/hqdefault.jpg`;
        const link = document.createElement('a');
        link.href = '/Speaking/Practice/' + encodeURIComponent(item.id);
        link.className = 'text-decoration-none d-block h-100';
        const article = document.createElement('article');
        article.className = 'spk-pub-card h-100';
        const image = document.createElement('img');
        image.src = thumb;
        image.alt = item.title || '';
        image.loading = 'lazy';
        image.className = 'spk-thumb-img';
        const imageWrap = document.createElement('div');
        imageWrap.className = 'spk-video-thumb';
        imageWrap.appendChild(image);
        const body = document.createElement('div');
        body.className = 'spk-pub-card-body';
        const topic = document.createElement('span'); topic.className = 'spk-pub-card-topic';
        const title = document.createElement('h4'); title.className = 'spk-pub-card-title';
        body.append(topic, title); article.append(imageWrap, body); link.appendChild(article); col.appendChild(link);
        const topicEl = col.querySelector('.spk-pub-card-topic');
        if (topicEl) topicEl.textContent = item.topic || '';
        const titleEl = col.querySelector('.spk-pub-card-title');
        if (titleEl) titleEl.textContent = item.title || '';
        track.appendChild(col);
    }

    function appendPrivateCard(grid, item) {
        if (!grid || !item || grid.querySelector(`[data-id="${item.id}"]`)) return;
        const card = document.createElement('div');
        card.className = 'my-video-card' + (item.isLocked ? ' my-video-card--locked' : '');
        card.dataset.id = String(item.id); card.dataset.title = item.title || '';
        const body = document.createElement('div'); body.className = 'spk-my-card';
        const image = document.createElement('img'); image.className = 'spk-thumb-img';
        image.src = item.thumbnailUrl || `https://img.youtube.com/vi/${encodeURIComponent(item.youtubeId)}/hqdefault.jpg`;
        image.alt = item.title || ''; image.loading = 'lazy'; body.appendChild(image);
        const content = document.createElement('div'); content.className = 'spk-my-card-body';
        const title = document.createElement('h3'); title.className = 'spk-my-card-title'; title.textContent = item.title || '';
        content.appendChild(title);
        const status = document.createElement('p'); status.className = 'small text-muted';
        status.textContent = item.isLocked ? 'Đã khóa theo gói hiện tại' : item.importStatus === 'processing' ? 'Đang xử lý' : item.importStatus === 'failed' ? 'Xử lý thất bại' : 'Sẵn sàng';
        content.appendChild(status);
        const actions = document.createElement('div'); actions.className = 'spk-my-card-actions';
        if (item.isLocked || item.importStatus === 'ready') {
            const practice = document.createElement('a'); practice.className = 'spk-btn spk-btn--primary spk-btn--flex';
            practice.href = item.isLocked ? '/Account/Premium' : '/Speaking/Practice/' + encodeURIComponent(item.id);
            practice.textContent = item.isLocked ? 'Nâng cấp Premium' : 'Luyện tập'; actions.appendChild(practice);
        }
        if (!item.isLocked && item.importStatus !== 'processing') {
            const remove = document.createElement('button'); remove.type = 'button';
            remove.className = 'spk-btn spk-btn--danger-outline my-btn-delete';
            remove.dataset.id = String(item.id); remove.dataset.title = item.title || '';
            remove.textContent = 'Xóa video'; actions.appendChild(remove);
        }
        content.appendChild(actions); body.appendChild(content); card.appendChild(body); grid.appendChild(card);
    }

    function loadError(button, error) {
        let message = button.parentElement.querySelector('[data-spk-load-error]');
        if (!message) {
            message = document.createElement('p'); message.dataset.spkLoadError = '';
            message.className = 'small text-danger'; message.setAttribute('role', 'status');
            button.parentElement.appendChild(message);
        }
        message.textContent = error ? 'Không tải được video. Vui lòng thử lại.' : '';
    }

    document.querySelectorAll('.spk-load-more-public').forEach(btn => {
        btn.addEventListener('click', async () => {
            if (btn.disabled) return;
            btn.disabled = true;
            loadError(btn, null);
            try {
                const page = Number(btn.dataset.page || '2');
                const params = new URLSearchParams({
                    level: btn.dataset.level || '',
                    topic: btn.dataset.topic || '',
                    search: btn.dataset.search || '',
                    page: String(page),
                    pageSize: btn.dataset.pageSize || '24'
                });
                const response = await fetch('/Speaking/Videos?' + params.toString(), {
                    headers: { 'Accept': 'application/json' },
                    credentials: 'same-origin'
                });
                if (!response.ok) throw new Error('Video page request failed');
                const payload = await response.json();
                const track = document.getElementById(`track-${btn.dataset.level}`);
                const items = Array.isArray(payload.items) ? payload.items : [];
                items.forEach(item => appendPublicCard(track, item));
                if (payload.hasMore) {
                    btn.dataset.page = String(page + 1);
                    btn.disabled = false;
                } else {
                    btn.remove();
                }
                applyFilters();
            } catch (error) {
                loadError(btn, error);
            } finally {
                if (btn.isConnected) btn.disabled = false;
            }
        });
    });

    document.querySelectorAll('.spk-load-more-mine').forEach(btn => {
        btn.addEventListener('click', async () => {
            if (btn.disabled) return;
            btn.disabled = true;
            loadError(btn, null);
            try {
                const page = Number(btn.dataset.page || '2');
                const params = new URLSearchParams({
                    mine: 'true',
                    page: String(page),
                    pageSize: btn.dataset.pageSize || '24',
                    search: btn.dataset.search || ''
                });
                const response = await fetch('/Speaking/Videos?' + params.toString(), {
                    headers: { 'Accept': 'application/json' },
                    credentials: 'same-origin'
                });
                if (!response.ok) throw new Error('Private video page request failed');
                const payload = await response.json();
                const grid = document.getElementById('my-videos-grid');
                const items = Array.isArray(payload.items) ? payload.items : [];
                items.forEach(item => appendPrivateCard(grid, item));
                if (payload.hasMore) {
                    btn.dataset.page = String(page + 1);
                    btn.disabled = false;
                } else {
                    btn.remove();
                }
            } catch (error) {
                loadError(btn, error);
            } finally {
                if (btn.isConnected) btn.disabled = false;
            }
        });
    });

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
    let assessmentChoice = 'automatic';
    let selfClip = null;

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
            width: '100%',
            height: '100%',
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
        if (activeSentIdx < 0 && SENTENCES.length > 0) selectSentence(0, false);
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
    function isSelfPractice() {
        return assessmentChoice === 'self';
    }

    function availabilityMessage(kind) {
        const helper = global.TctAiAvailability;
        if (helper && typeof helper.message === 'function') {
            return helper.message(kind);
        }
        if (kind === 'selfPractice' || kind === 'self') {
            return 'Bạn vẫn có thể luyện và nghe lại. Bản thu này chưa được chấm tự động.';
        }
        if (kind === 'unassessed') return 'Chưa chấm tự động';
        if (kind === 'unavailable') return 'Tính năng AI tạm thời chưa sẵn sàng.';
        if (kind === 'rate') return 'Bạn đang gửi yêu cầu quá nhanh.';
        if (kind === 'access') return 'Phiên đăng nhập đã hết hoặc bạn không có quyền.';
        if (kind === 'mic') return 'Mic bị từ chối hoặc không khả dụng — chưa lưu tiến độ.';
        if (kind === 'nospeech') return 'Chưa nhận được lời nói — chưa lưu tiến độ.';
        return 'Dịch vụ đánh giá đang gián đoạn.';
    }

    function pauseSelfAudio() {
        const audio = $('spk-self-audio');
        if (audio && typeof audio.pause === 'function') {
            try { audio.pause(); } catch (_) { /* ignore */ }
        }
    }

    function releaseSelfClip() {
        pauseSelfAudio();
        const urlApi = global.URL || global.webkitURL;
        if (selfClip && selfClip.url && urlApi && typeof urlApi.revokeObjectURL === 'function') {
            try { urlApi.revokeObjectURL(selfClip.url); } catch (_) { /* ignore */ }
        }
        selfClip = null;
        const audio = $('spk-self-audio');
        if (audio) {
            audio.src = '';
            if (typeof audio.removeAttribute === 'function') audio.removeAttribute('src');
        }
        const wrap = $('spk-self-playback');
        if (wrap) wrap.hidden = true;
    }

    function showUnassessedBoard() {
        const label = availabilityMessage('unassessed');
        SCORE_METRICS.forEach(function (metric) {
            const valEl = $(metric.id);
            if (valEl) {
                valEl.textContent = '—';
                valEl.title = label;
            }
            const ringEl = $(metric.ringId);
            if (ringEl) ringEl.setAttribute('stroke-dasharray', '0, 100');
            const barEl = $(metric.barId);
            if (barEl) barEl.style.width = '0%';
        });
    }

    function syncChoiceButtons() {
        const automatic = $('spk-choice-automatic');
        const self = $('spk-choice-self');
        const selfSelected = isSelfPractice();
        if (automatic) {
            automatic.classList.toggle('is-selected', !selfSelected);
            automatic.setAttribute('aria-pressed', selfSelected ? 'false' : 'true');
        }
        if (self) {
            self.classList.toggle('is-selected', selfSelected);
            self.setAttribute('aria-pressed', selfSelected ? 'true' : 'false');
        }
        const note = $('spk-self-practice-note');
        if (note) {
            note.hidden = !selfSelected;
            if (selfSelected) note.textContent = availabilityMessage('selfPractice');
        }
        if (!btnRecord) return;
        const label = isSelfPractice() ? 'Thu và nghe lại' : 'Kiểm tra phát âm';
        if (!isRecording && !isScoring && recordLabel) recordLabel.textContent = label;
        if (!isRecording && !isScoring) btnRecord.setAttribute('aria-label', label);
    }

    function showSelfOffer(visible) {
        const offer = $('spk-offer-self');
        if (offer) offer.hidden = !visible;
    }

    function attachPlayback(snapshot, blob, asUnassessed) {
        if (!blob || recordingEpoch !== snapshot.epoch) return;
        const current = SENTENCES[activeSentIdx];
        if (!current || current.id !== snapshot.sentenceId) return;
        releaseSelfClip();
        const urlApi = global.URL || global.webkitURL;
        const url = urlApi && typeof urlApi.createObjectURL === 'function' ? urlApi.createObjectURL(blob) : '';
        selfClip = { url: url, sentenceId: snapshot.sentenceId, epoch: snapshot.epoch };
        const audio = $('spk-self-audio');
        const wrap = $('spk-self-playback');
        const playbackLabel = $('spk-self-playback-label');
        if (audio && url) audio.src = url;
        if (wrap) wrap.hidden = !url;
        if (playbackLabel) playbackLabel.textContent = availabilityMessage('unassessed');
        if (asUnassessed) {
            showUnassessedBoard();
            setAssessmentStatus(availabilityMessage('selfPractice'));
            showSelfOffer(false);
        }
    }

    function setAssessmentChoice(choice) {
        const next = choice === 'self' ? 'self' : 'automatic';
        if (next === assessmentChoice) {
            syncChoiceButtons();
            return;
        }
        assessmentChoice = next;
        if (isRecording || activeRecognition || activePcmRecorder || activeAssessController) {
            abortActiveRecognition('mode');
        }
        syncChoiceButtons();
        if (next === 'self') {
            if (selfClip) {
                showUnassessedBoard();
                setAssessmentStatus(availabilityMessage('selfPractice'));
            } else {
                setAssessmentStatus('Tự luyện: thu và nghe lại trên trình duyệt, không gửi đi chấm.');
            }
            showSelfOffer(false);
            return;
        }
        releaseSelfClip();
        showSelfOffer(false);
        const sentence = SENTENCES[activeSentIdx];
        if (sentence) updateScoreBoard(sentence, null);
        setAssessmentStatus('Đã về chấm tự động. Hãy thu một bản mới; bản cũ không được gửi lại.');
    }

    function classifyAssessFailure(response, body) {
        const helper = global.TctAiAvailability;
        if (helper && typeof helper.classifySpeakingFailure === 'function') {
            return helper.classifySpeakingFailure(response, body);
        }
        const status = response && response.status ? response.status : 0;
        const code = body && (body.code || body.errorCode) ? String(body.code || body.errorCode) : '';
        if ((response && response.redirected) || status === 401 || status === 403) {
            if (!code || code === 'auth') return { kind: 'access', message: availabilityMessage('access') };
        }
        if (status === 404) return { kind: 'access', message: (body && body.error) || 'Không tìm thấy câu này.' };
        if (code === 'engine_auth' || code === 'quota_exhausted') return { kind: 'unavailable', message: availabilityMessage('unavailable') };
        if (code === 'engine_busy') {
            const server = body && (body.error || body.message) ? String(body.error || body.message) : '';
            if (/hàng đợi/i.test(server)) return { kind: 'busy', message: server };
            return { kind: 'rate', message: availabilityMessage('rate') };
        }
        if (status === 429) return { kind: 'rate', message: availabilityMessage('rate') };
        return { kind: 'outage', message: availabilityMessage('outage') };
    }

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
        if (reason === 'sentence-change' || reason === 'tab-switch' || reason === 'dispose' || reason === 'replace') {
            releaseSelfClip();
        }
        if (reason === 'sentence-change') {
            setAssessmentStatus('Đã hủy ghi âm vì đổi câu — kết quả cũ không được gắn sang câu mới.');
            clearWordComparison();
        }
    }

    function selectSentence(idx, autoPlay = true) {
        if (idx < 0 || idx >= SENTENCES.length) return;

        if (idx !== activeSentIdx) {
            abortActiveRecognition(activeSentIdx >= 0 ? 'sentence-change' : 'init');
        } else if (isRecording || activeRecognition || activePcmRecorder || activeAssessController) {
            abortActiveRecognition('reselect');
        }

        activeSentIdx = idx;
        const s = SENTENCES[idx];

        // Update slider buttons
        $$('.spk-sent-btn').forEach(btn => btn.classList.remove('spk-sent-btn--active'));
        const activeBtn = document.querySelector(`.spk-sent-btn[data-index="${idx}"]`);
        if (activeBtn) {
            activeBtn.classList.add('spk-sent-btn--active');
            const isReducedMotion = typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            activeBtn.scrollIntoView({ behavior: isReducedMotion ? 'auto' : 'smooth', block: 'nearest', inline: 'center' });
        }
        // Sync dictation slider
        const activeDicBtn = document.querySelector(`.spk-sent-btn[data-dic-index="${idx}"]`);
        if (activeDicBtn) {
            activeDicBtn.classList.add('spk-sent-btn--active');
            const isReducedMotion = typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            activeDicBtn.scrollIntoView({ behavior: isReducedMotion ? 'auto' : 'smooth', block: 'nearest', inline: 'center' });
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
        clearWordComparison();

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

    function clearWordComparison() {
        const panel = $('spk-word-comparison');
        const body = $('spk-word-comparison-body');
        if (body) {
            while (body.firstChild) {
                body.removeChild(body.firstChild);
            }
        }
        if (panel) {
            panel.hidden = true;
        }
    }

    function renderWordComparison(comparison) {
        const panel = $('spk-word-comparison');
        const body = $('spk-word-comparison-body');
        if (!panel || !body) {
            return;
        }

        while (body.firstChild) {
            body.removeChild(body.firstChild);
        }

        if (!comparison || comparison.available !== true || !Array.isArray(comparison.operations)) {
            const note = document.createElement('p');
            note.className = 'spk-word-comparison-empty';
            note.textContent = comparison && comparison.unavailableReason === 'token_limit'
                ? 'Câu quá dài để đối chiếu an toàn trong lần này.'
                : 'Chưa có đối chiếu nội dung nhận diện.';
            body.appendChild(note);
            panel.hidden = false;
            return;
        }

        if (comparison.operations.length === 0) {
            const note = document.createElement('p');
            note.className = 'spk-word-comparison-empty';
            note.textContent = 'Không có từ để đối chiếu.';
            body.appendChild(note);
            panel.hidden = false;
            return;
        }

        const list = document.createElement('ul');
        list.className = 'spk-word-comparison-list';
        list.setAttribute('role', 'list');

        comparison.operations.forEach(function (op) {
            const kind = String(op && op.kind || '').toLowerCase();
            const item = document.createElement('li');
            item.className = 'spk-word-chip spk-word-chip--' + (kind || 'matched');
            item.setAttribute('role', 'listitem');

            const mark = document.createElement('span');
            mark.className = 'spk-word-chip-mark';
            mark.setAttribute('aria-hidden', 'true');
            mark.textContent = kind === 'missing' ? '−'
                : kind === 'extra' ? '+'
                    : kind === 'substituted' ? '≠'
                        : '✓';
            item.appendChild(mark);

            const label = document.createElement('span');
            label.className = 'spk-word-chip-label';
            if (kind === 'missing') {
                label.textContent = String(op.referenceToken || '');
                item.setAttribute('aria-label', 'Thiếu: ' + label.textContent);
            } else if (kind === 'extra') {
                label.textContent = String(op.hypothesisToken || '');
                item.setAttribute('aria-label', 'Thừa: ' + label.textContent);
            } else if (kind === 'substituted') {
                label.textContent = String(op.referenceToken || '') + ' → ' + String(op.hypothesisToken || '');
                item.setAttribute('aria-label', 'Khác: ' + label.textContent);
            } else {
                label.textContent = String(op.hypothesisToken || op.referenceToken || '');
                item.setAttribute('aria-label', 'Khớp: ' + label.textContent);
            }
            item.appendChild(label);
            list.appendChild(item);
        });

        body.appendChild(list);
        panel.hidden = false;
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

        pauseSelfAudio();

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
            expectedText: sentence.text || '',
            choice: assessmentChoice
        };
        activeRecordingSnapshot = snapshot;
        // New recording epoch owns the comparison panel — drop prior result immediately.
        clearWordComparison();
        releaseSelfClip();

        if (isSelfPractice()) {
            if (!(global.TctSpeechRecorder && global.TctSpeechRecorder.isSupported())) {
                showToast('Trình duyệt chưa hỗ trợ thu âm trên máy. Bạn vẫn có thể nghe bài.', 'error');
                setAssessmentStatus(availabilityMessage('mic'));
                return;
            }
            startPcmRecording(snapshot);
            return;
        }

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
            btnRecord.classList.add('is-recording', 'recording', 'spk-record-btn--active');
            if (typeof btnRecord.setAttribute === 'function') { btnRecord.setAttribute('aria-busy', 'true'); btnRecord.setAttribute('aria-label', 'Đang ghi âm. Nhấn để dừng.'); }
            if (recordIcon) {
                recordIcon.className = 'material-symbols-outlined';
                recordIcon.textContent = 'stop_circle';
            }
            if (recordLabel) recordLabel.textContent = 'Đang ghi âm...';
        } else {
            btnRecord.classList.remove('is-recording', 'recording', 'spk-record-btn--active');
            if (typeof btnRecord.removeAttribute === 'function') { btnRecord.removeAttribute('aria-busy'); }             if (typeof btnRecord.setAttribute === 'function') { btnRecord.setAttribute('aria-label', isSelfPractice() ? 'Thu và nghe lại' : 'Kiểm tra phát âm'); }
            if (recordIcon) {
                recordIcon.className = 'material-symbols-outlined';
                recordIcon.textContent = 'mic';
            }
            if (recordLabel) recordLabel.textContent = isSelfPractice() ? 'Thu và nghe lại' : 'Kiểm tra phát âm';
        }
    }

    function setScoringUIState(scoring) {
        if (!btnRecord) return;
        if (scoring) {
            btnRecord.classList.add('is-scoring', 'assessing', 'spk-record-btn--assessing');
            if (typeof btnRecord.setAttribute === 'function') { btnRecord.setAttribute('aria-busy', 'true'); btnRecord.setAttribute('aria-label', 'Đang chấm điểm...'); }
            btnRecord.disabled = true;
            if (recordIcon) {
                recordIcon.className = 'fas fa-spinner fa-spin';
                recordIcon.textContent = '';
            }
            if (recordLabel) recordLabel.textContent = 'Đang chấm điểm...';
        } else {
            btnRecord.classList.remove('is-scoring', 'assessing', 'spk-record-btn--assessing');
            if (typeof btnRecord.removeAttribute === 'function') { btnRecord.removeAttribute('aria-busy'); } if (typeof btnRecord.setAttribute === 'function') { btnRecord.setAttribute('aria-label', 'Kiểm tra phát âm'); }
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
                if (snapshot.choice === 'self') {
                    attachPlayback(snapshot, blob, true);
                    return;
                }
                assessAndCommitFromWav(snapshot, blob);
            },
            onError: function () {
                if (recordingEpoch !== snapshot.epoch) return;
                setAssessmentStatus(availabilityMessage('mic'));
            }
        });
        activePcmRecorder = recorder;
        isRecording = true;
        setRecordUIState(true);
        setAssessmentStatus(snapshot.choice === 'self'
            ? 'Đang thu để nghe lại. Bản này không gửi đi chấm.'
            : 'Đang thu PCM — sẽ gửi máy chủ chấm (không dùng điểm trình duyệt).');
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
            clearWordComparison();
            showToast('Không mở được micro — kiểm tra quyền mic/HTTPS.', 'error');
            setAssessmentStatus(availabilityMessage('mic'));
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
        clearWordComparison();

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
            if (assessBody.status === 'NoSpeech' || assessBody.code === 'no_speech') {
                showToast(assessBody.message || 'Chưa nhận được lời nói.', 'warning');
                setAssessmentStatus(assessBody.message || availabilityMessage('nospeech'));
                clearWordComparison();
                return;
            }
            if (!assessResponse.ok || !assessBody.success) {
                const classified = classifyAssessFailure(assessResponse, assessBody);
                showToast(classified.message, 'warning');
                setAssessmentStatus(classified.message);
                clearWordComparison();
                if (classified.kind === 'outage' || classified.kind === 'unavailable' || classified.kind === 'rate' || classified.kind === 'busy') {
                    attachPlayback(snapshot, wavBlob, false);
                    showSelfOffer(true);
                }
                return;
            }

            if (!assessBody.canCommitProgress || !assessBody.assessmentId) {
                showToast(assessBody.message || 'Chưa nhận được lời nói.', 'warning');
                setAssessmentStatus(assessBody.message || availabilityMessage('nospeech'));
                clearWordComparison();
                return;
            }

            updateScoreBoard(s, Math.round(assessBody.textMatchScore || 0));
            renderWordComparison(assessBody.comparison);
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
            showToast(availabilityMessage('outage'), 'error');
            setAssessmentStatus(availabilityMessage('outage'));
            attachPlayback(snapshot, wavBlob, false);
            showSelfOffer(true);
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
                    // Not yet reached -> show asterisk in slate
                    charSpans += '<span class="char-untyped spk-dic-char--untyped">*</span>';
                } else if (typedText[pos].toLowerCase() === word[c].toLowerCase()) {
                    // Correct — show expected char in vivid green (preserves original case)
                    charSpans += `<span class="char-correct spk-dic-char--correct">${escapeHtml(word[c])}</span>`;
                } else {
                    // Incorrect — show what user typed in vivid red
                    const wrongChar = typedText[pos] === ' ' ? '·' : typedText[pos];
                    charSpans += `<span class="char-incorrect spk-dic-char--wrong">${escapeHtml(wrongChar)}</span>`;
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
        $('spk-choice-automatic')?.addEventListener('click', function () { setAssessmentChoice('automatic'); });
        $('spk-choice-self')?.addEventListener('click', function () { setAssessmentChoice('self'); });
        $('spk-offer-self')?.addEventListener('click', function () { setAssessmentChoice('self'); });

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
        const tabsTrack = $('spk-practice-tabs') || tabPronunciation?.parentElement;

        tabPronunciation?.addEventListener('click', function () {
            if (isRecording || activeRecognition || activePcmRecorder || activeAssessController || selfClip) {
                abortActiveRecognition('tab-switch');
            }
            this.classList.add('spk-pill-active', 'spk-practice-tab--active');
            tabDictation?.classList.remove('spk-pill-active', 'spk-practice-tab--active');
            tabsTrack?.classList.remove('spk-tabs--dictation-active');
            document.querySelector('.spk-practice-wrapper')?.classList.remove('spk-mode--dictation');
            panePronounciation?.classList.remove('d-none');
            panePronounciation?.classList.add('d-flex');
            paneDictation?.classList.add('d-none');
            paneDictation?.classList.remove('d-flex');
        });

        tabDictation?.addEventListener('click', function () {
            if (isRecording || activeRecognition || activePcmRecorder || activeAssessController || selfClip) {
                abortActiveRecognition('tab-switch');
            }
            this.classList.add('spk-pill-active', 'spk-practice-tab--active');
            tabPronunciation?.classList.remove('spk-pill-active', 'spk-practice-tab--active');
            tabsTrack?.classList.add('spk-tabs--dictation-active');
            document.querySelector('.spk-practice-wrapper')?.classList.add('spk-mode--dictation');
            paneDictation?.classList.remove('d-none');
            paneDictation?.classList.add('d-flex');
            panePronounciation?.classList.add('d-none');
            panePronounciation?.classList.remove('d-flex');
            setTimeout(() => dicInput?.focus(), 120);
        });

        // Keyboard shortcuts and page lifecycle. Shortcuts stay on the
        // document so a focused button, link, field, or editor keeps the
        // browser's own Enter, Space, and Tab behavior.
        document.addEventListener('keydown', onKeyDown);
        bindPageLifecycle();
    }

    function isShortcutBlockedTarget(node) {
        let current = node;
        while (current && current !== document && current !== document.documentElement) {
            const tag = current.tagName ? String(current.tagName).toUpperCase() : '';
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || tag === 'BUTTON' || tag === 'A' || tag === 'AUDIO' || tag === 'VIDEO' || tag === 'SUMMARY') {
                return true;
            }
            if (current.isContentEditable) return true;
            const role = typeof current.getAttribute === 'function' ? current.getAttribute('role') : '';
            if (role === 'button' || role === 'link' || role === 'textbox' || role === 'searchbox' || role === 'slider' || role === 'tab' || role === 'menuitem' || role === 'checkbox' || role === 'radio' || role === 'switch' || role === 'combobox' || role === 'option' || role === 'listbox' || role === 'spinbutton') {
                return true;
            }
            current = current.parentElement || current.parentNode;
        }
        return false;
    }

    function onKeyDown(e) {
        if (!e || e.defaultPrevented || e.isComposing || e.ctrlKey || e.metaKey || e.altKey) return;
        if (e.code === 'Tab' || isShortcutBlockedTarget(e.target)) return;

        switch (e.code) {
            case 'Space':
                if (e.shiftKey) return;
                e.preventDefault();
                if (player && typeof player.getPlayerState === 'function') {
                    if (player.getPlayerState() === YT.PlayerState.PLAYING) player.pauseVideo();
                    else player.playVideo();
                }
                break;
            case 'Enter':
                if (e.shiftKey) return;
                e.preventDefault();
                startRecording();
                break;
            case 'ArrowLeft':
                if (e.shiftKey) { e.preventDefault(); btnPrev?.click(); }
                break;
            case 'ArrowRight':
                e.preventDefault();
                btnNext?.click();
                break;
        }
    }

    let pageLifecycleTarget = null;

    function releasePracticeForLifecycle() {
        const canceledRecording = isRecording || !!activePcmRecorder || !!activeRecognition;
        const canceledSend = isScoring || !!activeAssessController;
        const removedClip = !!selfClip;
        abortActiveRecognition('pagehide');
        releaseSelfClip();
        if (playbackTimer) {
            clearInterval(playbackTimer);
            playbackTimer = null;
        }
        isScoring = false;
        setScoringUIState(false);
        setRecordUIState(false);
        if (canceledRecording || canceledSend) {
            setAssessmentStatus('Đã dừng thao tác trên trang này. Nếu bản thu đã được gửi, hãy tải lại kết quả trước khi thu lại.');
        } else if (removedClip) {
            setAssessmentStatus('Bản nghe lại đã được gỡ khi rời trang. Điểm đã lưu vẫn giữ nguyên.');
        }
    }

    function onPageHide() {
        releasePracticeForLifecycle();
    }

    function onPageShow(event) {
        if (!event || event.persisted !== true) return;
        releasePracticeForLifecycle();
    }

    function bindPageLifecycle() {
        if (pageLifecycleTarget) return;
        const host = typeof global.addEventListener === 'function' && typeof global.removeEventListener === 'function'
            ? global
            : document;
        pageLifecycleTarget = host;
        pageLifecycleTarget.addEventListener('pagehide', onPageHide);
        pageLifecycleTarget.addEventListener('pageshow', onPageShow);
    }

    function unbindPageLifecycle() {
        if (!pageLifecycleTarget) return;
        pageLifecycleTarget.removeEventListener('pagehide', onPageHide);
        pageLifecycleTarget.removeEventListener('pageshow', onPageShow);
        pageLifecycleTarget = null;
    }

    // ────────────────────────────────────────────────────────────────
    //  INIT / DISPOSE
    // ────────────────────────────────────────────────────────────────
    function dispose() {
        abortActiveRecognition('dispose');
        releaseSelfClip();
        if (playbackTimer) {
            clearInterval(playbackTimer);
            playbackTimer = null;
        }
        if (toastTimer) {
            clearTimeout(toastTimer);
            toastTimer = null;
        }
        document.removeEventListener('keydown', onKeyDown);
        unbindPageLifecycle();
        isScoring = false;
    }

    function init() {
        bindEvents();
        // Sentence controls stay usable while the external video API is loading.
        if (SENTENCES.length > 0) selectSentence(0, false);
        if (TEST_MODE) {
            return;
        }
        loadYouTubeAPI();
        showToast('⌨️  Space=Play  Shift+←/→=Câu  Enter=Ghi âm', 'info');
    }

    global.TctSpeakingPractice = {
        startRecording,
        selectSentence,
        dispose,
        setAssessmentChoice,
        getAssessmentChoice: function () { return assessmentChoice; },
        applyRecordingFeedback,
        renderDictationFeedback,
        isPreviewOnly: BROWSER_SCORE_PREVIEW_ONLY,
    };
    global.renderDictationFeedback = renderDictationFeedback;

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
        const serverTotal = Number(mySecCount.dataset.totalCount);
        const n = mySecCount.dataset.totalCount !== undefined && Number.isFinite(serverTotal)
            ? serverTotal : realCards.length;
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
                if (mySecCount?.dataset.totalCount !== undefined) {
                    mySecCount.dataset.totalCount = String(Math.max(0, Number(mySecCount.dataset.totalCount) - 1));
                }

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

        const openConfirmation = () => {
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
        };
        btn.addEventListener('click', openConfirmation);
        return openConfirmation;
    }

    // Bind existing delete buttons on page load
    document.querySelectorAll('.my-btn-delete').forEach(bindDeleteButton);
    myVideosGrid?.addEventListener('click', event => {
        const button = event.target.closest('.my-btn-delete');
        if (!button || button._deleteHandlerBound) return;
        // HTML suppresses a nested .click() while the original activation is in
        // progress. Invoke the new handler directly so the first click works.
        bindDeleteButton(button)();
    });

    // Wire up modal confirm button
    if (confirmBtn) {
        confirmBtn.addEventListener('click', executeDelete);
    }

    // ── Initial count sync ─────────────────────────────────────────
    updateCount();

    // ── Global Helpers for Empty State Bento ───────────────────────────
    window.focusImportInput = function () {
        var input = document.getElementById('mip-url-input');
        if (input) {
            if (typeof input.scrollIntoView === 'function') {
                input.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
            setTimeout(function () {
                input.focus();
                var wrap = input.closest ? input.closest('.spk-hero-url-wrap') : null;
                if (wrap) {
                    wrap.classList.add('spk-halo-pulse');
                    setTimeout(function () { wrap.classList.remove('spk-halo-pulse'); }, 1400);
                }
            }, 300);
        }
    };

    window.setSampleVideo = function (btn) {
        if (!btn) return;
        var url = btn.dataset ? btn.dataset.url : btn.getAttribute('data-url');
        var input = document.getElementById('mip-url-input');
        if (input && url) {
            input.value = url;
            if (typeof Event === 'function') {
                input.dispatchEvent(new Event('input', { bubbles: true }));
            }
            window.focusImportInput();
        }
    };

})();
