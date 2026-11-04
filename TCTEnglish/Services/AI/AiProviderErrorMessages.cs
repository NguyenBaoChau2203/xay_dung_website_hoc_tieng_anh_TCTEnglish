namespace TCTEnglish.Services.AI;

public static class AiProviderErrorMessages
{
    public static string For(string code) => code switch
    {
        "reply_persistence_failed" => "AI đã xử lý yêu cầu nhưng chưa lưu được kết quả. Hãy tải lại lịch sử trước khi gửi yêu cầu mới.",
        AiProviderException.ErrorCodeInvalidConfiguration => "OpenAI chưa được cấu hình đúng. Vui lòng liên hệ quản trị viên để kiểm tra cấu hình và API key.",
        AiProviderException.ErrorCodeAuthentication or AiProviderException.ErrorCodeModelAccess => "OpenAI từ chối quyền truy cập. Vui lòng liên hệ quản trị viên để kiểm tra API key và quyền dùng gpt-6-luna.",
        AiProviderException.ErrorCodeQuota => "Tài khoản OpenAI đã hết hạn mức API. Vui lòng liên hệ quản trị viên để kiểm tra số dư và hạn mức.",
        AiProviderException.ErrorCodeIncompleteResponse => "OpenAI chưa trả về kết quả đầy đủ. Chưa lưu kết quả bị cắt; vui lòng thử lại với nội dung ngắn hơn.",
        AiProviderException.ErrorCodeRefusal => "OpenAI không thể đáp ứng yêu cầu này. Vui lòng điều chỉnh nội dung.",
        AiProviderException.ErrorCodeTimeout => "OpenAI phản hồi quá thời gian cho phép. Vui lòng thử lại sau.",
        AiProviderException.ErrorCodeRateLimited => "OpenAI đang giới hạn tốc độ. Vui lòng chờ một chút rồi thử lại.",
        _ => "OpenAI tạm thời không khả dụng. Vui lòng thử lại sau."
    };
}
