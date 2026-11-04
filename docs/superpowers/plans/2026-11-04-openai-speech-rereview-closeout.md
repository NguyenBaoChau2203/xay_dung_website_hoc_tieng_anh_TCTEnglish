# OpenAI Speech — review lại và sửa phần còn thiếu, 04/11/2026

Owner cho phép review và fix tại local. Một agent; giữ branch và toàn bộ dirty
work. Không commit/push/PR/CI remote/merge/deploy, không paid request hay thay DB.
Kế thừa repair R1–R7 ngày 03/11; chỉ mở rộng khi có bằng chứng cụ thể.

- [x] Đọc ledger, review trước, source hiện tại qua CodeGraph và bug memory.
- [x] Baseline speech optimized: 67 passed / 0 failed / 1 skipped.
- [x] Kiểm tra budget HTTP: xác minh WAV PCM16 mono16k, chunk bounds, duplicate
  multipart file; regression từ chối trước send và không làm mất body hợp lệ.
- [x] Đồng bộ launcher với resolver: key explicit-empty trong User Secrets
  không được ánh xạ OPENAI_API_KEY ưu tiên thấp hơn; không ghi/echo secret.
- [x] Status metadata: không lộ credential/query trong URL sai; đối chiếu
  appsettings→Development→secrets→env, xử lý bool/model nhất quán.
- [x] Bổ sung assertion DB cho fake-HTTP assess/commit/replay (một completion
  credit, không tăng lại khi replay); kiểm tra các regression expiry/capacity
  của admission hiện có, không đổi admission runtime trong lượt này.
- [x] Đỏ→xanh các regression mới; speech focused, full .NET, JS và Python;
  validator tooling cho phần script; git diff --check.
- [x] Ghi report, ledger, bug memory và các giới hạn live/browser còn lại.

Tiêu chí đóng local: lỗi đã tái hiện không còn, test không gọi paid API, không
đổi text/audio model hay schema. Paid live, chất lượng mic thật, clean-PC và
production là các gate riêng, không suy từ fake HTTP/test xanh.

Đã hoàn tất local: full .NET897/0/12; JS12/12; Python Ran99, skipped1
(98 executed pass); setup9/9, tooling verified, diff check sạch.
[Báo cáo chi tiết](../../feature-audits/2026-11-04-openai-speech-rereview.md).
