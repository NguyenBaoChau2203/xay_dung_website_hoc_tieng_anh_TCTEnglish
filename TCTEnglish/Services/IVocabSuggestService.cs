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
    }
}
