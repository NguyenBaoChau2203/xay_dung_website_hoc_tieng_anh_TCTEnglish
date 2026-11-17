# Stage 03 — Dry-run bằng chứng lịch sử và thiết kế đối soát

Ngày 14/11/2026. Chỉ đọc trên fixture disposable; không chạy đối soát tài khoản
thật, không sửa lịch sử hoặc đồng bộ personal data.

## Kết quả fixture có kiểm soát

Test `LegacyEvidenceAndEmptyOrXpOnlyRows_DoNotMintHistoryOnRead_AndAreUserScoped`
tạo một Accepted attempt hôm qua, một empty daily row hai ngày trước và một daily
row chỉ có7 XP hôm nay. Người khác có10 thẻ hôm nay.

| Dấu hiệu | Dry-run của đúng user | Sau GET Goals |
| --- | --- | --- |
| Accepted business date cũ không có daily reward row |1 ngày có bằng chứng | Hiển thị confirmed ngày đó; không dựng lại streak |
| Empty daily row |1 | Không tính ngày học |
| Generic XP-only row |1 với7 XP đã lưu | Không tính ngày học; không xóa XP đã lưu |
| Lifetime Writing completion credit |0 | Vẫn0; một câu Accepted không chứng minh hoàn thành bài |
| UserBadges |0 | Vẫn0; qualification trong projection không phải awarded history |
| Current streak |0 | Vẫn0; không backfill bằng read |
| Ngày học của user khác |1 ngày của user khác | Không nhập vào ngày học của user đang xem |

Đây là kết quả synthetic fixture, không phải thống kê thiếu thưởng của người dùng
thật. Không thể công bố tổng thiếu XP/streak/badge trong DB thực khi chưa đọc nó
theo một phạm vi đối soát được chủ dự án duyệt.

## Thiết kế đối soát dành cho stage04

1. Chốt user/date range, clock snapshot, phiên bản rule và chế độ chỉ đọc. Bỏ user
   anonymized/deleted; không lấy Login/LastLogin/browser duration làm bằng chứng.
2. Lấy distinct business dates từ Passed persisted Writing attempts và daily
   counters/bonus confirmed. Báo riêng future timestamps, empty/XP-only rows và
   vùng trước khi có dữ liệu; không suy ra âm tính chắc chắn từ thiếu dữ liệu.
3. Đối chiếu completion evidence bằng exercise progress/credit/counter và earliest
   complete evidence; không suy first completion từ một sentence hay ngày replay.
   Ngày thưởng cũ/counter legacy thiếu provenance phải ghi unknown/conflict.
4. Với badge, tính rule từ bằng chứng đã xác minh, so UserBadges và chỉ đề xuất
   insert missing unique(user,badge). Không thu hồi hoặc giảm earned awards.
5. Xuất preview gồm số liệu tổng hợp, candidate keys, evidence/rule và conflict;
   dữ liệu cá nhân chi tiết nằm ignored/private, không đưa vào Git.
6. Chỉ áp dụng sau review phạm vi cụ thể; user lock + transaction + outbox unique,
   batch bounded, rerun idempotent. Kiểm chứng preservation và stop khi conflict.

Stage03 không chạy bước apply. Việc dựng lại streak/XP quá khứ không thuộc safe
badge catch-up: cần quyết định riêng về ngày hiệu lực, thưởng đã có và lịch sử
thiếu. Không có quyết định đó thì giữ nguyên dữ liệu cũ.
