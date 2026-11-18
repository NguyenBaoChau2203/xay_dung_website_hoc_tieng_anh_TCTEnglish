using TCTEnglish.Areas.Admin.ViewModels;

namespace TCTEnglish.Services.Reading;

public interface IReadingExcelService

{

    byte[] BuildTemplate();

    Task<byte[]> ExportReadingAsync(int passageId, CancellationToken ct = default);

    Task<ReadingExcelPreviewResult> ParseAndValidateAsync(Stream stream, CancellationToken ct = default);

    byte[] BuildErrorReport(IEnumerable<ReadingExcelError> errors);

    Task<ReadingExcelImportConfirmResult> ConfirmImportAsync(

        IReadOnlyList<ReadingEditorViewModel> models,

        int actorUserId,

        CancellationToken ct = default);

}

public sealed class ReadingExcelPreviewResult

{

    /// <summary>True when at least one reading is importable (partial success allowed).</summary>

    public bool HasImportableReadings => ValidReadings.Count > 0;

    public List<ReadingExcelError> Errors { get; set; } = new();

    public List<ReadingExcelWarning> Warnings { get; set; } = new();

    public List<ReadingEditorViewModel> ValidReadings { get; set; } = new();

    public List<ReadingExcelFailedReading> FailedReadings { get; set; } = new();

    /// <summary>Legacy single-preview accessor for older views/tests.</summary>

    public ReadingEditorViewModel? Preview => ValidReadings.Count == 1 ? ValidReadings[0] : ValidReadings.FirstOrDefault();

    public bool IsValid => Errors.Count == 0 && FailedReadings.Count == 0 && ValidReadings.Count > 0;

}

public sealed class ReadingExcelFailedReading

{

    public string ReadingCode { get; set; } = "";

    public string? Title { get; set; }

    public List<ReadingExcelError> Errors { get; set; } = new();

}

public sealed class ReadingExcelImportConfirmResult

{

    public List<ReadingExcelImportItemResult> Items { get; set; } = new();

    public int SuccessCount => Items.Count(i => i.Status == "Success");

    public int FailedCount => Items.Count(i => i.Status == "Failed");

    public int SkippedCount => Items.Count(i => i.Status == "Skipped");

}

public sealed class ReadingExcelImportItemResult

{

    public string ReadingCode { get; set; } = "";

    public string Title { get; set; } = "";

    public string Status { get; set; } = ""; // Success | Failed | Skipped

    public int? PassageId { get; set; }

    public string? Message { get; set; }

}

public sealed class ReadingExcelError

{

    public string? ReadingCode { get; set; }

    public string Sheet { get; set; } = "";

    public int Row { get; set; }

    public string Column { get; set; } = "";

    public string? Value { get; set; }

    public string Message { get; set; } = "";

    public string? FixHint { get; set; }

}

public sealed class ReadingExcelWarning

{

    public string? ReadingCode { get; set; }

    public string Sheet { get; set; } = "";

    public int Row { get; set; }

    public string Column { get; set; } = "";

    public string Message { get; set; } = "";

}
