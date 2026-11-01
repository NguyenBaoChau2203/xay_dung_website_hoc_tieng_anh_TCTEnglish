# Team local rollout — Docker, Whisper, OpenAI

Owner01/11/2026 cho phép setup/commit/push/PR/merge master, giữ deployment off.
Một agent thực thi/review. Nhánh: **feature/local-docker-whisper-openai**.
Baseline master03c9c5c, kế thừa toàn bộ Plan1/Plan2 đã review; không reset dev DB,
không chuyển secret/backup/binary/native trust qua Git.

## Thành viên mới nhận gì

- Source/config chung dùng OpenAI gpt-6-luna/low và Whisper tiny.en CPU.
- Docker MariaDB10.5.23 cùng migrations; mỗi máy có volume/database/key riêng.
- `node scripts/check-local.cjs` chỉ kiểm prerequisite: Windows x64, Node/npm,
  Git, pwsh7, VC++ runtime, .NET9+ và Docker Linux engine. Có link cài mục thiếu.
- SETUP-AGENTS.cmd cho toolkit4clients; SETUP-LOCAL.cmd kiểm prerequisites rồi
  chạy launcher DB→Whisper→migrations→bootstrap mẫu→web.
- Bootstrap CLI Development-only, loopback/tctenglish_dev/tct_dev. Không có user
  mới tạo3account ngẫu nhiên + sample learning; nếu có user giữ nguyên. Password
  chỉ ignored access.json. Không có schema/entity/mapping mới.
- Key owner cấp được nhập riêng trên từng máy qua masked helper; không cần đổi
  source. Shared AGENTS hướng cả4clients dùng launcher và xác nhận readiness,
  speech/key-source metadata. Không paid request khi start/folderOpen/hook.

Fixture data dùng cho phát triển, không phải bản sao dữ liệu thật trên máy Châu
hoặc phụ đề media đã nghiệm thu. Backup/private uploads chia sẻ ngoài Git nếu
cần. Chưa thử trên một máy Windows khác không có prerequisites; doctor mô phỏng
mất PATH và MariaDB cold provider acceptance là bằng chứng có giới hạn cụ thể.

## Kiểm chứng local

| Check | Kết quả | Evidence ignored |
| --- | --- | --- |
| Bootstrap regression trước implementation |5failed/1passed | .tmp/team-onboarding/bootstrap-red-behavior.log |
| Bootstrap guards/new/random account/hash/rerun/edits/existing credentials |6/6passed | bootstrap-green.log |
| MariaDB10.5.23 mới riêng, migrations→bootstrap→repeat |PASS,3users, credential hash unchanged | cold-final.log / cold-result.json |
| Cold web/account auth |health200, Login JSON success, authenticated Profile200 | cold-result.json |
| Release full app + real Whisper/media opt-in |814passed/0failed/5skipped of819 | application-final.log / final.trx |
| Full scripts |107passed/0failed | scripts-full.log |
| Node setup |9passed | node.log |
| Tooling/native-hook contracts |PASS; IDE invocation/trust độc lập | tooling.log / hooks.log |
| Prerequisite doctor actual + missing PATH |PASS; missing prerequisites returns1, no install | doctor-final.log / doctor-missing.log |
| Pending EF model |No changes since last migration | pending-model-final.log |
| New key live provider |2requests actual gpt-6-luna pass ở phiên review ngay trước | .tmp/plan2-review/openai-live-final.log |

Full app lượt đầu phát hiện CLI dùng Console.WriteLine trái rule hiện hành;
đổi sang app ILogger, không sửa test để bỏ rule. Full cuối814/0/5 đã pass.
Năm skip: hai cloud live (không bật paid API trong full), ba MariaDB application
acceptance. Cold bootstrap provider đã chạy thật; không đổi ba skip thành pass.

Container disposable chỉ xóa sau kiểm label runId; volume dev và các project
Docker khác giữ nguyên. Account fixture lưu private dưới .tmp, không commit.
App cũ Debug5100 giữ nguyên; Release review-owned được restart khi cần rebuild.

## Git và deployment

Commits chia Whisper lifecycle, OpenAI/media migration, team bootstrap và tài liệu.
Commit hooks kiểm staged index; credential scans không in giá trị. Các artifact
gồm model/FFmpeg, logs, SQL/backup, database.env/User Secrets/access.json bị Git
bỏ qua. ReportBTH2 không liên quan được giữ ngoài PR.

Trước publish đã kiểm thực tế GitHub: repo variable **PLESK_DEPLOYMENT_ENABLED=false**,
production environment không override variable và repository hooks rỗng.
Workflow release có điều kiện master + gate true; PR/master vẫn chạy build/tests.
Không đổi Plesk hoặc gọi webhook. Phải kiểm run master/release job thực tế sau
merge trước khi báo kết quả cuối; kết quả nằm trong PR/Actions và handoff chat.

Không có production deployment trong scope. Production ASR/FFmpeg provisioning,
credential rotation, full browser/human mic/YouTube/load và recovery acceptance
vẫn theo các gate hiện hành; merge code không chứng minh các mục đó đã xong.

- [Onboarding và links cài](../operations/team-local-onboarding.md)
- [Đoạn chat gửi nhóm](../operations/team-local-group-message.md)
- [Plan thực thi](../superpowers/plans/2026-11-01-team-local-bootstrap-release.md)
