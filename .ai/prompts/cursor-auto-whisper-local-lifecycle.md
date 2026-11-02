# Cursor Auto — thực thi Plan 1: Whisper + web local

Bạn là một agent duy nhất trong repo TCT English. Chế độ Cursor Auto; không
spawn/delegate hoặc gọi AI CLI khác. Đọc AGENTS.md, rồi chỉ plan đang thực thi:
`docs/superpowers/plans/2026-11-01-whisper-local-lifecycle-plan.md`.
Áp dụng tct-superpowers, tct-engineering, tct-tooling-review và speaking skill khi
đụng luồng speech. CodeGraph trước source; không tự init/rebuild index.

## Quyền và đầu ra

Owner yêu cầu triển khai local: thêm setup/lifecycle Whisper đã khóa phiên bản,
tích hợp vào scripts/start-local.ps1 và hướng dẫn chung để mỗi lần AI được yêu cầu
chạy web local thì speech cũng sẵn sàng. Được download artifact pinned vào ignored
cache và chạy local checks. Không commit, push, PR, remote CI, merge, deploy,
Plesk/gate hoặc thay native trust. Không tự chuyển sang Plan 2/OpenAI.

Tạo nhánh feature phù hợp nếu cần, gợi ý codex/whisper-local-lifecycle. Bảo toàn
dirty files và workload; không stash/reset dữ liệu để lấy tree sạch. File
docs/superpowers/plans/2026-10-31-bth2-report.md đã untracked lúc lập plan, ngoài scope.

## Trình tự bắt buộc

1. Xác minh baseline/process/ports/disk; đọc launcher hiện tại và ghi checklist W0.
2. W1: lock b5454+tiny.en; reuse cache chỉ sau hash/provenance; không tải model lớn
   hoặc Docker image Whisper. CPU2thread là điểm bắt đầu, chưa là benchmark kết luận.
3. W2: helper Setup/Start/Status/Stop/Benchmark, hidden loopback process, lock và
   ownership thực; state/PID đơn lẻ không đủ để stop. Start CLI return vẫn phải sống.
4. W3: một lệnh DB→speech→web; giữ DatabaseOnly/backup DB trống; NoBrowser vẫn
   bật Whisper; SkipWhisper explicit; env child đúng và restore được. Không kill
   web/cổng bận. Không start dịch vụ khi folderOpen/build/MCP/hook chạy.
5. W4: test đỏ→xanh process ownership/concurrent start/hash failure, live ASR,
   full app speech→receipt→progress→credit/replay; đo cold/warm5/10/15s và RAM.
   Silence không chứng minh chấm tiếng nói. Browser mic chưa thử phải ghi Not run.
6. W5: build/tests/tooling checks theo plan; review diff inline và lập ledger
   docs/feature-audits/2026-11-01-whisper-local-lifecycle-ledger.md.

Chỉ mở source/skill theo active step; không preload mọi lịch sử. Nếu có lỗi thật,
đọc bug memory liên quan và ghi fix log theo quy định. Không sửa Program.cs,
appsettings/csproj chỉ để tiện launcher; không reset DB dev hoặc ghi hosting.

## Báo cáo cuối bằng tiếng Việt

- Đã đổi file gì, một lệnh chạy mỗi ngày, URL web, status/stop/opt-out.
- Tests thực chạy/pass/fail/skip và latency/RAM đo được, không lặp số test cũ.
- Process được giữ chạy phục vụ owner là process nào; lỗi/mic check nào còn thiếu.
- Schema impact, scope không deploy, diff local chưa commit và link ledger.
- Dừng để owner đem kết quả cho Codex review; không tự sang OpenAI.
