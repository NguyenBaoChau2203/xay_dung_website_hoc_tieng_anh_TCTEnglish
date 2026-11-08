using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public static class CorpusVersions
{
    public const int SchemaVersion = 1;
    public const string CorpusVersion = "lexical-v1";

    /// <summary>
    /// paragraph-sentence-v2 splits a sentence that exceeds <see cref="MaxChunkTokens"/>
    /// into exact contiguous spans. paragraph-sentence-v1 snapshots are rejected so an
    /// oversized sentence cannot stay in service. An admin must rebuild after this change.
    /// </summary>
    public const string ChunkerVersion = "paragraph-sentence-v2";
    public const string NormalizerVersion = "vi-en-fold-v1";

    /// <summary>Initial BM25 hypotheses. They are not tuned on the held-out set.</summary>
    public const double Bm25K1 = 1.2;
    public const double Bm25B = 0.75;
    public const double TitleWeight = 1.5;
    public const double KeywordWeight = 1.2;
    public const double BodyWeight = 1.0;

    public const int TargetChunkTokens = 300;
    public const int MaxChunkTokens = 600;
    public const int MaxOverlapTokens = 40;
    public const int MaxCandidateCount = 20;
    public const int MaxResultCount = 3;
    public const int MaxSourceRows = 2000;
    public const int MaxBodyCharacters = 200_000;
    public const int MaxSentencesPerDocument = 2000;

    public static readonly IReadOnlySet<string> AllowedSourceKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "guide",
        "reading",
        "writing",
        "listening",
        "speaking"
    };

    public static readonly string[] ForbiddenKeyPrefixes =
    [
        "card:",
        "user:",
        "chat:",
        "payment:",
        "recording:",
        "upload:",
        "goal:",
        "notification:",
        "billing:",
        "class:"
    ];
}

public static class CorpusHash
{
    public static string Sha256(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    public static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string Canonical(string sourceKey, string visibility, string route, string title, IReadOnlyList<string> keywords, string body)
    {
        return string.Join('\n', sourceKey, visibility, route, title, string.Join('\u001f', keywords), body);
    }
}

/// <summary>
/// Approximate counter shared with <see cref="TCTEnglish.Services.AI.SimpleAiTokenCounter"/>.
/// It is characters/4, not a proof of model context length.
/// </summary>
public static class ApproximateTokenCounter
{
    public static int Count(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }
}

public sealed record CorpusExclusion(string SourceKey, string Reason);

public sealed record CorpusDocument(
    string SourceKind,
    string SourceKey,
    string EntityKey,
    string Title,
    IReadOnlyList<string> Keywords,
    string Body,
    string Route,
    string Visibility,
    string ContentHash,
    string SourceVersion);

public sealed record CorpusSentence(
    string SentenceId,
    int Number,
    string Text,
    string ContentHash,
    int ParagraphIndex,
    bool IsHeading);

public sealed record CorpusChunk(
    string ChunkId,
    string SourceKey,
    int Number,
    IReadOnlyList<int> SentenceNumbers,
    IReadOnlyList<string> TitleTokens,
    IReadOnlyList<string> KeywordTokens,
    IReadOnlyList<string> BodyTokens);

public sealed record PreparedCorpusDocument(
    CorpusDocument Document,
    IReadOnlyList<CorpusSentence> Sentences,
    IReadOnlyList<CorpusChunk> Chunks);

public sealed class CorpusCollection
{
    public List<CorpusDocument> Documents { get; } = [];
    public List<CorpusExclusion> Exclusions { get; } = [];
    public bool Truncated { get; set; }
}

public enum LexicalAvailability
{
    Ready,
    Missing,
    Corrupt,
    Unsupported
}

public sealed record LexicalHit(
    string SourceKey,
    string SourceType,
    string SentenceId,
    string ContentHash,
    string CorpusVersion,
    string ChunkerVersion,
    string? Route,
    string Visibility,
    string Text,
    string Title,
    double Score);

public sealed class LexicalRetrievalResult
{
    public LexicalAvailability Availability { get; init; } = LexicalAvailability.Missing;
    public IReadOnlyList<LexicalHit> Hits { get; init; } = [];
    public bool RecheckRejected { get; init; }
    public bool AmbiguousTie { get; init; }
    public string? Detail { get; init; }
}

public sealed class RetrievalIndexOperationResult
{
    public string Status { get; init; } = "failed";
    public int DocumentCount { get; init; }
    public int ChunkCount { get; init; }
    public int ExclusionCount { get; init; }
    public bool Truncated { get; init; }
    public string? SnapshotId { get; init; }
    public string? ChecksumSha256 { get; init; }
    public long SnapshotBytes { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<CorpusExclusion> Exclusions { get; init; } = [];
    public IReadOnlyList<string> SourceHashes { get; init; } = [];
}

public sealed record SourceRecheck(bool Eligible, CorpusDocument? Document);

public sealed class RetrievalIndexStatus
{
    public LexicalAvailability Availability { get; init; } = LexicalAvailability.Missing;
    public string? SnapshotId { get; init; }
    public int DocumentCount { get; init; }
    public int ChunkCount { get; init; }
    public DateTime? PromotedAtUtc { get; init; }
    public long SnapshotBytes { get; init; }
    public string? Detail { get; init; }
}

public static class LexicalBoundaryMessages
{
    public const string IndexUnavailable =
        "Chỉ mục từ vựng dùng chung chưa có bản hợp lệ hoặc đã hỏng. Mình không dùng bản thiếu checksum để trả lời câu này.";

    public const string SourceRevoked =
        "Mình không còn nguồn đã xuất bản và còn hiệu lực cho nội dung vừa khớp. Mình không dùng bản cũ sau khi nguồn bị gỡ hoặc đổi.";

    public const string Unsupported =
        "Mình tìm thấy học liệu có cùng một số từ, nhưng học liệu đó không nêu đủ ý được hỏi. Mình không dùng phần không đủ chứng cứ để trả lời đầy đủ.";
}
