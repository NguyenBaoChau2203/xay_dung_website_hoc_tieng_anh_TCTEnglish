using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TCTVocabulary.Models
{
    public class JsonFolderDto
    {
        [JsonPropertyName("FolderName")]
        public string FolderName { get; set; } = string.Empty;

        [JsonPropertyName("Sets")]
        public List<JsonSetDto> Sets { get; set; } = new();
    }

    public class JsonSetDto
    {
        [JsonPropertyName("SetName")]
        public string SetName { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("Cards")]
        public List<JsonCardDto> Cards { get; set; } = new();
    }

    public class JsonCardDto
    {
        [JsonPropertyName("Term")]
        public string Term { get; set; } = string.Empty;

        [JsonPropertyName("Definition")]
        public string Definition { get; set; } = string.Empty;

        [JsonPropertyName("ImageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("Phonetic")]
        public string? Phonetic { get; set; }

        [JsonPropertyName("Example")]
        public string? Example { get; set; }

        [JsonPropertyName("ExampleTranslation")]
        public string? ExampleTranslation { get; set; }

        [JsonPropertyName("Topic")]
        public string? Topic { get; set; }
    }
}
