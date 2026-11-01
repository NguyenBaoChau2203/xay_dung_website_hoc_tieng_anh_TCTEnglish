# Codex — thực thi Plan 2: ứng dụng OpenAI GPT-6 Luna low + Whisper audio

Một agent thực thi/review/verify inline; không subagent/AI CLI khác. Đọc AGENTS.md
và plan `docs/superpowers/plans/2026-11-01-openai-provider-migration-plan.md`.
Đọc kết quả chốt `docs/feature-audits/2026-11-01-whisper-plan1-final-closeout.md`.
Không bắt đầu khi owner chưa yêu cầu bước này. Giữ code Whisper đã review;
không quay về master làm mất thay đổi Plan 1 chưa commit.

## Target và quyền

Theo đính chính mới nhất ngày01/11/2026, **ứng dụng** dùng `gpt-6-luna`, Responses
`reasoning.effort=low` cho mọi tác vụ văn bản hiện gọi Gemini. Audio Gemini
chuyển sang Whisper; không gửi audio tới Luna. **Agent thực thi là Codex, owner
chọn GPT-6.1 Sol high trong giao diện**. Không ghi Sol/high vào cấu hình API web,
không tự sửa cấu hình model của Codex hoặc AI IDE. Target API Luna low giữ nguyên.

Được sửa provider/DI/options/default appsettings và startup/key helper liên quan
cho local. Không thay model của Cursor/AI IDE, Google OAuth, ML.NET, SMTP/payment.
Không commit/push/PR/remote CI/merge/deploy/Plesk/gate. Giữ dữ liệu, dirty files,
secrets và workloads khác. Dùng nhánh feature phù hợp kế thừa Plan 1.

## Quy trình từng phần

1. P0: inventory tất cả IAiProviderClient consumers và raw Google HTTP. Bảng
   trong plan gồm Chat, Writing feedback/generation, Vocabulary, Reading,
   Listening và YoutubeTranscriptService; bổ sung call sites mới nếu phát hiện.
2. P1: test đỏ→xanh OpenAiProviderClient trên HttpClient/JSON hiện có. Giữ reply
   contract, parse typed output và usage đúng; reasoning-first/refusal/incomplete
   không thành success giả. Bỏ temperature khi low, key ở Bearer header, không
   log key/body nhạy cảm. Deadline/cancel/retry tôn trọng caller và budget3s.
3. P2: đổi DI và kiểm từng consumer qua adapter thật với fake HTTP. Giữ Local
   Writing, stream app, parser JSON, quota/replay và credit. Kiểm reasoning output
   cap theo workload; không giải quyết JSON thiếu bằng đổi model hoặc cap vô hạn.
   Với Luna low, đo reasoning/output/latency khi owner đặt key; giữ budget3s của
   optional Writing và kết quả Local nếu quá hạn, không âm thầm hạ model/effort.
4. P3: nonsecret defaults chung OpenAI/GPT-6 Luna/low; key chỉ User Secrets hoặc env.
   Helper nhập masked không echo/history và giữ các secret khác. Xử lý legacy
   Google override có chủ đích; test bằng secret-store fixture. Không sửa source
   riêng mỗi máy. Bootstrap lần đầu provision dependency đã lock, không yêu cầu
   đồng đội tự tìm codec/model/endpoint sau khi pull.
5. P4 BẮT BUỘC: YoutubeTranscriptService đang gọi Google audio trực tiếp. Thay
   bằng Whisper audio transcript/decode hữu hạn + timestamps thật. FFmpeg máy
   Châu8.1.1 đã có nhưng PATH chưa là artifact pin; hoàn thành bootstrap/pin cho
   team. Không đưa video dài vào clip15s adapter; không bịa timecodes; giữ learner
   Speaking captions-only. Hoàn thành fake media+real decode/ASR regression.
6. P5: sửa UI copy/README/runbook hiện hành; runtime Google calls/config hết,
   audit lịch sử và Google OAuth không bị replace. Không bỏ test để đạt scan xanh.
7. P6: focused→build/full suite/pending-model/tooling checks; review diff inline,
   ledger docs/feature-audits/2026-11-01-openai-provider-migration-ledger.md.

Tái xác minh docs OpenAI chính thức trước code. Áp dụng tct-superpowers,
tct-engineering, tct-speaking-feature, tct-api-endpoint nếu đổi endpoint và
tct-tooling-review nếu sửa setup. CodeGraph trước indexed source; source history
on demand. UI copy cần DESIGN/tct-ui-component. Không tạo migration khi không
đổi persisted model; schema thay đổi ngoài dự kiến phải đánh giá theo AGENTS.

## API key và kết quả trung thực

Không yêu cầu owner gửi key vào chat. Hoàn thành code/offline trước; key thiếu
thì đánh dấu live API Not run rồi báo owner dùng helper nhập key local. Không
dump secrets, không tự tìm/dùng key từ project khác, không fake smoke success.
Sau owner đặt key, chạy smoke tổng hợp nhỏ đúng model và từng feature local;
không chạy toàn bộ suite qua API trả phí. Key/account/model access/quota/network
là điều kiện bên ngoài, không cam kết mọi key đều dùng được.

Báo tiếng Việt: file đổi, inventory runtime, commands nhập key/chạy web, checks
và actual model/usage khi live, cold teammate test, schema impact, từng gate
Implemented/Offline/Live/Not run/Blocked. Nêu rõ production chưa đổi và không
thực hiện Git publish. Dừng và báo owner kết quả/diff/ledger để review bước tiếp theo.
