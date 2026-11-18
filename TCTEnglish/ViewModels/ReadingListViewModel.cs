namespace TCTEnglish.ViewModels
{
    public class ReadingListViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string? ImageUrl { get; set; }

        public string Level { get; set; } = "A1";

        public string? Topic { get; set; }

        public string? Description { get; set; }

        public int? EstimatedTimeMinutes { get; set; }

        public int QuestionCount { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsInProgress { get; set; }

        /// <summary>Short blurb for card hover/mobile reveal; falls back to topic + level.</summary>
        public string ShortDescription
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Description))
                {
                    var text = Description.Trim();
                    return text.Length <= 120 ? text : text[..117].TrimEnd() + "…";
                }

                if (!string.IsNullOrWhiteSpace(Topic))
                    return $"Bài đọc chủ đề {Topic.Trim()} · trình độ {Level}";

                return $"Bài đọc hiểu trình độ {Level}";
            }
        }

        /// <summary>Stable CSS accent key derived from topic (or level).</summary>
        public string TopicAccentKey
        {
            get
            {
                var raw = (Topic ?? Level ?? "default").Trim().ToLowerInvariant();
                if (raw.Contains("tech") || raw.Contains("ai") || raw.Contains("công nghệ"))
                    return "tech";
                if (raw.Contains("environ") || raw.Contains("planet") || raw.Contains("môi trường"))
                    return "environment";
                if (raw.Contains("educat") || raw.Contains("learn") || raw.Contains("giáo dục") || raw.Contains("học"))
                    return "education";
                if (raw.Contains("daily") || raw.Contains("life") || raw.Contains("đời sống") || raw.Contains("ngày"))
                    return "daily";
                return "default";
            }
        }
    }
}
