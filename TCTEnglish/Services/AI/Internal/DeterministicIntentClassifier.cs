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

        if (IsClearlyOutOfScope(normalizedMessage))
        {
            return Create(UserIntent.OutOfScope, 0.3f);
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

    private static bool IsClearlyOutOfScope(string normalizedMessage)
    {
        if (AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "present perfect",
            "past simple",
            "passive voice",
            "viet essay",
            "viet doan van",
            "cover letter",
            "giup toi lam bai",
            "lam bai nay",
            "lam bai reading nay",
            "tra loi bai reading",
            "giai bai reading",
            "thoi tiet",
            "thu do",
            "random fact"))
        {
            return true;
        }

        var hasPlatformFeatureContext = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "reading",
            "writing",
            "listening",
            "tct english",
            "website",
            "trang web",
            "chatbox",
            "he thong");

        return !hasPlatformFeatureContext
            && AiTextNormalizer.ContainsAny(
                normalizedMessage,
                "giai thich",
                "ngu phap",
                "grammar",
                "dich giup",
                "dich cau",
                "dich doan");
    }

    private static bool IsWebsiteGuide(string normalizedMessage)
    {
        var hasGuideVerb = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cach",
            "huong dan",
            "lam sao",
            "lam bai",
            "how to",
            "su dung",
            "tinh nang",
            "hoat dong",
            "ra sao",
            "nhu the nao",
            "gom",
            "o dau",
            "bat dau",
            "gioi thieu",
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
            "reading",
            "read mode",
            "writing",
            "listening",
            "quiz",
            "flashcard",
            "matching",
            "write",
            "goal",
            "goals",
            "muc tieu",
            "huy hieu",
            "badge",
            "daily challenge",
            "thu thach",
            "cau hoi moi ngay",
            "streak",
            "tai khoan",
            "profile",
            "mat khau",
            "contact",
            "support",
            "lien he",
            "ho tro",
            "chat lop",
            "class chat",
            "nhan tin",
            "gui anh",
            "sap xep",
            "to chuc",
            "di chuyen set",
            "thong bao",
            "notification",
            "bell",
            "chuong",
            "website",
            "trang web",
            "tct english",
            "tong quan",
            "overview",
            "navigation",
            "dieu huong",
            "menu",
            "dashboard",
            "about",
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
