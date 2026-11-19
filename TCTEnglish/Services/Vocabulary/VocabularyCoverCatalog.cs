namespace TCTVocabulary.Services.Vocabulary
{
    /// <summary>A card of a course that has a usable image, as the cover picker sees it.</summary>
    public sealed record CoverCandidate(int CardId, string Term, string ImageUrl);

    /// <summary>The picture chosen for a course cover and its short label.</summary>
    public sealed record CoverChoice(string ImageUrl, string Term);

    /// <summary>
    /// The cover word of each system course: the term whose own reviewed card image becomes the course cover.
    /// Courses are matched by their exact name (trimmed, ordinal), the same convention as the seeder and the
    /// shared-data packages. A <c>null</c> term means "no single picture fits, show the illustration".
    /// A course that is not listed falls back to its first card that has an image.
    /// The same data lives in <c>data/content/vocabulary/covers.json</c>; a test keeps the two in sync.
    /// </summary>
    public static class VocabularyCoverCatalog
    {
        public static readonly IReadOnlyDictionary<string, string?> Terms = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            // Folder A: basic topics
            ["Động vật nuôi & trang trại"] = "cow",
            ["Động vật hoang dã"] = "tiger",
            ["Trái cây"] = "watermelon",
            ["Rau củ"] = "carrot",
            ["Món ăn hằng ngày"] = "pizza",
            ["Đồ uống"] = "juice",
            ["Cơ thể người"] = "hand",
            ["Quần áo & phụ kiện"] = "jacket",
            ["Gia đình & con người"] = "family",
            ["Nhà cửa & các phòng"] = "house",
            ["Nội thất & đồ gia dụng"] = "sofa",
            ["Nhà bếp & dụng cụ nấu ăn"] = "frying pan",
            ["Trường học & đồ dùng học tập"] = "backpack",
            ["Nghề nghiệp"] = "chef",
            ["Địa điểm trong thành phố"] = "bakery",
            ["Phương tiện giao thông"] = "bus",
            ["Thời tiết & các mùa"] = "rainbow",
            ["Thiên nhiên & cảnh quan"] = "mountain",
            ["Thể thao"] = "football",
            ["Sở thích & giải trí"] = "chess",
            ["Nhạc cụ & nghệ thuật"] = "guitar",
            ["Màu sắc & hình dạng"] = "star",
            ["Cảm xúc"] = "happy",
            ["Sức khỏe & bệnh thường gặp"] = "medicine",

            // Folder B: intermediate topics
            ["Công việc & văn phòng"] = "laptop",
            ["Công nghệ & internet"] = "smartphone",
            ["Môi trường"] = "solar panel",
            ["Du lịch & sân bay"] = "boarding pass",
            ["Mua sắm & tiền bạc"] = "shopping bag",
            ["Nấu ăn & chế biến"] = "saucepan",
            ["Sức khỏe & thể chất"] = "dumbbell",
            ["Giáo dục & học tập"] = "graduation gown",
            ["Truyền thông & tin tức"] = "newspaper",
            ["Thành phố & nhà ở"] = "skyscraper",
            ["Khoa học & vũ trụ"] = "telescope",
            ["Thể thao & sự kiện"] = "trophy",
            ["Nghệ thuật & văn hóa"] = "easel",
            ["Pháp luật & xã hội"] = "judge",
            ["Các mối quan hệ"] = "couple",
            ["Kinh doanh & khởi nghiệp"] = "bar chart",

            // Folder C: everyday phrases (the first phrase of each scene)
            ["Chào hỏi & giới thiệu"] = "Hello!",
            ["Lịch sự: cảm ơn, xin lỗi, nhờ vả"] = "Thank you.",
            ["Gọi món ở nhà hàng"] = "A table for two, please.",
            ["Hỏi và chỉ đường"] = "Where is the station?",
            ["Mua sắm & trả giá"] = "How much is this?",
            ["Gọi điện thoại"] = "Hello, this is Anna speaking.",
            ["Ở sân bay & khách sạn"] = "Where is the check-in desk?",
            ["Nói chuyện xã giao"] = "What a lovely day!",

            // Older sets
            ["Đồ ăn & Thức uống"] = "Rice",
            ["Du lịch & Địa điểm"] = "Airport",
            ["Màu sắc"] = "Red",
            ["1000 từ tiếng Anh thông dụng"] = null,
            ["Từ vựng tiếng Anh giao tiếp"] = null,
            ["3000 từ vựng Oxford A1"] = null,
            ["3000 từ vựng Oxford A2"] = null
        };

        /// <summary>
        /// The cover of one course.
        /// <list type="bullet">
        /// <item>Listed with a term: that card's image (term compared ignoring case and spaces at the ends).</item>
        /// <item>Listed with <c>null</c>: no image, the caller shows the illustration.</item>
        /// <item>Not listed, or the listed term has no image: the first card by id that has an image.</item>
        /// </list>
        /// </summary>
        public static CoverChoice? Pick(string? setName, IReadOnlyList<CoverCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var listed = Terms.TryGetValue((setName ?? string.Empty).Trim(), out var term);
            if (listed && term == null)
            {
                return null;
            }

            if (listed)
            {
                var wanted = term!.Trim();
                var match = candidates
                    .Where(card => string.Equals(card.Term?.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(card => card.CardId)
                    .FirstOrDefault();
                if (match != null)
                {
                    return new CoverChoice(match.ImageUrl, match.Term.Trim());
                }
            }

            var first = candidates.OrderBy(card => card.CardId).First();
            return new CoverChoice(first.ImageUrl, first.Term?.Trim() ?? string.Empty);
        }
    }
}
