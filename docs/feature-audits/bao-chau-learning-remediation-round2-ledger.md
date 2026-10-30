# Ledger vòng 2 — Learning Reliability

Ngày: 2026-10-30. Nhánh `feature/bao-chau-ui-audit`, HEAD `3d27a68c8468207eca5a11653c72e735117fc637`. Working tree trước phiên được giữ nguyên; baseline hash nằm ở `.tmp/learning-remediation-round2/baseline-sha256.txt` (888 file tracked). Không commit, không push, không apply database thật, không cài engine.

CodeGraph lúc bắt đầu còn stale (68 added / 45 modified) và không được re-index. Các symbol mới được đọc trực tiếp từ source.

## Migration

Additive, chưa apply.

- Bảng `SpeakingImportJobs`, cột nullable `SourceContentHash` varchar(64).
- Row cũ giữ `NULL`. `SourceKind` null vẫn là import caption cũ.
- Không DROP dữ liệu. Unique `(OwnerUserId, RequestFingerprint)` được giữ.
- File: `TCTEnglish/Migrations/20261030113000_LearningImportSourceHash.cs`.
- Preflight duplicate trên database thật: **Not run**.

## Từng phát hiện

| Mục | Trạng thái | Kiểm chứng | Còn lại |
| --- | --- | --- | --- |
| RR01 import retry | Implemented, fixture verified | `SpeakingImportJobLifecycleTests`: failed/cancelled dùng lại cùng PublicId; hai enqueue song song một job; user khác không đọc được; video đã xóa được import lại | MySQL chưa chạy |
| RR02 / F3 notification lease | Implemented, fixture verified | 201 và 450 user, một `BadgeNear` có sẵn, cuối cùng đủ và không trùng; lỗi DB không phải unique không nhảy cursor; lease hết hạn không nhân đôi; heartbeat gia hạn sau 2 phút giả lập | Nhiều instance MySQL: Not run. SQLite không phải bằng chứng cluster |
| RR03 PCM/HTTP | Implemented, fixture verified | Node: đổi câu/dispose dừng track; mic denied không unhandled rejection; auto-stop có WAV >= 44 byte và một callback | Browser/mic thật: Not run |
| RR04 / F5 UI job và phụ đề | Implemented, fixture verified | Form gọi `/api/speaking/imports` và poll; file tùy chọn gọi `/api/speaking/imports/subtitles`; worker publish video + câu + job trong một transaction; Premium bị thu hồi trước publish không tạo video; Gemini counter = 0 | YouTube/mạng thật: Not run. ASR-for-import: Deferred |
| RR05 deadline ASR | Implemented, fixture verified | Hàng đợi timeout trong ngân sách 1 giây khi slot bị giữ; request thứ sáu busy và không gọi HTTP; hủy khi đang xếp hàng trả slot; engine chưa cấu hình = 0 HTTP | whisper.cpp thật: Not run. Cap là theo process, không phải cluster |
| RR06 transition | Implemented, fixture verified | 4→5 với longest 5 không có StreakRecord; 5→6 có một event; học lại trong ngày và reset không tạo record; badge cũ trong cửa sổ 1 phút không được báo; goal Speaking 1/1 có GoalCompleted và drain giữ thời điểm occurrence | MySQL: Not run |
| RR07 / F6 / F7 | Implemented, fixture verified | LanguageTool thiếu `matches` là `invalid_schema`; Ollama chỉ nhận JSON allowlist; cloud chậm + grammar/model chung một ngân sách khoảng 3 giây; endpoint sai nghĩa không qua vì grammar sạch | LanguageTool, Ollama và corpus 40 câu: Not run |
| F8 test gate / SQLite | Implemented, fixture verified | Conflict test chờ provider vào xử lý, `finally` luôn nhả gate; 5 lần riêng đều pass. Fixture dùng shared-cache, bỏ `EnsureDeleted`. Hai ca từng flake (`WritingProgress_ListAndJson...`, `GetGoalsAsync_FillsMissingWeeklyDays...`) pass | Không sửa production AI Chat |
| F9 full suite | Fixture verified | Lần cuối **614 passed, 0 failed, exit 0**, blame báo mọi test đã chạy xong. TRX: `.tmp/learning-remediation-round2/full-final/full.trx` | Các cổng môi trường bên dưới vẫn Not run |

Lần full trước đó đã chạy xong, không abort: 613 pass / 1 fail vì `SanitizeFeedback` nuốt xuống dòng của review Writing. Sau khi giữ newline, suite cuối xanh. Không lấy số pass trước fail làm nghiệm thu.

## Lệnh đã chạy

```text
node --test scripts/tests/test_notification_behavior.cjs scripts/tests/test_speaking_behavior.cjs scripts/tests/test_speech_recorder.cjs scripts/tests/test_speaking_import_behavior.cjs
```

Kết quả: 17/17 pass.

```text
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --blame-hang --blame-hang-timeout 60s --blame-hang-dump-type none --logger "trx;LogFileName=full.trx" --results-directory .tmp/learning-remediation-round2/full-final
```

Kết quả: Passed, 614/614, exit 0.

`git diff --check`: exit 0, chỉ còn cảnh báo LF/CRLF của file đã có trước phiên.

## Không chạy

- Apply migration / MySQL concurrency.
- Browser và microphone thật.
- whisper.cpp, LanguageTool, Ollama, corpus chất lượng.
- ASR dùng để tạo bài Speaking. Sinh bài Writing song ngữ vẫn đi cloud.
