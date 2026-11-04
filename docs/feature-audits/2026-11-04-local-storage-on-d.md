# Local storage chuyển sang D — 04/11/2026

Owner yêu cầu chuyển các thư mục liên quan dự án khỏi C và xác nhận được chuyển
cả cache dùng chung nếu an toàn. Một agent; dữ liệu nguồn/Git trên D từ trước.
Không commit/push/remote CI/deploy, không migration, không paid API.

## Kết quả dung lượng

- Trước khi chuyển: C free16.01GiB, D free50.77GiB.
- Sau chuyển và verification: C free~27.83GiB, D free~38.49GiB.
- C tăng khoảng **11.8GiB free**. Đây là số đo volume thực tế; không lấy tổng
  logical bytes làm số đã giải phóng vì allocation/hardlinks và workload khác.
- 7 directory relocations,146259 files,12.026GiB logical bytes được đối chiếu
  inventory tên/kích thước trước/sau; cache có hash samples, file data nhỏ có
  hash verification tương ứng. Hai SQL backup kiểm toàn bộ file hash.

## Vị trí hiện tại

| Dữ liệu | Physical target | Logical size |
| --- | --- | --- |
| NuGet global packages | D:\TCTEnglishLocal\cache\nuget\packages | 1.604GiB |
| npm cache | D:\TCTEnglishLocal\cache\npm | 7.741GiB |
| uv cache | D:\TCTEnglishLocal\cache\uv | 0.642GiB tại thời điểm move |
| Playwright browser cache | D:\TCTEnglishLocal\cache\playwright | 2.038GiB |
| NuGet HTTP/plugins/scratch | D:\TCTEnglishLocal\cache\nuget\{http,plugins,scratch} | nhỏ |
| TCTEnglish/ML.NET/sprint temp cũ | D:\TCTEnglishLocal\temp | ~0.9MiB |
| Hai SQL backup từ Downloads | D:\TCTEnglishLocal\backups | 2 ×207531 bytes |
| TEMP/TMP của web/build child trong launcher | D:\xay_dung_website_hoc_tieng_anh_TCTEnglish\.appdata\local\temp | tạo theo nhu cầu |
| Docker Desktop VHDX | D:\DockerDesktopWSL\disk\docker_data.vhdx | 37.58GiB, **đã ở D từ trước** |

C cache directories được thay bằng junction đến D; không phải hai bản cache.
Giữ đường dẫn cũ cho process/IDE đang chạy. Private root D:\TCTEnglishLocal
có protected ACL chỉ current owner/SYSTEM/Administrators. Cài đặt Windows,
.NET SDK, runtime/profile Codex/Python đang sử dụng không bị chuyển trong lúc
chạy. Đó không phải dữ liệu cache disposable của đợt này.

## Cấu hình cho những lần chạy sau

Đã lưu7 User environment variables: NUGET_PACKAGES, NUGET_HTTP_CACHE_PATH,
NUGET_PLUGINS_CACHE_PATH, NUGET_SCRATCH, NPM_CONFIG_CACHE, UV_CACHE_DIR và
PLAYWRIGHT_BROWSERS_PATH; mỗi biến trỏ đúng target D tương ứng.

