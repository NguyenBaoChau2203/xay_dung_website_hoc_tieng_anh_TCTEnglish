using System.Text.Json;

namespace TCTEnglish.Services.AI;

public sealed class AiProviderRequestOptions
{
    public int? MaxOutputTokens { get; init; }

    public int? RequestTimeoutSeconds { get; init; }

    public double? Temperature { get; init; }

    /// <summary>
    /// Chat sentence selection only. Null leaves the text-only payload unchanged
    /// for writing, vocab, reading, listening and speaking consumers.
    /// </summary>
    public ChatConstrainedOutput? ConstrainedOutput { get; init; }

    /// <summary>Chat selection only. Zero leaves the transport cap unset.</summary>
    public int MaxInputBytes { get; init; }

    /// <summary>Chat selection only. Null keeps the provider retry setting.</summary>
    public int? MaxAttempts { get; init; }

    /// <summary>Optional chat guard, checked immediately before every HTTP attempt.</summary>
    public Func<CancellationToken, Task<bool>>? ConfirmCurrent { get; init; }

    /// <summary>Cumulative known/uncertain usage, persisted with a bounded independent token.</summary>
    public Func<int, int, CancellationToken, Task<bool>>? RecordUsage { get; init; }
}

/// <summary>
/// Responses API <c>text.format</c> json_schema. Official shape is type, name, schema, strict.
/// A valid schema does not make the selected sentences factually sufficient.
/// </summary>
public sealed class ChatConstrainedOutput
{
    public ChatConstrainedOutput(string name, string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64
            || name.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("Constrained output name must match the Responses schema name rules.", nameof(name));
        }

        using var schema = JsonDocument.Parse(schemaJson);
        if (schema.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Constrained output schema must be a JSON object.", nameof(schemaJson));
        }

        Name = name;
        SchemaJson = schemaJson;
    }

    public string Name { get; }

    public string SchemaJson { get; }
}
