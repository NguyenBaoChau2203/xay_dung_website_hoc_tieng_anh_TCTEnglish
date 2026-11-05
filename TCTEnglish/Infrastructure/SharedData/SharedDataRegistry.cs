using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Reflection;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure.SharedData;

public sealed record SharedReference(string Property, string Kind, bool Optional = false);

/// <summary>Explicit portable content contract. No arbitrary EF entity/table access.</summary>
public sealed record SharedEntitySpec(Type Type, string Id, string[] Fields, SharedReference[] References,
    string[] Owners, Func<DbflashcardContext, CancellationToken, Task<IList>> Load)
{
    public PropertyInfo Property(string name) => Type.GetProperty(name) ?? throw new SharedDataException("Unknown content property.");
    public int GetId(object entity) => (int)Property(Id).GetValue(entity)!;
}

public static class SharedDataRegistry
{
    public static readonly string[] Roots = ["Folder", "Set", "ReadingPassage", "ListeningLesson", "SpeakingPlaylist", "SpeakingVideo", "WritingExercise", "PremiumPlan", "Class"];
    private static SharedEntitySpec Spec<T>(string id, string fields, SharedReference[]? references = null, string[]? owners = null) where T : class
        => new(typeof(T), id, fields.Split(' ', StringSplitOptions.RemoveEmptyEntries), references ?? [], owners ?? [],
            async (db, ct) => await db.Set<T>().ToListAsync(ct));

    public static readonly IReadOnlyDictionary<string, SharedEntitySpec> Entities = new Dictionary<string, SharedEntitySpec>(StringComparer.Ordinal)
    {
        ["Folder"] = Spec<Folder>("FolderId", "FolderName", [new("ParentFolderId", "Folder", true)], ["UserId"]),
        ["Set"] = Spec<Set>("SetId", "SetName Description", [new("FolderId", "Folder", true)], ["OwnerId"]),
        ["Card"] = Spec<Card>("CardId", "Term Definition ImageUrl Phonetic Example ExampleTranslation Topic", [new("SetId", "Set")]),
        ["ReadingPassage"] = Spec<ReadingPassage>("Id", "Title Content Level Topic ImageUrl IsPublished"),
        ["ReadingQuestion"] = Spec<ReadingQuestion>("Id", "QuestionText OrderIndex", [new("PassageId", "ReadingPassage")]),
        ["ReadingOption"] = Spec<ReadingOption>("Id", "OptionText IsCorrect", [new("QuestionId", "ReadingQuestion")]),
        ["ListeningLesson"] = Spec<ListeningLesson>("Id", "Title Level Topic YoutubeId AudioUrl ThumbnailUrl Duration Speaker1Name Speaker2Name Speaker1Country Speaker2Country IsPublished TranscriptSource"),
        ["ListeningTranscriptLine"] = Spec<ListeningTranscriptLine>("Id", "OrderIndex Speaker Text VietnameseMeaning StartTime EndTime", [new("LessonId", "ListeningLesson")]),
        ["ListeningQuizQuestion"] = Spec<ListeningQuizQuestion>("Id", "OrderIndex QuestionText OptionA OptionB OptionC OptionD CorrectAnswer Explanation", [new("LessonId", "ListeningLesson")]),
        ["ListeningVocabItem"] = Spec<ListeningVocabItem>("Id", "OrderIndex Word Definition ExampleSentence ImageUrl", [new("LessonId", "ListeningLesson")]),
        ["SpeakingPlaylist"] = Spec<SpeakingPlaylist>("Id", "Name Description ThumbnailUrl"),
        ["SpeakingVideo"] = Spec<SpeakingVideo>("Id", "Title YoutubeId Level Topic ThumbnailUrl Duration SourceUrl SourceType TranscriptSource ImportStatus", [new("PlaylistId", "SpeakingPlaylist", true)]),
        ["SpeakingSentence"] = Spec<SpeakingSentence>("Id", "StartTime EndTime Text VietnameseMeaning", [new("VideoId", "SpeakingVideo")]),
        ["WritingExercise"] = Spec<WritingExercise>("Id", "Title Level ContentType Topic SourceType PreviewText IsPublished"),
        ["WritingExerciseSentence"] = Spec<WritingExerciseSentence>("Id", "SortOrder VietnameseText EnglishMeaning BreakAfter", [new("WritingExerciseId", "WritingExercise")]),
        ["WritingAcceptedAnswer"] = Spec<WritingAcceptedAnswer>("Id", "AnswerText NormalizedHash", [new("WritingExerciseSentenceId", "WritingExerciseSentence")], ["ApprovedByUserId"]),
        ["PremiumPlan"] = Spec<PremiumPlan>("Id", "Code Name Description PriceVnd DurationDays IsActive DisplayOrder"),
        ["Class"] = Spec<Class>("ClassId", "ClassName ImageUrl Description IsChatLocked AllowMemberToPost RequiresApproval", owners: ["OwnerId"]),
        ["ClassFolder"] = Spec<ClassFolder>("ClassFolderId", "", [new("ClassId", "Class"), new("FolderId", "Folder")], ["AddedByUserId"])
    };

    public static SharedEntitySpec Get(string kind) => Entities.TryGetValue(kind, out var spec) ? spec : throw new SharedDataException($"Unsupported shared entity kind: {kind}.");

    public static bool IsOwnedChild(string parent, string child, string property) => (parent, child, property) switch
    {
        ("Folder", "Folder", "ParentFolderId") or ("Folder", "Set", "FolderId") or ("Set", "Card", "SetId") or
        ("ReadingPassage", "ReadingQuestion", "PassageId") or ("ReadingQuestion", "ReadingOption", "QuestionId") or
        ("ListeningLesson", "ListeningTranscriptLine", "LessonId") or ("ListeningLesson", "ListeningQuizQuestion", "LessonId") or ("ListeningLesson", "ListeningVocabItem", "LessonId") or
        ("SpeakingPlaylist", "SpeakingVideo", "PlaylistId") or ("SpeakingVideo", "SpeakingSentence", "VideoId") or
        ("WritingExercise", "WritingExerciseSentence", "WritingExerciseId") or ("WritingExerciseSentence", "WritingAcceptedAnswer", "WritingExerciseSentenceId") or
        ("Class", "ClassFolder", "ClassId") => true,
        _ => false
    };

    public static void AssertShareable(object row)
    {
        if (row is ListeningLesson { OwnerUserId: not null } or SpeakingVideo { OwnerUserId: not null } or WritingExercise { UserId: not null })
            throw new SharedDataException("Private learner content cannot be exported/adopted as a shared catalog item.");
        if (row is Class { HasPassword: true })
            throw new SharedDataException("Password-protected classes require an explicit private transfer, not a Git data package.");
    }
}
