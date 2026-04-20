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
    [InlineData("tôi nên học gì tiếp theo", UserIntent.StudyRecommendation)]
    [InlineData("gợi ý ôn tập cho tôi", UserIntent.StudyRecommendation)]
    [InlineData("đề xuất học tiếp", UserIntent.StudyRecommendation)]
    [InlineData("giải thích present perfect", UserIntent.OutOfScope)]
    [InlineData("dịch giúp tôi câu này", UserIntent.OutOfScope)]
    [InlineData("thời tiết hôm nay thế nào", UserIntent.OutOfScope)]
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
