/**
 * READING STUDY JAVASCRIPT
 * Chức năng: Điều hướng câu hỏi, Dịch toàn bài (Anti-Limit), autosave / nộp bài.
 * Selection toolbar + My Vocab: reading-hybrid-toolbar.js
 */

document.addEventListener("DOMContentLoaded", function () {
    // --- KHAI BÁO BIẾN ---
    const panels = document.querySelectorAll('.question-panel');
    const totalQuestions = panels.length;
    let currentIdx = 0;

    // --- 1. ĐIỀU HƯỚNG CÂU HỎI ---
    window.showQuestion = function (index) {
        currentIdx = index;
        // Ẩn tất cả panel, hiện panel được chọn
        panels.forEach(p => p.classList.add('d-none'));
        const activePanel = document.getElementById(`q-panel-${index}`);
        if (activePanel) activePanel.classList.remove('d-none');

        // Cập nhật trạng thái nút điều hướng (Số câu)
        document.querySelectorAll('.nav-btn').forEach(btn => btn.classList.remove('active'));
        const navBtn = document.getElementById(`nav-btn-${index}`);
        if (navBtn) navBtn.classList.add('active');
    };

    window.markNavDone = function (index) {
        document.getElementById(`nav-btn-${index}`)?.classList.add('done');
    };

    window.nextQuestion = function () {
        if (currentIdx < totalQuestions - 1) showQuestion(currentIdx + 1);
    };

    window.prevQuestion = function () {
        if (currentIdx > 0) showQuestion(currentIdx - 1);
    };

    // Khởi tạo câu đầu tiên
    if (totalQuestions > 0) showQuestion(0);


    // --- 2. DỊCH TOÀN BÀI (CHIA NHỎ CÂU - TRÁNH LỖI 500 CHARS) ---
    document.getElementById("toggleTranslation")?.addEventListener("click", async function () {
        const btn = this;
        const isShowing = btn.classList.contains("active");
        const blocks = document.querySelectorAll(".paragraph-block");

        btn.classList.toggle("active");
        btn.innerHTML = isShowing ?
            '<i class="fa-solid fa-language" aria-hidden="true"></i> Dịch toàn bài' :
            '<i class="fa-solid fa-eye-slash" aria-hidden="true"></i> Ẩn bản dịch';
        for (const block of blocks) {
            const transEl = block.querySelector(".translation");
            if (!transEl) continue;

            if (isShowing) {
                transEl.classList.add("d-none");
                continue;
            }

            const originalText = block.querySelector(".passage-text").innerText.trim();
            if (originalText.length < 3) {
                transEl.classList.add("d-none");
                continue;
            }

            transEl.classList.remove("d-none");
            if (transEl.dataset.loaded) continue;

            transEl.innerHTML = '<small class="text-primary mt-2 d-block"><span class="spinner-border spinner-border-sm"></span> Đang dịch...</small>';

            try {
                // 1. Tách đoạn văn thành các câu
                const sentences = originalText.match(/[^.!?]+[.!?]+/g) || [originalText];

                // 2. TẠO DANH SÁCH CÁC REQUEST CHẠY CÙNG LÚC (Gán ở đây)
                const translationPromises = sentences.map(async (sentence) => {
                    const clean = sentence.trim();
                    if (clean.length < 2) return "";

                    try {
                        const res = await fetch(`/Reading/Translate?text=${encodeURIComponent(clean)}`);
                        const data = await res.json();
                        return data.translation || "";
                    } catch {
                        return ""; // Nếu một câu lỗi thì bỏ qua câu đó
                    }
                });

                // 3. ĐỢI TẤT CẢ CÁC CÂU DỊCH XONG CÙNG MỘT LÚC
                const results = await Promise.all(translationPromises);
                const fullTranslation = results.filter(t => t !== "").join(" ");

                if (fullTranslation.trim().length > 0) {
                    transEl.innerText = fullTranslation;
                    transEl.dataset.loaded = "true";
                } else {
                    transEl.innerText = "Không thể dịch đoạn này.";
                }
            } catch (err) {
                console.error(err);
                transEl.innerText = "Lỗi kết nối.";
            }
        }
    });


    // Mobile tabs + font size
    const grid = document.getElementById('readingStudyGrid');
    document.querySelectorAll('[data-study-tab]').forEach((btn) => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('[data-study-tab]').forEach((b) => b.classList.remove('active'));
            btn.classList.add('active');
            grid?.classList.remove('tab-passage', 'tab-questions');
            grid?.classList.add(btn.dataset.studyTab === 'questions' ? 'tab-questions' : 'tab-passage');
        });
    });

    const passageText = document.getElementById('passageContent');
    let fontSize = 1.1;
    document.getElementById('fontLarger')?.addEventListener('click', () => {
        fontSize = Math.min(1.6, fontSize + 0.1);
        if (passageText) passageText.style.fontSize = fontSize + 'rem';
    });
    document.getElementById('fontSmaller')?.addEventListener('click', () => {
        fontSize = Math.max(0.9, fontSize - 0.1);
        if (passageText) passageText.style.fontSize = fontSize + 'rem';
    });

    const toolbar = document.querySelector('.reading-toolbar');
    const attemptId = Number(toolbar?.dataset.attemptId || 0);
    const mode = toolbar?.dataset.mode || 'Practice';
    const timeLimit = Number(toolbar?.dataset.timeLimit || 0);
    const tokenInput = () => document.querySelector('#readingForm input[name="__RequestVerificationToken"]')?.value;

    async function autosaveAnswer(questionId, { selectedOptionId = null, answerText = null, matchingJson = null, isFlagged = false } = {}) {
        if (!attemptId) return;
        try {
            await fetch('/Reading/SaveAnswer', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': tokenInput() || ''
                },
                body: JSON.stringify({
                    attemptId,
                    questionId,
                    selectedOptionId,
                    answerText,
                    matchingJson,
                    isFlagged: !!isFlagged
                })
            });
        } catch { /* keep local form state */ }
    }

    function buildMatchingJson(panel) {
        const map = {};
        panel.querySelectorAll('.matching-row').forEach((row) => {
            const leftId = row.dataset.leftId;
            const select = row.querySelector('.matching-select');
            const rightId = select?.value;
            if (!leftId || !rightId) return;
            map[String(leftId)] = String(rightId);
        });
        return Object.keys(map).length ? JSON.stringify(map) : null;
    }

    function restoreMatchingSelections(panel) {
        const raw = panel.dataset.savedMatching;
        if (!raw) return;
        let map;
        try {
            map = JSON.parse(raw);
        } catch {
            return;
        }
        if (!map || typeof map !== 'object') return;

        panel.querySelectorAll('.matching-row').forEach((row) => {
            const leftId = String(row.dataset.leftId || '');
            const value = map[leftId];
            if (!value) return;
            const rightId = String(value);
            const select = row.querySelector('.matching-select');
            if (select && rightId) {
                select.value = rightId;
            }
        });

        if (panel.querySelectorAll('.matching-select').length &&
            [...panel.querySelectorAll('.matching-select')].every((s) => s.value)) {
            markNavDone(Number(panel.dataset.qIndex || 0));
        }
    }

    document.querySelectorAll('.question-panel input[type="radio"]').forEach((input) => {
        input.addEventListener('change', () => {
            const panel = input.closest('.question-panel');
            const qid = Number(panel?.dataset.qId || 0);
            const idx = Number(panel?.dataset.qIndex || 0);
            markNavDone(idx);
            autosaveAnswer(qid, { selectedOptionId: Number(input.value) });
        });
        if (input.checked) {
            const panel = input.closest('.question-panel');
            markNavDone(Number(panel?.dataset.qIndex || 0));
        }
    });

    let textSaveTimers = new Map();
    document.querySelectorAll('.reading-text-answer').forEach((field) => {
        const persist = () => {
            const panel = field.closest('.question-panel');
            const qid = Number(panel?.dataset.qId || 0);
            const idx = Number(panel?.dataset.qIndex || field.dataset.qIndex || 0);
            const value = field.value.trim();
            if (value) markNavDone(idx);
            autosaveAnswer(qid, { answerText: field.value });
        };
        field.addEventListener('input', () => {
            const qid = field.closest('.question-panel')?.dataset.qId || field.id;
            clearTimeout(textSaveTimers.get(qid));
            textSaveTimers.set(qid, setTimeout(persist, 400));
        });
        field.addEventListener('change', persist);
        if (field.value.trim()) {
            markNavDone(Number(field.dataset.qIndex || field.closest('.question-panel')?.dataset.qIndex || 0));
        }
    });

    document.querySelectorAll('.question-panel[data-q-type="Matching"]').forEach((panel) => {
        restoreMatchingSelections(panel);
        panel.querySelectorAll('.matching-select').forEach((select) => {
            select.addEventListener('change', () => {
                const qid = Number(panel.dataset.qId || 0);
                const idx = Number(panel.dataset.qIndex || select.dataset.qIndex || 0);
                const matchingJson = buildMatchingJson(panel);
                if (matchingJson) markNavDone(idx);
                autosaveAnswer(qid, { matchingJson });
            });
        });
    });

    async function flushPendingAnswers() {
        const tasks = [];
        document.querySelectorAll('.question-panel').forEach((panel) => {
            const qid = Number(panel.dataset.qId || 0);
            if (!qid) return;
            const type = panel.dataset.qType || '';
            if (type === 'MultipleChoice' || type === 'TrueFalse') {
                const checked = panel.querySelector('input[type="radio"]:checked');
                if (checked) tasks.push(autosaveAnswer(qid, { selectedOptionId: Number(checked.value) }));
            } else if (type === 'GapFill' || type === 'ShortAnswer') {
                const field = panel.querySelector('.reading-text-answer');
                if (field) tasks.push(autosaveAnswer(qid, { answerText: field.value }));
            } else if (type === 'Matching') {
                const matchingJson = buildMatchingJson(panel);
                if (matchingJson) tasks.push(autosaveAnswer(qid, { matchingJson }));
            }
        });
        await Promise.all(tasks);
    }

    // Timed mode
    const timerEl = document.getElementById('readingTimer');
    if (mode === 'Timed' && timeLimit > 0 && timerEl) {
        timerEl.classList.remove('d-none');
        let remaining = timeLimit;
        const tick = () => {
            const m = String(Math.floor(remaining / 60)).padStart(2, '0');
            const s = String(remaining % 60).padStart(2, '0');
            timerEl.textContent = `${m}:${s}`;
            if (remaining === 300 || remaining === 60) {
                timerEl.classList.add('text-warning');
                if (window.TctFeedback) {
                    window.TctFeedback.toast({
                        type: 'warning',
                        title: remaining === 300 ? 'Còn 5 phút làm bài' : 'Còn 1 phút làm bài',
                        message: remaining === 300
                            ? 'Hãy kiểm tra lại câu chưa trả lời trước khi hết giờ.'
                            : 'Sắp hết giờ — nộp bài khi bạn sẵn sàng.'
                    });
                }
            }
            if (remaining <= 0) {
                clearInterval(timer);
                document.getElementById('readingForm')?.requestSubmit();
                return;
            }
            remaining -= 1;
        };
        tick();
        var timer = setInterval(tick, 1000);
    }

    // --- 4. XỬ LÝ NỘP BÀI AJAX ---
    document.getElementById('readingForm')?.addEventListener('submit', async function (e) {
        e.preventDefault();
        const btn = document.getElementById('btnSubmit');
        const formData = new FormData(this);
        btn.disabled = true;

        try {
            await flushPendingAnswers();
            const token = formData.get('__RequestVerificationToken');
            let response;
            if (attemptId) {
                const body = new URLSearchParams({ attemptId: String(attemptId) });
                response = await fetch('/Reading/SubmitAttempt', {
                    method: 'POST',
                    headers: token ? { 'RequestVerificationToken': token, 'Content-Type': 'application/x-www-form-urlencoded' } : {},
                    body
                });
            } else {
                response = await fetch('/Reading/SubmitReading', {
                    method: 'POST',
                    headers: token ? { 'RequestVerificationToken': token } : {},
                    body: formData
                });
            }
            const payload = await response.json();
            const result = payload.result || payload;

            if (payload.success || result) {
                const correctCount = result.correctCount ?? result.CorrectCount;
                const totalCount = result.totalCount ?? result.TotalCount;
                const details = result.details || result.Details || [];
                document.getElementById('quizStatus').className = "alert alert-success d-block border-0 shadow-sm";
                document.getElementById('quizStatus').innerHTML = `<b>Kết quả: ${correctCount}/${totalCount}</b>`;

                details.forEach((item) => {
                    const qid = item.questionId ?? item.QuestionId;
                    const panel = document.querySelector(`.question-panel[data-q-id="${qid}"]`);
                    if (!panel) return;
                    const idx = Number(panel.dataset.qIndex || 0);
                    const navBtn = document.getElementById(`nav-btn-${idx}`);
                    const feedback = panel.querySelector('.result-feedback');
                    const isCorrect = item.isCorrect ?? item.IsCorrect;
                    const correctOptionText = item.correctOptionText ?? item.CorrectOptionText;
                    const correctAnswerText = item.correctAnswerText ?? item.CorrectAnswerText;
                    const explanation = item.explanation ?? item.Explanation;
                    const evidence = item.evidenceText ?? item.EvidenceText;
                    const paraphrase = item.paraphrase ?? item.Paraphrase;
                    const answerHint = correctOptionText || correctAnswerText || '';

                    if (feedback) {
                        feedback.classList.remove('d-none');
                        if (isCorrect) {
                            feedback.innerHTML = `<span class="text-success fw-bold small">Chính xác</span>`;
                        } else {
                            feedback.innerHTML = `<span class="text-danger fw-bold small">Sai.${answerHint ? ` Đáp án: ${answerHint}` : ''}</span>`;
                        }
                        if (explanation || evidence || paraphrase) {
                            feedback.innerHTML += `<div class="small text-muted mt-2">${explanation || ''}<br>${evidence || ''}<br>${paraphrase || ''}</div>`;
                        }
                    }
                    if (navBtn) {
                        navBtn.classList.add(isCorrect ? 'nav-correct' : 'nav-wrong');
                    }
                    panel.querySelectorAll('input, textarea, select').forEach((el) => { el.disabled = true; });
                });
                btn.classList.add('d-none');
                window.scrollTo({ top: 0, behavior: 'smooth' });
            } else {
                btn.disabled = false;
            }
        } catch (err) {
            btn.disabled = false;
            if (window.TctFeedback) {
                window.TctFeedback.toast({
                    type: 'danger',
                    title: 'Không nộp được bài đọc',
                    message: 'Kiểm tra mạng rồi thử nộp lại. Câu trả lời đã lưu tạm vẫn được giữ.'
                });
            }
        }
    });
});
