# Cursor Auto — sửa Whisper sau review WH01–WH04

> **SUPERSEDED — 04/11/2026. Chỉ lưu lịch sử, không thực thi prompt này.**
> Local Whisper đã được thay bằng OpenAI Speech-to-Text. Xem AGENTS.md và
> `docs/operations/local-speech.md`; không phục hồi engine hoặc auto-start cũ.

Một agent implement/review/verify inline. Đọc AGENTS.md, rồi:

- Review: docs/feature-audits/2026-11-01-cursor-whisper-lifecycle-review.md
- Plan: docs/superpowers/plans/2026-11-01-whisper-lifecycle-remediation-plan.md
- Ledger hiện tại: docs/feature-audits/2026-11-01-whisper-local-lifecycle-ledger.md

Khi owner gửi prompt này, scope là sửa implementation Plan 1 local. Không bắt
đầu OpenAI/Plan 2, không commit/push/PR/merge/remote CI/deploy/Plesk/gate. Bảo toàn
current dirty work, DB dev, file BTH2, process Whisper owner và các ứng dụng khác.
Không reset/stash/checkout branch khác để dọn tree.

## Làm theo thứ tự

1. R0/WH02 trước: teardown đang taskkill PID đọc từ state, negative test ghi PID4.
   Sửa fixture cleanup và negative tests an toàn trước khi chạy full discover.
   Registry/handle tạo process độc lập với state đang bị test; không kill System.
2. R1/WH01: OS path mismatch/missing phải fail closed; state.commandLine không
   cấp quyền stop. Expected runtime độc lập với state, handle/start time đúng
   trước mọi kill, gồm failed-start cleanup. Thêm regression sai executable.
3. R2/WH04: pin/hash CPU backend DLL từ ZIP đúng lock, verify cache reuse/prestart.
   DLL hỏng/thừa không vượt gate; giữ b5454/tiny.en, không tải latest để né fix.
4. R3/WH03: probe kiểm body inference, không chỉ HTTP200. Cùng validator cho
   Start/reuse/Status/Benchmark, silence hợp lệ vẫn ready; error/HTML/JSON sai
   không ready. Test loopback HTTP thật với fake server modes và cleanup đúng.
5. R4: metric PeakWS phải đúng hoặc đổi tên; log startup phải có bằng chứng thật.
   Kiểm launcher behavior/env, live app speech→credit/replay, queue/overflow và
   speech benchmark. Mic/browser chưa chạy phải ghi Not run.
6. R5: build/tests/tooling checks, review diff inline, bug log sau fix và cập nhật
   ledger thực tế. Không lặp kết quả cũ hoặc coi silence là nghiệm thu toàn bộ.

Tct-superpowers/tct-engineering/tct-tooling-review + speaking skill theo active
step; CodeGraph trước indexed source, không tự reindex. Chỉ đọc lịch sử bug liên
quan. Test đỏ→xanh có ý nghĩa; không xóa test hoặc làm yếu ownership để pass.

Báo tiếng Việt: file/sửa lỗi/bằng chứng4 repro, checks và skips, URL/status/stop,
process còn chạy cho owner, schema impact, remaining gates. Dừng để Codex review
lại, không tự chuyển provider hay publish Git.
