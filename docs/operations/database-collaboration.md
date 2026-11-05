# Database local và database dev/staging dùng chung

**Cập nhật05/11/2026:** đã bổ sung công cụ gói có phiên bản và đồng bộ sau pull.
Xem [hướng dẫn hiện hành](shared-data-workflow.md). Các mô tả quy trình trước
khi có công cụ ở phần dưới chỉ là bối cảnh; không cần copy toàn bộ Docker/DB.

Owner chọn bổ sung dev/staging chung và đã cho phép cấu hình trên hosting hiện
có. **Đã thiết lập và nghiệm thu cơ bản05/11/2026** tại
https://staging.tctenglish.site, DB riêng `nhtctsnn_tctenglish_staging` trên gói N518.
Xem [runbook staging](staging-plesk-runbook.md) cho truy cập, version, backup và
giới hạn AI/HTTPS renewal. Các checklist/lựa chọn cấp phát bên dưới là quy trình
tham khảo; không yêu cầu mua thêm server hoặc tạo lại staging đang hoạt động.

## Git đồng bộ những gì?

| Loại thay đổi | Cách chia sẻ | Trạng thái hiện tại |
| --- | --- | --- |
| Code, quy tắc tính XP/goal, giao diện | PR → merge master → pull | Theo source |
| Bảng/cột/index và badge catalog `HasData` | Migration + Designer + snapshot cùng PR | Launcher áp migration đã commit vào Docker local |
| Học liệu mẫu ban đầu | Bootstrap local khi DB chưa có user | Không chạy lại trên DB đang dùng |
| Học liệu Admin thêm/sửa trong DB | Export gói được chọn → PR/merge → pull-local + sync | Có công cụ; xem shared-data-workflow.md |
| Goal cá nhân, tiến độ, XP, streak, huy hiệu đã nhận, bài làm | Thuộc user trong DB tương ứng | Không tự đi theo Git |
| Key, mật khẩu, backup, runtime uploads | Kho riêng theo môi trường; không commit | Chỉ media học liệu được chọn/kiểm tra mới vào assets của gói |

Ví dụ: sửa thuật toán Goals rồi push sẽ chia sẻ thuật toán. Tạo mục tiêu Writing
2 bài/ngày trong Docker của Châu chỉ tạo dữ liệu của user đó. `UserGoal.UserId`
và query ownership giữ mục tiêu riêng theo tài khoản. Hai người cùng dùng staging
cũng chỉ thấy mục tiêu của chính mình; tính năng goal chung cho cả nhóm cần
thiết kế riêng, không tự xuất hiện khi đổi connection string.

## Cách làm việc được chọn

Mỗi thành viên tiếp tục chạy `scripts/start-local.ps1 -NoBrowser` với Docker riêng
để code, thử migration và thử lỗi. Sau review, bản code thống nhất được đưa lên
một **web staging** dùng MariaDB riêng. Cả nhóm mở cùng web staging để kiểm thử
tích hợp và dùng cùng bộ học liệu. Thay đổi dữ liệu ở đó được ghi vào DB staging,
không cần push Git để người khác thấy sau khi tải lại trang.

Ưu tiên web staging kết nối DB trong cùng máy/mạng riêng. Không cần mở cổng DB ra
Internet cho mọi laptop. Cách này cũng tránh nhiều nhánh code khác schema cùng
ghi vào một DB và tránh các worker chạy trùng trên mỗi máy.

`DatabaseSettings.Read` hiện **chặn remote DB trong Development**. Launcher cũng
cố định Docker loopback và áp migration local. Không sửa guard, đổi môi trường
thành Staging trên laptop, hoặc sửa launcher để lách quy định này. Local vẫn
là local; web staging có cấu hình host riêng.

## Thông tin cần xác nhận trước khi cấp phát

1. Nơi đặt: gói Nhân Hòa hiện tại cho phép thêm site/subdomain + MariaDB riêng,
   hay VPS/máy chủ khác. Xác nhận giới hạn DB/site/storage/backup với nhà cung cấp.
2. Subdomain staging, cách bảo vệ truy cập và người quản lý tài khoản test.
3. Quyền tạo database/user riêng, cấu hình IIS/app pool và lưu secret trên host.
   Nhập credentials trực tiếp trong kho riêng; không gửi vào chat hoặc Git.
4. Dữ liệu ban đầu: synthetic đã duyệt, hoặc bản sao đã loại dữ liệu cá nhân.
   Mặc định không lấy tài khoản/tiến độ/credential production sang staging.
5. Cách chuyển uploads/media, lịch backup và nơi giữ bản backup riêng.

## Lựa chọn nơi đặt trước thiết lập (tham khảo)

Đã chọn tận dụng gói N518 sau khi kiểm tra trực tiếp05/11. Các lựa chọn VPS dưới
đây là phương án khi hết hạn mức hoặc cần cách ly tài nguyên mạnh hơn.

