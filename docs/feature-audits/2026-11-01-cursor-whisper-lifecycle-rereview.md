# Rereview Whisper Plan 1 — sau sửa WH01–WH04

Ngày01/11/2026 UTC+7. Owner yêu cầu review lại; một agent kiểm source và chạy
verification trực tiếp. Nhánh `codex/whisper-local-lifecycle`, HEAD `03c9c5c`;
diff local chưa commit. Không sửa implementation, không publish/deploy/Plan 2.

Kết luận: **hai lỗi P2 còn tái hiện được**. Bản sửa đã cải thiện rõ và các suite
hiện có xanh, nhưng chưa phủ hai negative cases bên dưới.

## WR01 — P2: cleanup registry bỏ qua start time đã ghi nhận

File `scripts/tests/test_local_speech.py:161–189`. `_register_fixture` lưu
start_time, nhưng `_kill_registered` chỉ đọc pid/path, kiểm process còn sống và
path bằng executable Python rồi taskkill. Timestamp không được so sánh. Nếu PID
fixture đã kết thúc và được tái sử dụng bởi Python khác, process ngoài fixture
vẫn vượt gate. Khắc phục state.pid/System trước đây đã có, nhưng identity của
registry vẫn chưa đủ để cleanup an toàn.

Repro an toàn: entry chứa PID/path của Python harness hiện tại nhưng start_time
giả `2000-01-01T00:00:00.0000000+07:00`. Mock chặn taskkill, các truy vấn OS path
vẫn chạy thật. Helper vẫn phát yêu cầu taskkill cho PID đó. Không process bị kill.
Log: `.tmp/whisper-plan1-rereview/registry-repro.log`.

Sửa: đối chiếu OS path và creation time/handle với registry độc lập trước kill;
thiếu/mismatch phải từ chối. Thêm test stale timestamp + cùng executable, không
chỉ khác path hoặc state hash. Test teardown hiện chỉ loop registry, không gọi
toàn teardown, và có assertion luôn đúng `alive or not alive` ở491; thay bằng
quan sát không kill victim/no orphan có ý nghĩa.

## WR02 — P2: inference validator chưa khớp contract adapter

File `scripts/local-speech.ps1:470–522`. Đã từ chối `error` và malformed JSON,
nhưng vẫn nhận các payload sau trong fixture hàm thực:

| Payload | Helper hiện tại | Adapter ứng dụng |
| --- | --- | --- |
| `{"segments":[]}` | Accepted | invalid_schema: thiếu text rõ ràng và không có segment |
| `{"text":"hello","segments":[42]}` | Accepted | invalid_schema: segment không là object |
| `{"text":"hello","segments":[{"start":3,"end":1,"text":"hello"}]}` | Accepted | invalid_schema: end < start |
| `[{"text":""}]` | Accepted | invalid_schema: root không là object |

Đối chiếu `WhisperHttpTranscriber.MapVerboseJson/TryReadSegment`: root object,
segment object, text/timestamp đúng kiểu và số hữu hạn, end>=start. ConvertFrom-Json
qua pipeline làm mất phân biệt root array một phần tử; segment check dùng
`$item.PSObject` cũng không loại primitive như mong muốn. Nội dung segment và
quy tắc silence không được kiểm tra đầy đủ. Start/reuse/Status/Benchmark đều dùng
validator này, vì vậy có thể báo ready trong khi app từ chối cùng payload.

Log `.tmp/whisper-plan1-rereview/probe-schema.log`. Các hàm được load từ AST source
hiện tại, không sửa source và không inject response vào engine của owner. Payload
error cũ đã bị từ chối; silence hợp lệ `{"text":"","segments":[]}` vẫn được nhận.

Sửa validator trên JSON DOM không làm mất root type, theo contract hiện hành của
adapter; table-driven positive/negative tests và ít nhất một loopback HTTP test.
Không nới adapter để cho helper pass hoặc reject mọi payload segment-only hợp lệ.

## Trạng thái phát hiện trước

| ID | Kết quả rereview |
| --- | --- |
| WH01 | Ca sai executable đã hết: repro cũ trả False; missing/mismatched OS path fail closed |
| WH02 | Đã bỏ kill theo state.pid, bỏ PID4/System; còn WR01 về timestamp registry |
| WH03 |200/error JSON bị chặn; còn WR02 về schema/segment/silence |
| WH04 | Có9 CPU backend hashes, thiếu/sai/thừa bị check; tamper regression pass |
| Metric/log | Có PeakWorkingSet64 và sample riêng; docs làm rõ WMI logs chỉ là marker |

## Reviewer verification mới

| Check | Kết quả |
| --- | --- |
| Python full scripts discover |83 pass,0 fail;105.028s; gồm11 lifecycle tests |
| Node setup |9 pass,0 fail |
| Tooling validator |Pass |
| Release build + focused Speaking/Whisper/limiter/live tests |33 pass,0 fail,0 skip; live silence thật trên owner8092 |
| Artifact Verify |Pass exe/model và DLL pins |
| Owner Whisper Status |ready, PID24532, loopback8092; process được giữ nguyên |
| Repro WH01 |Sai executable bị từ chối |
| Repro WR01 |Timestamp sai vẫn có taskkill request, đã chặn bằng mock |
| Repro WR02 |Bốn payload sai vẫn Accepted |
| git diff --check |Pass; cảnh báo line-ending chuyển LF/CRLF |

Full app suite742/3skip là số Cursor ghi trong ledger, không phải full suite mà
reviewer vừa chạy. Reviewer build và chạy33 test liên quan; app C# không đổi trong
đợt sửa scripts. Không thay đổi engine/cache thật/DB dev hoặc native client trust.

## Acceptance còn chưa chạy

Ledger vẫn ghi Not run: app speech→assessment→progress→credit/reload, overflow,
mic/browser và benchmark lời nói5/10/15s dưới tải web+DB+IDE. Đây không phải test
fail, nhưng không được gọi toàn Plan 1 đã nghiệm thu đủ. Hai lỗi source/test ở
trên cần sửa riêng; silence pass không đóng được các gate còn thiếu.

## Handoff

- [Plan sửa vòng2](../superpowers/plans/2026-11-01-whisper-lifecycle-round2-plan.md).
- [Prompt Cursor Auto](../../.ai/prompts/cursor-auto-whisper-lifecycle-round2.md).
- Chưa cấp quyền sửa implementation trong lượt review. Owner gửi prompt sửa sẽ
  giao scope local; không cần làm lại các phần đã có bằng chứng pass.
