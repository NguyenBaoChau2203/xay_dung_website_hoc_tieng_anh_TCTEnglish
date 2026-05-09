/**
 * READING STUDY JAVASCRIPT
 * Chức năng: Điều hướng câu hỏi, Dịch từng câu (Anti-Limit), Popup dịch từ bám theo scroll.
 */

// Helper: Lấy Anti-forgery token (Phải khai báo global để dùng trong các hàm async bên ngoài)
function getCsrfToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
}

// --- 1. ĐIỀU HƯỚNG CÂU HỎI (GLOBAL) ---
window.showQuestion = function (index) {
    const panels = document.querySelectorAll('.question-panel');
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
    const panels = document.querySelectorAll('.question-panel');
    const activePanel = Array.from(panels).find(p => !p.classList.contains('d-none'));
    const currentIdx = activePanel ? parseInt(activePanel.id.split('-').pop()) : 0;
    if (currentIdx < panels.length - 1) window.showQuestion(currentIdx + 1);
};

window.prevQuestion = function () {
    const panels = document.querySelectorAll('.question-panel');
    const activePanel = Array.from(panels).find(p => !p.classList.contains('d-none'));
    const currentIdx = activePanel ? parseInt(activePanel.id.split('-').pop()) : 0;
    if (currentIdx > 0) window.showQuestion(currentIdx - 1);
};

// --- 2. DỊCH TỪNG CÂU (INLINE TRANSLATION - GLOBAL) ---
let isInlineMode = false;

window.toggleInlineTranslationMode = async function () {
    isInlineMode = !isInlineMode;
    const myId = document.getElementById('myTranslationId').value;
    const actions = document.getElementById('inlineTranslationActions');
    const btnToggle = document.getElementById('btnToggleInlineTranslation');
    const btnPublish = document.getElementById('btnPublishInlineTranslation');
    const aiResultContainer = document.getElementById('inlineAiResult');

    const passageContainer = document.querySelector('.passage-container');
    if (passageContainer) passageContainer.classList.toggle('translation-mode-active', isInlineMode);

    if (isInlineMode) {
        actions.classList.remove('d-none');
        if (aiResultContainer) aiResultContainer.classList.remove('d-none');
        btnToggle.classList.add('active');

        const btnDelete = document.getElementById('btnDeleteInlineTranslation');
        if (btnDelete) btnDelete.classList.add('d-none');

        // Nếu đã có bản dịch, load dữ liệu
        if (myId) {
            if (btnDelete) btnDelete.classList.remove('d-none');
            try {
                const res = await fetch(`/Reading/Translation/${myId}/Json`);
                const data = await res.json();
                if (data.translatedTitle) {
                    const titleInput = document.getElementById('titleTranslationInput');
                    if (titleInput) titleInput.value = data.translatedTitle;
                }
                if (data.translatedContent) {
                    const lines = data.translatedContent.split("\n");
                    const inputFields = document.querySelectorAll('.translation-input');
                    inputFields.forEach((input, index) => {
                        if (index < lines.length) {
                            input.value = lines[index];
                        }
                    });
                }
                if (data.isAiApproved === true) {
                    btnPublish.classList.remove('d-none');
                } else {
                    btnPublish.classList.add('d-none');
                }
                if (data.isAiApproved !== null) {
                    showInlineAiResult(data);
                }
            } catch (err) {
                console.error("Load translation fail", err);
            }
        } else {
            btnPublish.classList.add('d-none');
        }
    } else {
        actions.classList.add('d-none');
        if (aiResultContainer) aiResultContainer.classList.add('d-none');
        btnToggle.classList.remove('active');
        const btnDelete = document.getElementById('btnDeleteInlineTranslation');
        if (btnDelete) btnDelete.classList.add('d-none');
    }
};

