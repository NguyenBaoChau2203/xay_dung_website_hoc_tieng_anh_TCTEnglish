/**
 * time-tracker.js — TCT English
 * Đo thời gian user ở tại 1 trang học và gửi lên server khi rời trang.
 *
 * Cách dùng: thêm vào view cần track:
 *   <script src="~/js/time-tracker.js" data-feature="Speaking" defer></script>
 *
 * Hoặc gọi thủ công:
 *   TimeTracker.start("Flashcard");
 */
(function () {
    'use strict';

    // Lấy feature từ attribute của script tag
    const scriptEl = document.currentScript
        || document.querySelector('script[data-feature]');
    const featureName = scriptEl?.getAttribute('data-feature') ?? null;

    if (!featureName) return;

    let startTime = Date.now();
    let sent = false;

    // Lấy anti-forgery token từ form ẩn trên trang (Razor @Html.AntiForgeryToken)
    function getAntiforgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    function sendTime() {
        if (sent) return;
        const durationSeconds = Math.round((Date.now() - startTime) / 1000);
        if (durationSeconds < 10) return;   // quá ngắn → bỏ qua

        sent = true;
        const token = getAntiforgeryToken();

        // navigator.sendBeacon đảm bảo gửi ngay cả khi đóng tab
        const payload = JSON.stringify({ feature: featureName, durationSeconds });
        const blob = new Blob([payload], { type: 'application/json' });

        // sendBeacon không cho phép set header → dùng fetch thay thế khi trang còn active
        // Khi visibility=hidden → dùng sendBeacon (không có header nhưng controller dùng [AutoValidateAntiforgeryToken])
        // Giải pháp: gắn token vào query string để server tự validate
        const url = `/api/LearningApi/track-time?__RequestVerificationToken=${encodeURIComponent(token)}`;

        if (navigator.sendBeacon) {
            navigator.sendBeacon(url, blob);
        } else {
            // Fallback: keepalive fetch
            fetch('/api/LearningApi/track-time', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: payload,
                keepalive: true
            }).catch(() => {});
        }
    }

    // Gửi khi user ẩn tab / chuyển tab / đóng trình duyệt
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            sendTime();
        } else {
            // User quay lại trang → reset timer
            if (sent) {
                startTime = Date.now();
                sent = false;
            }
        }
    });

    // Fallback: beforeunload (không đáng tin bằng visibilitychange nhưng hỗ trợ rộng hơn)
    window.addEventListener('pagehide', sendTime);

    // Expose API để gọi thủ công nếu cần
    window.TimeTracker = {
        start: function (feature) {
            featureName = feature;
            startTime = Date.now();
            sent = false;
        },
        stop: sendTime
    };
})();
