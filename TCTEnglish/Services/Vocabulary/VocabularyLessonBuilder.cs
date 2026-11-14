namespace TCTVocabulary.Services.Vocabulary
{
    /// <summary>A card as the lesson builder sees it: only its id and topic.</summary>
    public sealed record LessonCard(int CardId, string? Topic);

    /// <summary>One computed lesson of a course. Lessons are never stored; they follow the current cards.</summary>
    public sealed record LessonPlan(int Number, string Topic, int Part, int PartCount, IReadOnlyList<int> CardIds)
    {
        public string Title => PartCount > 1
            ? $"Bài {Number} · {Topic} · phần {Part}"
            : $"Bài {Number} · {Topic}";
    }

    /// <summary>
    /// Groups the cards of one course into lessons of about <c>size</c> words.
    /// Pure and deterministic: the same cards always give the same lessons.
    /// </summary>
    public static class VocabularyLessonBuilder
    {
        public const string DefaultTopic = "Từ vựng chung";
        public const int DefaultSize = 10;
        public const int DefaultMinTail = 4;

        public static IReadOnlyList<LessonPlan> Build(
            IReadOnlyList<LessonCard> cards,
            int size = DefaultSize,
            int minTail = DefaultMinTail)
        {
            if (cards == null || cards.Count == 0)
            {
                return Array.Empty<LessonPlan>();
            }

            size = Math.Max(1, size);
            minTail = Math.Max(1, minTail);

            var groups = cards
                .OrderBy(card => card.CardId)
                .GroupBy(card => NormalizeTopic(card.Topic), StringComparer.Ordinal)
                .ToList();

            var lessons = new List<LessonPlan>();
            var number = 0;

            foreach (var group in groups)
            {
                var ids = group.Select(card => card.CardId).ToList();
                var parts = new List<List<int>>();

                for (var offset = 0; offset < ids.Count; offset += size)
                {
                    parts.Add(ids.Skip(offset).Take(size).ToList());
                }

                if (parts.Count > 1 && parts[^1].Count < minTail)
                {
                    parts[^2].AddRange(parts[^1]);
                    parts.RemoveAt(parts.Count - 1);
                }

                for (var index = 0; index < parts.Count; index++)
                {
                    number++;
                    lessons.Add(new LessonPlan(number, group.Key, index + 1, parts.Count, parts[index]));
                }
            }

            return lessons;
        }

        public static string NormalizeTopic(string? topic)
        {
            return string.IsNullOrWhiteSpace(topic) ? DefaultTopic : topic.Trim();
        }
    }
}
