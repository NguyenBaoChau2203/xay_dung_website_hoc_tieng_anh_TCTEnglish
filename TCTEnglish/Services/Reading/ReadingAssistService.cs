using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Reading;

public sealed class ReadingAssistService(
    IReadingLearningService learning,
    IAiProviderClient aiProvider,
    DbflashcardContext db,
    IOptions<AiOptions> aiOptions) : IReadingAssistService
{
    private readonly AiOptions _aiOptions = aiOptions.Value;
    public const string SchemaName = "reading_assist";

    public const string DeveloperPrompt =
        """
        You help Vietnamese learners of English during reading practice.
        Return only the JSON schema fields. Do not invent IPA when unsure — use null.
        Keep definitions short. Translate English to Vietnamese only.
        For Translate mode fill translation and vietnameseMeaning with the same short VI gloss; leave other optional fields null when unused.
        For Dictionary mode fill lemma, partOfSpeech, englishDefinition, vietnameseMeaning, and one short example using the surrounding sentence when useful.
        """;

    public const string SchemaJson =
        """
        {"type":"object","additionalProperties":false,"properties":{"lemma":{"type":"string"},"translation":{"type":"string"},"partOfSpeech":{"type":["string","null"]},"ipa":{"type":["string","null"]},"englishDefinition":{"type":["string","null"]},"vietnameseMeaning":{"type":"string"},"example":{"type":["string","null"]}},"required":["lemma","translation","partOfSpeech","ipa","englishDefinition","vietnameseMeaning","example"]}
        """;

    private const int MaxSelectedChars = 120;
    private const int MaxWords = 8;
    private const int MaxSurroundingChars = 240;
    private const int MaxFieldChars = 280;
    private const int MaxOutputTokens = 400;

    public async Task<ReadingAssistResultVm> AssistAsync(int userId, ReadingAssistRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mode = NormalizeMode(request.Mode);
        var selected = NormalizeSelected(request.SelectedText);
        if (selected is null)
        {
            return Fail("invalid_selection", "Chọn 1–8 từ trong đoạn văn (tối đa 120 ký tự).");
        }

        if (await learning.GetStudyAsync(userId, request.PassageId, ct) is null)
            return Fail("not_found", "Không tìm thấy bài đọc.", notFound: true);

        var local = await learning.LookupWordAsync(request.PassageId, selected, ct);
        if (local is not null)
        {
            return FromPassageVocab(local, mode);
        }

        var notebookHit = await FindRecentNotebookAsync(userId, selected, ct);
        if (notebookHit is not null && HasUsableMeaning(notebookHit))
        {
            return FromNotebook(notebookHit, mode);
        }

        if (string.IsNullOrWhiteSpace(_aiOptions.ApiKey))
        {
            return Fail(
                "not_configured",
                "AI chưa được cấu hình trên máy này. Chạy scripts/set-local-ai-key.ps1 rồi restart start-local.");
        }

        try
        {
            var surrounding = Clamp(request.SurroundingSentence, MaxSurroundingChars);
            var messages = new List<AiContextMessage>
            {
                new("developer", DeveloperPrompt),
                new("user", BuildUserPrompt(mode, selected, surrounding, request.ParagraphNo))
            };

            var reply = await aiProvider.GenerateReplyAsync(userId, messages, ct, new AiProviderRequestOptions
            {
                MaxOutputTokens = MaxOutputTokens,
                ConstrainedOutput = new ChatConstrainedOutput(SchemaName, SchemaJson)
            });

            return ParseAiReply(reply.Text, selected, mode);
        }
        catch (AiProviderException ex)
        {
            return Fail(MapErrorCode(ex.ErrorCode), UserMessageFor(ex.ErrorCode));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail("timeout", "Hết thời gian chờ dịch vụ AI. Thử lại.");
        }
    }

    private static ReadingAssistResultVm FromPassageVocab(ReadingVocabularyItem item, string mode) =>
        new()
        {
            Success = true,
            Mode = mode,
            Source = ReadingAssistSources.PassageVocab,
            VocabItemId = item.Id,
            Word = item.Word,
            Lemma = item.Word,
            Translation = item.VietnameseMeaning,
            PartOfSpeech = item.PartOfSpeech,
            Ipa = item.Ipa,
            EnglishDefinition = item.EnglishDefinition,
            VietnameseMeaning = item.VietnameseMeaning,
            Example = item.Example
        };

    private static ReadingAssistResultVm FromNotebook(StudentVocabulary item, string mode) =>
        new()
        {
            Success = true,
            Mode = mode,
            Source = ReadingAssistSources.Notebook,
            Cached = true,
            VocabItemId = item.SourceVocabularyItemId,
            Word = item.Word,
            Lemma = item.Word,
            Translation = item.VietnameseMeaning,
            PartOfSpeech = item.PartOfSpeech,
            Ipa = item.Ipa,
            EnglishDefinition = item.EnglishDefinition,
            VietnameseMeaning = item.VietnameseMeaning,
            Example = item.Example
        };

    private static ReadingAssistResultVm ParseAiReply(string text, string selected, string mode)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fail("empty_response", "AI không trả về nội dung. Thử lại.");

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Fail("invalid_response", "Phản hồi AI không hợp lệ. Thử lại.");

            var lemma = Clamp(ReadString(root, "lemma"), MaxFieldChars) ?? selected;
            var translation = Clamp(ReadString(root, "translation"), MaxFieldChars);
            var vietnamese = Clamp(ReadString(root, "vietnameseMeaning"), MaxFieldChars) ?? translation;
            var gloss = vietnamese ?? translation;
            if (string.IsNullOrWhiteSpace(gloss))
                return Fail("invalid_response", "AI không trả về nghĩa. Thử lại.");

            return new ReadingAssistResultVm
            {
                Success = true,
                Mode = mode,
                Source = ReadingAssistSources.Ai,
                Word = selected,
                Lemma = lemma,
                Translation = gloss,
                PartOfSpeech = Clamp(ReadNullableString(root, "partOfSpeech"), 64),
                Ipa = Clamp(ReadNullableString(root, "ipa"), 64),
                EnglishDefinition = Clamp(ReadNullableString(root, "englishDefinition"), MaxFieldChars),
                VietnameseMeaning = gloss,
                Example = Clamp(ReadNullableString(root, "example"), MaxFieldChars)
            };
        }
        catch (JsonException)
        {
            return Fail("invalid_response", "Phản hồi AI không hợp lệ. Thử lại.");
        }
    }

    private async Task<StudentVocabulary?> FindRecentNotebookAsync(int userId, string word, CancellationToken ct)
    {
        var normalized = word.Trim().ToLowerInvariant();
        return await db.StudentVocabularies.AsNoTracking()
            .Where(v => v.UserId == userId && v.Word.ToLower() == normalized)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private static bool HasUsableMeaning(StudentVocabulary item) =>
        !string.IsNullOrWhiteSpace(item.VietnameseMeaning)
        || !string.IsNullOrWhiteSpace(item.EnglishDefinition);

    private static string BuildUserPrompt(string mode, string selected, string? surrounding, int? paragraphNo)
    {
        var paragraph = paragraphNo is > 0 ? paragraphNo.Value.ToString() : "unknown";
        return
            $"""
            Mode: {mode}
            SelectedText: {selected}
            ParagraphNo: {paragraph}
            SurroundingSentence: {surrounding ?? ""}
            """;
    }

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, ReadingAssistModes.Dictionary, StringComparison.OrdinalIgnoreCase)
            ? ReadingAssistModes.Dictionary
            : ReadingAssistModes.Translate;

    private static string? NormalizeSelected(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (trimmed.Length is < 1 or > MaxSelectedChars) return null;
        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 1 or > MaxWords) return null;
        return string.Join(' ', words);
    }

    private static string? Clamp(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? ReadNullableString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static string MapErrorCode(string code) => code switch
    {
        AiProviderException.ErrorCodeInvalidConfiguration => "not_configured",
        AiProviderException.ErrorCodeAuthentication => "auth",
        AiProviderException.ErrorCodeQuota => "quota",
        AiProviderException.ErrorCodeRateLimited => "rate_limited",
        AiProviderException.ErrorCodeTimeout => "timeout",
        AiProviderException.ErrorCodeNetwork => "network",
        AiProviderException.ErrorCodeModelAccess => "model_access",
        AiProviderException.ErrorCodeRefusal => "refusal",
        _ => "provider_error"
    };

    private static string UserMessageFor(string code) => code switch
    {
        AiProviderException.ErrorCodeInvalidConfiguration =>
            "AI chưa được cấu hình trên máy này. Chạy scripts/set-local-ai-key.ps1 rồi restart start-local.",
        AiProviderException.ErrorCodeAuthentication => "Khóa AI không hợp lệ hoặc thiếu quyền.",
        AiProviderException.ErrorCodeQuota => "Hết hạn mức AI. Thử lại sau.",
        AiProviderException.ErrorCodeRateLimited => "AI đang bị giới hạn tốc độ. Thử lại sau.",
        AiProviderException.ErrorCodeTimeout => "Hết thời gian chờ dịch vụ AI. Thử lại.",
        AiProviderException.ErrorCodeNetwork => "Không kết nối được tới AI. Kiểm tra mạng.",
        AiProviderException.ErrorCodeModelAccess => "Mô hình AI hiện không khả dụng.",
        AiProviderException.ErrorCodeRefusal => "AI từ chối yêu cầu này.",
        _ => "Không lấy được nghĩa từ AI. Thử lại."
    };

    private static ReadingAssistResultVm Fail(string code, string message, bool notFound = false) =>
        new()
        {
            Success = false,
            ErrorCode = notFound ? "not_found" : code,
            ErrorMessage = message
        };
}
