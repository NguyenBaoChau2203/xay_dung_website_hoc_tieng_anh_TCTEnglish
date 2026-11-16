using System.Text.Json;
using System.Text.RegularExpressions;
using TCTEnglish.Services.Writing;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure.WritingContent;

public sealed record WritingContentSentence(int SortOrder, string VietnameseText, string EnglishMeaning,
    bool BreakAfter, List<string> ApprovedAlternatives);
public sealed record WritingContentLesson(string Key, string Level, string ContentType, string Title,
    string Topic, string PreviewText, string SourceType, List<WritingContentSentence> Sentences);

/// <summary>Explicit offline authoring/import contract. Never registered in application startup.</summary>
public static class WritingContentBatch
{
    public static readonly string[] Levels = ["beginner", "intermediate", "advanced"];
    public static readonly string[] Formats = ["emails", "diaries", "essays", "articles", "stories", "reports"];
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void Validate(IReadOnlyList<WritingContentLesson> lessons, string level)
    {
        Require(Levels.Contains(level), "Unknown level.");
        Require(lessons.Count == 36, "A batch must contain 36 complete lessons.");
        Require(lessons.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == 36, "Duplicate stable key.");
        Require(lessons.Select(x => Normalize(x.Title)).Distinct().Count() == 36, "Duplicate title.");
        foreach (var format in Formats) Require(lessons.Count(x => x.ContentType == format) == 6, "Six new lessons per format required.");
        foreach (var lesson in lessons)
        {
            Require(lesson.Level == level && Formats.Contains(lesson.ContentType), "Wrong level or format.");
            Require(Regex.IsMatch(lesson.Key, "^team\\.writing\\.20261114\\." + level + "\\.[a-z0-9.-]+$"), "Invalid key.");
            Require(lesson.SourceType == "original-20261114", "Original provenance required.");
            Require(lesson.Title.Length is > 5 and <= 180 && lesson.Topic.Length is > 0 and <= 100
                && lesson.PreviewText.Length is > 20 and <= 500, "Missing/oversize metadata.");
            var (minSentences, maxSentences, minWords, maxWords) = level switch
            {
                "beginner" => (6, 10, 60, 120), "intermediate" => (10, 14, 120, 190), _ => (12, 18, 180, 280)
            };
            Require(lesson.Sentences.Count >= minSentences && lesson.Sentences.Count <= maxSentences, "Incomplete lesson sentence count.");
            Require(lesson.Sentences.Select(x => x.SortOrder).SequenceEqual(Enumerable.Range(1, lesson.Sentences.Count)), "Non-contiguous sentence order.");
            var wordCount = Regex.Matches(string.Join(' ', lesson.Sentences.Select(x => x.EnglishMeaning)), @"\b\w+(?:['’-]\w+)*\b").Count;
            Require(wordCount >= minWords && wordCount <= maxWords, $"Word target failed: {lesson.Key} ({wordCount}).");
            Require(lesson.Sentences.Last().BreakAfter && lesson.Sentences.Take(lesson.Sentences.Count - 1).Any(x => x.BreakAfter), "Paragraph structure missing.");
            Require(lesson.Sentences.Select(x => Normalize(x.EnglishMeaning)).Distinct().Count() == lesson.Sentences.Count, "Repeated filler sentence.");
            foreach (var sentence in lesson.Sentences)
            {
                Require(!string.IsNullOrWhiteSpace(sentence.VietnameseText) && sentence.VietnameseText.Length <= 1000
                    && !string.IsNullOrWhiteSpace(sentence.EnglishMeaning) && sentence.EnglishMeaning.Length <= 1000, "Empty/oversize sentence pair.");
                Require(sentence.VietnameseText.Any(c => c > 127) && !sentence.VietnameseText.Contains('\uFFFD'), "Vietnamese text missing or corrupted.");
                Require(Normalize(sentence.VietnameseText) != Normalize(sentence.EnglishMeaning)
                    && sentence.EnglishMeaning.Any(char.IsAsciiLetter), "English reference cannot be a copy of the Vietnamese task.");
                Require(!Regex.IsMatch(sentence.EnglishMeaning + sentence.VietnameseText, @"(?i)\b(todo|placeholder|lorem ipsum)\b"), "Placeholder content.");
                Require(sentence.ApprovedAlternatives.Count <= WritingAssessmentPolicy.MaxAcceptedVariantsPerSentence, "Too many variants.");
                var normalized = new HashSet<string>(StringComparer.Ordinal) { Normalize(sentence.EnglishMeaning) };
                foreach (var variant in sentence.ApprovedAlternatives)
                    Require(!string.IsNullOrWhiteSpace(variant) && variant.Length <= 1000 && normalized.Add(Normalize(variant)), "Empty/duplicate variant.");
                Require(WritingAssessmentPolicy.Assess(sentence.EnglishMeaning, sentence.EnglishMeaning).Passed, "Reference rejected.");
                foreach (var variant in sentence.ApprovedAlternatives)
                    Require(WritingAssessmentPolicy.Assess(sentence.EnglishMeaning, variant, sentence.ApprovedAlternatives).Passed, "Variant rejected.");
                Require(!WritingAssessmentPolicy.Assess(sentence.EnglishMeaning, "").Passed
                    && !WritingAssessmentPolicy.Assess(sentence.EnglishMeaning, "An unrelated purple elephant dances.").Passed, "Assessment guard failed.");
            }
        }
        for (var i = 0; i < lessons.Count; i++)
            foreach (var other in lessons.Skip(i + 1))
                Require(Similarity(Text(lessons[i]), Text(other)) < .75, "Near-duplicate lesson: " + lessons[i].Key + " / " + other.Key);
    }