window.saveInlineTranslation = async function () {
    const passageId = document.getElementById('passageId').value;
    const inputFields = document.querySelectorAll('.translation-input');
    const lines = [];
    
    inputFields.forEach(input => {
        lines.push(input.value.trim());
    });

    if (!lines.some(l => l.length > 0)) {
        alert("Vui lòng nhập ít nhất một câu dịch.");
        return;
    }

    const content = lines.join("\n");
    const btnSave = document.getElementById('btnSaveInlineTranslation');
    const btnPublish = document.getElementById('btnPublishInlineTranslation');
    
    btnSave.disabled = true;
    btnSave.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang lưu...';
    btnPublish.classList.add('d-none');

    const titleInput = document.getElementById('titleTranslationInput');
    const translatedTitle = titleInput ? titleInput.value.trim() : "";

    try {
        const saveRes = await fetch('/Reading/Translation/Save', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getCsrfToken()
            },
            body: JSON.stringify({ 
                passageId: parseInt(passageId), 
                translatedTitle: translatedTitle, 
                translatedContent: content 
            })
        });
        const saveData = await saveRes.json();

        if (!saveRes.ok) throw new Error(saveData.error || "Lỗi lưu bản dịch");

        const translationId = saveData.translationId;
        document.getElementById('myTranslationId').value = translationId;
        const btnDelete = document.getElementById('btnDeleteInlineTranslation');
        if (btnDelete) btnDelete.classList.remove('d-none');
        
        btnSave.innerHTML = '<span class="spinner-border spinner-border-sm"></span> AI đang chấm...';
        const evalRes = await fetch(`/Reading/Translation/Evaluate/${translationId}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': getCsrfToken() }
        });
        const evalData = await evalRes.json();

        if (!evalRes.ok) throw new Error(evalData.error || "AI chấm bài lỗi, vui lòng thử lại sau");

        showInlineAiResult(evalData);
        
        if (evalData.isApproved) {
            btnPublish.classList.remove('d-none');
        }

        const originalIcon = '<i class="bi bi-save me-1"></i> Hoàn thành';
        btnSave.innerHTML = '<i class="bi bi-check me-1"></i> Đã lưu & chấm xong';
        setTimeout(() => {
            btnSave.innerHTML = originalIcon;
            btnSave.disabled = false;
        }, 3000);

    } catch (err) {
        alert(err.message);
        btnSave.disabled = false;
        btnSave.innerHTML = '<i class="bi bi-save me-1"></i> Hoàn thành';
    }
};

window.publishInlineTranslation = async function () {
    const id = document.getElementById('myTranslationId').value;
    if (!id) return;
    const btn = document.getElementById('btnPublishInlineTranslation');
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang xử lý...';

    try {
        const res = await fetch(`/Reading/Translation/Publish/${id}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': getCsrfToken() }
        });
        if (res.ok) {
            alert("Chúc mừng! Bản dịch của bạn đã được công khai.");
            location.reload(); 
        } else {
            const data = await res.json();
            alert(data.error || "Lỗi public");
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-globe me-1"></i> Public bản dịch';
        }
    } catch (err) {
        alert("Lỗi kết nối");
        btn.disabled = false;
    }
};

