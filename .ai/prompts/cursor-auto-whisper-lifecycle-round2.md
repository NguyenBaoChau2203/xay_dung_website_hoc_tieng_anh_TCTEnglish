# Cursor Auto — khép WR01/WR02 của Whisper Plan 1

Một agent duy nhất thực thi/review/verify. Đọc AGENTS.md và:

- docs/feature-audits/2026-11-01-cursor-whisper-lifecycle-rereview.md
- docs/superpowers/plans/2026-11-01-whisper-lifecycle-round2-plan.md

Owner gửi prompt này để giao sửa hai lỗi còn lại tại local. Giữ branch/diff hiện
có, BTH2, DB dev và Whisper owner. Không commit/push/PR/remote CI/merge/deploy,
Plesk/gate, trust hoặc Plan 2/OpenAI. Không làm lại phần WH01/WH04 đã pass.

1. WR01: `_kill_registered` đang bỏ qua start_time. Test stale registry + cùng
   executable phải không phát kill request; kiểm OS path+creation time/handle
   trước cleanup. Test toàn teardown, state bị sửa và assertion fail; bỏ tautology
   `alive or not alive`. Không thử kill PID System/process ngoài fixture.
2. WR02: validator hiện nhận root array, primitive segment, end<start và
   segments-only empty. Giữ JSON type và bám contract C# MapVerboseJson/
   TryReadSegment. Không làm yếu adapter. Table tests có positive/negative và
   loopback HTTP; legitimate silence/segment-only hợp lệ vẫn hoạt động.
3. Chạy checks T3, review diff inline, update bug log và ledger vòng2. Số tests
   phải từ lần chạy mới; app/credit/mic/benchmark chưa thử tiếp tục ghi Not run.

Áp dụng tct-superpowers/tct-engineering/tct-tooling-review và CodeGraph theo active
step; một agent, không tự reindex. TDD cho regression mới. Báo tiếng Việt source
fix/checks/limits và dừng để Codex rereview; không tự triển khai OpenAI.
