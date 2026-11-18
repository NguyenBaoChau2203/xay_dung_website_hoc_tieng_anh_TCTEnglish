using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TCTVocabulary.Services
{
    public class DefinitionSuggestion
    {
        public string PartOfSpeech { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }

    public class ImageSuggestion
    {
        public string PreviewUrl { get; set; } = string.Empty;
        public string WebformatUrl { get; set; } = string.Empty;
    }

    public class VocabSuggestionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        
        public string? Phonetic { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        
        public IReadOnlyList<DefinitionSuggestion> Definitions { get; set; } = new List<DefinitionSuggestion>();
        public IReadOnlyList<ImageSuggestion> Images { get; set; } = new List<ImageSuggestion>();
    }

    public interface IVocabSuggestService
    {
        Task<VocabSuggestionResult> SuggestAsync(string term, CancellationToken ct = default);

        /// <summary>Structured dictionary entry for the personal-set composer (Premium/Admin callers only).</summary>
        Task<VocabEnrichResponse> EnrichAsync(string term, string? meaningHint = null, string? context = null, CancellationToken ct = default);

        /// <summary>1-10 terms, at most three at a time, order preserved; one failure does not fail the batch.</summary>
        Task<IReadOnlyList<VocabEnrichBatchItem>> EnrichBatchAsync(IReadOnlyList<VocabEnrichBatchRequest> items, CancellationToken ct = default);

        Task<IReadOnlyList<ImageSearchHit>> SearchImagesAsync(string query, int max, CancellationToken ct = default);
    }

    public class VocabEnrichResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TCTEnglish.ViewModels.Sets.VocabEnrichResultDto? Result { get; set; }
    }

    public class VocabEnrichBatchRequest
    {
        public string? Term { get; set; }
        public string? MeaningHint { get; set; }
    }

    public class VocabEnrichBatchItem
    {
        public string Term { get; set; } = string.Empty;
        public bool Ok { get; set; }
        public TCTEnglish.ViewModels.Sets.VocabEnrichResultDto? Result { get; set; }
        public string? Error { get; set; }
    }

    public class ImageSearchHit
    {
        public string PreviewUrl { get; set; } = string.Empty;
        public string WebformatUrl { get; set; } = string.Empty;
        public string? PageUrl { get; set; }
        public string? Author { get; set; }
    }
}