window.deleteInlineTranslation = async function () {
    const id = document.getElementById('myTranslationId').value;
    if (!id) return;

    if (!confirm("Bạn có chắc chắn muốn xóa bản dịch này?")) return;

    const btn = document.getElementById('btnDeleteInlineTranslation');
    const oldHtml = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

    try {
        const res = await fetch(`/Reading/Translation/Delete/${id}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': getCsrfToken() }
        });
        if (res.ok) {
            alert("Đã xóa bản dịch.");
            location.reload();
        } else {
            const data = await res.json();
            alert(data.error || "Lỗi xóa");
            btn.disabled = false;
            btn.innerHTML = oldHtml;
        }
    } catch (err) {
        alert("Lỗi kết nối");
        btn.disabled = false;
        btn.innerHTML = oldHtml;
    }
};

function showInlineAiResult(data) {
    let container = document.getElementById('inlineAiResult');
    if (!container) {
        const actions = document.getElementById('inlineTranslationActions');
        container = document.createElement('div');
        container.id = 'inlineAiResult';
        container.className = 'mt-3 w-100';
        actions.parentNode.insertBefore(container, actions);
    }

    const isApproved = data.isApproved ?? data.isAiApproved;
    const score = data.score ?? data.aiScore;
    const feedback = data.feedback ?? data.aiFeedback;

    const statusClass = isApproved ? 'approved' : 'failed';
    const icon = isApproved ? 'bi-check-circle-fill text-success' : 'bi-exclamation-triangle-fill text-danger';
    const title = isApproved ? 'Bản dịch đạt yêu cầu!' : 'Cần cải thiện thêm';

    container.innerHTML = `
        <div class="ai-eval-card ${statusClass} mb-3 p-3">
            <div class="d-flex align-items-center gap-3 mb-2">
                <div class="ai-score-circle ${isApproved ? 'text-success' : 'text-danger'}" style="width:50px; height:50px; font-size:1rem;">
                    ${score}
                </div>
                <div>
                    <h6 class="fw-bold mb-1"><i class="bi ${icon} me-1"></i> ${title}</h6>
                    <p class="text-muted small mb-0">Điểm số dựa trên độ chính xác và tính tự nhiên.</p>
                </div>
            </div>
            <div class="bg-white p-2 rounded border small text-secondary">
                ${feedback}
            </div>
        </div>
    `;
    container.classList.remove('d-none');
}

// DOM Content Loaded - Chỉ chạy các logic khởi tạo DOM
document.addEventListener("DOMContentLoaded", function () {
    const popup = document.getElementById("wordPopup");
    const panels = document.querySelectorAll('.question-panel');
    let translateTimeout = null;

    // Khởi tạo câu đầu tiên
    if (panels.length > 0) window.showQuestion(0);

    // --- DỊCH TOÀN BÀI ---
    document.getElementById("toggleTranslation")?.addEventListener("click", async function () {
        const btn = this;
        const isShowing = btn.classList.contains("active");
        const blocks = document.querySelectorAll(".paragraph-block");

        btn.classList.toggle("active");
        btn.innerHTML = isShowing ?
            '<i class="bi bi-translate"></i> Dịch toàn bài' :
            '<i class="bi bi-eye-slash"></i> Ẩn bản dịch';
        for (const block of blocks) {
            const transEl = block.querySelector(".translation");
            if (!transEl) continue;
            if (isShowing) { transEl.classList.add("d-none"); continue; }
            const originalText = block.querySelector(".passage-text").innerText.trim();
            if (originalText.length < 3) { transEl.classList.add("d-none"); continue; }
            transEl.classList.remove("d-none");
            if (transEl.dataset.loaded) continue;
            transEl.innerHTML = '<small class="text-primary mt-2 d-block"><span class="spinner-border spinner-border-sm"></span> Đang dịch...</small>';
            try {
                const sentences = originalText.match(/[^.!?]+[.!?]+/g) || [originalText];
                const translationPromises = sentences.map(async (sentence) => {
                    const clean = sentence.trim();
                    if (clean.length < 2) return "";
                    try {
                        const res = await fetch(`/Reading/Translate?text=${encodeURIComponent(clean)}`);
                        const data = await res.json();
                        return data.translation || "";
                    } catch { return ""; }
                });
                const results = await Promise.all(translationPromises);
                const fullTranslation = results.filter(t => t !== "").join(" ");
                if (fullTranslation.trim().length > 0) {
                    transEl.innerText = fullTranslation;
                    transEl.dataset.loaded = "true";
                } else { transEl.innerText = "Không thể dịch đoạn này."; }
            } catch (err) { transEl.innerText = "Lỗi kết nối."; }
        }
    });

    // --- POPUP DỊCH TỪ ---
    function updatePopupPosition() {
        const selection = window.getSelection();
        if (selection.rangeCount === 0 || popup.classList.contains("d-none")) return;
        const range = selection.getRangeAt(0);
        const rect = range.getBoundingClientRect();
        const popupWidth = popup.offsetWidth;
        popup.style.left = `${rect.left + (rect.width / 2) - (popupWidth / 2)}px`;
        popup.style.top = `${rect.top - popup.offsetHeight - 10}px`;
    }

    document.addEventListener("mouseup", function () {
        clearTimeout(translateTimeout);
        translateTimeout = setTimeout(async () => {
            const selection = window.getSelection();
            const text = selection.toString().trim();
            if (!text || text.length < 2 || text.split(/\s+/).length > 5) {
                popup.classList.add("d-none"); return;
            }
            popup.classList.remove("d-none");
            popup.innerHTML = '<i class="bi bi-hourglass-split"></i>';
            updatePopupPosition();
            try {
                const res = await fetch(`/Reading/Translate?text=${encodeURIComponent(text)}`);
                const data = await res.json();
                popup.innerHTML = `<strong>${text}</strong>${data.translation}`;
                setTimeout(updatePopupPosition, 10);
            } catch { popup.innerHTML = "Lỗi kết nối"; }
        }, 250);
    });

    window.addEventListener("scroll", updatePopupPosition, true);
    document.addEventListener("mousedown", (e) => {
        if (!popup.contains(e.target)) popup.classList.add("d-none");
    });

    // --- NỘP BÀI AJAX ---
    document.getElementById('readingForm')?.addEventListener('submit', async function (e) {
        e.preventDefault();
        const btn = document.getElementById('btnSubmit');
        const formData = new FormData(this);
        btn.disabled = true;
        try {
            const response = await fetch('/Reading/SubmitReading', { method: 'POST', body: formData });
            const result = await response.json();
            if (result.success) {
                document.getElementById('quizStatus').className = "alert alert-success d-block border-0 shadow-sm";
                document.getElementById('quizStatus').innerHTML = `<b>Kết quả: ${result.correctCount}/${result.totalCount}</b>`;
                result.details.forEach((item, index) => {
                    const panel = document.querySelector(`.question-panel[data-q-id="${item.questionId}"]`);
                    const navBtn = document.getElementById(`nav-btn-${index}`);
                    const feedback = panel.querySelector('.result-feedback');
                    feedback.classList.remove('d-none');
                    if (item.isCorrect) {
                        navBtn.classList.add('nav-correct');
                        feedback.innerHTML = `<span class="text-success fw-bold small">✓ Chính xác</span>`;
                    } else {
                        navBtn.classList.add('nav-wrong');
                        feedback.innerHTML = `<span class="text-danger fw-bold small">✘ Sai. Đáp án: ${item.correctOptionText}</span>`;
                    }
                    panel.querySelectorAll('input').forEach(i => i.disabled = true);
                });
                btn.classList.add('d-none');
                window.scrollTo({ top: 0, behavior: 'smooth' });
            }
        } catch (err) { btn.disabled = false; alert("Có lỗi xảy ra khi nộp bài."); }
    });
});