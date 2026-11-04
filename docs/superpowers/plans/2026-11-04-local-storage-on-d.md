# Chuyển storage local từ C sang D — 04/11/2026

Owner yêu cầu dữ liệu dự án nằm trên D và xác nhận được chuyển cả cache dùng
chung nếu an toàn. Một agent; giữ key/data/history, không remote/deploy. Không
di chuyển app đang chạy, Codex profile/database hoặc cài đặt Windows.

- [x] Inventory: repo trên D; Docker VHDX37.58GiB trên D:\DockerDesktopWSL.
  C còn16.01GiB, D50.77GiB trước khi chuyển. NuGet1.604GiB, npm7.741GiB,
  uv0.606GiB, Playwright2.038GiB trên C; không executable đang dùng các cache này.
- [x] Tạo D:\TCTEnglishLocal với ACL private (owner/SYSTEM/Administrators).
  Probe cross-volume Move-Item và directory junction: đạt, old path vẫn đọc được.
- [x] Kiểm nguồn/đích/reparse/capacity, move caches sang D và tạo junction C→D.
  Giữ file inventory/bytes và kiểm kết quả từng cache; không chồng lên đích cũ.
- [x] Hai SQL backup đã chuyển sang private D, hash verified. **User Secrets
  không hoàn tất relocation**: bản đã chuyển bị EFS/read-denied, C store còn
  AI:ApiKey rỗng. Dừng thao tác credential, không tạo junction cho key store,
  không bỏ EFS/đổi key. Owner đã được báo và chọn tự nhập key lại sau bằng
  set-local-ai-key.ps1. Key nằm ngoài phạm vi cache hoàn tất, không báo AI ready.
- [x] Move các thư mục temp tên riêng TCT English nếu không workload sử dụng;
  giữ junction để các đường dẫn cũ hoạt động.
- [x] Launcher map TEMP/TMP trong child processes vào ignored local temp trên
  checkout D, restore env sau khi thoát/lỗi. Regression cho map/restore.
- [x] Kiểm npm/uv/NuGet/Playwright và speech metadata; script/tooling checks,
  build/relevant tests sau relocation. Ghi C/D free space thực tế và report.

Không đặt secret/SQL vào Git. Cache chia sẻ dùng D ngoài repo để tránh bị xóa
khi đổi checkout. Không hứa logical bytes bằng disk space đã giải phóng vì
hardlinks/compression và concurrent workloads có thể ảnh hưởng phép đo.

Kết quả: 7 directory relocations/146259 files; 12.026GiB logical data trên D,
C tăng khoảng11.8GiB free (16.01→27.83GiB), D còn38.49GiB. Thêm3 NuGet cache
paths và7 supported user env preferences trỏ D. Full890/0/12; Python Ran99/skip1;
launcher TEMP red3→green4/4; setup9/9; hooks/tooling đạt; restore từ local cache
D thành công. Chi tiết: docs/feature-audits/2026-11-04-local-storage-on-d.md.
