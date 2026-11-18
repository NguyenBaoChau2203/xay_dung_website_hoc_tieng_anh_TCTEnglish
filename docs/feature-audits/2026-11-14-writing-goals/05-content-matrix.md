# Stage05 final content matrix — 108/108 new lessons complete

Ngày 14/11/2026. Hoàn tất local **108/108 bài Writing public mới**, gồm beginner36, intermediate36 và advanced36. Mỗi ô level × format tăng đúng sáu so với baseline gốc; không tính 10 bài public cũ vào quota. Chỉ tính `UserId == null && IsPublished == true`.

| Level | Format | Public baseline gốc | Public trước advanced | Mới advanced | Public sau cùng | Tổng bài mới so baseline |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| beginner | emails | 10 | 16 | 0 | 16 | 6/6 |
| beginner | diaries | 0 | 6 | 0 | 6 | 6/6 |
| beginner | essays | 0 | 6 | 0 | 6 | 6/6 |
| beginner | articles | 0 | 6 | 0 | 6 | 6/6 |
| beginner | stories | 0 | 6 | 0 | 6 | 6/6 |
| beginner | reports | 0 | 6 | 0 | 6 | 6/6 |
| intermediate | emails | 0 | 6 | 0 | 6 | 6/6 |
| intermediate | diaries | 0 | 6 | 0 | 6 | 6/6 |
| intermediate | essays | 0 | 6 | 0 | 6 | 6/6 |
| intermediate | articles | 0 | 6 | 0 | 6 | 6/6 |
| intermediate | stories | 0 | 6 | 0 | 6 | 6/6 |
| intermediate | reports | 0 | 6 | 0 | 6 | 6/6 |
| advanced | emails | 0 | 0 | 6 | 6 | 6/6 |
| advanced | diaries | 0 | 0 | 6 | 6 | 6/6 |
| advanced | essays | 0 | 0 | 6 | 6 | 6/6 |
| advanced | articles | 0 | 0 | 6 | 6 | 6/6 |
| advanced | stories | 0 | 0 | 6 | 6 | 6/6 |
| advanced | reports | 0 | 0 | 6 | 6 | 6/6 |
| **Tổng** | | **10** | **82** | **36** | **118** | **108/108** |

Ba receipt local có đúng 108 stable key và 108 ID khác nhau, không ID nào thuộc 22 bài baseline; 36 ID advanced không thuộc bất kỳ bài nào trong 94 bài trước đợt này. Verify lại cả ba batch đối chiếu metadata, văn bản, thứ tự, đoạn, đáp án và hash persisted. Không chuyển bài private thành public để đạt quota.

Bảo toàn theo multiset SHA-256 từng dòng: 42.409 dòng baseline gốc và 44.209 dòng trước advanced trên 72 bảng; giữ 11 bài private, một bài hệ thống chưa công bố, dữ liệu người học, attempts, progress, huy hiệu và credits. 343 package version trước advanced và production.json nguyên bytes; baseline gốc nguyên SHA-256. Snapshot/ID/receipt riêng nằm trong ignored state.

Advanced gồm 36 bài, 432 cặp, 36 biến thể, 9.387 từ tiếng Anh; 12 cặp/ba đoạn và 225–279 từ mỗi bài. Agent rà mọi cặp/biến thể mới; editorial QA của 72 bài trước được giữ trong ledger gốc. Test hiện tại kiểm lại cả 108 bài/1.152 cặp/108 biến thể; không câu tham chiếu trùng sau chuẩn hóa. Sáu báo cáo mới nêu rõ tình huống và số liệu giả định.

Kiểm chứng: 112 focused tests pass/0 fail/0 skip; pilot sáu format trong test host trước dev import, cloud tắt; dry-run36; import36 rồi rerun0/0/0; rerun cuối cả ba level đều0/0/0. Canonical export36 v1, re-export0 version; Validate379 releases/17.904 record revisions; Sync504 rồi0 operations, hai receipt khớp catalog hash. Fresh MariaDB10.5.23 nhận36/432/36, so bilingual/order/paragraph/answers và assessment đạt, repeat0 changed rows. Không gọi provider trả phí.

Xem [advanced ledger](05-advanced-ledger.md), [advanced editorial review](05-advanced-editorial-review.md), [advanced public receipt](../../../../data/writing/2026-11-14/advanced-receipt.json), [manifest](../../../../data/writing/2026-11-14/manifest.json), [beginner ledger](05-beginner-ledger.md) và [intermediate ledger](05-intermediate-ledger.md). Chỉ xác nhận local; chưa chạy CI/staging/production hay nghiệm thu UI live.

Stage07 đã tái kiểm cả ba batch:108/1.152/108, verify và import-rerun từng level
đều không tạo thêm bản ghi. Test mới nhập **cả108 package vào cùng một recipient
MariaDB10.5.23**, so exact bilingual/order/paragraph/variants, local assessment,
snapshot bảo toàn và repeat toàn DB bằng hash:1 pass. Đây là bằng chứng gộp bổ
sung cho ba receipt roundtrip riêng trước đó, không thay đổi package cũ.

Browser trên web dev thật đọc cả18 ô qua catalog endpoint:beginner/emails16,
17 ô còn lại6, tổng118 public ID khác nhau. Các batch vẫn tăng đúng6 mỗi ô so
baseline. Validate379 releases/17.904 revisions, hai Sync0 operations, Git
immutable-history check và receipt catalog hash đạt. Xem [ledger07](07-integrated-acceptance.md).
