namespace TCTEnglish.Services.AI.Internal;

public sealed class DeterministicIntentClassifier : IAiQueryClassifier
{
    public IntentClassification Classify(string userMessage)
    {
        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);

        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return Create(UserIntent.OutOfScope, 0.2f);
        }

        if (IsGreeting(normalizedMessage))
        {
            return Create(UserIntent.Greeting, 1.0f);
        }

        if (IsWebsiteGuide(normalizedMessage))
        {
            return Create(UserIntent.WebsiteGuide, 0.96f);
        }

        if (IsCardLookup(normalizedMessage))
        {
            return Create(UserIntent.CardLookup, 0.92f);
        }

        if (IsMyProgress(normalizedMessage))
        {
            return Create(UserIntent.MyProgress, 0.94f);
        }

        if (IsSpeakingSuggestion(normalizedMessage))
        {
            return Create(UserIntent.SpeakingSuggestion, 0.93f);
        }

        if (IsStudyRecommendation(normalizedMessage))
        {
            return Create(UserIntent.StudyRecommendation, 0.9f);
        }

        if (IsMyVocabulary(normalizedMessage))
        {
            return Create(UserIntent.MyVocabulary, 0.95f);
        }

        if (IsClassInfo(normalizedMessage))
        {
            return Create(UserIntent.ClassInfo, 0.93f);
        }

        return Create(UserIntent.OutOfScope, 0.25f);
    }

    private static IntentClassification Create(UserIntent intent, float confidence)
        => new(intent, confidence, "keyword");

    private static bool IsGreeting(string normalizedMessage)
    {
        if (AiTextNormalizer.ContainsAny(
                normalizedMessage,
                "xin chao",
                "chao",
                "hello",
                "hi",
                "hey",
                "good morning",
                "good afternoon",
                "good evening"))
        {
            return true;
        }

        return normalizedMessage.Length <= 24
            && AiTextNormalizer.ContainsAny(normalizedMessage, "ban la ai", "ai day", "tro ly", "co o do khong");
    }

    private static bool IsWebsiteGuide(string normalizedMessage)
    {
        var hasGuideVerb = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cach",
            "huong dan",
            "lam sao",
            "how to",
            "su dung",
            "tao",
            "them",
            "chinh sua",
            "xoa",
            "doi mat khau",
            "roi");

        var hasFeatureKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "bo tu",
            "tu vung",
            "set",
            "the",
            "folder",
            "lop hoc",
            "class",
            "speaking",
            "playlist",
            "shadowing",
            "dictation",
            "quiz",
            "flashcard",
            "matching",
            "write",
            "goal",
            "streak",
            "tai khoan",
            "profile",
            "chatbox",
            "ai");

        return hasGuideVerb && hasFeatureKeyword;
    }

    private static bool IsMyVocabulary(string normalizedMessage)
    {
        var hasVocabularyKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "bo tu",
            "tu vung",
            "set",
            "flashcard");

        var hasPersonalQuery = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cua toi",
            "toi co",
            "co nhung",
            "bao nhieu",
            "danh sach",
            "list",
            "xem");

        return hasVocabularyKeyword && hasPersonalQuery;
    }

    private static bool IsMyProgress(string normalizedMessage)
    {
        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "tien do",
            "progress",
            "streak",
            "thanh thao",
            "mastered",
            "dang hoc",
            "chua bat dau",
            "goal",
            "muc tieu",
            "hom nay hoc den dau");
    }

    private static bool IsCardLookup(string normalizedMessage)
    {
        var hasLookupKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "nghia la gi",
            "dinh nghia",
            "tra tu",
            "tim tu",
            "phien am",
            "vi du",
            "example",
            "word",
            "term");

        if (hasLookupKeyword)
        {
            return true;
        }

        var tokens = AiTextNormalizer.Tokenize(normalizedMessage);
        return tokens.Count <= 3
            && !AiTextNormalizer.ContainsAny(normalizedMessage, "speaking", "lop hoc", "set", "bo tu", "tien do");
    }

    private static bool IsSpeakingSuggestion(string normalizedMessage)
    {
        var hasSpeakingKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "speaking",
            "shadowing",
            "dictation",
            "listening",
            "playlist",
            "video");

        var hasSuggestionKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "goi y",
            "phu hop",
            "nen hoc",
            "de xuat",
            "bai nao",
            "video nao");

        return hasSpeakingKeyword && hasSuggestionKeyword;
    }

    private static bool IsClassInfo(string normalizedMessage)
    {
        var hasClassKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "lop hoc",
            "lop",
            "class");

        var hasInfoKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cua toi",
            "tham gia",
            "dang tham gia",
            "dang o",
            "owner",
            "thanh vien",
            "bao nhieu lop",
            "lop nao");

        return hasClassKeyword && hasInfoKeyword;
    }

    private static bool IsStudyRecommendation(string normalizedMessage)
    {
        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "nen hoc gi",
            "hoc gi tiep",
            "goi y hoc",
            "goi y on tap",
            "de xuat hoc",
            "on tap",
            "on tap gi",
            "recommend");
    }
}
