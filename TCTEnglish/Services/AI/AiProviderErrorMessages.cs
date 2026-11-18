namespace TCTEnglish.Services.AI;

public static class AiProviderErrorMessages
{
    public static string For(string code) => code switch
    {
        "reply_persistence_failed" => "AI đã xử lý yêu cầu nhưng chưa lưu được kết quả. Hãy tải lại lịch sử trước khi gửi yêu cầu mới.",
        AiProviderException.ErrorCodeInvalidConfiguration
            or AiProviderException.ErrorCodeAuthentication
            or AiProviderException.ErrorCodeModelAccess
            or AiProviderException.ErrorCodeQuota => "Tính năng AI tạm thời chưa sẵn sàng.",
        AiProviderException.ErrorCodeCircuitOpen
            or AiProviderException.ErrorCodeTimeout
            or AiProviderException.ErrorCodeNetwork
            or AiProviderException.ErrorCodeProviderUnavailable => "Dịch vụ đánh giá đang gián đoạn.",
        AiProviderException.ErrorCodeIncompleteResponse => "Chưa nhận được kết quả đầy đủ. Chưa lưu phần bị cắt.",
        AiProviderException.ErrorCodeRefusal => "Yêu cầu này chưa được đáp ứng. Vui lòng điều chỉnh nội dung.",
        AiProviderException.ErrorCodeRateLimited => "Bạn đang gửi yêu cầu quá nhanh.",
        _ => "Dịch vụ đánh giá đang gián đoạn."
    };
}
