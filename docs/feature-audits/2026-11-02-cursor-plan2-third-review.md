# Review Cursor Plan2 — vòng3, 02/11/2026

**Chưa nghiệm thu Plan2.** Các trigger cũ ở9CursorReviewProbes +4RepairReviewTests
đã pass; source mới có retry quota, timestamp, validation, reset atomic consume
và single-page ordering. Review mở rộng tái hiện3finding dưới đây. BE0/BE4–BE8
vẫn còn phần chưa làm theo chính ledger Cursor.

Nhánh`feature/chau-ui-performance-audit`, HEAD210bf2d. Một agent review inline;
không sửa runtime/commit/push/CIremote/PR/merge/deploy. Giữ UIPlan1 và DBdev.
Gate GitHub đọc lại`PLESK_DEPLOYMENT_ENABLED=false`. AI probes fake provider,
không paid API/SMTP. Website model vẫn`gpt-6-luna`/low.

## Bằng chứng mới

| Check | Kết quả | Giới hạn |
| --- | --- | --- |
| Fresh app suite `CursorThirdReview`,Optimize=true | **843 pass /0 fail /11 skipped /854**,1m59s | Không `--no-build`; live/MariaDB flags off |
| 9CursorReviewProbes +4RepairReviewTests | **13/13 pass**, nằm trong suite trên | Chứng minh đúng các trigger cũ |
| ResetPassword_ConcurrentPosts_OnlyOneConsumesToken | **Pass**, trong suite | SQLite/TestServer, không phải provider MariaDB proof |
| Node submit guard +Loadolder | **5/5 pass** | Loadolder test mới chỉ1page |
| 3diagnostic cases mới của reviewer | **3 fail /3**, tái hiệnD1/D2 | Fakeprovider +SQLite, không dùng data thật |
| Chrome realDOM, productionloadOlderMessages,2pages | Tái hiệnD3 | Fakefetch/hydration; không phải browser acceptance cảwebsite |
| EF pending-model check | Sạch | Migrations không đổi ở vòng sửa này; không thay MariaDB upgrade |
| `git diff --check` | Pass | LF/CRLF warnings không phải whitespace failure |
| MariaDB simultaneousreserve/upgrade | **Chưa chạy lại** | DockerLinux APIpipe không tồn tại trong lượt này |

Docker command trả lỗi kết nối pipe`dockerDesktopLinuxEngine`; reviewer không
restart Docker/services hoặc đụng các workload khác. Không suy diễn việc thiếu
môi trường là C1fail; cũng không dùng kết quả3/3 trước của Cursor làm proof mới
của reviewer. C1code đã có boundedretry/stableidentity nhưng provider gate còn chờ.

Artifacts riêng, ignored `.tmp/cursor-third-review/`:

- `application.trx`, `application.log` — fullsuitefresh.
- `third-review-probes.trx`, `probes.log` —3diagnosticcases đỏ.
- `ThirdReviewTests.cs`, `ThirdReview.targets` — import riêng chỉ khi lệnh test
  chỉ định; không sửa `.csproj`/source runtime.
- `browser-history-multipage.json` — order thực và expected từ ChromeDOM.

Không tính3probe đỏ hay11skip thành acceptancepass. Các lệnh vàflags khác ledger
850/0/5/855; chỉ báo actualcounters của từng lượt tương ứng.

## Findings

### D1 — P1: cancel sau provider reply trước Save làm quota flush dữ liệu dở dang

**Files:** `Services/AI/AiChatService.cs:247–262,309–363`;
`AiDailyQuotaReservationService.cs:131–185`.

Hai service dùng chung scopedDbContext. Sau provider return, assistant và success
log ở stateAdded. Nếu caller cancellation làm SaveChanges token gốc bị hủy,
catch gọi Release với token cleanup. Release chỉ detach quota entities và
SaveChanges trong transaction của nó, nên **flush cả assistant/successlog đang
pending của caller**. Sau đó chat cleanup xóa userprompt vì replyCommitted=false.

Probe `CancelAfterProviderReplyBeforeSave_DoesNotLeaveAssistantWithoutUser`:

| Fixture | Kết quả sau cancellation |
| --- | --- |
| Existingconversation | **user0 /assistant1**,successlog1,ledgerReleased/ActualTokens0 |
| Newdraft | user0/assistant0,successlog0 nhưng ledgerReleased/0 dù đã có known providerreply20tokens |

Hai case đều đỏ. Existingconversation có orphanassistant; cả hai nhả quota sau
khi đã biết kết quả provider. Các test cũ hủy trong provider hoặc sau replycommit
chưa bao phủ ranh giới này.

**Sửa:** phân biệt providerreplyknown với replyDBcommitted; quota unitofwork không
được SaveChanges các entries khác của caller. Không release known usage chỉ vì
callerabort/persistfail, không lưu successlog/assistant như side effect của release.
Cleanup/terminal state rõ ở mọi boundary, có recovery cho unknown/pending.

