# Khôi phục HTTP 503 trên staging ngày 21/11/2026

- [x] Kiểm tra qua HTTPS. Lúc 14:05 ngày 21/11 (UTC+7), IIS trả 503 cho `/` và `/health/ready`. Kiểm tra lại bằng tài khoản cổng staging cho cùng kết quả ở cả hai đường dẫn và `/tct-release.json`.
- [x] Đối chiếu GitHub Actions. Run `44507271887` xác minh thành công commit `93959ab4e5d5` lúc 17:51 ngày 19/11. Năm run `master` tiếp theo thất bại. Ở run mới nhất `44719006197`, build/test/MariaDB thành công, Plesk nhận webhook nhưng trình xác minh hết hạn khi chờ marker và readiness của `1aa86a481a90`. Gate staging đang bật, gate production đang tắt.
- [x] Kiểm tra Plesk sau khi chủ tài khoản đăng nhập. Gói `Sinh Vien N518` của `tctenglish.site` hiển thị `Suspended`. Plesk báo tất cả website trong gói bị tạm ngưng, `2946.2 MB used of 2 GB`, `Overuse is not allowed`. Cả Websites & Domains và Statistics đều quay về Account. Chưa có số liệu theo thư mục hoặc lý do tạm ngưng do nhà cung cấp xác nhận.
- [x] Theo yêu cầu của chủ tài khoản, gửi email đến bộ phận kỹ thuật Nhân Hòa đề nghị xác nhận lý do tạm ngưng, mở tạm quyền quản lý tệp hoặc cung cấp bảng dung lượng theo thư mục. Lúc 14:18, hệ thống Nhân Hòa xác nhận đã nhận phiếu hỗ trợ, Phòng Kỹ thuật, trạng thái `Đang chờ`. Lúc 14:22, cả `/` và `/health/ready` vẫn trả 503.
- [x] Lúc 14:31 Phòng Kỹ thuật Nhân Hòa xác nhận nguyên nhân tạm ngưng là vượt hạn mức và đã mở tạm gói. Plesk trở lại `Active`, hiện `Files` và bảng dung lượng. Dùng `Calculate Size` để đo các thư mục và kiểm tra nội dung bảy thư mục triển khai lớn nhất.
- [x] Chủ tài khoản cho phép xóa các bản ZIP nếu không ảnh hưởng website. Kiểm đúng sáu release assets, kích thước và SHA-256 trên GitHub. Xóa vĩnh viễn riêng `release.zip` trong sáu thư mục run lỗi `43538144629`, `44544549896`, `44637978453`, `44674197070`, `44711250235`, `44719006197`. Tổng tương ứng 1.180.953.020 byte (1,10 GiB). Đo lại `tctenglish-staging-state` bằng File Manager từ 2,13 GB xuống 1,03 GB. Run lỗi cũ còn 526 MB, năm run mới còn 30,1–36,3 MB mỗi run. Thư mục dự phòng của bản đang chạy vẫn 245 MB.
- [x] Lúc 14:31–14:32 trang chủ và `/health/ready` khi ẩn danh đều yêu cầu xác thực (401). Qua cổng bảo vệ, trang chủ TCT English trả 200, `/health/ready` trả 200 với `status=ready` và `/tct-release.json` trả 200. Commit `93959ab4e5d5e2d1932077caa0fa6a7b6b093ce5`, run `44507271887` và SHA-256 `04f17d7b879942b83734689bd93556ff327be8df05656780793f343f2099a81f` khớp GitHub của bản đang phục vụ.
- [x] Kiểm chứng sau khi dọn tệp. Trang chủ xác thực 200, readiness 200/ready, release marker giữ nguyên cả ba giá trị đã đối chiếu, trang chủ ẩn danh 401.
- [x] Cập nhật thống kê qua nút `Refresh usage stats`. Lúc 14:48 (UTC+7), Plesk Account ghi `Active`, **1.820,4 MB trên 2 GB**; Statistics hiển thị **89%**, Web 1,75 GB. Gói còn khoảng 227,6 MB. Bản mới nhất cần khoảng 620 MiB cho riêng ZIP và phần giải nén đồng thời nên còn rủi ro vượt hạn mức trong lần triển khai tiếp theo.

