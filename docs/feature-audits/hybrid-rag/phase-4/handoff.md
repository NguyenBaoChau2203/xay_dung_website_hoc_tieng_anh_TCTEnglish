# Phase 4 handoff — câu trả lời bám nguồn và ngân sách lượt chat

## Phạm vi

- Phase 4, lần triển khai local. Prompt `.ai/prompts/hybrid-rag/phase-4-grounded-answers-budget.md`. Plan `docs/superpowers/plans/hybrid-rag/phase-4-grounded-answers-budget.md`.
- Nhánh `feature/hybrid-rag-chatbot`. HEAD đầu và cuối đều `6af768bd32d16cc6d012d484ee82df4cab805c69`. Không commit.
- Trước khi sửa, manifest Phase 3 khớp 89/89, fingerprint `66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`.
- Cây dirty của Phase 0–3 được giữ. Phase 4 thêm composer, pipeline, selector, ngân sách lượt, cột lease trên ledger và migration additive.
- Grant đã có: chỉ đăng ký DI chat trong `Program.cs`. `IAiProviderClient` vẫn là `OpenAiProviderClient`. Không đổi `appsettings.json` hay `.csproj`.
- Không gọi API trả phí, không push, không deploy. Bằng chứng model là fake provider và embedding bị chặn trong test host.
- Manifest: [scope-manifest.json](scope-manifest.json), 109 mục, fingerprint `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`.
- Vòng đời: [lifecycle.md](lifecycle.md).

## Hành vi đã kiểm

Câu chào và câu ngoài phạm vi vẫn trả lời bằng câu cố định của server, không embed. Câu hỏi nguồn một câu được sao đúng chữ nguồn, ví dụ “The bell is red.” Đường dẫn tương đối được phép thì nối thêm; `/Admin` và URL tuyệt đối bị bỏ.

Câu trộn phần không có nguồn chỉ trả phần được nguồn chứng minh và nêu phần chưa trả lời. Follow-up dùng câu user trước, không dùng lời assistant. Nhiều câu cùng khớp thì hỏi lại, không đoán. Id lạ, câu không phủ câu hỏi, hoặc JSON hỏng được lưu bằng câu từ chối cố định, HTTP 200, không lưu văn model.

Hết ngân sách trước khi reserve thì không gọi retrieval và không tốn câu hỏi. Câu chào hoàn tất tốn 1 câu Standard. Hủy trước bước trả phí thì trả lại câu hỏi và không tạo dòng assistant. Embedding đã gửi rồi hủy thì không tạo assistant nhưng settle bound đã biết hoặc chưa biết.

Hai hội thoại chồng nhau: lượt đang EmbeddingKnown, ActualTokens = 0, không bị lượt kia settle. Hết lease 3 phút thì settle đúng số token đã ghi, một lần. Bound generation không rõ 80 được settle khi lease hết; Settle(500) sau đó không đổi 80. Token chưa biết không bị ghi thành 0.

Evidence và citation chỉ sống trong request. Tải lại hội thoại không biến chúng thành bằng chứng. Phase 5 vẫn khóa.

## Sổ kiểm tra

| Kiểm tra | Lệnh | Môi trường | Kết quả |
| --- | --- | --- | --- |
| Manifest Phase 3 trước khi sửa | So SHA-256 từng file rồi fingerprint thứ tự path | Local, không API | 89/89, `66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb` |
| Regression Phase 4 | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Local --filter FullyQualifiedName~AiRagPhase4GroundedTests` | SQLite, fake provider, embedding chặn | 10 passed, 0 failed |
| Chat, quota, cancel, provider | Filter closeout, cancel, quota, OpenAiProviderClient, out-of-scope, ordinary chat | SQLite, fake provider | 53 passed, 0 failed |
| Pending model | `dotnet ef migrations has-pending-model-changes --configuration Phase4Local` sau build | EF 9.0.4, không đụng DB dev | No changes, exit 0 |
| Nâng schema disposable | Cùng các bước `scripts/verify-mariadb.ps1`, nhưng `--configuration Phase4Local` vì `bin/Release` bị `TCTEnglish.exe` pid 19632 khóa | MariaDB 10.5.23, digest `sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730`, container `tct-migration-check-phase4`, DB `tct_migration_check`, port 33317 | 14 migration, checksum user 900001 không đổi, chạy lại không đổi history, 7 cột stage nullable |
| Lease trên MariaDB | `dotnet test ... -c Phase4Local --filter FullyQualifiedName~MariaDbAiTurnLifecycleTests` với `TCT_ACCEPTANCE_RUN_ID=phase4rag` | `tctenglish-mariadb-acceptance-phase4rag`, DB `tct_acceptance_phase4rag`, 127.0.0.1:3336 | 1 passed, 0 skipped |
| Full suite | `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4Local` sau khi gỡ biến acceptance | Local Phase4Local, không API | 1078 passed, 20 skipped, 0 failed, 5 m 3 s |

Script `scripts/verify-mariadb.ps1` gốc không chạy được vì nó gắn `--configuration Release --no-build` và process owner đang khóa output Release. Các bước rỗng DB, nâng từ `20260510020134_AddReadingUserTranslation`, checksum, upgrade, repeat và đếm history đã chạy trên cùng loại container disposable. DB dev `tctenglish-mariadb-local` port 3307 không bị dùng.

20 skip của full suite gồm các fact MariaDB opt-in, trong đó có lifecycle fact. Fact đó đã chạy riêng và passed, không được tính là pass của full suite.

## Rủi ro cho reviewer

- Câu từ chối do id sai được lưu vào dòng assistant để client nhận 200 và không tự gửi lại. Đó là câu server, không phải câu model được chấp nhận.
- Evidence không còn sau reload. Đừng coi đây là citation bền.
- `ActualTokens` phải ở 0 khi lease còn sống. Ghi sớm sẽ làm recovery kiểu cũ settle nhầm lượt.
- Container disposable mới còn chạy: `tct-migration-check-phase4` và `tctenglish-mariadb-acceptance-phase4rag`. Không xóa container dev.
- Chưa có bằng chứng API OpenAI thật. Phase 7 mới là chỗ chấp nhận provider sống.

## Trạng thái

Phase 4 là AWAITING_REVIEW. Phase 5, 6 và 7 vẫn LOCKED. Báo cáo này không phải APPROVED và không phải production.

Đoạn dán cho Codex:

Phase 4 local đang AWAITING_REVIEW. Scope 109 mục, fingerprint 78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5. Phase 3 trước khi sửa khớp 89/89, fingerprint 66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb. HEAD vẫn 6af768bd32d16cc6d012d484ee82df4cab805c69. Nhờ review; đây không phải tự duyệt. Phase 5–7 vẫn LOCKED.
