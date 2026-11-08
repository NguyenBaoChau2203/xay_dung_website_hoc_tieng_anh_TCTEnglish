# Phase 1 — Codex review vòng 6: APPROVED

Ngày 2026-11-06. **APPROVED cho baseline local của Phase 1. Phase 2 READY;
Phase 3–7 LOCKED.** Không còn actionable finding trong phạm vi Phase 1.

Owner đã chuyển từ review-only sang yêu cầu hoàn thiện trực tiếp trong lượt này:
“Còn gì chưa ổn hãy hoàn thiện luôn cho tôi để tôi qua phase 2”. Codex thực hiện
inline theo owner authority, không giao thêm vòng sửa cho Cursor, rồi kiểm tra
lại mã nguồn, tests, probe và snapshot bằng cùng một agent.

## Identity và phạm vi đã duyệt

- Nhánh thực tế `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`; chưa commit.
- Cursor trả về 46 entry fingerprint
  `2f8ca1d0d125a456703985a5f8606242f53548de4704218f4a9557e378f2d16d`, tất cả
  khớp trước sửa; archived ở `scope-manifest-fix-round-5.json`.
- **Snapshot đã duyệt: 48 entry**, fingerprint
  **`764d370dc1b2482cd1b78954a8959dfd4706b16b5156ed00659a9301b729e7a9`**.
  Theo `scope-manifest.json`, path/status/raw-content-hash sắp ordinal, TAB/LF thật,
  một LF cuối, SHA-256 UTF-8. Manifest và result/state không tự nằm trong digest.
- 42 entry Cursor giữ nguyên; bốn entry sửa là boundary, policy, regression tests
  và bug log; hai entry mới là transient lookup request và executed closeout plan.
