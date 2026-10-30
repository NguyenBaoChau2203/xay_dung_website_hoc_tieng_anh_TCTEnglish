using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Speaking;

public sealed class SpeakingAssessmentOutcome
{
    public required bool Success { get; init; }
    public int StatusCode { get; init; } = 200;
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public Guid? AssessmentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public double TextMatchScore { get; init; }
    public bool SpeechDetected { get; init; }
    public string TranscriptPreview { get; init; } = string.Empty;
    public bool FluencyAvailable { get; init; }
    public bool CompletenessAvailable { get; init; }
    public bool CanCommitProgress { get; init; }
    public int? RetryAfterSeconds { get; init; }
}

public interface ISpeakingAssessmentService
{
    Task<SpeakingAssessmentOutcome> AssessAsync(
        int userId,
        int sentenceId,
        Stream audioStream,
        string? contentType,
        CancellationToken cancellationToken);

    Task<SpeakingAssessment?> GetUsableAssessmentAsync(
        int userId,
        int sentenceId,
        Guid assessmentPublicId,
        CancellationToken cancellationToken);
}

public sealed class SpeakingAssessmentService : ISpeakingAssessmentService
{
    private readonly DbflashcardContext _context;
    private readonly ISpeechTranscriber _transcriber;
    private readonly SpeechProcessingOptions _options;
    private readonly ILogger<SpeakingAssessmentService> _logger;

    public SpeakingAssessmentService(
        DbflashcardContext context,
        ISpeechTranscriber transcriber,
        IOptions<SpeechProcessingOptions> options,
        ILogger<SpeakingAssessmentService> logger)
    {
        _context = context;
        _transcriber = transcriber;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SpeakingAssessmentOutcome> AssessAsync(
        int userId,
        int sentenceId,
        Stream audioStream,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var access = await LoadSentenceAccessAsync(userId, sentenceId, cancellationToken);
        if (access == null)
        {
            return Fail(404, "not_found", "Câu không tồn tại hoặc bạn không có quyền.");
        }

        await using var buffer = new MemoryStream();
        await audioStream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > _options.MaxPayloadBytes)
        {
            return Fail(413, "payload_too_large", "File âm thanh vượt quá dung lượng cho phép.");
        }

        var bytes = buffer.ToArray();
        var validation = WavAudioValidator.ValidatePcm16Mono16kHz(
            bytes,
            _options.MaxPayloadBytes,
            _options.MaxAudioSeconds);
        if (!validation.IsValid)
        {
            return Fail(400, validation.ErrorCode ?? "invalid_audio", validation.Message ?? "Audio không hợp lệ.");
        }

        buffer.Position = 0;
        var transcription = await _transcriber.TranscribeAsync(buffer, cancellationToken);

        if (transcription.Status is SpeechTranscriptionStatus.NotReady
            or SpeechTranscriptionStatus.Unavailable
            or SpeechTranscriptionStatus.Timeout)
        {
            return new SpeakingAssessmentOutcome
            {
                Success = false,
                StatusCode = transcription.ErrorCode == "engine_busy"
                    ? 429
                    : transcription.Status == SpeechTranscriptionStatus.Timeout ? 504 : 503,
                ErrorCode = transcription.ErrorCode ?? "engine_unavailable",
                Message = transcription.Message ?? "ASR chưa sẵn sàng.",
                RetryAfterSeconds = transcription.ErrorCode == "engine_busy" ? 5 : null,
                FluencyAvailable = false,
                CompletenessAvailable = false,
                CanCommitProgress = false
            };
        }

        if (transcription.Status == SpeechTranscriptionStatus.InvalidAudio)
        {
            return Fail(400, transcription.ErrorCode ?? "invalid_audio", transcription.Message ?? "Audio không hợp lệ.");
        }

        var now = DateTime.UtcNow;
        var speechDetected = transcription.Status == SpeechTranscriptionStatus.Ok
            && !string.IsNullOrWhiteSpace(transcription.Transcript);
        var textMatch = speechDetected
            ? SpeakingTextMatch.ScorePercent(access.ReferenceText, transcription.Transcript)
            : 0d;

        if (!speechDetected)
        {
            return new SpeakingAssessmentOutcome
            {
                Success = true,
                Status = "NoSpeech",
                SpeechDetected = false,
                CanCommitProgress = false,
                Message = string.IsNullOrWhiteSpace(transcription.Message)
                    ? "Chưa nhận được lời nói."
                    : transcription.Message,
                FluencyAvailable = false,
                CompletenessAvailable = false
            };
        }

        var assessment = new SpeakingAssessment
        {
            PublicId = Guid.NewGuid(),
            UserId = userId,
            SentenceId = sentenceId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(Math.Clamp(_options.AssessmentTtlMinutes, 1, 60)),
            EngineName = string.IsNullOrWhiteSpace(transcription.EngineName) ? "whisper.cpp" : transcription.EngineName,
            ModelIdentity = transcription.ModelIdentity ?? _options.ModelIdentity,
            TranscriptText = speechDetected ? transcription.Transcript : string.Empty,
            TextMatchScore = Math.Round(textMatch, 2),
            SpeechDetected = speechDetected,
            Status = speechDetected ? "Completed" : "NoSpeech",
            Consumed = false
        };

        _context.SpeakingAssessments.Add(assessment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Speaking assessment {AssessmentId} created for user {UserId} sentence {SentenceId} status {Status} score {Score}",
            assessment.PublicId,
            userId,
            sentenceId,
            assessment.Status,
            assessment.TextMatchScore);

        return new SpeakingAssessmentOutcome
        {
            Success = true,
            AssessmentId = assessment.PublicId,
            Status = assessment.Status,
            TextMatchScore = assessment.TextMatchScore,
            SpeechDetected = assessment.SpeechDetected,
            TranscriptPreview = Truncate(assessment.TranscriptText, 120),
            FluencyAvailable = false,
            CompletenessAvailable = false,
            CanCommitProgress = assessment.SpeechDetected,
            Message = assessment.SpeechDetected
                ? null
                : (transcription.Message ?? "Chưa nhận được lời nói.")
        };
    }

