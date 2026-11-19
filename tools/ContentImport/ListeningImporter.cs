using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace ContentImport;

public sealed record LsRights(string PolicyUrl, string ItemUrl, string Quote);
public sealed record LsLevelEvidence(int WordsPerMinute, string Reason);
public sealed record LsSpeaker(string Name, string? Country);
public sealed record LsLine(string Speaker, string Text, string Vi, double Start, double End);
public sealed record LsQuiz(string Question, string OptionA, string OptionB, string OptionC, string OptionD, string Correct, string Explanation);
public sealed record LsVocab(string Word, string Definition, string Example);
public sealed record LsLesson(
    string YoutubeId, string Title, string Level, string Topic, string Duration, int DurationSeconds, string ChannelName,
    LsRights Rights, LsLevelEvidence LevelEvidence, List<LsSpeaker> Speakers, List<LsLine> Lines, List<LsQuiz> Quiz, List<LsVocab> Vocab);
public sealed record LsManifest(int SchemaVersion, List<LsLesson> Lessons);

public sealed record LessonReport(string YoutubeId, string Title, string Action, int? LessonId);

/// <summary>
/// Idempotent importer of data/listening/system-a1-c2-2026-11.json. Matches public lessons (OwnerUserId = null) by
/// the case-sensitive YouTube id. An existing public lesson with the same id but different content is a conflict:
/// it is reported and preserved, never overwritten.
/// </summary>
public sealed class ListeningImporter(DbflashcardContext db, string repoRoot)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string TranscriptSource = "youtube-captions+reviewed";

    public LsManifest Read()
        => JsonSerializer.Deserialize<LsManifest>(File.ReadAllText(Path.Combine(repoRoot, "data/listening/system-a1-c2-2026-11.json")), Json)
           ?? throw new InvalidDataException("Invalid Listening manifest.");

    public static void Validate(LsLesson l)
    {
        if (YoutubeUrlHelper.NormalizeYoutubeId(l.YoutubeId) != l.YoutubeId) throw new InvalidDataException($"Bad YouTube id {l.YoutubeId}.");
        if (l.Lines.Count == 0 || l.Quiz.Count == 0 || l.Vocab.Count == 0) throw new InvalidDataException($"{l.YoutubeId}: lines, quiz and vocabulary are required.");
        double prevStart = -1, prevEnd = -1;
        foreach (var line in l.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text) || string.IsNullOrWhiteSpace(line.Vi) || line.Start < 0 || line.Start < prevStart
                || line.End <= line.Start || line.End < prevEnd || line.End > l.DurationSeconds + 2)
                throw new InvalidDataException($"{l.YoutubeId}: invalid line time/order.");
            prevStart = line.Start; prevEnd = line.End;
        }
        foreach (var q in l.Quiz)
            if (q.Correct is not ("A" or "B" or "C" or "D") || new[] { q.OptionA, q.OptionB, q.OptionC, q.OptionD }.Distinct().Count() != 4)
                throw new InvalidDataException($"{l.YoutubeId}: invalid quiz question.");
    }

    public async Task<LessonReport> ImportOneAsync(LsLesson l, bool write, CancellationToken ct = default)
    {
        Validate(l);
        var publicRows = await db.ListeningLessons.AsNoTracking().Where(x => x.OwnerUserId == null)
            .Select(x => new { x.Id, x.YoutubeId }).ToListAsync(ct);
        var matches = publicRows.Where(x => string.Equals(x.YoutubeId, l.YoutubeId, StringComparison.Ordinal)).ToList();
        if (matches.Count > 1) return new(l.YoutubeId, l.Title, "conflict: several public rows", null);
        if (matches.Count == 1)
        {
            var existing = await db.ListeningLessons.AsNoTracking().Include(x => x.TranscriptLines).Include(x => x.QuizQuestions)
                .Include(x => x.VocabItems).SingleAsync(x => x.Id == matches[0].Id, ct);
            return Same(existing, l)
                ? new(l.YoutubeId, l.Title, "unchanged", existing.Id)
                : new(l.YoutubeId, l.Title, "conflict: existing public lesson differs and was preserved", existing.Id);
        }
        if (!write) return new(l.YoutubeId, l.Title, "would create", null);

        var lesson = new ListeningLesson
        {
            Title = l.Title, Level = l.Level, Topic = l.Topic, YoutubeId = l.YoutubeId, Duration = l.Duration,
            ThumbnailUrl = YoutubeUrlHelper.BuildDefaultThumbnailUrl(l.YoutubeId), OwnerUserId = null, IsPublished = true,
            TranscriptSource = TranscriptSource, CreatedAt = DateTime.UtcNow,
            Speaker1Name = l.Speakers.ElementAtOrDefault(0)?.Name, Speaker1Country = l.Speakers.ElementAtOrDefault(0)?.Country,
            Speaker2Name = l.Speakers.ElementAtOrDefault(1)?.Name, Speaker2Country = l.Speakers.ElementAtOrDefault(1)?.Country,
            TranscriptLines = l.Lines.Select((x, i) => new ListeningTranscriptLine
            { OrderIndex = i + 1, Speaker = x.Speaker, Text = x.Text, VietnameseMeaning = x.Vi, StartTime = x.Start, EndTime = x.End }).ToList(),
            QuizQuestions = l.Quiz.Select((q, i) => new ListeningQuizQuestion
            { OrderIndex = i + 1, QuestionText = q.Question, OptionA = q.OptionA, OptionB = q.OptionB, OptionC = q.OptionC, OptionD = q.OptionD, CorrectAnswer = q.Correct, Explanation = q.Explanation }).ToList(),
            VocabItems = l.Vocab.Select((v, i) => new ListeningVocabItem
            { OrderIndex = i + 1, Word = v.Word, Definition = v.Definition, ExampleSentence = v.Example }).ToList(),
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ListeningLessons.Add(lesson);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(l.YoutubeId, l.Title, "created", lesson.Id);
    }

    private static bool Same(ListeningLesson e, LsLesson l)
        => e.IsPublished && e.Level == l.Level && e.Title == l.Title && e.Topic == l.Topic && e.Duration == l.Duration
           && e.TranscriptLines.Count == l.Lines.Count && e.QuizQuestions.Count == l.Quiz.Count && e.VocabItems.Count == l.Vocab.Count
           && e.TranscriptLines.OrderBy(t => t.OrderIndex).Select(t => (t.Text, t.VietnameseMeaning)).SequenceEqual(l.Lines.Select(t => (t.Text, (string?)t.Vi)));
}
