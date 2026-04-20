/**
 * READING STUDY JAVASCRIPT
 * Chức năng: Điều hướng câu hỏi, Dịch từng câu (Anti-Limit), Popup dịch từ bám theo scroll.
 */

document.addEventListener("DOMContentLoaded", function () {
    // --- KHAI BÁO BIẾN ---
    const popup = document.getElementById("wordPopup");
    const panels = document.querySelectorAll('.question-panel');
    const totalQuestions = panels.length;
    let currentIdx = 0;
    let translateTimeout = null;

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
            '<i class="bi bi-translate"></i> Dịch toàn bài' :
            '<i class="bi bi-eye-slash"></i> Ẩn bản dịch';
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


    // --- 3. POPUP DỊCH TỪ KHI BÔI ĐEN (FIXED & SCROLL ADAPTIVE) ---
    function updatePopupPosition() {
        const selection = window.getSelection();
        if (selection.rangeCount === 0 || popup.classList.contains("d-none")) return;

        const range = selection.getRangeAt(0);
        const rect = range.getBoundingClientRect();

        // Tính toán vị trí dựa trên Viewport (vì popup là position: fixed)
        // rect.top là vị trí so với mép trên màn hình
        const popupWidth = popup.offsetWidth;
        popup.style.left = `${rect.left + (rect.width / 2) - (popupWidth / 2)}px`;
        popup.style.top = `${rect.top - popup.offsetHeight - 10}px`; // Nằm trên từ 10px
    }

    document.addEventListener("mouseup", function () {
        clearTimeout(translateTimeout);
        translateTimeout = setTimeout(async () => {
            const selection = window.getSelection();
            const text = selection.toString().trim();

            // Chỉ dịch từ đơn hoặc cụm từ ngắn (dưới 5 từ)
            if (!text || text.length < 2 || text.split(/\s+/).length > 5) {
                popup.classList.add("d-none");
                return;
            }

            popup.classList.remove("d-none");
            popup.innerHTML = '<i class="bi bi-hourglass-split"></i>';
            updatePopupPosition();

            try {
                const res = await fetch(`/Reading/Translate?text=${encodeURIComponent(text)}`);
                const data = await res.json();
                popup.innerHTML = `<strong>${text}</strong>${data.translation}`;
                // Cập nhật lại vị trí sau khi nội dung thay đổi kích thước popup
                setTimeout(updatePopupPosition, 10);
            } catch {
                popup.innerHTML = "Lỗi kết nối";
            }
        }, 250);
    });

    // Lắng nghe sự kiện cuộn ở mọi nơi để cập nhật vị trí popup
    window.addEventListener("scroll", updatePopupPosition, true);

    document.addEventListener("mousedown", (e) => {
        if (!popup.contains(e.target)) popup.classList.add("d-none");
    });


    // --- 4. XỬ LÝ NỘP BÀI AJAX ---
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
        } catch (err) {
            btn.disabled = false;
            alert("Có lỗi xảy ra khi nộp bài.");
        }
    });
});