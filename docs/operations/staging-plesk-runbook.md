# Staging Nhân Hòa/Plesk — vận hành từ05/11/2026

Staging đang chạy tại **https://staging.tctenglish.site** trên gói Sinh Viên N518.
Đã kiểm HTTPS, readiness, quyền Admin, học liệu chung và progress riêng. Website
production `tctenglish.site` và DB production không được dùng để thử nghiệm.

## Cấu hình thực tế

| Thành phần | Giá trị |
| --- | --- |
| Plesk site |staging.tctenglish.site, siteId274 |
| Document root |`\staging.tctenglish.site` trong subscription |
| IIS pool |`staging.tctenglish.site(domain)(pool)`, riêng,64bit,loadUserProfile=true |
| Database |`nhtctsnn_tctenglish_staging`,MariaDB10.5.23,localhost:3306,case1 |
| App DB user |`nhtctsnn_tct_stage`; Create user all-databases unchecked |
| DNS |Hostinger: A staging→103.28.36.90,TTL300; @/www giữ nguyên |
| HTTPS |Let's Encrypt riêng staging, hạn03/02/2027; xác thực chứng chỉ thật |
| App environment |ASPNETCORE_ENVIRONMENT và DOTNET_ENVIRONMENT đều Staging |
| Schema |12migration, cuối20261104110702_AddWritingAcceptedAnswers |
| Build ban đầu |Windows x64 self-contained; snapshot worktree đã review, không phải một GitHub release mới |

`/tct-release.json` sau xác thực ghi environment=Staging,
deploymentKind=manual-reviewed-worktree,sourceDirty=true và assembly hash. Base
commit8203e30aa39ea77bcdca6fee593b15456772bdce; dirty changes được review/test local
trước publish. Không dùng base commit để gọi toàn bộ code này đã merge lên master.

Production gate `PLESK_DEPLOYMENT_ENABLED` đọc mới vẫn **false**. Chưa tạo workflow
CD staging, chưa push/merge/remote CI trong lần setup này. Push Git không tự cập
nhật app hoặc rows trên staging. Chỉ triển khai bản được owner cho phép sau review.

**Follow-up cùng05/11:** owner đã yêu cầu và cấp quyền hoàn thiện CI/CD staging,
commit/PR/merge và bật riêng staging. Job/script/regressions được bổ sung trong
[quy trình staging tự động](staging-automation.md), nhánh manifest `release/staging`,
Environment chỉ master, secrets riêng. Đoạn trên ghi trạng thái lần setup ban đầu;
không dùng để kết luận chưa có source CD ở follow-up. Việc kích hoạt phải đối chiếu
run/gate/marker thực tế trong audit.

## Đăng nhập và dữ liệu nhóm

Staging có2lớp đăng nhập: mật khẩu bảo vệ website của Plesk, sau đó tài khoản app.
Thông tin được sinh ngẫu nhiên và chỉ giữ trong file ignored trên máy Châu:

- `.appdata/local/staging/portal-access.json`: lớp bảo vệ ngoài.
- `.appdata/local/staging/accounts.json`: Admin/Standard/Premium test.
- `.appdata/local/staging/hosting-secrets.json`: credential DB, không đưa cho nhóm
  chỉ để họ đăng nhập web.

Mở riêng các file khi cần; không paste vào chat/PR, commit, chụp màn hình hoặc
chuyển toàn bộ thư mục. Châu cấp tài khoản qua kênh riêng. Các thành viên nên dùng
tài khoản app cá nhân; không dùng chung Admin để ghi nhận tiến độ.

Học liệu synthetic hiện có: Writing1, `Staging Shared Writing 2026-11-05`,2câu
“Xin chào. Tôi thích trà.”. Admin tạo/xuất bản qua UI; cả Standard và Premium đã
mở cùng bài. Standard đặt Writing2và hoàn tất bài→1/2,50%; Premium đặt Writing5
vẫn0/5,0%. Hai tài khoản đều403ở Admin. Không có dữ liệu/tài khoản production
được sao chép sang đây. Docker local của mỗi thành viên vẫn độc lập.

## AI và dịch vụ ngoài

Staging chưa được cấp key OpenAI riêng; không lấy User Secrets/OPENAI_API_KEY của
máy Châu để đưa lên hosting. Speech được tắt trong appsettings.Staging.json cho
đến cấu hình có chủ đích. Writing dùng Local, cloud enhancement=false. Chưa có
paid/live acceptance. Không gọi đây là môi trường đã nghiệm thu chức năng AI/audio.

**Follow-up05/11:** owner đã nhập key staging riêng bằng helper kín, đã đưa vào
environment của site274. Metadata của hai model200; một text tối thiểu gọi từ máy
owner200/completed. Speech đã cấu hình enabled, Writing Local +cloud feedback8s.
Tuy nhiên Chat trên hosting503/timeout và Speech504/engine_timeout trong lần probe
đầu, nên còn điều tra kết nối outbound của hosting. Phân biệt configured với
live acceptance; xem audit tự động hóa cho kết quả mới hơn. Không copy key local.

