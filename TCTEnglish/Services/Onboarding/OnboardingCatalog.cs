namespace TCTEnglish.Services.Onboarding;

/// <summary>One learning goal the learner can pick in step 1. Title and description are the literal UI copy.</summary>
public sealed record OnboardingGoal(string Key, string Title, string Description, string Icon);

/// <summary>One system vocabulary set offered as a topic in step 2. <see cref="Key"/> is the set code (A01..C08).</summary>
public sealed record OnboardingTopic(string Key, string SetName)
{
    /// <summary>Phrase sets (folder C) ignore the CEFR window.</summary>
    public bool IsPhrase => Key[0] == 'C';

    public string Band => Key[0] switch { 'A' => "A1–A2", 'B' => "B1–B2", _ => "Giao tiếp" };
}

/// <summary>
/// The fixed lists behind the first-run flow: goals, the 48 topics (system sets) and which topics fit which goal.
/// Static and deterministic: a test checks every topic against the committed manifests by code and exact name.
/// </summary>
public static class OnboardingCatalog
{
    public const int MaxGoals = 3;
    public const int MinTopics = 3;
    public const int MaxTopics = 8;

    public static readonly IReadOnlyList<string> Levels = new[] { "A1", "A2", "B1", "B2" };
    public static readonly IReadOnlyList<int> Paces = new[] { 5, 10, 15, 20 };

    public static readonly IReadOnlyList<OnboardingGoal> Goals = new OnboardingGoal[]
    {
        new("communication", "Giao tiếp hằng ngày", "Chào hỏi, gọi món, hỏi đường, nói chuyện xã giao", "fa-solid fa-comments"),
        new("travel", "Du lịch", "Sân bay, khách sạn, phương tiện, mua sắm", "fa-solid fa-plane"),
        new("work", "Công việc", "Văn phòng, email, họp, kinh doanh", "fa-solid fa-briefcase"),
        new("study", "Học ở trường & thi cử", "Từ vựng học thuật, giáo dục, khoa học", "fa-solid fa-graduation-cap"),
        new("hobbies", "Phim, nhạc & sở thích", "Giải trí, thể thao, nghệ thuật", "fa-solid fa-film"),
        new("beginner", "Bắt đầu từ con số 0", "Những từ cơ bản nhất cho người mới", "fa-solid fa-seedling"),
    };

    public static readonly IReadOnlyList<OnboardingTopic> Topics = new OnboardingTopic[]
    {
        new("A01", "Động vật nuôi & trang trại"),
        new("A02", "Động vật hoang dã"),
        new("A03", "Trái cây"),
        new("A04", "Rau củ"),
        new("A05", "Món ăn hằng ngày"),
        new("A06", "Đồ uống"),
        new("A07", "Cơ thể người"),
        new("A08", "Quần áo & phụ kiện"),
        new("A09", "Gia đình & con người"),
        new("A10", "Nhà cửa & các phòng"),
        new("A11", "Nội thất & đồ gia dụng"),
        new("A12", "Nhà bếp & dụng cụ nấu ăn"),
        new("A13", "Trường học & đồ dùng học tập"),
        new("A14", "Nghề nghiệp"),
        new("A15", "Địa điểm trong thành phố"),
        new("A16", "Phương tiện giao thông"),
        new("A17", "Thời tiết & các mùa"),
        new("A18", "Thiên nhiên & cảnh quan"),
        new("A19", "Thể thao"),
        new("A20", "Sở thích & giải trí"),
        new("A21", "Nhạc cụ & nghệ thuật"),
        new("A22", "Màu sắc & hình dạng"),
        new("A23", "Cảm xúc"),
        new("A24", "Sức khỏe & bệnh thường gặp"),
        new("B01", "Công việc & văn phòng"),
        new("B02", "Công nghệ & internet"),
        new("B03", "Môi trường"),
        new("B04", "Du lịch & sân bay"),
        new("B05", "Mua sắm & tiền bạc"),
        new("B06", "Nấu ăn & chế biến"),
        new("B07", "Sức khỏe & thể chất"),
        new("B08", "Giáo dục & học tập"),
        new("B09", "Truyền thông & tin tức"),
        new("B10", "Thành phố & nhà ở"),
        new("B11", "Khoa học & vũ trụ"),
        new("B12", "Thể thao & sự kiện"),
        new("B13", "Nghệ thuật & văn hóa"),
        new("B14", "Pháp luật & xã hội"),
        new("B15", "Các mối quan hệ"),
        new("B16", "Kinh doanh & khởi nghiệp"),
        new("C01", "Chào hỏi & giới thiệu"),
        new("C02", "Lịch sự: cảm ơn, xin lỗi, nhờ vả"),
        new("C03", "Gọi món ở nhà hàng"),
        new("C04", "Hỏi và chỉ đường"),
        new("C05", "Mua sắm & trả giá"),
        new("C06", "Gọi điện thoại"),
        new("C07", "Ở sân bay & khách sạn"),
        new("C08", "Nói chuyện xã giao"),
    };

    // The topics that fit each goal, judged against the set names. A topic may fit several goals.
    private static readonly IReadOnlyDictionary<string, string[]> GoalTopics = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["travel"] = new[] { "A16", "A15", "B04", "B05", "C04", "C05", "C07" },
        ["communication"] = new[] { "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "A09", "A23" },
        ["work"] = new[] { "A14", "B01", "B02", "B16", "C06" },
        ["study"] = new[] { "A13", "B08", "B11", "B09", "B03" },
        ["hobbies"] = new[] { "A19", "A20", "A21", "B12", "B13", "A03", "A06", "A05" },
        ["beginner"] = new[]
        {
            "A01", "A02", "A03", "A04", "A05", "A06", "A07", "A08", "A09", "A10", "A11", "A12", "A17", "A18", "A22", "A24"
        },
    };

    private static readonly IReadOnlyDictionary<string, OnboardingTopic> TopicByKey =
        Topics.ToDictionary(topic => topic.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, OnboardingGoal> GoalByKey =
        Goals.ToDictionary(goal => goal.Key, StringComparer.Ordinal);

    public static bool IsGoal(string? key) => key != null && GoalByKey.ContainsKey(key);

    public static bool TryGetTopic(string? key, out OnboardingTopic topic)
    {
        if (key != null && TopicByKey.TryGetValue(key, out var found))
        {
            topic = found;
            return true;
        }

        topic = null!;
        return false;
    }

    public static OnboardingTopic Topic(string key) => TopicByKey[key];

    public static IReadOnlyList<string> TopicsOfGoal(string goalKey) =>
        GoalTopics.TryGetValue(goalKey, out var topics) ? topics : Array.Empty<string>();

    public static IReadOnlyList<string> GoalsOfTopic(string topicKey) =>
        Goals.Where(goal => TopicsOfGoal(goal.Key).Contains(topicKey)).Select(goal => goal.Key).ToList();

    /// <summary>A1 for someone starting from zero, otherwise A2.</summary>
    public static string DefaultLevel(IEnumerable<string> goals) => goals.Contains("beginner") ? "A1" : "A2";

    /// <summary>How many days it takes to meet every word at <paramref name="pace"/> new words a day.</summary>
    public static int EstimateDays(int words, int pace) => pace <= 0 ? 0 : (words + pace - 1) / pace;
}
