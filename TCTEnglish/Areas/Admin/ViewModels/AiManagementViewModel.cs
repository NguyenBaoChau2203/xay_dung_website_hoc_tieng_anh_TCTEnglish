namespace TCTVocabulary.Areas.Admin.ViewModels;

/// <summary>
/// ViewModel for the Admin AI Management page.
/// Displays the current state of the ML.NET model artifact and seed dataset.
/// </summary>
public class AiManagementViewModel
{
    /// <summary>Whether the trained model .zip file exists on disk.</summary>
    public bool ModelExists { get; set; }

    /// <summary>Absolute path to the expected model artifact.</summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>File size in bytes of the model artifact (null when model does not exist).</summary>
    public long? ModelFileSizeBytes { get; set; }

    /// <summary>Last-modified timestamp of the model artifact in UTC (null when model does not exist).</summary>
    public DateTime? ModelLastModifiedUtc { get; set; }

    /// <summary>Whether the seed dataset CSV file exists on disk.</summary>
    public bool DatasetExists { get; set; }

    /// <summary>Absolute path to the seed dataset.</summary>
    public string DatasetPath { get; set; } = string.Empty;

    /// <summary>Number of labelled samples in the seed dataset (null when dataset does not exist).</summary>
    public int? DatasetSampleCount { get; set; }
}