Việc catchsettle saucommit và trả200 đã sửa trigger C3cũ, nhưng log “leaving
pending for reconciliation” hiện chưa đi kèm cơ chế reconciliation trong source
đã kiểm. Không đóng toàn bộ BE2 lifecycle/restart gate chỉ từ13cases cũ pass.

### D2 — P2: Speaking cắt24video mà chưa có continuation hoặc server filters

**File:** `Services/SpeakingService.cs:32–46,99–105,122–143`;
consumer `SpeakingController.Index` và `Views/Speaking/Index.cshtml`.

Publiccatalog vẫn ToList toàn bộ trước rồi Take24 mỗi level trong memory.
MyVideos mới Take24 ở SQL. Controller Index không nhận paging/filter inputs;
VM/UI chưa có metadata/Loadmore cho hai danh sách này. Topic list lại lấy đủ DB,
còn filter UI chỉ hoạt động trên các card đã trả về.

Probe `Speaking_CatalogFiltersAndPrivateList_DoNotLoseVideosAfterTwentyFour`
seed25publicC2 +25private của syntheticPremiumuser: VM trả **24public/24private**;
topic duy nhất ở video25 vẫn có trong Topics, nhưng **0card tương ứng** trong
returned publiclist. Đây là thay đổi hành vi làm ẩn nội dung hiện hữu, không phải
SQLpagination hoàn chỉnh. Dữ liệu chưa bị xóa nhưng không có đường xem tiếp qua UI.

**Sửa:** serverfilter/count/page/cursor +continuation và đúngtotal trước khi áp
caps. Có thể bỏ caps để khôi phục baseline trong batch correctness rồi thực hiện
BE4 đầy đủ. Không test bằng cách chỉ tăng cap hoặc render toàn bộ mãi rồi claim
đã tối ưu. PublicallVideos query cần bounded SQL thật; giữ private/role/resume.

PlaylistpreviewTake6/HasMore và Profilebadgequery là các slice mới; chưa có số
liệu trước–sau. Không claim wholeBE4/BE6done khi Writing/launcher và EXPLAIN thiếu.

### D3 — P2: Loadolder đúng từng page nhưng sai thứ tự sau page thứ2

**File:** `wwwroot/js/ai-chat.js:1364–1369` (fragment insertion).

Fragment được chèn **trước control Loadolder**. Sau lần1 control nằm dưới page
vừa tải; lần2 page còn cũ hơn được chèn dưới page1. Test Node mới chỉ1response
hasMore=false nên không kiểm đường này.

ChromeDOM chạy actualproductionfunction, fakefetch2responses:

```text
Initial: m4,m5
Page1 [m2,m3], hasMore=true  → m2,m3,m4,m5 (đúng)
Page2 [m0,m1], hasMore=false → m2,m3,m0,m1,m4,m5 (sai)
Expected: m0,m1,m2,m3,m4,m5
```

**Sửa:** control/anchor phải ở đầu lịch sử và chèn chronologicalfragment tại
đúng vị trí trước message cũ nhất, không làm anchor trôi xuống dưới page mới.
Test2–3pages thật, timestamps ties, duplicate prevention, scroll/focus và full/embed.

## Đánh giá C1–C6 và các batch

| Phần | Kết luận có giới hạn |
| --- | --- |
| C1MariaDBdeadlock | Có boundedretry/stablerequestId; chưa chạy lại providerproof vìDockerunavailable |
| C2/C3postcommit |4RepairReviewTests pass; D1precommitknownreply +durablerecovery còn thiếu |
| C4order | SinglepageNodepass; D3multipage vẫn sai |
| C5timestamp | Persistedrecencyprobe pass; selective detach tránh Clearall, nhưng sharedSave side effect D1 cần sửa |
| C6validation/reset | UpdateEmailinvalid và concurrentreset tests pass trong SQLite |
| BE4Speaking mới | Partial và gâyD2; WritingSQLaggregates/truepaging/EXPLAIN chưa làm |
| BE6Profile mới | Query badges awardedonly, không GETaward; chưa có regression/measurement riêng và launcherreuse còn thiếu |

BE0scale lab30samples, BE5SMTP/unlock, BE7assets/traces/diagnostics và BE8browser/
ASR/cleanPC vẫn pending/partial. Ledger đã tự ghi “Plan2 not fully complete”;
review không mở rộng phạm vi thành Pronunciationengine hay semanticWriting.

Không có runtimefix trong lượt review này. Xem
[repairplan vòng3](../superpowers/plans/2026-11-02-cursor-plan2-third-review-repair.md)
và [promptCursor](../../.ai/prompts/cursor-plan2-third-review-repair.md).
