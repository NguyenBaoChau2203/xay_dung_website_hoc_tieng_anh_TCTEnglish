/**
 * Hybrid Reading Toolbar — selection toolbar + My Vocab dock panel.
 */
(function () {
    'use strict';

    const MAX_WORDS = 8;
    const MAX_CHARS = 120;

    document.addEventListener('DOMContentLoaded', () => {
        const passage = document.getElementById('passageContent');
        const toolbar = document.getElementById('readingHybridToolbar');
        const panel = document.getElementById('readingVocabPanel');
        const backdrop = document.getElementById('readingVocabBackdrop');
        const shell = document.getElementById('readingStudyShell');
        const studyToolbar = document.querySelector('.reading-toolbar');
        if (!passage || !toolbar || !panel) return;

        const passageId = Number(studyToolbar?.dataset.passageId || document.getElementById('passageId')?.value || 0);
        const token = () =>
            document.querySelector('#readingForm input[name="__RequestVerificationToken"]')?.value
            || document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || '';

        const els = {
            empty: document.getElementById('readingVocabEmpty'),
            loading: document.getElementById('readingVocabLoading'),
            error: document.getElementById('readingVocabError'),
            errorText: document.getElementById('readingVocabErrorText'),
            list: document.getElementById('readingVocabList'),
            retry: document.getElementById('readingVocabRetry'),
            close: document.getElementById('readingVocabPanelClose'),
            toast: document.getElementById('readingHybridToast')
        };

        /** @type {{ text: string, paragraphNo: number|null, sentence: string, range: Range|null }} */
        let selectionState = { text: '', paragraphNo: null, sentence: '', range: null };
        /** @type {{ mode: string, text: string, paragraphNo: number|null, sentence: string }|null} */
        let lastAssist = null;
        /** @type {Map<string, object>} */
        const sessionCards = new Map();
        let selectionTimer = null;
        let panelOpener = null;

        loadAnnotations();

        document.addEventListener('mouseup', (e) => {
            if (toolbar.contains(e.target) || panel.contains(e.target)) return;
            clearTimeout(selectionTimer);
            selectionTimer = setTimeout(() => syncSelectionFromWindow(), 120);
        });

        document.addEventListener('keyup', (e) => {
            if (e.key === 'Escape') {
                hideToolbar();
                if (!panel.hidden) closePanel();
                return;
            }
            if (e.key === 'Shift' || e.key.startsWith('Arrow')) {
                clearTimeout(selectionTimer);
                selectionTimer = setTimeout(() => syncSelectionFromWindow(), 120);
            }
        });

        window.addEventListener('scroll', () => {
            if (!toolbar.classList.contains('d-none')) positionToolbar();
        }, true);

        toolbar.querySelectorAll('[data-hybrid-action]').forEach((btn) => {
            btn.addEventListener('click', async (e) => {
                e.preventDefault();
                e.stopPropagation();
                const action = btn.getAttribute('data-hybrid-action');
                if (!selectionState.text) return;
                if (action === 'highlight') await highlightSelection();
                else if (action === 'listen') listenSelection();
                else if (action === 'translate') await assist('Translate');
                else if (action === 'dictionary') await assist('Dictionary');
            });
        });

        els.close?.addEventListener('click', () => closePanel());
        backdrop?.addEventListener('click', () => closePanel());
        els.retry?.addEventListener('click', async () => {
            if (!lastAssist) return;
            await assist(lastAssist.mode, lastAssist);
        });

        function syncSelectionFromWindow() {
            const sel = window.getSelection();
            if (!sel || sel.rangeCount === 0 || sel.isCollapsed) {
                hideToolbar();
                return;
            }

            const text = sel.toString().replace(/\s+/g, ' ').trim();
            if (!isValidSelection(text)) {
                hideToolbar();
                return;
            }

            const range = sel.getRangeAt(0);
            if (!passage.contains(range.commonAncestorContainer)) {
                hideToolbar();
                return;
            }

            const block = closestParagraphBlock(range.commonAncestorContainer);
            selectionState = {
                text,
                paragraphNo: block ? Number(block.dataset.paragraphNo || 0) || null : null,
                sentence: extractSurroundingSentence(block, text),
                range: range.cloneRange()
            };
            showToolbar();
            positionToolbar();
        }

        function isValidSelection(text) {
            if (!text || text.length > MAX_CHARS) return false;
            const words = text.split(/\s+/).filter(Boolean);
            return words.length >= 1 && words.length <= MAX_WORDS;
        }

        function showToolbar() {
            toolbar.classList.remove('d-none');
            toolbar.setAttribute('aria-hidden', 'false');
        }

        function hideToolbar() {
            toolbar.classList.add('d-none');
            toolbar.setAttribute('aria-hidden', 'true');
        }

        function positionToolbar() {
            const sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return;
            const rect = sel.getRangeAt(0).getBoundingClientRect();
            if (!rect || (rect.width === 0 && rect.height === 0)) return;

            const tw = toolbar.offsetWidth || 280;
            const th = toolbar.offsetHeight || 48;
            let left = rect.left + rect.width / 2 - tw / 2;
            let top = rect.top - th - 10;
            left = Math.max(8, Math.min(left, window.innerWidth - tw - 8));
            if (top < 8) top = rect.bottom + 10;
            toolbar.style.left = `${left}px`;
            toolbar.style.top = `${top}px`;
        }

        function openPanel() {
            panelOpener = document.activeElement;
            panel.hidden = false;
            panel.setAttribute('aria-hidden', 'false');
            shell?.classList.add('vocab-panel-open');
            if (window.matchMedia('(max-width: 768px)').matches) {
                backdrop?.classList.remove('d-none');
                backdrop && (backdrop.hidden = false);
            }
            els.close?.focus();
        }

        function closePanel() {
            panel.hidden = true;
            panel.setAttribute('aria-hidden', 'true');
            shell?.classList.remove('vocab-panel-open');
            backdrop?.classList.add('d-none');
            if (backdrop) backdrop.hidden = true;
            if (panelOpener && typeof panelOpener.focus === 'function') panelOpener.focus();
            panelOpener = null;
        }

        function setPanelState(state) {
            [els.empty, els.loading, els.error, els.list].forEach((node) => {
                if (!node) return;
                const match = node.dataset.vocabState === state;
                node.classList.toggle('d-none', !match);
            });
        }

        async function assist(mode, replay) {
            const payload = replay || {
                mode,
                text: selectionState.text,
                paragraphNo: selectionState.paragraphNo,
                sentence: selectionState.sentence
            };
            lastAssist = payload;
            openPanel();
            setPanelState('loading');

            try {
                const res = await fetch('/Reading/Assist', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token()
                    },
                    body: JSON.stringify({
                        passageId,
                        selectedText: payload.text,
                        mode: payload.mode,
                        paragraphNo: payload.paragraphNo,
                        surroundingSentence: payload.sentence
                    })
                });
                const data = await res.json();
                if (!res.ok || !data.success) {
                    showError(data.errorMessage || data.ErrorMessage || 'Không lấy được nghĩa. Thử lại.');
                    return;
                }
                upsertCard(normalizeAssist(data, payload.mode));
                renderCards();
                setPanelState('list');
            } catch {
                showError('Lỗi kết nối. Thử lại.');
            }
        }

        function normalizeAssist(data, mode) {
            return {
                key: `${(data.word || data.Word || '').toLowerCase()}|${mode}`,
                mode: data.mode || data.Mode || mode,
                source: data.source || data.Source || '',
                cached: !!(data.cached ?? data.Cached),
                vocabItemId: data.vocabItemId ?? data.VocabItemId ?? null,
                word: data.word || data.Word || '',
                lemma: data.lemma || data.Lemma || data.word || data.Word || '',
                translation: data.translation || data.Translation || data.vietnameseMeaning || data.VietnameseMeaning || '',
                partOfSpeech: data.partOfSpeech || data.PartOfSpeech || '',
                ipa: data.ipa || data.Ipa || '',
                englishDefinition: data.englishDefinition || data.EnglishDefinition || '',
                vietnameseMeaning: data.vietnameseMeaning || data.VietnameseMeaning || data.translation || data.Translation || '',
                example: data.example || data.Example || ''
            };
        }

        function upsertCard(card) {
            sessionCards.set(card.key, card);
        }

        function renderCards() {
            if (!els.list) return;
            els.list.innerHTML = '';
            const cards = Array.from(sessionCards.values()).reverse();
            cards.forEach((card) => {
                const article = document.createElement('article');
                article.className = 'reading-vocab-card';
                const metaParts = [
                    card.mode === 'Dictionary' ? 'Dictionary' : 'Translate',
                    card.partOfSpeech,
                    card.ipa,
                    card.source === 'PassageVocab' ? 'Trong bài' : (card.cached ? 'Đã lưu' : null)
                ].filter(Boolean);

                article.innerHTML = `
                    <h3 class="reading-vocab-card__word"></h3>
                    <p class="reading-vocab-card__meta"></p>
                    <p class="reading-vocab-card__meaning"></p>
                    <p class="reading-vocab-card__example d-none"></p>
                    <button type="button" class="btn btn-sm btn-outline-primary">Lưu sổ tay</button>
                `;
                article.querySelector('.reading-vocab-card__word').textContent = card.lemma || card.word;
                article.querySelector('.reading-vocab-card__meta').textContent = metaParts.join(' · ');
                const meaning = card.mode === 'Dictionary'
                    ? [card.englishDefinition, card.vietnameseMeaning].filter(Boolean).join(' — ')
                    : (card.vietnameseMeaning || card.translation);
                article.querySelector('.reading-vocab-card__meaning').textContent = meaning || '—';
                const exampleEl = article.querySelector('.reading-vocab-card__example');
                if (card.example) {
                    exampleEl.textContent = card.example;
                    exampleEl.classList.remove('d-none');
                }
                article.querySelector('button')?.addEventListener('click', () => saveNotebook(card));
                els.list.appendChild(article);
            });
        }

        function showError(message) {
            if (els.errorText) els.errorText.textContent = message;
            setPanelState('error');
        }

        function showToast(message) {
            if (!els.toast) return;
            els.toast.textContent = message;
            els.toast.classList.remove('d-none');
            setTimeout(() => els.toast.classList.add('d-none'), 2200);
        }

        async function saveNotebook(card) {
            try {
                const res = await fetch('/Reading/Notebook/Save', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token()
                    },
                    body: JSON.stringify({
                        passageId,
                        vocabItemId: card.vocabItemId,
                        word: card.word || card.lemma,
                        ipa: card.ipa || null,
                        partOfSpeech: card.partOfSpeech || null,
                        englishDefinition: card.englishDefinition || null,
                        vietnameseMeaning: card.vietnameseMeaning || card.translation || null,
                        example: card.example || null
                    })
                });
                const data = await res.json();
                if (!res.ok || !data.success) {
                    showToast('Không lưu được sổ tay.');
                    return;
                }
                showToast('Đã lưu vào sổ tay.');
            } catch {
                showToast('Không lưu được sổ tay.');
            }
        }

        async function highlightSelection() {
            const text = selectionState.text;
            const range = selectionState.range;
            if (!text || !range) return;

            const offsets = computePassageOffsets(range);
            try {
                surroundRangeWithMark(range);
            } catch {
                // Selection may cross nodes; still persist annotation.
            }

            try {
                await fetch('/Reading/Annotations', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token()
                    },
                    body: JSON.stringify({
                        passageId,
                        annotationType: 'Highlight',
                        startOffset: offsets.start,
                        endOffset: offsets.end,
                        selectedText: text,
                        color: 'yellow'
                    })
                });
                showToast('Đã tô sáng.');
            } catch {
                showToast('Không lưu được highlight.');
            }

            hideToolbar();
            window.getSelection()?.removeAllRanges();
        }

        function listenSelection() {
            const text = selectionState.text;
            if (!text) return;
            if (!window.speechSynthesis || typeof SpeechSynthesisUtterance === 'undefined') {
                openPanel();
                showError('Trình duyệt không hỗ trợ phát âm.');
                return;
            }
            window.speechSynthesis.cancel();
            const utter = new SpeechSynthesisUtterance(text);
            utter.lang = 'en-US';
            utter.rate = 0.95;
            window.speechSynthesis.speak(utter);
        }

        function closestParagraphBlock(node) {
            let current = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
            while (current && current !== passage) {
                if (current.classList?.contains('paragraph-block')) return current;
                current = current.parentElement;
            }
            return null;
        }

        function extractSurroundingSentence(block, selected) {
            const raw = (block?.querySelector('.passage-text')?.textContent || passage.textContent || '').replace(/\s+/g, ' ').trim();
            if (!raw) return selected;
            const idx = raw.toLowerCase().indexOf(selected.toLowerCase());
            if (idx < 0) return raw.slice(0, MAX_CHARS * 2);
            const start = Math.max(0, raw.lastIndexOf('.', idx - 1) + 1);
            let end = raw.indexOf('.', idx + selected.length);
            if (end < 0) end = raw.length;
            return raw.slice(start, end + 1).trim().slice(0, 240);
        }

        function computePassageOffsets(range) {
            const pre = document.createRange();
            pre.selectNodeContents(passage);
            pre.setEnd(range.startContainer, range.startOffset);
            const start = pre.toString().length;
            const end = start + range.toString().length;
            return { start, end };
        }

        function surroundRangeWithMark(range) {
            const mark = document.createElement('mark');
            mark.className = 'tct-highlight';
            range.surroundContents(mark);
        }

        async function loadAnnotations() {
            if (!passageId) return;
            try {
                const res = await fetch(`/Reading/Annotations/${passageId}`);
                const data = await res.json();
                const items = data.items || data.Items || [];
                items
                    .filter((item) => (item.annotationType || item.AnnotationType || 'Highlight') === 'Highlight')
                    .forEach((item) => {
                        const start = item.startOffset ?? item.StartOffset ?? 0;
                        const end = item.endOffset ?? item.EndOffset ?? 0;
                        if (end > start) applyHighlightOffsets(start, end);
                    });
            } catch {
                /* ignore restore failures */
            }
        }

        function applyHighlightOffsets(start, end) {
            const walker = document.createTreeWalker(passage, NodeFilter.SHOW_TEXT);
            let current = 0;
            /** @type {{ node: Text, start: number, end: number }[]} */
            const parts = [];
            while (walker.nextNode()) {
                const node = walker.currentNode;
                const len = node.textContent.length;
                const nodeStart = current;
                const nodeEnd = current + len;
                const overlapStart = Math.max(start, nodeStart);
                const overlapEnd = Math.min(end, nodeEnd);
                if (overlapStart < overlapEnd) {
                    parts.push({
                        node,
                        start: overlapStart - nodeStart,
                        end: overlapEnd - nodeStart
                    });
                }
                current = nodeEnd;
            }
            parts.reverse().forEach((part) => {
                try {
                    const range = document.createRange();
                    range.setStart(part.node, part.start);
                    range.setEnd(part.node, part.end);
                    surroundRangeWithMark(range);
                } catch {
                    /* skip unstable nodes */
                }
            });
        }
    });
})();
