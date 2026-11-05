# Staging automation + OpenAI — 05/11/2026

Owner yêu cầu hoàn thiện staging/AI/dữ liệu nhóm, sau đó xác nhận rõ quyền commit
theo nhóm, push, PR, merge master và bật riêng staging. Một agent; production gate
false. Các kết quả dưới đây là evidence trước merge; release thực tế phải đối
chiếu Actions và expected marker, không suy diễn từ file source.

## Kết quả mã và cấu hình

- Thêm job staging vào deploy.yml: needs build, chỉ master/non-PR và gate staging;
  Environment `staging` chỉ cho master. Giữ nguyên gate/script release production.
- Publisher riêng: prerelease bất biến, SHA256/tag-source proof, branch
  `release/staging` chỉ hai file, POST webhook, HTTPS Basic Auth không redirect,
  marker Staging/commit/runId/digest và readiness.
- Host script riêng cố định root `staging.tctenglish.site`, DB
  `nhtctsnn_tctenglish_staging`, user `nhtctsnn_tct_stage`, loopback3306. State và
  DB/portal handoff trong private; không đưa lên release artifact/Git. Backup app
  và DB trước EF bundle, giữ config/env/API key/uploads, không reset dữ liệu.
- Khóa run/idempotency; không gỡ offline của operator. Backup lỗi dừng trước
  migration; migration/readiness lỗi giữ maintenance +recovery. Thành công dọn
  archive/extract riêng và giữ SQL/log/newest previous-app, không dọn failed runs.
- Plesk repo staging chọn release/staging, private deployment directory; bootstrap
  manifest enabled=false đã chạy preflight. Không pull source trực tiếp vào web root.
- Tạo GitHub Environment/master policy, ba secrets riêng qua stdin; không có
  OpenAI key/DB credential trên GitHub. Gate staging false khi bootstrap, chỉ bật
  sau checks và trước merge đã được owner cho phép. Production false.

## AI thật trên hosting

Owner nhập key staging bằng masked helper; đưa vào .NET Toolkit site274. Không
lấy key local, không sửa User Secrets, không in key. Sau kiểm thành công, tiêu thụ
file key handoff. Text gpt-6-luna/low; Speech gpt-transcribe; Writing Local làm
quyết định chính, optional cloud feedback8s.

Lần đầu Chat503/timeout, Speech504/engine_timeout. Key/model metadata trên máy
owner200 và text tối thiểu200 chưa đủ chứng minh host kết nối được. Đã build probe
.NET9 nhỏ, upload vào private và chạy qua bootstrap post-action (đã gỡ probe khỏi
post-action thường). Kết quả host: IPv4=2/IPv6=2, default và noProxy timeout~6s;
forceIPv4/noProxy metadata200/846ms. Chỉ staging thêm
DOTNET_SYSTEM_NET_DISABLEIPV6=1 và NO_PROXY=api.openai.com, giữ TLS/SNI validation.

Sau cấu hình: Chat trên web200, trả STAGING_OK. Speech trên web200/Completed;
audio tổng hợp PCM16mono16k88046bytes/~2.75s nhận “Hello. I like tea.”,100 text
match và word comparison=true. Chưa thử micro/người nói thật hoặc mọi tác vụ AI.
Không tính text match như chứng chỉ phát âm. Temporary synthetic Speaking video,
sentence, assessment đã dọn theo exact schema/id/title/source; users3,Writing1,
completion-credit1 giữ nguyên. Không gửi progress giả hoặc import audio180windows.

## Dữ liệu nhóm

Admin xuất bản Writing1; Standard/Premium thấy cùng bài, Admin bị chặn403 với
hai role đó; progress Standard1/2 và Premium0/5 riêng. Dữ liệu đã có trong cùng DB
staging, không cần Git sync để thấy bài mới. Docker/draft/private exercises và
progress không tự chia sẻ; Admin catalog published là luồng chia sẻ học liệu.

## Xác minh local

- .NET Release:929pass/0fail/18skip,947total,~1m44. Provider/live opt-in skipped
  được giữ rõ; provider upgrade/restore evidence đã có ở review trước.
- Node:58pass/0fail.
- Python toàn scripts/tests:120tests/0fail/1skip; staging15 gồm Windows PS5.1
  enabled-path drills success/repeat, backup fail, migration fail, readiness fail,
  ZIP traversal và guard production root/DB. Production deploy18/18.
- agent_tooling working tree pass; hooks kiểm index khi commit. Không coi tooling
  check thay cho app/browser/live request.
- Quét86files trước push: không có key/private-key pattern hoặc mật khẩu private mới
  bị đưa vào Git. Credential/profile/backup/temp đều ignored; không đưa secret
  bằng argv hoặc PR. Chạy lại scan trước push với file mới.

## HTTPS còn cần nhà cung cấp

Chứng chỉ staging hợp lệ đến03/02/2027; root401/authenticated ready200. Anonymous
ACME HTTP/HTTPS vẫn401, auth token200/87bytes. Customer UI của ACME dẫn tới bảo vệ
kế thừa `/`; local module override không cho ra token200, đã giữ/phục hồi cấu hình
site thay vì nới bảo vệ toàn web. Không sửa common challenge/server hoặc production.

Cần Nhân Hòa exception đúng đường ACME và reissue khi root vẫn bảo vệ. Nội dung
[ticket](../operations/staging-https-support-request.md) đã sẵn; agent chưa gửi
tin nhắn cho hỗ trợ. Không gọi automatic renewal đã đạt. HTTPS hiện tại không cản
mỗi lần deploy/test. [Quy trình vận hành](../operations/staging-automation.md).

## Đóng release

PR125 đã merge thành ef58fda91c1d1389656c17160cf2bf3a5f38975f. Host hoàn tất
run41965473404 lúc04:18:09UTC; `/tct-release.json` đúng commit/run/environment và
digest e25f2ece06a74a5bbbce84049f063d88763ea29d5708748eac3e89bb10309fd2;
ready200. Backup/migration log tồn tại, archive/stage scratch đã được dọn bởi
script sau success. Users3/history12/Writing1/goals2/credits1, học liệu/progress giữ.

CI lần đầu verifier hết thời gian trước khi host tải xong140MB; đã đối chiếu
private current.json +marker +ready trước recovery. Rerun failed job cùng identity
đã pass (attempt2), không ép xóa lock/attempt hoặc migrate lại. Build/MariaDB và
staging jobs success, production skipped/gatefalse. Sửa publisher deadline20phút
tính cả HTTP, job25phút, download15phút và tắt progress output cho lần sau.
Regression slow-host8phút/deadline có red→green17/17; Python toàn bộ122tests.

Owner đã xác nhận HTTPS hiện tại đến03/02/2027 đủ dùng, không cần tự gia hạn hoặc
gửi ticket. Đây là phạm vi đã bỏ theo owner, không phải nghiệm thu auto-renew.
Temporary PHP/network probes không tham gia post-action thường; PHP probe đã gỡ
khỏi website (404), post-action chuẩn được phục hồi. Không kill process hoặc
đưa thêm transport/custom deploy endpoint vào ứng dụng.
