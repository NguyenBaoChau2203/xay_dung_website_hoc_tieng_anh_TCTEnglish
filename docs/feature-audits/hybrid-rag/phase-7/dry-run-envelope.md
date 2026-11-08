# Phase 7 — envelope sửa sau review vòng 1

Ngày tính lại: 2026-11-08. Nhánh `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`. Predecessor 155 entry
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`.

Các số 60 POST / 100.000 token, phương án A/B, **169.087** và **282.974** không
còn là đề nghị chi. 169.087 chỉ là 113.887 byte duy nhất cộng reserve 12 lượt
chat. 282.974 là ví dụ nếu mỗi input bị tính hai lần khi usage chưa biết. Cả hai
không phải ngân sách được xin trong file này.

Chưa có lệnh text, embedding hoặc speech.

## Policy đang chạy được

Production giữ nguyên trần một job embed: 12 POST và 40.000 token giữ chỗ.
`AiAcceptanceRunGuard` là cổng của harness, kiểm tra trước từng HTTP giả, cộng
POST, giữ unknown đến hết run, và không promote khi còn unknown hoặc thiếu chunk.
Guard không nâng trần job. Bổ sung local ngày 2026-11-08:
`AiAcceptanceHttpHandler` bọc transport của hai client production trong test,
nên mỗi send/retry thật của client đi qua cùng cổng trước HTTP giả. Guard dùng
Stopwatch khi không truyền clock; run deadline liên kết với HTTP/body read và
kiểm lại trước promote. Usage âm/0/không hợp lệ giữ reservation thành unknown.
Các test này không đăng ký handler trong app và không cấp quyền gọi API live.

Ledger thật: trước mỗi send, reserve đúng trần byte của batch. Response mất thì
unknown giữ nguyên số đó. Retry chỉ được gửi khi phần còn lại của job chứa nổi
batch ấy. Unknown không bị xóa để build cho xong.

Đo lại snapshot theo thứ tự chunk, known giả định bằng trần byte:

| Job | Chunk | POST | Known byte | Batch |
| --- | ---: | ---: | ---: | --- |
| 1 | 66 | 2 | 38.066 | 36.276 + 1.790 |
| 2 | 85 | 2 | 39.951 | 37.558 + 2.393 |
| 3 | 336 | 6 | 35.870 | 6.206 + 10.853 + 6.796 + 4.118 + 6.681 + 1.216 |

Tổng nominal khi mọi response trả usage và không có unknown: **3 job, 10 POST,
113.887 byte**. Đây là đường thành công, không phải dự phòng retry.

Batch 36.276 để lại 3.724 trong job 40.000. Retry 36.276 bị từ chối. Batch
37.558 để lại 2.442 và cũng không retry được. Hai batch này làm run dừng ở
trạng thái partial, 0 chunk của batch đó được cache, index không promote.
Batch của job 3 đủ nhỏ để một retry còn chỗ trong job mới, nhưng unknown của
batch lớn vẫn chặn promote của cả run.

Vì vậy envelope không hứa hoàn thành index sau lỗi, không hứa đúng 3 job khi có
retry, và không thêm 10 POST “phòng retry” cho corpus.

## Các stage và trần guard đề xuất

Chưa được duyệt để gọi API. Số dưới là trần harness đã có test giả, để review
chọn hoặc từ chối.

| Stage | Khi nào gửi | POST nominal | Trần byte | Retry trong job 40.000 |
| --- | --- | ---: | ---: | --- |
| Nguồn synthetic 51 đoạn | Disposable DB, không phải DB owner | 1 | 3.565 | Còn chỗ: 2 × 3.565 = 7.130 |
| Corpus local 487 chunk | Sau dry-run DB khớp snapshot và cache miss | 10 | 113.887 | Batch 36.276 và 37.558 không retry được |
| Query development 80 câu | Index đã promote, cùng snapshot | 2 | 3.807 | Còn chỗ vì từng batch nhỏ hơn 40.000 / 2 |
| Query held-out 40 câu | Cùng điều kiện, không dùng để chỉnh model | 1 | 1.860 | Còn chỗ |
| 20 lượt online | Xem mục dưới | 0 cho đến khi có index và quyền | Reserve mỗi lượt tối đa 4.600 | Không gửi khi lexical_degraded |

Tổng đường thành công của bốn stage embed, chưa gồm chat: **14 POST** và
**123.119** byte đã biết (113.887 + 3.565 + 3.807 + 1.860). Guard trong test
corpus dùng riêng 10 POST / 113.887 / 20 phút vì đó là phần vượt trần một job.
Gộp bốn stage embed vào một run thì trần tương ứng là 14 POST và 123.119 token
account, vẫn cộng unknown chứ không xóa. Thời lượng cứng của guard là 20 phút.
Vượt POST, token hoặc thời lượng thì send tiếp theo bị từ chối trước HTTP.

20 lượt online chưa nằm trong 14 POST. Index hiện thiếu nên retrieval là
`lexical_degraded` và **0** embedding câu hỏi. Mọi ca fixture là Lan Standard,
tối đa 15 câu và 60.000 token mỗi ngày UTC. 20 lượt cần sang ngày UTC sau hoặc
giữ partial. Không đổi Lan thành Admin để né trần 15 câu. Stage này tiếp tục
BLOCKED cho đến khi owner duyệt đúng số POST, token account và thời lượng, và
DB disposable đã có Lan cùng ngày `2026-11-06`.

## Preflight local ngày 2026-11-08

Sau khi owner mở lại Docker, MariaDB 10.5.23 tại localhost:3307 phục vụ bình
thường. Probe dùng transaction read-only trên `tctenglish_dev`, gọi chính
`VectorIndexService.DryRunAsync` và lexical dry-run: 471 tài liệu, 487 chunk,
snapshot `s6466491683224781ace3ddbe570ff492`, 113.887 byte, 487 cache miss,
0 embedding call. Lần này DB thực tế khớp snapshot, không chỉ checksum file.
Ước lượng dry-run là 8 request / 27.610 token; con số đó không thay replay
admission có trần job ở trên (10 POST nominal / 113.887 byte).

Index vẫn thiếu (`manifest-missing`); không bấm Build/Tạo vector. Không reset
DB, không đổi trần production và không gửi text/embedding/speech trả phí.
Diễn tập trình duyệt local được ghi riêng trong local-completion-handoff.md;
nó không thay benchmark synthetic hoặc nghiệm thu live.
