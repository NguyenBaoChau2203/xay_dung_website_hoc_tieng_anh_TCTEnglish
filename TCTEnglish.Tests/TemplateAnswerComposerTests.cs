using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class TemplateAnswerComposerTests
{
    private readonly TemplateAnswerComposer _composer = new();

    [Fact]
    public async Task ComposeAsync_Greeting_ReturnsCanonicalGreeting()
    {
        var result = await _composer.ComposeAsync(UserIntent.Greeting, "xin chào", [], CancellationToken.None);

        Assert.Contains("Chào bạn! 👋", result);
        Assert.Contains("- Xem bộ từ vựng và tiến độ học", result);
    }

    [Fact]
    public async Task ComposeAsync_MyVocabulary_ReturnsSetSummary()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.MyVocabulary,
            "bộ từ của tôi",
            [
                new KnowledgeSnippet("summary", "totalCount=2", KnowledgeSnippetSources.UserVocabularySummary),
                new KnowledgeSnippet("Business Terms", "cardCount=18", KnowledgeSnippetSources.UserVocabularySet),
                new KnowledgeSnippet("Daily English", "cardCount=12", KnowledgeSnippetSources.UserVocabularySet)
            ],
            CancellationToken.None);

        Assert.Contains("Bạn hiện có 2 bộ từ vựng:", result);
        Assert.Contains("**Business Terms**: 18 thẻ", result);
        Assert.Contains("**Daily English**: 12 thẻ", result);
    }

    [Fact]
    public async Task ComposeAsync_MyProgress_ReturnsProgressTemplate()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.MyProgress,
            "tiến độ học",
            [
                new KnowledgeSnippet(
                    "summary",
                    "streakDays=4|masteredCount=15|learningCount=6|newCount=9|goalCount=20|goalMetToday=false|remainingCount=5",
                    KnowledgeSnippetSources.ProgressSummary)
            ],
            CancellationToken.None);

        Assert.Contains("Tiến độ học của bạn:", result);
        Assert.Contains("**15**", result);
        Assert.Contains("còn thiếu 5 thẻ", result);
    }

    [Fact]
    public async Task ComposeAsync_CardLookup_ReturnsLookupTemplate()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.CardLookup,
            "forecast nghĩa là gì",
            [
                new KnowledgeSnippet(
                    "forecast",
                    "setName=Business Terms|definition=dự báo|phonetic=/ˈfɔː.kæst/|example=Sales forecast improved.",
                    KnowledgeSnippetSources.CardLookupResult)
            ],
            CancellationToken.None);

        Assert.Contains("Mình tìm thấy từ \"forecast\"", result);
        Assert.Contains("**Định nghĩa:** dự báo", result);
        Assert.Contains("**Phiên âm:** /ˈfɔː.kæst/", result);
    }

    [Fact]
    public async Task ComposeAsync_SpeakingSuggestion_ReturnsSuggestionTemplate()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.SpeakingSuggestion,
            "gợi ý speaking",
            [
                new KnowledgeSnippet("Team Meeting Basics", "level=A2|topic=Meeting", KnowledgeSnippetSources.SpeakingSuggestion),
                new KnowledgeSnippet("Office Introductions", "level=A1|topic=Office", KnowledgeSnippetSources.SpeakingSuggestion)
            ],
            CancellationToken.None);

        Assert.Contains("Một số bài speaking phù hợp với bạn:", result);
        Assert.Contains("**Team Meeting Basics** — Level A2, Chủ đề: Meeting", result);
    }

    [Fact]
    public async Task ComposeAsync_ClassInfo_ReturnsClassTemplate()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.ClassInfo,
            "lớp học của tôi",
            [
                new KnowledgeSnippet("summary", "totalCount=2|ownerClassName=TOEIC Sprint", KnowledgeSnippetSources.ClassInfoSummary),
                new KnowledgeSnippet("TOEIC Sprint", "role=owner|memberCount=12", KnowledgeSnippetSources.ClassInfoItem),
                new KnowledgeSnippet("Speaking Club", "role=member|memberCount=8", KnowledgeSnippetSources.ClassInfoItem)
            ],
            CancellationToken.None);

        Assert.Contains("Bạn đang tham gia 2 lớp học:", result);
        Assert.Contains("Vai trò: Chủ lớp", result);
        Assert.Contains("**TOEIC Sprint**", result);
    }

    [Fact]
    public async Task ComposeAsync_WebsiteGuide_ReturnsGuideBodyAndRoute()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.WebsiteGuide,
            "cách tạo lớp học",
            [
                new KnowledgeSnippet(
                    "Tạo lớp học",
                    "1. Vào trang Lớp học.\n2. Nhấn Tạo lớp mới.",
                    KnowledgeSnippetSources.WebsiteGuide,
                    "/Home/CreateClass")
            ],
            CancellationToken.None);

        Assert.Contains("1. Vào trang Lớp học.", result);
        Assert.Contains("Bạn có thể truy cập ngay tại: /Home/CreateClass", result);
    }

    [Fact]
    public async Task ComposeAsync_WebsiteGuide_TemplateRoute_DoesNotRenderRouteLine()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.WebsiteGuide,
            "chinh sua set",
            [
                new KnowledgeSnippet(
                    "Chỉnh sửa bộ từ",
                    "1. Mở bộ từ cần chỉnh sửa.",
                    KnowledgeSnippetSources.WebsiteGuide,
                    "/Home/EditSet/{id}")
            ],
            CancellationToken.None);

        Assert.Contains("1. Mở bộ từ cần chỉnh sửa.", result);
        Assert.DoesNotContain("Bạn có thể truy cập ngay tại", result);
        Assert.DoesNotContain("{id}", result);
    }

    [Fact]
    public async Task ComposeAsync_StudyRecommendation_ReturnsStudyPlanTemplate()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.StudyRecommendation,
            "tôi nên học gì",
            [
                new KnowledgeSnippet(
                    "Daily English",
                    "remainingCount=7|streakDays=3|goalRemaining=4",
                    KnowledgeSnippetSources.StudyRecommendation)
            ],
            CancellationToken.None);

        Assert.Contains("Dựa trên dữ liệu học của bạn, mình gợi ý:", result);
        Assert.Contains("**Daily English**", result);
        Assert.Contains("Cần thêm 4 thẻ.", result);
    }

    [Fact]
    public async Task ComposeAsync_StudyRecommendation_PrefersHigherPrioritySnippet()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.StudyRecommendation,
            "tôi nên học gì",
            [
                new KnowledgeSnippet(
                    "Low Priority Set",
                    "remainingCount=8|streakDays=1|goalRemaining=2",
                    KnowledgeSnippetSources.StudyRecommendation,
                    Priority: 1),
                new KnowledgeSnippet(
                    "High Priority Set",
                    "remainingCount=3|streakDays=2|goalRemaining=1",
                    KnowledgeSnippetSources.StudyRecommendation,
                    Priority: 5)
            ],
            CancellationToken.None);

        Assert.Contains("**High Priority Set**", result);
        Assert.DoesNotContain("Low Priority Set", result);
    }

    [Fact]
    public async Task ComposeAsync_WebsiteGuide_WithBillingSnippets_ReturnsSafeBillingSummary()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.WebsiteGuide,
            "gói premium của tôi",
            [
                new KnowledgeSnippet(
                    "billing-summary",
                    "isPremium=true|planName=Monthly Premium|endsAtUtc=2026-05-30|pendingOrderCount=0|activePlanCount=2",
                    KnowledgeSnippetSources.BillingSummary,
                    "/Premium"),
                new KnowledgeSnippet(
                    "TCT-PAID-001",
                    "provider=vnpay|status=paid|amountVnd=99000|createdAtUtc=2026-04-30|planName=Monthly Premium",
                    KnowledgeSnippetSources.BillingOrderItem,
                    "/Billing/History")
            ],
            CancellationToken.None);

        Assert.Contains("Monthly Premium", result);
        Assert.Contains("TCT-PAID-001", result);
        Assert.Contains("không hiển thị URL thanh toán", result);
    }

    [Fact]
    public async Task ComposeAsync_MyProgress_WithGoalsSnippets_ReturnsGoalsSummary()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.MyProgress,
            "mục tiêu hôm nay của tôi",
            [
                new KnowledgeSnippet("goal-summary", "activeGoalCount=1|todayXp=20|totalXp=120|badgeCount=1", KnowledgeSnippetSources.GoalSummary),
                new KnowledgeSnippet("Vocabulary", "target=10|completed=6|remaining=4", KnowledgeSnippetSources.GoalAreaItem),
                new KnowledgeSnippet("Starter Badge", "awardedAt=2026-04-30", KnowledgeSnippetSources.BadgeItem)
            ],
            CancellationToken.None);

        Assert.Contains("Goals hôm nay", result);
        Assert.Contains("Vocabulary: 6/10", result);
        Assert.Contains("Starter Badge", result);
    }

    [Fact]
    public async Task ComposeAsync_OutOfScope_WithSecurityPolicy_ReturnsSecurityRefusal()
    {
        var result = await _composer.ComposeAsync(
            UserIntent.OutOfScope,
            "api key",
            [new KnowledgeSnippet("security-policy", "reason=sensitive-platform-data", KnowledgeSnippetSources.SecurityPolicy)],
            CancellationToken.None);

        Assert.Contains("dữ liệu nhạy cảm", result);
        Assert.Contains("API key", result);
    }

    [Fact]
    public async Task ComposeAsync_OutOfScope_ReturnsCanonicalRefusal()
    {
        var result = await _composer.ComposeAsync(UserIntent.OutOfScope, "giải thích ngữ pháp", [], CancellationToken.None);

        Assert.Contains("Hiện tại mình chỉ hỗ trợ câu hỏi liên quan đến dữ liệu và tính năng của TCT English", result);
        Assert.Contains("- Tôi có những bộ từ vựng nào?", result);
    }
}
