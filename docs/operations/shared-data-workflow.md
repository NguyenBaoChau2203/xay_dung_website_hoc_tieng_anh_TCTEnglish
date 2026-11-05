# Dùng chung học liệu, mỗi máy có Docker riêng

Cập nhật05/11/2026. Giữ ASP.NET Core/MariaDB/Plesk; hủy chuyển Vercel/Supabase.
Production tiếp tục tắt deploy. Staging là nơi cả nhóm xem thử dữ liệu chung.

## Quy trình của nhóm

Bạn tạo/sửa nội dung trên web local. AI Agent xuất đúng phần đó thành gói dữ
liệu có phiên bản, đưa cùng source vào PR. Sau khi Châu review và merge, các
bạn khác yêu cầu Agent cập nhật bằng:

```powershell
pwsh -NoProfile -File scripts/pull-local.ps1
```

Lệnh lấy `origin/master` bằng `--ff-only`, rồi nhập gói mới vào Docker riêng;
không reset/stash/rebase. Đang ở feature branch, nếu không fast-forward được,
Agent giải quyết Git merge theo quyền được cấp rồi chạy
`pwsh -NoProfile -File scripts/shared-data.ps1 -Action Sync`. Nhánh khác truyền
`-Branch tên-nhánh`. Code đã pull nhưng nhập lỗi phải báo lỗi, chưa được bàn giao.

Ví dụ A thêm bài nghe → PR kèm gói → Châu merge → B/C pull theo lệnh trên →
cả ba có bài nghe đó. Điểm và lịch sử mỗi người vẫn riêng. Sửa local không hiện
ngay trên máy khác cho đến khi xuất, merge và pull. Staging nhập cùng gói tự động
sau build/test, backup và migration. Production chưa nhận những thay đổi này.

Git hook post-merge chạy sync nếu đã setup và có merge thực sự. “Already up to
date” không chạy hook, nên Agent dùng wrapper trên. Startup local cũng nhập bù
trước khi mở web. Mở IDE/MCP/session đơn thuần không khởi động Docker/speech.

## Máy mới

Theo [onboarding](team-local-onboarding.md): Git, Node22+/npm, PowerShell7, .NET
SDK9+/runtime phù hợp, Docker Desktop Linux containers. Chạy
`node scripts/check-local.cjs`, `SETUP-AGENTS.cmd` một lần, xác nhận trust riêng
trên IDE rồi dùng pull wrapper. Không cần SSMS, MariaDB Windows hay Whisper.
Database mới có tài khoản local ngẫu nhiên ở `.appdata/local/access.json` và gói
mẫu `team.onboarding`. Không sao chép volume/backup/key của Châu.
Key OpenAI nhập riêng bằng `scripts/set-local-ai-key.ps1`; sync không sửa key.

## Phạm vi hỗ trợ

| Gốc được chọn khi xuất | Dữ liệu đi kèm |
| --- | --- |
| `ReadingPassage` | Bài đọc, câu hỏi, lựa chọn |
| `ListeningLesson` | Bài nghe chung, transcript, quiz, từ vựng |
| `SpeakingPlaylist` / `SpeakingVideo` | Playlist, video chung, câu phụ đề |
| `WritingExercise` | Bài viết chung, câu mẫu, đáp án được chấp nhận |
| `Folder` / `Set` | Thư mục, bộ thẻ, thẻ và liên kết cần thiết |
| `Class` | Thông tin lớp không mật khẩu, liên kết thư mục/học liệu |
| `PremiumPlan` | Định nghĩa gói; không mang giao dịch/quyền mua của người dùng |

Hiện19 kiểu/trường nằm trong `SharedDataRegistry.cs`. Không xuất bài nghe/video/
bài viết riêng, users, mật khẩu, thành viên lớp, chat, thông báo, thanh toán,
credits, bài làm, điểm, tiến độ hoặc Goal cá nhân. Chia sẻ loại mới cần adapter
có kiểu rõ ràng và kiểm thử trong PR; không nhập bảng tùy ý/SQL dump. Gói mẫu
không chứa dữ liệu production. Snapshot production hiện có vẫn ở staging như
ledger lần copy trước, không tự đưa thông tin người dùng đó vào Git.

## Thêm/sửa dữ liệu bằng AI Agent

1. Pull/sync trước, tạo feature branch, thao tác nội dung trên web local.
2. Xem `pwsh -NoProfile -File scripts/shared-data.ps1 -Action Status` để biết
   stable key/package/local edits. Xác định đúng loại và ID vừa sửa.
3. Xuất gốc được chọn, ví dụ ID37 là bài nghe thực sự cần chia sẻ:

   ```powershell
   pwsh -NoProfile -File scripts/shared-data.ps1 -Action Export -Kind ListeningLesson -EntityId 37 -Key team.listening.campus -PackageId team.listening.campus
   ```

   ID37 chỉ là ví dụ. Key/package chọn một lần bằng chữ thường, không chứa tên/
   email. Lần sau dùng lại đúng key/package; công cụ tự tạo version2,3… Không dùng
   ID Docker làm khóa chung. Các bản ghi con có key riêng; ID máy nhận được ánh
   xạ theo database của máy đó. Export đăng ký ID nguồn để tự pull không nhân bản.
4. Review `data/shared/catalog.json`, `packages/<id>/<version>/package.json`,
   `assets/`: chỉ chứa học liệu/tệp được phép chia sẻ, không token/PII/recordings.
   Không sửa phiên bản đã merge; xuất bản mới. Không copy cả uploads/SQL backup.
