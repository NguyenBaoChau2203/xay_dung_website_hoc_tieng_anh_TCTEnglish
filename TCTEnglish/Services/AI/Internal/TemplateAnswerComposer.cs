namespace TCTEnglish.Services.AI.Internal;

public sealed class TemplateAnswerComposer : IAnswerComposer
{
    public Task<string> ComposeAsync(
        UserIntent intent,
        string userMessage,
        IReadOnlyList<KnowledgeSnippet> snippets,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var answer = intent switch
        {
            UserIntent.Greeting => ComposeGreeting(),
            UserIntent.MyVocabulary => ComposeMyVocabulary(snippets),
            UserIntent.MyProgress => ComposeMyProgress(snippets),
            UserIntent.CardLookup => ComposeCardLookup(userMessage, snippets),
            UserIntent.SpeakingSuggestion => ComposeSpeakingSuggestion(snippets),
            UserIntent.ClassInfo => ComposeClassInfo(snippets),
            UserIntent.WebsiteGuide => ComposeWebsiteGuide(snippets),
            UserIntent.StudyRecommendation => ComposeStudyRecommendation(snippets),
            _ => ComposeOutOfScope()
        };

        return Task.FromResult(answer);
    }

    private static string ComposeGreeting()
    {
        return string.Join(Environment.NewLine, [
            "Chào bạn! 👋 Mình là trợ lý TCT English. Mình có thể giúp bạn:",
            "- Xem bộ từ vựng và tiến độ học",
            "- Gợi ý bài speaking phù hợp",
            "- Tra nghĩa từ trong bộ thẻ của bạn",
            "- Hướng dẫn cách sử dụng website",
            string.Empty,
            "Bạn muốn hỏi về điều gì?"
        ]);
    }

    private static string ComposeMyVocabulary(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var totalCount = GetSummaryInt(snippets, KnowledgeSnippetSources.UserVocabularySummary, "totalCount");
        var setSnippets = snippets
            .Where(x => x.Source == KnowledgeSnippetSources.UserVocabularySet)
            .Take(5)
            .ToList();

        if (totalCount <= 0 || setSnippets.Count == 0)
        {
            return string.Join(Environment.NewLine, [
                "Hiện tại mình chưa tìm thấy bộ từ vựng nào trong tài khoản của bạn.",
                string.Empty,
                "Bạn có thể tạo bộ từ đầu tiên tại trang **Bộ từ vựng** → **Tạo bộ từ mới**."
            ]);
        }

        var lines = new List<string>
        {
            $"Bạn hiện có {totalCount} bộ từ vựng:"
        };

        foreach (var setSnippet in setSnippets)
        {
            var cardCount = GetIntValue(setSnippet, "cardCount");
            lines.Add($"- **{setSnippet.Title}**: {cardCount} thẻ");
        }

        var remainingCount = Math.Max(0, totalCount - setSnippets.Count);
        if (remainingCount > 0)
        {
            lines.Add($"... và {remainingCount} bộ khác.");
        }

        lines.Add(string.Empty);
        lines.Add("Bạn có muốn xem chi tiết bộ nào hoặc bắt đầu ôn tập không?");
        return string.Join(Environment.NewLine, lines);
    }

