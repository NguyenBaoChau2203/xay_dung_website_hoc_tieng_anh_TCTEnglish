# Ledger sửa sau review — Learning Reliability (F0–F9)

Ngày: 2026-10-30 (UTC+7). Nhánh `feature/bao-chau-ui-audit`, HEAD `3d27a68c8468207eca5a11653c72e735117fc637`. Không commit/push. Không apply migration. Không cài engine.

Review gốc giữ nguyên: `docs/feature-audits/2026-10-30-cursor-learning-implementation-review.md`.

## Ma trận R01–R08

| ID | Trạng thái code/test | Chưa đóng |
| --- | --- | --- |
| R01 | Transaction bao progress/completion/credit trên Speaking và Writing. Lỗi insert credit không còn nuốt thành success. Test `Progress_CreditInsertFailure_RollsBackCompletion` (SQLite trigger) kỳ vọng HTTP 500 và video chưa completed. | Chưa concurrency hai DbContext; chưa MySQL |
| R02 | Recorder finish-once, dispose tăng generation, auto-stop gọi cùng `stop()`. Node: auto-stop không trả blob khác lần stop thứ hai | Mic/browser thật **Not run**. Test lifecycle chưa phủ đủ late-permission sâu |
| R03 | Streak/badge worker lặp keyset `UserId > cursor`, batch 200, không dừng cả ngày sau 200 user đầu | Lease claim conditional/heartbeat/fencing **chưa** đủ như plan; chưa chứng minh hai process |
| R04 | `SpeechInferenceLimiter` singleton; hàng đợi chờ thật; busy vẫn map 429 khi assessment nhận `engine_busy` | Chưa test nhiều DI scope đồng thời; chưa load ASR thật. Giới hạn **theo process** |
| R05 | Job retry thấy video owner+youtube đã tạo thì đánh succeeded. Unique `(OwnerUserId, RequestFingerprint)` trong migration review | UI vẫn POST `/Speaking/My/Create`. Chưa multipart phụ đề trên form. Chưa claim atomic hai worker. ASR-import **Deferred** |
| R06 | Outbox payload v2, không đoán `GoalCompleted` khi không có goal. `OccurredAtUtc` riêng businessDate. Legacy payload → needs-review, không đóng thành công giả | Chưa test drain qua nửa đêm trên MySQL |
| R07 | `WritingService` gọi grammar/LLM trong ngân sách 3 giây; default off không HTTP; không đổi `passed` | Engine thật/corpus 40 câu **Not run**. Sinh bài song ngữ vẫn cloud |
| R08 | Timeout nội bộ → unavailable `timeout`. Caller cancel → `OperationCanceledException`, không gọi HTTP nếu token đã hủy | Không có LanguageTool/Ollama thật |

## Migration review

`20261030012638_LearningReviewRemediation` — additive columns (Phase, LastUserId, LeaseToken, OccurredAtUtc, NotificationEventKey, subtitle/job fields). Thay index fingerprint thành **unique** và widen `PayloadJson` 2000→4000. **Không apply.** Trước apply phải đếm duplicate `SpeakingImportJobs (OwnerUserId, RequestFingerprint)`; không tự xóa row. Down chỉ gỡ cột/index mới.

Hai migration cũ không sửa: `20261029175518_LearningNotificationReliability`, `20261029181904_LearningReliabilityL4ThroughL6`.

## Kiểm thử đã chạy

- Focused filter Speaking/Writing/Notification/Goals/Completion/OptionalEngine sau sửa: **146 passed / 0 failed** (lần trước cùng hướng 2 fail rồi sửa assertion/cancel).
- Node speaking/notification/recorder: **13 passed / 0 failed**.
- Full suite và ca AI `Send_WhenConversationAlreadyProcessing_ReturnsConflict`: **chưa chạy lại đến Completed** trong đợt này. Review trước đã abort sau 555 pass. Không kết luận full suite xanh.
- Browser/mic/MySQL/whisper/LanguageTool/Ollama: **Not run**.

## Không được hiểu là xong

Có transaction và limiter không có nghĩa production-ready. ASR, apply DB, UI job/poll, lease đa instance, corpus L7 vẫn mở.