5. Chạy `scripts/shared-data.ps1 -Action Validate` và `-Action Sync`, ghi kết quả
   trong PR cùng kiểm thử tính năng. Commit gói cùng source liên quan. Chỉ push/
   PR/merge khi được cấp quyền. Agent tự chuẩn bị gói khi thay đổi nội dung chung
   dù prompt không nhắc; source-only không cần gói rỗng.

Các kiểu có user sở hữu dùng bí danh, không xuất user/email. `team-owner` mặc
định là Admin đầu tiên của local; staging chỉ dùng Admin test
`admin@tct-stage.local`. Không đổi role/quyền. Cần chỉ định owner trên máy mình:

```powershell
pwsh -NoProfile -File scripts/shared-data.ps1 -Action MapOwner -OwnerAlias team-owner -EntityId 12
```

ID12 phải là user thật đã kiểm tra. Mapping chỉ lưu ở ignored
`.appdata/local/shared-data/owners.json`, không gửi cho máy khác. Alias `system`
được dành riêng, tạo tài khoản bị khóa/password ngẫu nhiên khi cần.

## Tệp và xung đột

Exporter chỉ lấy tệp nội dung trỏ tới trong `/images/` hoặc `/uploads/`, không
quét cả thư mục. Tối đa25MB/tệp,200tệp/gói; kiểm header/đuôi/SHA256/path/link,
từ chối SVG/HTML/executable. Tệp lớn dùng nguồn HTTPS đã kiểm quyền; không nhúng
token trong URL. Assets tên hash, nhập vào `/uploads/shared/<hash>.<đuôi>`.
Deploy giữ uploads. Sync sửa được tệp thiếu nhưng không ghi đè tệp khác hash hay
dọn tệp cũ. Giữ LF trong `.gitattributes` để hash giống Windows/Linux.

- Không có sửa riêng: cập nhật tại chỗ, giữ ID/FK/lịch sử. Đã bằng nội dung nhận:
  ghi nhận mà không nhân bản.
- Local đã sửa và khác gói nhận: dừng và rollback toàn bộ batch đang nhập. Giữ
  bản sửa, Agent báo stable key. Thống nhất nội dung cuối cùng, xuất version tiếp
  trên source đã cập nhật hoặc khôi phục đúng giá trị được duyệt. Không reset DB,
  sửa ledger hoặc dùng force. Khóa DB ngăn hai sync/export ghi đồng thời.
- Hai người xuất cùng versionN: giữ bản đã merge, cập nhật source rồi xuất N+1;
  không sửa hash/version cũ. Nên một người phụ trách một package.
- Không hard-delete gốc. Dùng IsPublished/IsActive=false khi có; loại không có cờ
  cần cleanup riêng được duyệt. Chỉ xóa bản ghi con thuộc gói, không sửa riêng và
  không còn phụ thuộc/lịch sử. Không cho cascade xóa bài làm.
- Bản cũ chưa có binding chỉ nhận làm cùng nội dung khi toàn bộ portable fields/
  refs khớp duy nhất. Trùng tên/ID không đủ. Khác ảnh/metadata có thể tạo bản mới;
  kiểm Status và thống nhất nguồn trước khi xuất.
- Export lỗi để lại versionfolder/`.catalog-export.tmp`: giữ nguyên để Agent
  kiểm tra transaction/catalog/binding, không tự xóa/sửa ledger. DB commit trước
  khi công bố catalog; file tạm giúp phục hồi nếu bước công bố lỗi.

Chỉ báo sync xong khi exit0 và ignored
`.appdata/local/shared-data/last-sync.json` có thời gian mới/hash catalog hiện
tại. Mỗi database có ledger version/hash riêng; không chia sẻ ledger qua Git.

## Duyệt production sau khi test

Production vẫn **OFF**, `data/shared/production.json` ban đầu rỗng. Khi Châu yêu
cầu, chuẩn bị danh sách phiên bản/hash đã nghiệm thu, ví dụ version2:

```powershell
pwsh -NoProfile -File scripts/shared-data.ps1 -Action Approve -PackageId team.listening.campus -Version 2
```

Lệnh chỉ sửa manifest duyệt trong source, gồm chuỗi phiên bản/dependencies; chưa
ghi hosting. Review PR/staging, tạo mapping chủ sở hữu private trên server nếu
cần (`shared-data-owners.json`; production không mặc định Admin). Sau đó Châu
cho phép mở gate và duyệt GitHub environment production.

Pipeline: CI → backup → committed migrations → nhập gói/hash đã duyệt → IIS →
kiểm commit/digest/readiness. Nhập lỗi giữ maintenance/backup, không tự rollback
DB. Merge/staging không cấp quyền production. Manifest rỗng chỉ phát hành code/
schema khi gate được mở. Job skipped không phải deploy thành công.

## Đoạn gửi AI Agent

> Đọc AGENTS.md và docs/operations/shared-data-workflow.md. Pull master bằng
> scripts/pull-local.ps1 để đồng bộ code/gói vào Docker riêng; không reset DB.
> Khi sửa học liệu chung, tự xuất đúng gốc bằng shared-data.ps1 -Action Export,
> giữ key/package, tạo version mới và review dữ liệu/tệp trước PR. Không chia sẻ
> tài khoản/key/tiến độ. Chạy web bằng scripts/start-local.ps1 -NoBrowser, dùng key
> đã lưu. Báo rõ sync thành công/xung đột; không bật deploy production.