Các thiết lập được đối chiếu tài liệu chính thức:
[NuGet cache folders](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders),
[npm environment configuration](https://docs.npmjs.com/cli/v11/using-npm/config/),
[uv cache directory](https://docs.astral.sh/uv/concepts/cache/),
[Playwright browser location](https://playwright.dev/docs/browsers#managing-browser-binaries).

Mở lại terminal/IDE sau phiên này để nhận environment mới. Process cũ vẫn có
compatibility junction. Không thay TEMP/TMP toàn cục Windows. Launcher chỉ map
child TEMP/TMP vào checkout drive và restore sau exit/failure/busy-port.
Rule chung cho4 IDE và quickstart đã ghi rõ hành vi này.

Host-only manifest và preferences ở D:\TCTEnglishLocal; review artifacts trong
ignored `.tmp/storage-move/`. SQL/key không được thêm vào Git.

## User Secrets / AI key — việc đã hoãn theo owner

**AI local hiện chưa configured; owner sẽ nhập lại key sau.** Đây là ngoại lệ
quan trọng, không được suy từ test suite xanh rằng paid AI đã hoạt động.

Tôi đã đưa credential directory vào batch move mà chưa xử lý riêng bảo vệ
credential. Bản file904 bytes chuyển sang D có thuộc tính Encrypted/EFS và phiên
công cụ không đọc được. Source C vẫn có JSON740 bytes, AI:ApiKey hiện rỗng;
metadata status báo User Secrets AI:ApiKey-invalid. Không in key/hash key ra log.

Batch dừng trước tạo junction; không có junction cho User Secrets. Một lần copy
khôi phục chuẩn từ D về candidate C cũng bị UnauthorizedAccessException, không
ghi đè file C. Không tắt EFS, sửa ACL của file key, dùng key khác hoặc cố vượt
bảo vệ. Bản protected file vẫn được giữ ở D:\TCTEnglishLocal\secrets\...
nhưng chưa thể xác minh đọc/khôi phục nội dung; không coi nó là working key store.

Đã thông báo trực tiếp lỗi và xin lỗi owner. Owner chọn: **“bạn cứ hoàn thành
việc đi key tôi sẽ nhập lại sau”**. Vì vậy để User Secrets ở vị trí chuẩn Windows,
không chuyển tiếp credential. Khi sẵn sàng, owner chạy trong PowerShell thường:

```powershell
pwsh -NoProfile -File scripts/set-local-ai-key.ps1
pwsh -NoProfile -File scripts/speech-status.ps1
```

Nhập key vào ô che ký tự, không đưa vào chat/source. Helper giữ các setting khác.
configured chỉ là metadata, account/quota/model access vẫn cần live opt-in riêng.
Tránh lặp lại: key stores/auth profiles không phải cache; không đưa vào batch
relocation dù tổng yêu cầu là dọn C. Đây là bài học từ chính incident này.

## Cách thực hiện / tính toàn vẹn

Mỗi source/target được kiểm absolute checkout/user-cache allowlist, reparse
points, destination chưa tồn tại, process reference và D còn reserve5GiB.
Probe Move-Item cross-volume + junction thành công trước khi chuyển cache.

PowerShell move tuần tự quá chậm với npm113055 files. Chỉ dừng task-owned mover
đã xác minh PID/script, không dừng ứng dụng người dùng. Gộp inventory source+
destination, xác nhận113055 unique files rồi resume bằng native robocopy16
threads; không dùng MIR/PURGE. Đối chiếu lại toàn bộ file lengths và12 hash
samples sau transfer; junction chỉ tạo khi source đã hết files. Các cache sau
dùng native transfer; private data nhỏ dùng Move-Item. Metadata/counters được
lưu nhưng không log nội dung SQL, mật khẩu hoặc API key.

## Verification sau relocation

| Check | Kết quả |
| --- | --- |
| npm config get cache | D:\TCTEnglishLocal\cache\npm |
| uv cache dir | D:\TCTEnglishLocal\cache\uv |
| dotnet nuget locals all --list | packages/HTTP/temp/plugins đều D |
| CLI Playwright đã có trong cache | chạy --version thành công; không tải package/browser mới |
| Offline restore app + tests từ D NuGet cache | thành công, không đổi project versions/config |
| Full .NET optimized SpeechReview | **890 pass /0 fail /12 skip**, tổng902 |
| Python scripts | **Ran99, skipped1** =98 executed pass /0 fail |
| Launcher child TEMP/TMP map/restore | red3 → green4/4; giữ guard không start/download ASR |
| Native setup | **9/9** |
| agent_tooling.py | verified |
| check_team_hooks.py | four native hosts/events/malformed input: PASS |
| git diff --check | sạch; warnings CRLF của UI dirty trước đó |

Không chạy lại UI/paid mic. Docker Engine chưa online trong lần kiểm tra, không
khởi động web/DB thật hay thay VHDX. Source/runtime Whisper đã retired; cache
Whisper cũ nằm trên D trong ignored retired archive, không tự chạy cùng web.
[Whisper retirement audit](2026-11-04-whisper-runtime-retirement.md).