- 12 digest predecessor Phase 0 còn hiệu lực giữ nguyên; fixture canonical hash
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f` giữ nguyên.
- Grant Program DI hẹp được giữ. Codex closeout không sửa Program.cs, project,
  appsettings, persisted model/schema/shared content. Không có paid API, secret
  write, reset DB, remote Git/CI/release hoặc triển khai Phase 2.

## Finding được xác minh đóng

| ID | Kết quả hiện tại | Bằng chứng |
| --- | --- | --- |
| HR-P1-R1-F01 | **CLOSED** | Tất cả ca weather/gold/France/Japan/Kenya/Unit12/Lesson4/polite/cause không được full từ definition không đủ; complete target và requested field được kiểm trước khi chọn answer |
| HR-P1-R1-F02 | CLOSED, giữ hồi quy | Exact/contextual forecast trước hơn 100 broad hits và ca sau 500 unrelated cards pass |
| HR-P1-R1-F03 | CLOSED, giữ hồi quy | go/up, help wording, control và stopword pass |
| HR-P1-R1-F04 | CLOSED, giữ hồi quy | Full/quoted/Unicode/framing-word set names, prefix không tồn tại, 30 duplicate, ambiguity/comparison và stable card keys pass |
| HR-P1-R3-F05 | CLOSED, giữ hồi quy | Bare weather không bị tên bộ Weather ép sang cloud; source-qualified missing target không mượn Nature; cloud lookup đúng nguồn vẫn pass |

## F01: reproduction, repair và retest

Lúc nhận snapshot, `Explain why France has seasons` và `Giải thích vì sao France
có các mùa` vẫn Answered/full từ card France chỉ chứa `a European country`.
Forecast accuracy changes cũng trả definition `a prediction`. Đây là lỗi keyword
explain/meaning được coi là kind hợp lệ nhưng không có complete target/evidence.

Đã viết hồi quy trước triển khai: nhóm 13 ca có **11 fail/2 pass**, gồm cause,
partial phrase, evidence lẫn water/water cycle, thiếu phiên âm và control Weather/
cloud không đúng loại yêu cầu. Không thay fixture Phase 0 để làm test xanh.

Repair được review trên source hiện tại:

- `StoredCardLookupRequest` parse frame có điểm đầu/cuối thành kind/complete target;
  cùng các frame hợp lệ cũng có thể chứa payload dài, nhưng chỉ nguồn khớp toàn bộ
  payload mới được dùng. Không blacklist why/vì sao để vá từng reproduction.
- `AiChatAnswerService` lọc candidate theo complete normalized title và field cần
  trả; thiếu nguồn/field trả non-full với evidence rỗng. Lookup evidence chỉ chứa
  card đã chọn, không gắn guide không dùng làm bằng chứng.
- `ChatExternalFactPolicy` bỏ các boolean/attribute heuristic thay thế. Source ACL,
  query limits, stable identity và global provider không thay đổi.
- Oracle Weather/cloud được sửa đúng semantic contract, vẫn kiểm được source
  filter; có positive cloud-in-Weather và weather-in-Nature để giữ lookup hợp lệ.

Sau sửa: nguyên nhóm âm/dương và cả boundary suite xanh. Probe độc lập trên binary
Phase1FixRound6 xác nhận 10 scenario unsupported đều non-full/evidence rỗng; các
positive definition/source controls giữ đúng kết quả. Không chỉ sửa nhãn outcome
trên text trả sai.

## Ma trận Phase 1 và kiểm tra cuối

| Acceptance | Evidence local |
| --- | --- |
| Boundary chat riêng, scoped DI; không thay global provider | Source/DI diff; HTTP/SignalR consumer tests và non-chat provider tests pass |
| Không lộ private Speaking/card/class/count của user khác | AiRag boundary/access và baseline regressions pass; không gửi nội dung sang paid provider |
| Premium downgrade/class revocation | Speaking locked/private và membership removal regressions pass |
| Card ranking/cap/source identity | Exact >100, >500, short terms, qualifiers, ambiguity/comparison và complete target tests pass |
| Billing/notification minimization, business date | Current-user payload/count và accessible catalog/business-date regressions pass |
| Password help/credential refusal | Real boundary help và extraction refusal regressions pass |
| Quota, Stop/cancel, ownership/history, non-chat transport | Main lifecycle filter và extra provider/ML/probe filter pass |

Final checks:

- Boundary suite: **36 pass, 0 fail/skip**; build app/tests thành công.
- Main Phase 1 filter: **215 pass, 0 fail/skip**, khoảng 17 s.
- Extra provider/ML/review-probe filter: **65 pass, 0 fail/skip**, khoảng 10 s.
- Independent SQLite production-boundary probe: **30 quan sát**, gồm các ca trước
  sửa và các control; assertions cho 10 unsupported scenario về scope/evidence pass.
- Tooling validator exit 0. Python suite cuối **135 tests, 134 pass/1 skip**,
  khoảng 71 s. Skip là local-media non-Windows rejection contract trên Windows.
- Pomelo production query-builder translation test vẫn pass; server target MariaDB
  10.5.23, không mở kết nối hoặc thực thi SQL. Query builders không đổi trong closeout.
- Manifest, predecessor, frozen fixture, complete diff và whitespace đã kiểm.

Không cộng các filter .NET thành một số test duy nhất. Build có warning nullable/
analyzer hiện hữu, không có build error. Tooling không thay app acceptance.
Evidence ignored dưới `.tmp/hybrid-rag-review/phase-1/round-6/` có red/green TRX,
probe trước sửa và Probe-Fixed/behavior-probes-fixed sau sửa. Không dùng binary
Round6 cũ thay cho binary FixRound6 trong acceptance sau sửa.

## Giới hạn và gate

Approval này xác nhận boundary/access/deterministic baseline local của Phase 1;
không chứng nhận Hybrid RAG đầy đủ hoặc mọi cách diễn đạt. Câu chưa có complete
stored target/field phù hợp được hỏi rõ/từ chối an toàn. Corpus/BM25, semantic
retrieval, grounded generation, durable citation/UI và demo eval thuộc phase sau.
Không chạy browser, MariaDB disposable, paid API, CI/staging/production. Không có
schema/content change nên không phát sinh migration hay shared-data export.

[Handoff hoàn thiện](fix-round-6-handoff.md),
[executed repair plan](../../../superpowers/plans/hybrid-rag/phase-1-fix-round-6.md).
Review-state ghi Phase 1 APPROVED với fingerprint trên; chỉ Phase 2 READY.
Owner có thể chạy `.ai/prompts/hybrid-rag/phase-2-corpus-bm25.md`. Codex chưa thực
thi Phase 2 và không có commit/push/PR/deployment authority từ approval này.