    private static string ComposeMyProgress(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var summary = snippets.FirstOrDefault(x => x.Source == KnowledgeSnippetSources.ProgressSummary);
        if (summary is null)
        {
            return string.Join(Environment.NewLine, [
                "Mình chưa tìm thấy dữ liệu tiến độ học của bạn.",
                string.Empty,
                "Hãy bắt đầu học một bộ từ vựng để hệ thống ghi nhận tiến độ nhé!"
            ]);
        }

        var streakDays = GetIntValue(summary, "streakDays");
        var masteredCount = GetIntValue(summary, "masteredCount");
        var learningCount = GetIntValue(summary, "learningCount");
        var newCount = GetIntValue(summary, "newCount");
        var goalCount = GetIntValue(summary, "goalCount");
        var goalMetToday = GetBoolValue(summary, "goalMetToday");
        var remainingCount = GetIntValue(summary, "remainingCount");

        var lines = new List<string>
        {
            "Tiến độ học của bạn:",
            $"- Streak hiện tại: **{streakDays} ngày** liên tiếp",
            $"- Thẻ đã thành thạo: **{masteredCount}**",
            $"- Thẻ đang học: **{learningCount}**",
            $"- Thẻ chưa bắt đầu: **{newCount}**",
            $"- Mục tiêu học hằng ngày: **{goalCount} thẻ/ngày**",
            string.Empty
        };

        if (goalMetToday)
        {
            lines.Add("Hôm nay bạn đã đạt mục tiêu. Tốt lắm!");
        }
        else if (remainingCount > 0)
        {
            lines.Add($"Hôm nay bạn còn thiếu {remainingCount} thẻ để đạt mục tiêu.");
        }
        else if (streakDays <= 0)
        {
            lines.Add("Bạn chưa có streak. Học ngày hôm nay để bắt đầu chuỗi nhé!");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ComposeCardLookup(string userMessage, IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var results = snippets
            .Where(x => x.Source == KnowledgeSnippetSources.CardLookupResult)
            .ToList();

        if (results.Count == 0)
        {
            var safeTerm = string.IsNullOrWhiteSpace(userMessage) ? "đó" : userMessage.Trim();
            return string.Join(Environment.NewLine, [
                $"Mình không tìm thấy từ \"{safeTerm}\" trong các bộ từ vựng của bạn.",
                string.Empty,
                "Bạn có thể thêm từ này vào bộ từ mới hoặc tìm kiếm với từ khác."
            ]);
        }

        if (results.Count == 1)
        {
            var result = results[0];
            var setName = GetStringValue(result, "setName");
            var definition = GetStringValue(result, "definition");
            var phonetic = GetStringValue(result, "phonetic");
            var example = GetStringValue(result, "example");

            var lines = new List<string>
            {
                $"Mình tìm thấy từ \"{result.Title}\" trong bộ từ **{setName}**:",
                string.Empty,
                $"- **Từ:** {result.Title}",
                $"- **Định nghĩa:** {definition}"
            };

            if (!string.IsNullOrWhiteSpace(phonetic))
            {
                lines.Add($"- **Phiên âm:** {phonetic}");
            }

            if (!string.IsNullOrWhiteSpace(example))
            {
                lines.Add($"- **Ví dụ:** {example}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        var multiLines = new List<string>
        {
            $"Từ này xuất hiện trong {results.Count} bộ từ:"
        };

        foreach (var result in results)
        {
            var setName = GetStringValue(result, "setName");
            var definition = GetStringValue(result, "definition");
            multiLines.Add($"- **{setName}**: {definition}");
        }

        multiLines.Add(string.Empty);
        multiLines.Add("Bạn có thể mở bộ chứa từ này để xem thêm chi tiết.");
        return string.Join(Environment.NewLine, multiLines);
    }

    private static string ComposeSpeakingSuggestion(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var suggestions = snippets
            .Where(x => x.Source == KnowledgeSnippetSources.SpeakingSuggestion)
            .Take(3)
            .ToList();

        if (suggestions.Count == 0)
        {
            return string.Join(Environment.NewLine, [
                "Mình chưa tìm thấy bài speaking phù hợp trong hệ thống lúc này.",
                string.Empty,
                "Bạn có thể vào trang **Speaking** để duyệt toàn bộ playlist và video theo trình độ."
            ]);
        }

        var lines = new List<string>
        {
            "Một số bài speaking phù hợp với bạn:"
        };

        foreach (var suggestion in suggestions)
        {
            var level = GetStringValue(suggestion, "level");
            var topic = GetStringValue(suggestion, "topic");
            lines.Add($"- **{suggestion.Title}** — Level {level}, Chủ đề: {topic}");
        }

        lines.Add(string.Empty);
        lines.Add("Vào trang **Speaking** để xem danh sách đầy đủ và bắt đầu luyện tập.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string ComposeClassInfo(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var totalCount = GetSummaryInt(snippets, KnowledgeSnippetSources.ClassInfoSummary, "totalCount");
        var ownerClassName = GetSummaryString(snippets, KnowledgeSnippetSources.ClassInfoSummary, "ownerClassName");
        var classItems = snippets
            .Where(x => x.Source == KnowledgeSnippetSources.ClassInfoItem)
            .Take(5)
            .ToList();

        if (totalCount <= 0 || classItems.Count == 0)
        {
            return string.Join(Environment.NewLine, [
                "Bạn chưa tham gia lớp học nào.",
                string.Empty,
                "Bạn có thể vào trang **Lớp học** để tạo lớp mới hoặc tham gia lớp học bằng mã mời."
            ]);
        }

        var lines = new List<string>
        {
            $"Bạn đang tham gia {totalCount} lớp học:"
        };

        foreach (var classItem in classItems)
        {
            var role = GetStringValue(classItem, "role");
            var memberCount = GetIntValue(classItem, "memberCount");
            var displayRole = string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase)
                ? "Chủ lớp"
                : "Thành viên";

            lines.Add($"- **{classItem.Title}** — Vai trò: {displayRole}, Thành viên: {memberCount}");
        }

        if (!string.IsNullOrWhiteSpace(ownerClassName))
        {
            lines.Add(string.Empty);
            lines.Add($"Bạn là Chủ lớp của lớp **{ownerClassName}**.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ComposeWebsiteGuide(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var guide = snippets.FirstOrDefault(x => x.Source == KnowledgeSnippetSources.WebsiteGuide);
        if (guide is null)
        {
            return string.Join(Environment.NewLine, [
                "Mình chưa tìm thấy hướng dẫn phù hợp với câu hỏi của bạn.",
                string.Empty,
                "Các chủ đề mình có thể hỗ trợ bao gồm: tạo bộ từ, tạo lớp học, các chế độ học, luyện speaking và cài đặt tài khoản.",
                "Bạn có thể hỏi lại theo một trong những chủ đề này."
            ]);
        }

        if (string.IsNullOrWhiteSpace(guide.Route) || IsTemplateRoute(guide.Route))
        {
            return guide.Body.Trim();
        }

        return string.Join(Environment.NewLine, [
            guide.Body.Trim(),
            string.Empty,
            $"Bạn có thể truy cập ngay tại: {guide.Route}"
        ]);
    }

    private static bool IsTemplateRoute(string route)
    {
        return route.Contains('{', StringComparison.Ordinal)
            || route.Contains('}', StringComparison.Ordinal);
    }

    private static string ComposeStudyRecommendation(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var recommendation = SelectStudyRecommendation(snippets);
        if (recommendation is null)
        {
            return string.Join(Environment.NewLine, [
                "Mình chưa có đủ dữ liệu học để đưa ra gợi ý cụ thể.",
                string.Empty,
                "Hãy tạo bộ từ đầu tiên và bắt đầu học để mình có thể theo dõi tiến độ và đề xuất phù hợp hơn."
            ]);
        }

        var remainingCount = GetIntValue(recommendation, "remainingCount");
        var streakDays = GetIntValue(recommendation, "streakDays");
        var goalRemaining = GetIntValue(recommendation, "goalRemaining");

        var lines = new List<string>
        {
            "Dựa trên dữ liệu học của bạn, mình gợi ý:",
            $"- Ôn lại bộ **{recommendation.Title}** — còn {remainingCount} thẻ chưa thành thạo."
        };

        if (streakDays > 0)
        {
            lines.Add($"- Streak hiện tại: {streakDays} ngày. Học hôm nay để duy trì chuỗi!");
        }

        if (goalRemaining > 0)
        {
            lines.Add($"- Bạn chưa đạt mục tiêu hôm nay. Cần thêm {goalRemaining} thẻ.");
        }

        lines.Add(string.Empty);
        lines.Add("Chế độ ôn tập phù hợp: Flashcard hoặc Quiz.");
        return string.Join(Environment.NewLine, lines);
    }

    private static KnowledgeSnippet? SelectStudyRecommendation(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var candidates = snippets
            .Where(x => x.Source == KnowledgeSnippetSources.StudyRecommendation)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(candidate => candidate.Priority)
            .ThenByDescending(candidate => GetIntValue(candidate, "remainingCount"))
            .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static string ComposeOutOfScope()
    {
        return string.Join(Environment.NewLine, [
            "Hiện tại mình chỉ hỗ trợ câu hỏi liên quan đến dữ liệu và tính năng của TCT English như bộ từ vựng, tiến độ học, lớp học, speaking và cách sử dụng website.",
            string.Empty,
            "Bạn có thể hỏi theo các dạng như:",
            "- Tôi có những bộ từ vựng nào?",
            "- Tiến độ học của tôi ra sao?",
            "- Gợi ý bài speaking phù hợp",
            "- Cách tạo lớp học hoặc tạo set"
        ]);
    }

    private static int GetSummaryInt(IReadOnlyList<KnowledgeSnippet> snippets, string source, string key)
    {
        var summarySnippet = snippets.FirstOrDefault(x => x.Source == source);
        return summarySnippet is null ? 0 : GetIntValue(summarySnippet, key);
    }

    private static string GetSummaryString(IReadOnlyList<KnowledgeSnippet> snippets, string source, string key)
    {
        var summarySnippet = snippets.FirstOrDefault(x => x.Source == source);
        return summarySnippet is null ? string.Empty : GetStringValue(summarySnippet, key);
    }

    private static int GetIntValue(KnowledgeSnippet snippet, string key)
    {
        var value = GetStringValue(snippet, key);
        return int.TryParse(value, out var parsedValue) ? parsedValue : 0;
    }

    private static bool GetBoolValue(KnowledgeSnippet snippet, string key)
    {
        var value = GetStringValue(snippet, key);
        return bool.TryParse(value, out var parsedValue) && parsedValue;
    }

    private static string GetStringValue(KnowledgeSnippet snippet, string key)
    {
        var metadata = ParseMetadata(snippet.Body);
        return metadata.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static Dictionary<string, string> ParseMetadata(string body)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body) || !body.Contains('='))
        {
            return metadata;
        }

        foreach (var segment in body.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }
}
