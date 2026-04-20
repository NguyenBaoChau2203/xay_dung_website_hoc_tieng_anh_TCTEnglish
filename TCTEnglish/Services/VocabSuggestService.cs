using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TCTEnglish.Services.AI;

namespace TCTVocabulary.Services
{
    public class VocabSuggestService : IVocabSuggestService
    {
        private readonly IAiProviderClient _aiClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VocabSuggestService> _logger;

        public VocabSuggestService(
            IAiProviderClient aiClient,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<VocabSuggestService> logger)
        {
            _aiClient = aiClient;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<VocabSuggestionResult> SuggestAsync(string term, CancellationToken ct = default)
        {
            var result = new VocabSuggestionResult { Success = true };

            if (string.IsNullOrWhiteSpace(term))
            {
                result.Success = false;
                result.ErrorMessage = "Term cannot be empty.";
                return result;
            }

            var cleanTerm = term.Trim();
            if (cleanTerm.Length > 100)
            {
                cleanTerm = cleanTerm.Substring(0, 100);
            }

            var aiTask = FetchAiSuggestionsAsync(cleanTerm, ct);
            var imageTask = FetchPixabayImagesAsync(cleanTerm, ct);

            await Task.WhenAll(aiTask, imageTask);

            try
            {
                var aiResult = await aiTask;
                if (aiResult != null)
                {
                    result.Phonetic = aiResult.Phonetic;
                    result.Example = aiResult.Example;
                    result.ExampleTranslation = aiResult.ExampleTranslation;
                    if (aiResult.Definitions != null)
                    {
                        result.Definitions = aiResult.Definitions;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch AI suggestions for term: {Term}", cleanTerm);
                result.Success = false; // We can let it be partially successful if images work, but let's just log.
            }

            try
            {
                var images = await imageTask;
                if (images != null)
                {
                    result.Images = images;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Pixabay images for term: {Term}", cleanTerm);
                // Not failing the whole request just because images failed
            }

            if (!result.Definitions.Any() && !result.Images.Any())
            {
                result.Success = false;
                result.ErrorMessage = "Could not fetch suggestions.";
            }

            return result;
        }

        private async Task<AiSuggestionDto?> FetchAiSuggestionsAsync(string term, CancellationToken ct)
        {
            var prompt = $@"
Please provide information for the English word/phrase: ""{term}""
Return ONLY a valid JSON object in the exact format:
{{
    ""phonetic"": ""(IPA pronunciation)"",
    ""definitions"": [
        {{ ""partOfSpeech"": ""noun/verb/etc"", ""definition"": ""Vietnamese meaning"" }}
    ],
    ""example"": ""An English example sentence"",
    ""exampleTranslation"": ""Vietnamese translation of the example""
}}
Make sure to provide up to 3 definitions if applicable. Do not include markdown formatting like ```json or ``` in the output, just the raw JSON text.";

            var messages = new List<AiContextMessage>
            {
                new AiContextMessage("user", prompt)
            };

            var options = new AiProviderRequestOptions
            {
                MaxOutputTokens = 300,
                Temperature = 0.2,
                RequestTimeoutSeconds = 10
            };

            var reply = await _aiClient.GenerateReplyAsync(0, messages, ct, options);

            var json = reply.Text.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                json = json.Substring(7);
            }
            if (json.StartsWith("```"))
            {
                json = json.Substring(3);
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3);
            }

            json = json.Trim();

            var optionsJson = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<AiSuggestionDto>(json, optionsJson);
        }

        private async Task<List<ImageSuggestion>> FetchPixabayImagesAsync(string term, CancellationToken ct)
        {
            var apiKey = _configuration["Pixabay:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Pixabay API key is missing.");
                return new List<ImageSuggestion>();
            }

            var url = $"https://pixabay.com/api/?key={apiKey}&q={Uri.EscapeDataString(term)}&image_type=photo&per_page=6&safesearch=true&lang=en";
            
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pixabay API returned {StatusCode} for term: {Term}", response.StatusCode, term);
                return new List<ImageSuggestion>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<PixabayResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Hits == null)
            {
                return new List<ImageSuggestion>();
            }

            return result.Hits.Select(h => new ImageSuggestion
            {
                PreviewUrl = h.PreviewURL ?? string.Empty,
                WebformatUrl = h.WebformatURL ?? string.Empty
            }).ToList();
        }

        // --- Helper DTOs for JSON deserialization ---

        private class AiSuggestionDto
        {
            public string? Phonetic { get; set; }
            public List<DefinitionSuggestion>? Definitions { get; set; }
            public string? Example { get; set; }
            public string? ExampleTranslation { get; set; }
        }

        private class PixabayResponse
        {
            public List<PixabayHit>? Hits { get; set; }
        }

        private class PixabayHit
        {
            public string? PreviewURL { get; set; }
            public string? WebformatURL { get; set; }
        }
    }
}