**Đã xử lý tiếp:** probe .NET9 trên host chứng minh default/noProxy timeout,
forceIPv4→metadata200/846ms. Chỉ site staging thêm
`DOTNET_SYSTEM_NET_DISABLEIPV6=1`, `NO_PROXY=api.openai.com`. Chat qua web sau đó
200/`STAGING_OK`; Speech qua web200/Completed, audio tổng hợp2.75s nhận đúng câu,
text match100 và comparison có dữ liệu. Đây là live API smoke, chưa phải nghiệm
thu chất lượng micro/người nói thật. Fixture Speaking tạm đã dọn đúng sentinel.

Database startup migrate/seed=false; cleanup/expiry/reconciliation/auto-unlock
workers tắt; SMTP không có cấu hình gửi thật. Notification/learning worker vẫn
phục vụ DB staging. OAuth/payment/SMTP cần credentials/callback riêng nếu sau này
owner giao kiểm thử; không sao chép production config. Không có local Whisper.

## Nâng cấp staging lần sau

1. Xác định source/commit hoặc snapshot được owner cho phép, kiểm build/tests/
   migration/provider. Không kéo source master trực tiếp vào web root rồi chờ IIS build.
2. Backup **đúng database staging** trước nâng schema. Chứng minh identity khác
   `nhtctsnn_tctenglish`. Lưu backup private, không ở document root hay Git.
3. Giữ cấu hình host/secret/uploads. Đặt marker app_offline riêng của run; chỉ
   thao tác root staging. Không dùng script deploy production với đường dẫn khác
   để vượt guard yêu cầu httpdocs.
4. Publish win-x64 self-contained, chạy `scripts/prepare-publish.py` trên artifact
   trước khi thêm config Staging không có secret. Stage connection được lưu bằng
   .NET Toolkit environment ở site274; ZIP không chứa credential. Không overwrite
   web.config đã có host secrets nếu chưa có cơ chế giữ/khôi phục các giá trị đó.
5. Chạy migration đã review đúng DB, không tạo migration trên host. Khởi tạo lần
   đầu đã dùng SQL idempotent sinh từ repo EF9.0.4; không dùng dump Docker local đè DB.
6. Gỡ đúng marker offline của run, kiểm TLS +authenticated `/health/ready`, marker,
   login/roles và data preservation. Giữ bảo vệ truy cập trước khi mở cho nhóm.
7. Dọn artifact ZIP của run khỏi web root; ghi version/evidence. Production gate
   vẫn false, không gọi webhook hoặc chạm repo release production.

File Manager upload qua browser giới hạn50MB/lần; lần đầu đã chia artifact thành
5ZIP độc lập16–18MB và giải nén, không dùng ZIP chia byte cần ghép trên hosting.
ZIP ở web root đã được đưa vào Recycle Bin, không dọn thùng rác chung của owner.

## Backup/restore và kiểm tra đã chạy

Backup mới `.appdata/local/staging/backups/staging-2026-11-05.sql`,79581bytes,
SHA256 `36feb941034fafa3b323c20732fb544a99291ba75b96d173e9a29111d61142d6`.
Chứa dữ liệu test và password hashes: vẫn coi là file private, không commit.

Đã restore vào DB mới `tct_acceptance_stage_restore_1793817012399` trên Docker
MariaDB10.5.23/case1, giữ3users,12history,12badges,1Writing,2goal(target2/5),
1completion-credit. DB dev hiện có không bị reset; không tạo thêm container.
DB restore này là lab riêng, không phải datasource của app local hoặc hosting.

Đã chạy lại schema SQL trên hosting sau khi có dữ liệu:795queries thành công,
vẫn3users/12history/12badges/1Writing/2goals/1credit; hai tài khoản giữ1/2và0/5.
Readiness200, authenticated config/Git/ZIP probes404; anonymous website401.
Production public root đọc-only vẫn200. Không thử failure injection trên production.

## HTTPS renewal — giới hạn vận hành cần giữ rõ

Plesk hiển thị tự gia hạn, nhưng root Basic Auth đã từng chặn HTTP-01 bằng401.
Chứng chỉ hiện tại đã cấp thành công khi tạm gỡ **bảo vệ root staging** trong lúc
app_offline của run còn tồn tại, sau đó đã bật lại bảo vệ và kiểm401/ready200.
**Chưa chứng minh tự gia hạn xuyên qua lớp bảo vệ**, không gọi đó là auto-renew
đã nghiệm thu.

Trước kỳ gia hạn, cần Nhân Hòa cho phép anonymous **chỉ** virtual path
`/.well-known/acme-challenge/` của staging, rồi nghiệm thu HTTP-01 với root còn
bảo vệ. Không sửa thư mục challenge chung của server hoặc nới auth site khác.
Nếu chưa có exception, operator thực hiện lại quy trình maintenance→gỡ bảo vệ
staging→Reissue Certificate→bật lại root/user đã lưu→gỡ đúng maintenance→kiểm TLS,
anonymous401/authenticated ready200. Không gửi Basic credentials qua HTTP.

Đây là follow-up vận hành trước03/02/2027; staging hiện dùng được, production
certificate/bindings không bị thay đổi. Không mua chứng chỉ hoặc VPS trong setup.
