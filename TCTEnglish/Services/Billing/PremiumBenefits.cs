using System.Collections.Generic;

namespace TCTEnglish.Services.Billing
{
    /// <summary>One row of the "Premium có thêm gì" table.</summary>
    /// <param name="Title">The feature as the pricing table names it.</param>
    /// <param name="ShortTitle">A shorter wording for the "đã mở khóa" grid after a payment.</param>
    /// <param name="FreeText">What a free account gets; null means "not available" (shown as a dash).</param>
    /// <param name="PremiumText">What Premium gets; null means a plain check mark.</param>
    /// <param name="Gate">Where the code really enforces it (verified at B0, plan section 12).</param>
    public sealed record PremiumBenefit(string Title, string ShortTitle, string? FreeText, string? PremiumText, string Gate);

    /// <summary>
    /// The Premium-only features shown to learners. Every row is backed by a real check in the code;
    /// nothing is listed that the product does not gate ("Không có quảng cáo" was dropped: the app has no ads).
    /// </summary>
    public static class PremiumBenefits
    {
        public static readonly IReadOnlyList<PremiumBenefit> Gated = new[]
        {
            new PremiumBenefit(
                "Trợ lý AI", "Trợ lý AI không giới hạn", "15 lượt mỗi ngày", "Không giới hạn lượt",
                "Services/AI/AiChatService.cs GetDailyUsageAsync (Standard limit 15)"),
            new PremiumBenefit(
                "Tạo bài luyện viết bằng AI", "Luyện viết với AI", null, null,
                "Services/WritingService.Generation.cs (premium_required)"),
            new PremiumBenefit(
                "Thêm video riêng để luyện nói và shadowing", "Video nói và nghe riêng", null, null,
                "Services/SpeakingService.cs CreateOwnedVideoAsync / SpeakingImportJobService"),
            new PremiumBenefit(
                "Thêm bài nghe riêng từ YouTube", "Bài nghe riêng từ YouTube", null, null,
                "Services/ListeningService.cs CreateOwnedLessonAsync"),
            new PremiumBenefit(
                "Dịch lời thoại và tạo quiz bằng AI cho bài nghe", "Dịch lời thoại, quiz AI", null, null,
                "Services/ListeningService.cs TranslateTranscriptAsync / GenerateQuizFromTranscriptAsync"),
            new PremiumBenefit(
                "Đổi tốc độ phát bài nghe (0,5× – 1,5×)", "Đổi tốc độ bài nghe", "Chỉ tốc độ 1×", "0,5× – 1,5×",
                "Services/ListeningService.cs CanUseSpeed"),
            new PremiumBenefit(
                "Điền chi tiết thẻ và tìm ảnh minh họa bằng AI", "Tìm ảnh khi tạo thẻ", null, null,
                "Controllers/VocabApiController.cs enrich / images; SetController.SuggestVocab")
        };

        /// <summary>Rows that every account has; shown in the table so the free column is never empty.</summary>
        public static readonly IReadOnlyList<string> ForEveryone = new[]
        {
            "Từ vựng, bộ thẻ, luyện tập, ôn theo lịch",
            "Bài nghe, bài đọc, bài nói có sẵn"
        };

        /// <summary>The four highlights shown on the "thanh toán thành công" page.</summary>
        public static IReadOnlyList<string> UnlockedHighlights { get; } = new[]
        {
            Gated[1].ShortTitle,
            Gated[2].ShortTitle,
            Gated[4].ShortTitle,
            Gated[6].ShortTitle
        };
    }
}
