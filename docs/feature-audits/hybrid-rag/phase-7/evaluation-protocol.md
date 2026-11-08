# Phase 7 — protocol đánh giá có thể thực thi

Fixture revision 2 và corpus lexical local là hai suite khác nhau. Không lấy câu
hỏi của suite này chạy trên dữ liệu của suite kia rồi tính vào cùng một mẫu số.

## Suite synthetic đã đóng băng

- File: `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json`
- Canonical SHA-256: `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`
- 120 ca, 80 development / 40 held-out, 51 nguồn `src-*`, corpus `synthetic-phase0`
- Mọi ca dùng `actor-lan-standard`, role Standard, email `lan.synthetic@example.test`
- `businessDate` của mọi ca là `2026-11-06`, timezone `Asia/Ho_Chi_Minh`
- Hash nguồn là SHA-256 UTF-8 của `canonicalText`. 51 hash khác nhau. Các id này
  không phải source key của snapshot local 487 chunk.

| Mẫu số | Development | Held-out | Tổng |
| --- | ---: | ---: | ---: |
| Mọi ca | 80 | 40 | 120 |
| `answered` và có `requiredSourceIds` (Recall@5) | 44 | 18 | 62 |
| `answered` (câu được hỗ trợ) | 44 | 18 | 62 |
| `access_denied` |  |  | 20 |
| `out_of_scope` |  |  | 15 |
| `insufficient_evidence` |  |  | 10 |
| `needs_clarification` |  |  | 7 |
| `provider_unavailable` / `cancelled` / `quota_exceeded` |  |  | 3 / 2 / 1 |

Recall@5 dùng đúng 62 ca answered. Câu trả lời đúng dùng cùng 62 ca; held-out
18 ca là phần không được dùng để chỉnh retrieval rồi gọi là đo mù. Từ chối đúng
giữ nguyên outcome đã gắn. Không đổi mẫu số khi một ca lỗi hạ tầng.

Câu hỏi development 3.807 byte, held-out 1.860 byte. 51 `canonicalText` cộng
3.565 byte. Đây là trần byte của fixture, không phải hóa đơn.

### Seed chỉ trên database dùng một lần

1. Tạo database trống, không dùng `tctenglish_dev` và không ghi đè DB owner.
2. Tạo Lan đúng role Standard. Minh Premium và Hoa Standard chỉ để các ca từ chối
   dữ liệu của họ có chủ sở hữu thật. Không đổi Lan thành Admin hoặc Premium.
3. Nạp mỗi nguồn fixture thành tài liệu có khóa `src-*` và hash ở trên.
4. Chạy ca với ngày nghiệp vụ `2026-11-06`, không đổi đồng hồ máy owner.
5. Giữ trần ngày: Standard 15 câu và 60.000 token. 20 lượt online của Lan không
   nằm trọn trong một ngày UTC.

16 câu trong [demo-script.md](demo-script.md) là nhãn diễn tập held-out đã bị
liệt kê trước khi có kết quả live. Đó là exposure của held-out, không phải lần
đo mù nếu sau này sửa ranking theo kết quả ấy.

## Suite corpus local

Snapshot file `s6466491683224781ace3ddbe570ff492`, checksum
`cdbfb0dd5f7d65f734536afaabee5241fdfa794b3595596b93769eb88e286d10`: 471 tài liệu,
487 chunk, trần byte 113.887. Chưa có vector index. Preflight local bổ sung ngày
2026-11-08 sau khi owner mở Docker: dry-run service thật trên transaction
read-only MariaDB 10.5.23 khớp snapshot này (471/487, 113.887 byte, 0 embedding
call). Bằng chứng tại local-completion-handoff.md. Hai tài khoản kiểm thử riêng
và câu mẫu local không phải Lan/fixture và không được tính vào mẫu số 120/62/18.

Khi vector manifest thiếu, retrieval trả `lexical_degraded` / `manifest-missing`
trước embedding câu hỏi. Số embedding call của những lượt đó là 0. Không gọi
thêm model để đủ số POST.

So sánh lexical / dense / hybrid trên corpus này chỉ bắt đầu sau khi index cùng
snapshot được promote và dry-run DB không báo drift. Query embedding của suite
này là query thật trên corpus đó, không dùng vector ngẫu nhiên hay câu trả lời
soạn sẵn.

## Gate còn BLOCKED

Nghiệm thu cuối vẫn cần Recall@5 trên 62 ca answered, 95% câu được hỗ trợ trên
đúng 62 ca, từ chối đúng trên các outcome không-answered, và ít nhất 20 lượt
online khi budget cho phép. 12 hoặc 16 câu diễn tập không thay các mẫu số này.
Thiếu index dense, thiếu DB disposable và thiếu quyền API giữ các mục đó ở
BLOCKED.
