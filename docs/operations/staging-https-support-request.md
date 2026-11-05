# Tin nhắn nhờ Nhân Hòa hỗ trợ gia hạn SSL

Bạn gửi đoạn bên dưới qua kênh hỗ trợ Nhân Hòa đang dùng. Không cần gửi mật khẩu,
key OpenAI hay thông tin kết nối database. Hiện chưa có ticket nào được agent gửi.

Tiêu đề: Nhờ hỗ trợ tự gia hạn SSL cho staging.tctenglish.site

> Chào anh/chị, em đang dùng gói hosting Sinh Viên N518, quản lý bằng Plesk.
>
> Em có web test cho nhóm tại staging.tctenglish.site. Web này được đặt mật khẩu
> để chỉ các bạn trong nhóm vào được. SSL hiện tại vẫn dùng bình thường, có hạn
> đến ngày 03/02/2027.
>
> Em muốn SSL tự gia hạn mà vẫn giữ mật khẩu bảo vệ web. Hiện đường dẫn xác minh
> `/.well-known/acme-challenge/` cũng đang yêu cầu mật khẩu nên việc gia hạn bị
> chặn. Nhờ anh/chị cho phép truy cập không cần mật khẩu vào riêng đường dẫn xác
> minh này. Các trang còn lại của web test vẫn giữ mật khẩu như hiện tại giúp em.
>
> Nhờ anh/chị kiểm tra lại việc cấp/gia hạn SSL khi mật khẩu bảo vệ web vẫn đang
> bật, và giữ cấu hình này cho các lần gia hạn sau. Chỉ thay đổi trên
> staging.tctenglish.site, giữ nguyên website chính tctenglish.site và các bản
> ghi DNS của website chính giúp em nhé. Em cảm ơn anh/chị.

Nếu kỹ thuật cần thông tin để kiểm tra: IP hosting là 103.28.36.90, siteId của
staging là 274. Web chạy Windows/IIS; chứng chỉ dùng Let's Encrypt. Đường xác
minh hiện trả lỗi 401 khi không đăng nhập, còn đăng nhập thì tải được token
87 bytes với mã 200. Giao diện Plesk dẫn về bảo vệ của thư mục `/`; thử điều chỉnh
`PleskUrlProtectionModule` trong cấu hình staging chưa giải quyết được lỗi.
Chưa sửa thư mục xác minh chung hay cấu hình máy chủ.

Sau khi Nhân Hòa báo đã xử lý, gửi lại phản hồi cho agent để kiểm tra đường xác
minh trả 200 qua cả HTTP/HTTPS, các trang của web vẫn yêu cầu mật khẩu và việc cấp
lại chứng chỉ thành công. Chỉ có tùy chọn “tự gia hạn” trong Plesk chưa đủ để xác
nhận đã chạy được. Nhóm vẫn dùng HTTPS hiện tại để test; cập nhật code không cần
cấp lại SSL mỗi lần.