Ưu tiên kiểm tra gói Plesk Sinh Viên N518 có cho thêm subdomain/site chạy ASP.NET
Core9, app pool và database/user riêng hay không. Nếu đủ quyền và tài nguyên,
`staging.tctenglish.site` có thể dùng cùng nhà cung cấp với production nhưng
site/config/data tách biệt. Chưa có bằng chứng hạn mức gói này, nên không gọi
đây là dịch vụ miễn phí có sẵn hoặc tự thay đổi Plesk.

Nếu gói không đủ, đề xuất VPS x64 Linux riêng, tối thiểu dự kiến2vCPU/4GB RAM,
40–60GB SSD cho web+MariaDB của nhóm ít người. Đây là cấu hình khởi đầu theo
quy mô dự án, chưa phải kết quả load test. Build artifact ngoài VPS; không chạy
AI model cục bộ. Dành backup ngoài volume DB. Staging Linux cần kiểm tra riêng
filename case, media/import và đường dẫn; pipeline IIS win-x64 hiện tại không
tự trở thành pipeline Linux.

Tham khảo trang chính thức kiểm tra04/11/2026:

| Lựa chọn | Thông tin công khai | Điều cần xác nhận |
| --- | --- | --- |
| [Nhân Hòa NVMe Cloud VPS C](https://nhanhoa.com/may-chu/vps-nvme.html) |3cores,4GB RAM,55GB NVMe; trang hiển thị321.000đ/tháng ưu đãi |Giá checkout/gia hạn/VAT/kỳ hạn, backup và phần RAM tặng; không coi ưu đãi là báo giá cam kết |
| [DigitalOcean Basic Regular](https://www.digitalocean.com/pricing/droplets) |2vCPU,4GiB RAM,80GiB SSD,24USD/tháng theo bảng giá |Region, thuế, backup và tổng chi phí theo tài khoản |

Thiết lập thực tế đã dùng N518, không mua VPS; DNS staging được thêm riêng ở
Hostinger theo quyền owner. Nếu nâng cấp sau này, owner chọn đích/ngân sách và
cấp quyền qua cơ chế riêng. Không cần cài SSMS hoặc chuyển sang PostgreSQL để
giải quyết việc chia sẻ dữ liệu của dự án MariaDB này.

## Checklist cấp phát và nghiệm thu

- [ ] Tạo site/app pool, database và DB user riêng cho staging; quyền giới hạn
      đúng DB. Không dùng credential hay database production.
- [ ] Chọn MariaDB10.5.23 để khớp hosting hiện tại, kiểm tra provider/case rules.
- [ ] Cấu hình host `ASPNETCORE_ENVIRONMENT=Staging`,
      `ConnectionStrings__DefaultConnection`, `Database__ServerVersion` bằng
      kho cấu hình riêng. `Database__ApplyMigrationsOnStartup=false` và
      `Database__SeedOnStartup=false`; không bootstrap tài khoản mặc định.
- [ ] Cấu hình riêng OAuth callback, cookie scope, Data Protection, uploads,
      SMTP/payment/AI và worker. Không để staging gửi mail/thanh toán thật;
      chỉ một deployment quản lý worker. Key test theo môi trường và ngân sách.
- [ ] Backup staging trước nâng cấp; một operator/pipeline áp đúng migration đã
      review đúng một lần. Ghi commit/schema version, readiness và chạy lại
      không thay dữ liệu/history. Không tạo migration trên hosting.
- [ ] Tạo ba tài khoản test với mật khẩu riêng trong kho an toàn; nhập bộ học
      liệu mẫu qua Admin hoặc importer đã review, không dùng SQL dump trong Git.
- [ ] Hai browser/tài khoản xác nhận cùng học liệu; mục tiêu/tiến độ cá nhân
      không lộ chéo; Admin thay đổi học liệu hiện sau reload; IDOR/CSRF đạt.
- [ ] Diễn tập backup/restore trên DB thử riêng trước khi gọi staging sẵn sàng.
- [ ] Ghi lại hostname, người vận hành, commit, kết quả checks và cách rollback
      trong runbook riêng, không ghi credential hoặc token vào báo cáo.

Lần thiết lập05/11đã triển khai snapshot được review qua Plesk theo quyền owner,
chưa thêm workflow CD staging. Production và `PLESK_DEPLOYMENT_ENABLED=false`
giữ nguyên. Thành công staging không chứng minh production đã triển khai.

## Nếu cần cùng dữ liệu trên Docker riêng

Đó là snapshot/import, không phải đồng bộ liên tục. Dùng backup riêng đã kiểm
tra và chỉ import vào DB trống theo [onboarding](team-local-onboarding.md).
DB đã có dữ liệu phải có kế hoạch merge theo stable keys, xử lý trùng/quan hệ
và quyền sở hữu; không xóa volume, không đè cả DB. Seed tăng dần phải idempotent
và được review riêng. Không xuất UserGoal/UserBadges cá nhân thành seed chung.
