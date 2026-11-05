# Yêu cầu Nhân Hòa: HTTP-01 cho staging khi root có mật khẩu

Owner gửi qua tài khoản hỗ trợ Nhân Hòa; agent chưa gửi ticket hoặc liên hệ ai.
Không cần đưa key OpenAI, DB connection string hoặc mật khẩu vào nội dung.

> Xin hỗ trợ cấu hình riêng site `staging.tctenglish.site` (Windows/Plesk,
> siteId274, gói Sinh Viên N518, IP103.28.36.90). Website cần giữ Password-Protected
> Directories ở `/` để nhóm test. Chứng chỉ Let's Encrypt hiện hợp lệ đến03/02/2027.
>
> Khi root protection bật, anonymous HTTP/HTTPS tại
> `/.well-known/acme-challenge/<token>` trả401; cùng URL có portal credentials trả200
> với nội dung token87bytes. Plesk customer UI của đường ACME hiển thị bảo vệ kế
> thừa Directory name `/`, không có ngoại lệ anonymous riêng. Chúng tôi đã thử
> override `PleskUrlProtectionModule` trong cấu hình của riêng staging, nhưng URL
> challenge vẫn401; không thay cấu hình common challenge/server hay production.
>
> Nhờ kỹ thuật cho phép anonymous **chỉ** ACME challenge path trên staging trong
> IIS/Plesk common challenge routing (hoặc khắc phục SSL It!/URL-protection tương
> ứng). Root và mọi đường app khác vẫn yêu cầu mật khẩu. Không gỡ bảo vệ toàn site,
> không đổi DNS/@/www, certificate hoặc cấu hình `tctenglish.site` production.
>
> Tiêu chí: khi root staging vẫn401, URL token HTTP và HTTPS trả200, rồi Reissue
> Certificate thành công với root protection vẫn bật. Vui lòng xác nhận đường
> exception được giữ khi Plesk reconfigure và tự gia hạn. Có thể phối hợp token
> test với chủ tài khoản; không cần tắt xác thực hoặc cấp quyền server cho nhóm.

Sau phản hồi, agent kiểm token200/root401, reissue và certificate/readiness thật.
Không kết luận tự gia hạn đã được nghiệm thu chỉ dựa vào checkbox Plesk. Trong
lúc chờ, HTTPS hiện tại vẫn hợp lệ; không cần thao tác cert cho mỗi lần deploy.