## Đối chiếu dung lượng

Plesk báo 2.946,2 MB cho cả gói `tctenglish.site`. Sau khi gói được mở tạm, Statistics ghi Web 2,85 GB, cơ sở dữ liệu 25,8 MB, log 6,48 MB và mục Backups của Plesk 0 B. Bản sao do script tạo nằm trong thư mục `private` và được tính trong Web.

| Đo trực tiếp trong File Manager | Dung lượng Plesk hiển thị |
| --- | ---: |
| `private/tctenglish-staging-state` (lịch sử triển khai staging) | 2,13 GB |
| `httpdocs` (website chính) | 253 MB |
| `staging.tctenglish.site` (website staging) | 246 MB |
| `git` | 123 MB |

Trong `tctenglish-staging-state`, năm lần triển khai lỗi ngày 19–20/11 lần lượt chiếm 228, 234, 234, 234 và 234 MB. Mỗi thư mục chứa `release.zip` 197,6–197,8 MB, `stage` chỉ giải nén được 30,1–36,3 MB và `owner.json`. Lần lỗi cũ ngày 14/11 (`43538144629`) chiếm 663 MB, gồm phần giải nén 338 MB, ứng dụng dự phòng 177 MB, ZIP 137,5 MB, SQL và log. Run đang phục vụ (`44507271887`) chiếm 245 MB ở thư mục private, gồm ứng dụng dự phòng 236 MB, SQL 9 MB và log. Những số này theo đơn vị hiển thị đã làm tròn của Plesk.

Số liệu bên dưới lấy từ danh mục tệp bên trong ba ZIP phát hành chính thức trên GitHub. Cột ứng dụng và công cụ migration là các phần của cột sau giải nén. Đơn vị dùng ở bảng là MB thập phân (1.000.000 byte).

| Bản staging | ZIP tải về | Sau giải nén | Ứng dụng | Công cụ migration |
| --- | ---: | ---: | ---: | ---: |
| 19/11, run `44507271887`, xác minh thành công | 186,1 | 418,9 | 247,0 | 172,0 |
| 19/11, run `44544549896`, lần đầu xác minh thất bại sau đó | 207,1 | 442,0 | 270,0 | 172,0 |
| 20/11, run `44719006197`, lần mới nhất | 207,4 | 442,9 | 270,5 | 172,3 |

Cả ba commit dùng cùng phiên bản `scripts/deploy-staging.ps1`. Script tải `release.zip`, giải nén vào `stage`, sao lưu bản ứng dụng đang phục vụ vào `previous-app` và tạo `database-before.sql`. Với lần lỗi đầu, ZIP và phần giải nén cộng lại là 649,1 MB. Nếu đã tới bước sao lưu và ứng dụng cũ có kích thước như bản phát hành trước, ba phần cộng lại khoảng 896,1 MB trước khi tính SQL và log. Đây là mức có thể phát sinh trong một lần chạy, chưa có số đo xác nhận ba phần ấy đang cùng tồn tại trên hosting. Phần học liệu đóng gói chỉ tăng 20,8 MB giữa hai bản ngày 19/11.

Script dọn ZIP và `stage` sau khi xác minh thành công, giữ SQL/log và `previous-app` mới nhất. Những lần lỗi giữ các tệp đã tạo để phục hồi. Biên bản sự cố 14/11 ghi nhận run `43538144629` đã sao lưu và chạy migration trước khi import bị kẹt. File Manager xác nhận cả năm lần mới nhất đều còn ZIP trên hosting và mới giải nén một phần.
