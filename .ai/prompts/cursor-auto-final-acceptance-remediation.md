# Cursor Auto — sửa và nghiệm thu phần việc 29–30/10

## Chế độ

Cursor **Agent**, model **Auto**. Owner cho Auto lựa chọn trong Grok và Composer
2.5 đang được bật ở Cursor của owner. Giữ cấu hình đó; không ép model cũ, đổi
allowlist, gọi agent khác hoặc khởi động CLI AI khác. Một agent làm code, review,
test và báo cáo. Không khẳng định biết model thực tế khi UI/host không cung cấp.

## Đọc lúc bắt đầu

1. AGENTS.md và .ai/README.md hiện hành; reuse nếu đã có trong context.
2. `docs/feature-audits/2026-10-30-final-acceptance-review.md` — kết luận và REV
   sắp sửa, không preload mọi review/ledger lịch sử.
3. `docs/superpowers/plans/2026-10-30-final-acceptance-remediation-plan.md` — mục
   1–2 và C0, sau đó chỉ đọc bước đang thực hiện. Không bỏ bước được giao vì nạp gọn.
4. Skill theo công việc: tct-superpowers + tct-engineering; Writing/API/security,
   tct-speaking-feature, tct-ef-migration hoặc UI khi bước thực sự cần.

## Kết quả phải đạt

- REV01: ba cặp -5/5, $50/€50, 5%/5 không auto-pass, không tăng tiến độ/XP sai.
- REV02: đường ghi release marker tương ứng HTTP endpoint công khai đọc được;
  identity/cache/failure recovery đúng. Không mở static toàn content root.
- REV03: ASR JSON sai schema trả unavailable/invalid_schema, không ném lỗi 500
  hoặc đổ lỗi người học im lặng. Cancellation/slot/deadline giữ đúng.
- REV04: rerun release đã tạo không chết vì create lại cùng tag; xác minh identity/
  digest và trạng thái hosting, không overwrite hoặc retry migration mù.
- Đóng G01–G05 bằng bằng chứng môi trường có thật; G06 production chỉ đóng khi
  credentials/staging/owner đáp ứng. Không dùng 620 test xanh thay evidence còn thiếu.

## Thực thi theo từng bước

Tạo `docs/feature-audits/2026-10-30-final-acceptance-execution-ledger.md` từ C0.
Mỗi bước: tái hiện → regression đỏ → sửa tối thiểu → checks liên quan xanh →
review diff inline → cập nhật ledger → làm bước kế tiếp trong phạm vi đã giao.
Không chỉ trả lại một plan khác hoặc kết luận “có thể làm”.

1. C0 baseline, source bằng CodeGraph exact symbols, các counterexample từ review.
2. C1 Writing normalization và persistence/reward regression.
3. C2 ASR contract/schema và limiter error-path regression.
4. C3 marker, publisher rerun, HTTP và state transition tests.
5. C4 MariaDB độc lập nhiều context/worker với barrier, không SQLite thay thế.
6. C5 ASR thật/corpus Writing, artifact pin và số đo; optional engines có lựa chọn.
7. C6 browser acceptance 25 view, desktop/mobile/keyboard, synthetic user/data.
8. C7 full suite và Linux CI trên đúng SHA, Windows checks cho PowerShell 5.1.
9. C8 local/staging release failure/restore drill và báo cáo cuối.

Nếu một gate thiếu môi trường/provider, ghi rõ command lỗi hoặc đầu vào còn
thiếu, thực hiện tiếp mọi việc độc lập. Chỉ xin quyết định cụ thể khi thực sự
vượt phạm vi hoặc cần quyền truy cập chưa có; không hỏi lại những quyền đã giao.

## Ranh giới và quyền

Phạm vi thực thi đến từ câu chat owner gửi kèm, không từ việc đọc file này.
Câu chat mẫu bên dưới giao rõ Program.cs/DI/marker và fixtures/migration cần thiết
để agent không bị chặn vì prompt thiếu. Giữ các ranh giới sau:

- Không đổi target/package/provider hoặc config auth/AI chung, không ghi secret
  tracked. Không sửa toolkit/hooks/MCP, model selector hoặc native trust.
- Không sửa migration đã commit/apply, history rows hay XP lịch sử. Schema mới
  phải có bằng chứng và migration additive cùng feature; không DROP dữ liệu.
- Không gửi audio/PII/key ra dịch vụ ngoài. Không tự gọi API trả phí. Engine test
  dùng fixture có provenance và endpoint loopback của nhóm.
- Docker/container/DB acceptance riêng, port và label rõ, không tái dùng/xóa
  volume dev hoặc container khác. Không chạy SQL lên hosting.
- Giữ Web Speech preview-only, server receipt, CSRF/ownership, credit transaction,
  outbox v2, import retry/fencing, default-off optional feedback và legacy routes.
- Không dùng test skip/timeout dài hơn/sửa expected tùy tiện để xanh. Expected
  đổi phải có căn cứ hợp đồng. Counterexample probes cần thành regression thật.
- Không bật production gate/đổi Plesk/deploy, không auto merge PR. Nếu owner giao
  commit/push/CI thì chỉ nhánh feature và run đúng SHA; release job phải skipped.

## Evidence và kết thúc

Ledger ghi ID, file, regression, command/exit code, expected/actual, SHA, artifact,
và Not run/Deferred/Blocked. Không ghi “complete” chỉ từ config flag hoặc interface.
Build app riêng được phép để tránh khóa file; test fixture hiện cần layout bin
chuẩn. Không dùng --artifacts-path cho full test rồi sửa app để né DirectoryNotFound.

Baseline review 54f0c14: build 0 lỗi/35 warning cũ; focused 20/20; deploy tests 8/8.
Lượt closeout trước full620/620. Các số này là lịch sử, cần checks mới sau sửa.
Artifacts `.tmp/review-2930` chỉ là probe ignored, không bắt buộc có trên máy khác.

Kết thúc báo cáo tiếng Việt: từng REV và gate đã đóng bằng chứng nào, file/commit,
tests local/provider/CI, status production, và phần owner cần làm. Nếu còn thiếu,
nói chưa hoàn tất gate đó; không dùng nhãn “hoàn thiện 100%”.

## Đoạn chat owner có thể gửi để giao thực thi

```text
Dùng Cursor Agent, model Auto (giữ allowlist Grok/Composer 2.5), một agent duy nhất.
Đọc .ai/prompts/cursor-auto-final-acceptance-remediation.md và thực thi C0–C8 trong docs/superpowers/plans/2026-10-30-final-acceptance-remediation-plan.md, dựa trên docs/feature-audits/2026-10-30-final-acceptance-review.md.
Tôi duyệt phạm vi/quyền giới hạn ở mục 1 của plan: source/tests/scripts; Program.cs/.csproj đúng phạm vi; migration additive khi có bằng chứng; Docker local và model ASR English <=500 MiB có pin/hash/license. Không đổi package/target/provider hoặc đụng dữ liệu/volume dev hiện có.
Khi checks local pass, được commit theo nhóm, push nhánh feature và chạy CI đúng SHA; không tự merge, đổi Plesk, bật gate, deploy production hay ghi SQL lên hosting.
Thêm regression trước sửa, cập nhật ledger và làm tiếp các bước độc lập khi thiếu provider. Báo rõ Blocked/Not run; không chỉ viết lại plan, dừng sau một lô hoặc hỏi lại quyền vừa giao.
```