    public static string Text(WritingContentLesson lesson) => string.Join(' ', lesson.Sentences.Select(x => x.EnglishMeaning));
    public static string Normalize(string value) => WritingAssessmentPolicy.NormalizeForComparison(value);
    // Five-word shingles catch copied passages without flagging shared beginner vocabulary.
    public static double Similarity(string first, string second)
    {
        static HashSet<string> Shingles(string text)
        {
            var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return Enumerable.Range(0, Math.Max(0, words.Length - 4))
                .Select(i => string.Join(' ', words.Skip(i).Take(5))).ToHashSet(StringComparer.Ordinal);
        }
        var a = Shingles(first); var b = Shingles(second);
        if (a.Count == 0 || b.Count == 0) return Normalize(first) == Normalize(second) ? 1 : 0;
        return (double)a.Intersect(b).Count() / a.Union(b).Count();
    }

    public static WritingExercise Create(WritingContentLesson lesson, int approverId) => new()
    {
        Title = lesson.Title, Level = lesson.Level, ContentType = lesson.ContentType, Topic = lesson.Topic,
        SourceType = lesson.SourceType, PreviewText = lesson.PreviewText, IsPublished = true, UserId = null,
        CreatedAt = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc),
        WritingExerciseSentences = lesson.Sentences.Select(s => new WritingExerciseSentence
        {
            SortOrder = s.SortOrder, VietnameseText = s.VietnameseText, EnglishMeaning = s.EnglishMeaning,
            BreakAfter = s.BreakAfter, WritingAcceptedAnswers = s.ApprovedAlternatives.Select(answer => new WritingAcceptedAnswer
            {
                AnswerText = answer, NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash(answer),
                ApprovedByUserId = approverId, ApprovedAtUtc = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc)
            }).ToList()
        }).ToList()
    };

    public static void AssertMatches(WritingContentLesson lesson, WritingExercise row)
    {
        Require(row.UserId == null && row.IsPublished && row.Title == lesson.Title && row.Level == lesson.Level
            && row.ContentType == lesson.ContentType && row.Topic == lesson.Topic && row.SourceType == lesson.SourceType
            && row.PreviewText == lesson.PreviewText, "Existing lesson metadata conflict: " + lesson.Key);
        var actual = row.WritingExerciseSentences.OrderBy(s => s.SortOrder).Select(s => new WritingContentSentence(
            s.SortOrder, s.VietnameseText, s.EnglishMeaning, s.BreakAfter,
            s.WritingAcceptedAnswers.OrderBy(a => a.AnswerText, StringComparer.Ordinal).Select(a => a.AnswerText).ToList())).ToList();
        var expected = lesson.Sentences.Select(s => s with { ApprovedAlternatives = s.ApprovedAlternatives.Order(StringComparer.Ordinal).ToList() });
        Require(JsonSerializer.Serialize(actual, Json) == JsonSerializer.Serialize(expected, Json), "Existing lesson content conflict: " + lesson.Key);
        Require(row.WritingExerciseSentences.SelectMany(s => s.WritingAcceptedAnswers)
            .All(a => a.NormalizedHash == WritingAssessmentPolicy.ComputeNormalizedHash(a.AnswerText)), "Persisted answer hash mismatch.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