    public async Task<SpeakingAssessment?> GetUsableAssessmentAsync(
        int userId,
        int sentenceId,
        Guid assessmentPublicId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.SpeakingAssessments
            .FirstOrDefaultAsync(
                a => a.PublicId == assessmentPublicId
                    && a.UserId == userId
                    && a.SentenceId == sentenceId
                    && a.SpeechDetected
                    && a.Status == "Completed"
                    && !a.Consumed
                    && a.ExpiresAtUtc >= now,
                cancellationToken);
    }

    private async Task<SentenceAccess?> LoadSentenceAccessAsync(int userId, int sentenceId, CancellationToken cancellationToken)
    {
        var row = await _context.SpeakingSentences
            .AsNoTracking()
            .Where(s => s.Id == sentenceId)
            .Select(s => new
            {
                s.Id,
                s.Text,
                s.VideoId,
                OwnerUserId = s.SpeakingVideo.OwnerUserId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
        {
            return null;
        }

        if (row.OwnerUserId.HasValue && row.OwnerUserId.Value != userId)
        {
            return null;
        }

        if (row.OwnerUserId == userId)
        {
            var role = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (Roles.Normalize(role) == Roles.Standard)
            {
                return null;
            }
        }

        return new SentenceAccess(row.Text);
    }

    private static SpeakingAssessmentOutcome Fail(int statusCode, string code, string message) => new()
    {
        Success = false,
        StatusCode = statusCode,
        ErrorCode = code,
        Message = message,
        FluencyAvailable = false,
        CompletenessAvailable = false,
        CanCommitProgress = false
    };

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }

    private sealed record SentenceAccess(string ReferenceText);
}
