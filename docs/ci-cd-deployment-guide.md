# CI/CD và deployment TCT English

Quy trình hiện tại dùng GitHub Actions và Plesk Git trên Windows hosting Nhân Hòa.
Nhánh source mặc định là **master**; database là **MariaDB 10.5.23**.

Hướng dẫn đang áp dụng:

- [Chạy local bằng Docker MariaDB](operations/local-development-quickstart.md).
- [Cấu hình, phát hành và phục hồi trên Plesk](operations/plesk-cicd-runbook.md).

Pull request chạy build, toàn bộ application tests, kiểm tra artifact và migration
trên MariaDB thật. CI tạo app Windows self-contained và EF migration bundle từ
cùng commit. Release sau CI trên master mới cập nhật nhánh riêng
`codex/production-release` và gọi Plesk bằng HTTPS POST. Plesk chạy backup,
migration, triển khai app và readiness ngay trong hosting.

`PLESK_DEPLOYMENT_ENABLED=false` hiện giữ bước phát hành thật ở trạng thái tắt.
Plesk có thể tự nhận bootstrap trong private nhưng không thay website khi manifest
`enabled=false`. Xem bằng chứng đã chạy và điều kiện kích hoạt trong runbook.

## Làm việc hằng ngày

1. Bật Docker Desktop, chạy local và kiểm tra thay đổi.
2. Nếu sửa schema, tạo/review migration bằng EF tool local 9.0.4, commit cùng feature.
3. Push nhánh feature, mở PR, kiểm tra CI rồi merge vào master.
4. Khi production CD được kích hoạt, CI thành công sẽ phát hành app và chỉ áp dụng
   migration còn thiếu. Push nhánh khác không tự deploy website thật.

Không upload source thô vào httpdocs, không copy database dev đè database thật,
không sinh migration tự động lúc deploy. Dữ liệu học viên trên hosting được giữ;
thay đổi schema đi qua migration được review. Uploads và config production được
quản lý riêng với artifact. Seed data chỉ chạy khi có chỉ định rõ.

Credentials dùng User Secrets ở local và config private trên hosting. Không commit
mật khẩu DB, OAuth, SMTP hay AI key. Các credentials từng nằm trong Git public cần
được đổi tại nhà cung cấp; xóa khỏi file hiện tại không thu hồi được giá trị cũ.

Docker hiện dùng để chạy database dev và database kiểm tra CI. App production
được build tại CI rồi chạy IIS; chưa có bằng chứng gói hosting cho chạy Docker.
SSMS dành cho SQL Server, không cần cài thêm để dùng MariaDB của dự án này.
