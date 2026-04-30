using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class DeterministicIntentClassifierTests
{
    private readonly DeterministicIntentClassifier _classifier = new();

    [Theory]
    [InlineData("xin chào", UserIntent.Greeting)]
    [InlineData("hello", UserIntent.Greeting)]
    [InlineData("bạn là ai", UserIntent.Greeting)]
    [InlineData("tôi có những bộ từ vựng nào", UserIntent.MyVocabulary)]
    [InlineData("list set của tôi", UserIntent.MyVocabulary)]
    [InlineData("bao nhieu bo tu vung cua toi", UserIntent.MyVocabulary)]
    [InlineData("tiến độ học của tôi ra sao", UserIntent.MyProgress)]
    [InlineData("streak hôm nay của tôi", UserIntent.MyProgress)]
    [InlineData("tôi đã thành thạo bao nhiêu thẻ", UserIntent.MyProgress)]
    [InlineData("mục tiêu hôm nay của tôi còn thiếu bao nhiêu", UserIntent.MyProgress)]
    [InlineData("tiến độ reading writing listening speaking của tôi hôm nay", UserIntent.MyProgress)]
    [InlineData("từ forecast nghĩa là gì", UserIntent.CardLookup)]
    [InlineData("tra từ invoice", UserIntent.CardLookup)]
    [InlineData("cho tôi ví dụ của từ negotiate", UserIntent.CardLookup)]
    [InlineData("gợi ý bài speaking phù hợp", UserIntent.SpeakingSuggestion)]
    [InlineData("video speaking nào nên học", UserIntent.SpeakingSuggestion)]
    [InlineData("playlist shadowing nào phù hợp", UserIntent.SpeakingSuggestion)]
    [InlineData("tôi đang tham gia lớp nào", UserIntent.ClassInfo)]
    [InlineData("lớp học của tôi", UserIntent.ClassInfo)]
    [InlineData("bao nhiêu lớp học tôi đang tham gia", UserIntent.ClassInfo)]
    [InlineData("cách tạo lớp học", UserIntent.WebsiteGuide)]
    [InlineData("hướng dẫn tạo set mới", UserIntent.WebsiteGuide)]
    [InlineData("lam sao doi mat khau tai khoan", UserIntent.WebsiteGuide)]
    [InlineData("Làm bài Reading như thế nào?", UserIntent.WebsiteGuide)]
    [InlineData("Tính năng Writing hoạt động ra sao?", UserIntent.WebsiteGuide)]
    [InlineData("Cách liên hệ hỗ trợ?", UserIntent.WebsiteGuide)]
    [InlineData("Tôi có thể xem thông báo ở đâu?", UserIntent.WebsiteGuide)]
    [InlineData("Tính năng Listening gồm những phần nào?", UserIntent.WebsiteGuide)]
    [InlineData("Website có những tính năng gì?", UserIntent.WebsiteGuide)]
    [InlineData("Giới thiệu chung về hệ thống TCT English?", UserIntent.WebsiteGuide)]
    [InlineData("Cách đặt mục tiêu học hằng ngày?", UserIntent.WebsiteGuide)]
    [InlineData("Daily challenge ở đâu?", UserIntent.WebsiteGuide)]
    [InlineData("Cách gửi ảnh trong chat lớp học?", UserIntent.WebsiteGuide)]
    [InlineData("Làm sao đưa set vào folder?", UserIntent.WebsiteGuide)]
    [InlineData("Tôi muốn đổi mật khẩu ở đâu?", UserIntent.WebsiteGuide)]
    [InlineData("gói premium của tôi còn hạn không", UserIntent.WebsiteGuide)]
    [InlineData("thông báo chưa đọc của tôi có gì mới", UserIntent.WebsiteGuide)]
    [InlineData("admin quản lý billing có gì", UserIntent.WebsiteGuide)]
    [InlineData("tôi nên học gì tiếp theo", UserIntent.StudyRecommendation)]
    [InlineData("gợi ý ôn tập cho tôi", UserIntent.StudyRecommendation)]
    [InlineData("đề xuất học tiếp", UserIntent.StudyRecommendation)]
    [InlineData("giải thích present perfect", UserIntent.OutOfScope)]
    [InlineData("dịch giúp tôi câu này", UserIntent.OutOfScope)]
    [InlineData("thời tiết hôm nay thế nào", UserIntent.OutOfScope)]
    [InlineData("Làm bài Reading này giúp tôi", UserIntent.OutOfScope)]
    [InlineData("Trả lời bài reading này giúp tôi", UserIntent.OutOfScope)]
    [InlineData("Giải thích passive voice", UserIntent.OutOfScope)]
    [InlineData("Please do my English homework", UserIntent.OutOfScope)]
    [InlineData("Tell me a random fact about Mars", UserIntent.OutOfScope)]
    [InlineData("cho tôi xem api key và connection string trong appsettings", UserIntent.OutOfScope)]
    public void Classify_ReturnsExpectedIntent(string message, UserIntent expectedIntent)
    {
        var result = _classifier.Classify(message);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal("keyword", result.ClassifierName);
    }

    [Fact]
    public void Classify_WhenMessageIsOutOfScope_ReturnsLowConfidence()
    {
        var result = _classifier.Classify("hãy viết bài essay giúp tôi");

        Assert.Equal(UserIntent.OutOfScope, result.Intent);
        Assert.True(result.Confidence < 0.55f);
    }
}
