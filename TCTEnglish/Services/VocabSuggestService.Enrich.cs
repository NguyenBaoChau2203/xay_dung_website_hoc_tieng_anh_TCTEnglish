using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;

namespace TCTVocabulary.Services
{
    public partial class VocabSuggestService
    {
        private const int MaxTermLength = 100;
        private const int MaxBatchItems = 10;

        public async Task<VocabEnrichResponse> EnrichAsync(string term, string? meaningHint = null, string? context = null, CancellationToken ct = default)
        {
            var clean = term?.Trim() ?? string.Empty;
            if (clean.Length > MaxTermLength)
            {
                clean = clean[..MaxTermLength];
            }

            if (clean.Length == 0)
            {
                return new VocabEnrichResponse { Success = false, ErrorMessage = "Nhập từ trước khi gợi ý." };
            }

            var hint = Truncate(meaningHint, 200);
            var sentence = Truncate(context, 300);
            var cacheKey = $"vocab-enrich:v1:{clean.ToLowerInvariant()}:{hint?.ToLowerInvariant()}:{sentence?.ToLowerInvariant()}";
            if (_cache != null && _cache.TryGetValue(cacheKey, out VocabEnrichResultDto? cached) && cached != null)
            {
                return new VocabEnrichResponse { Success = true, Result = cached };
            }

            var messages = new List<AiContextMessage> { new("user", BuildEnrichPrompt(clean, hint, sentence)) };
            var options = new AiProviderRequestOptions { MaxOutputTokens = 1536, Temperature = 0.2, RequestTimeoutSeconds = 15 };

            string? lastProblem = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var reply = await _aiClient.GenerateReplyAsync(0, messages, ct, options);
                    var parsed = VocabEnrichParser.TryParse(reply.Text, out var result);
                    if (parsed && result != null)
                    {
                        if (_cache != null)
                        {
                            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                        }

                        return new VocabEnrichResponse { Success = true, Result = result };
                    }

                    lastProblem = "Kết quả gợi ý chưa đúng định dạng.";
                }
                catch (AiProviderException ex)
                {
                    _logger.LogWarning(ex, "AI enrich failed for term: {Term}", clean);
                    return new VocabEnrichResponse { Success = false, ErrorMessage = AiProviderErrorMessages.For(ex.ErrorCode) };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AI enrich failed for term: {Term}", clean);
                    lastProblem = "Không thể lấy gợi ý từ vựng lúc này.";
                }
            }

            return new VocabEnrichResponse { Success = false, ErrorMessage = lastProblem ?? "Không thể lấy gợi ý từ vựng lúc này." };
        }

        public async Task<IReadOnlyList<VocabEnrichBatchItem>> EnrichBatchAsync(IReadOnlyList<VocabEnrichBatchRequest> items, CancellationToken ct = default)
        {
            if (items == null || items.Count == 0 || items.Count > MaxBatchItems)
            {
                throw new ArgumentException($"Batch must contain 1-{MaxBatchItems} items.", nameof(items));
            }

            using var gate = new SemaphoreSlim(3);
            var tasks = items.Select(async item =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var term = item.Term?.Trim() ?? string.Empty;
                    var response = await EnrichAsync(term, item.MeaningHint, null, ct);
                    return new VocabEnrichBatchItem { Term = term, Ok = response.Success, Result = response.Result, Error = response.Success ? null : response.ErrorMessage };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AI enrich batch item failed.");
                    return new VocabEnrichBatchItem { Term = item.Term?.Trim() ?? string.Empty, Ok = false, Error = "Lỗi khi điền gợi ý." };
                }
                finally
                {
                    gate.Release();
                }
            }).ToList();

            return await Task.WhenAll(tasks);
        }

        public async Task<IReadOnlyList<ImageSearchHit>> SearchImagesAsync(string query, int max, CancellationToken ct = default)
        {
            var q = query?.Trim();
            if (string.IsNullOrEmpty(q))
            {
                return Array.Empty<ImageSearchHit>();
            }

            if (q.Length > 60)
            {
                q = q[..60];
            }

            var apiKey = _configuration["Pixabay:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Pixabay API key is missing.");
                return Array.Empty<ImageSearchHit>();
            }

            var perPage = Math.Clamp(max, 3, 8);
            var url = $"https://pixabay.com/api/?key={apiKey}&q={Uri.EscapeDataString(q)}&image_type=photo&per_page={perPage}&safesearch=true&lang=en";
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pixabay API returned {StatusCode} for query: {Query}", response.StatusCode, q);
                return Array.Empty<ImageSearchHit>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ImageSearchHit>();
            }

            var list = new List<ImageSearchHit>();
            foreach (var hit in hits.EnumerateArray())
            {
                var web = hit.TryGetProperty("webformatURL", out var w) ? w.GetString() : null;
                var preview = hit.TryGetProperty("previewURL", out var p) ? p.GetString() : null;
                if (string.IsNullOrEmpty(web) || string.IsNullOrEmpty(preview)
                    || !CardInputNormalizer.IsPixabayUrl(web) || !CardInputNormalizer.IsPixabayUrl(preview))
                {
                    continue;
                }

                var page = hit.TryGetProperty("pageURL", out var pg) ? pg.GetString() : null;
                list.Add(new ImageSearchHit
                {
                    PreviewUrl = preview,
                    WebformatUrl = web,
                    PageUrl = page != null && page.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? page : null,
                    Author = hit.TryGetProperty("user", out var u) ? Truncate(u.GetString(), 100) : null,
                });
                if (list.Count >= perPage)
                {
                    break;
                }
            }

            return list;
        }

        private static string? Truncate(string? value, int max)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.Length > max ? text[..max] : text;
        }

        public static string BuildEnrichPrompt(string term, string? meaningHint, string? context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an English-Vietnamese dictionary for Vietnamese office workers who are learning English.");
            sb.AppendLine($"Describe the English word or phrase: \"{term}\".");
            sb.AppendLine("Rules:");
            sb.AppendLine("- meaning_vi: natural, common Vietnamese, 1-6 words, several senses separated by commas.");
            sb.AppendLine("- examples: at most 15 words, everyday or workplace register, each with a natural Vietnamese translation, no sensitive content.");
            if (!string.IsNullOrWhiteSpace(meaningHint))
            {
                sb.AppendLine($"- The learner already wrote this Vietnamese meaning: \"{meaningHint}\". The primary sense must match it; never replace it.");
            }

            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine($"- The word is used in this sentence: \"{context}\". Pick the sense used there.");
            }

            sb.AppendLine("- If it is not a real English word or phrase, set is_valid_word to false and put the likeliest spelling in did_you_mean.");
            sb.AppendLine("- topic_vi must be exactly one of: " + string.Join(", ", SetTopics.All) + ".");
            sb.AppendLine("- image_query: 1-3 concrete English words that would find a helpful photo.");
            sb.AppendLine("- Limits: senses 1-3 with 2-3 examples each; synonyms at most 6; antonyms at most 4; collocations at most 5; word_family at most 6.");
            sb.AppendLine("Return ONLY raw JSON (no markdown, no code fences) in exactly this shape; use null or [] for unknown values:");
            sb.AppendLine("{\"is_valid_word\":true,\"did_you_mean\":null,\"lemma\":\"run\",\"ipa_us\":\"/rʌn/\",\"ipa_uk\":null,\"cefr\":\"A1\",");
            sb.AppendLine("\"senses\":[{\"pos\":[\"verb\"],\"meaning_vi\":\"chạy\",\"meaning_en\":\"move fast on foot\",\"examples\":[{\"text\":\"I run every morning.\",\"vi\":\"Tôi chạy mỗi sáng.\"}]}],");
            sb.AppendLine("\"synonyms\":[],\"antonyms\":[],\"collocations\":[{\"text\":\"run a meeting\",\"vi\":\"điều hành cuộc họp\"}],");
            sb.AppendLine("\"word_family\":[{\"text\":\"runner\",\"pos\":\"noun\",\"vi\":\"người chạy\"}],");
            sb.AppendLine("\"verb_forms\":{\"past\":\"ran\",\"past_participle\":\"run\",\"ing\":\"running\",\"third_person\":\"runs\"},");
            sb.AppendLine("\"plural\":null,\"topic_vi\":\"Thể thao\",\"emoji\":\"🏃\",\"image_query\":\"person running\",\"memory_tip_vi\":null}");
            sb.AppendLine("pos values must come from: " + string.Join(", ", CardPartsOfSpeech.All.Select(p => p.Code)) + ".");
            sb.AppendLine("cefr must be one of A1, A2, B1, B2, C1, C2 or null.");
            return sb.ToString();
        }
    }

    /// <summary>Tolerant parser + post-validation of the model's JSON. Never throws; returns false on unusable output.</summary>
    public static class VocabEnrichParser
    {
        public static bool TryParse(string? text, out VocabEnrichResultDto? result)
        {
            result = null;
            var json = ExtractJson(text);
            if (json == null)
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var dto = new VocabEnrichResultDto
                {
                    IsValidWord = !root.TryGetProperty("is_valid_word", out var valid) || valid.ValueKind != JsonValueKind.False,
                    DidYouMean = Str(root, "did_you_mean", 100),
                    Lemma = Str(root, "lemma", 100),
                    IpaUs = Ipa(Str(root, "ipa_us", 100)),
                    IpaUk = Ipa(Str(root, "ipa_uk", 100)),
                    Cefr = CefrLevels.IsValid(Str(root, "cefr", 2)?.ToUpperInvariant()) ? Str(root, "cefr", 2)!.ToUpperInvariant() : null,
                    Synonyms = StrList(root, "synonyms", 6, 80),
                    Antonyms = StrList(root, "antonyms", 4, 80),
                    Plural = Str(root, "plural", 80),
                    TopicVi = Topic(Str(root, "topic_vi", 60)),
                    Emoji = SingleGrapheme(Str(root, "emoji", 16)),
                    ImageQuery = Str(root, "image_query", 60),
                    MemoryTipVi = Str(root, "memory_tip_vi", 300),
                };

                if (root.TryGetProperty("senses", out var senses) && senses.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sense in senses.EnumerateArray().Take(3))
                    {
                        if (sense.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var item = new VocabEnrichSense
                        {
                            Pos = CardPartsOfSpeech.Filter(PosList(sense)).ToList(),
                            MeaningVi = Str(sense, "meaning_vi", 300),
                            MeaningEn = Str(sense, "meaning_en", 400),
                        };

                        if (sense.TryGetProperty("examples", out var examples) && examples.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var example in examples.EnumerateArray().Take(3))
                            {
                                var textValue = Str(example, "text", 300);
                                if (textValue != null)
                                {
                                    item.Examples.Add(new CardExample { Text = textValue, Vi = Str(example, "vi", 300) });
                                }
                            }
                        }

                        if (item.MeaningVi != null)
                        {
                            dto.Senses.Add(item);
                        }
                    }
                }

                if (root.TryGetProperty("collocations", out var collocations) && collocations.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in collocations.EnumerateArray().Take(5))
                    {
                        var t = Str(c, "text", 120);
                        if (t != null)
                        {
                            dto.Collocations.Add(new CardCollocation { Text = t, Vi = Str(c, "vi", 200) });
                        }
                    }
                }

                if (root.TryGetProperty("word_family", out var family) && family.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in family.EnumerateArray().Take(6))
                    {
                        var t = Str(f, "text", 80);
                        if (t != null)
                        {
                            var pos = Str(f, "pos", 20)?.ToLowerInvariant();
                            dto.WordFamily.Add(new CardWordFamilyItem { Text = t, Pos = CardPartsOfSpeech.IsValid(pos) ? pos : null, Vi = Str(f, "vi", 200) });
                        }
                    }
                }

                if (root.TryGetProperty("verb_forms", out var forms) && forms.ValueKind == JsonValueKind.Object)
                {
                    var verbForms = new CardVerbForms
                    {
                        Past = Str(forms, "past", 80),
                        PastParticiple = Str(forms, "past_participle", 80),
                        Ing = Str(forms, "ing", 80),
                        ThirdPerson = Str(forms, "third_person", 80),
                    };
                    dto.VerbForms = verbForms.IsEmpty ? null : verbForms;
                }

                // A real word without any usable sense is a schema failure (the caller retries once).
                if (dto.IsValidWord && dto.Senses.Count == 0)
                {
                    return false;
                }

                result = dto;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string? ExtractJson(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewLine = trimmed.IndexOf('\n');
                trimmed = firstNewLine >= 0 ? trimmed[(firstNewLine + 1)..] : trimmed[3..];
                var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0)
                {
                    trimmed = trimmed[..fence];
                }
            }

            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            return start >= 0 && end > start ? trimmed[start..(end + 1)] : null;
        }

        private static string? Str(JsonElement element, string name, int max)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = value.GetString()?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.Length > max ? text[..max] : text;
        }

        private static List<string> StrList(JsonElement element, string name, int maxCount, int maxLength)
        {
            var list = new List<string>();
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(text) && !list.Contains(text, StringComparer.OrdinalIgnoreCase))
                        {
                            list.Add(text.Length > maxLength ? text[..maxLength] : text);
                        }
                    }

                    if (list.Count >= maxCount)
                    {
                        break;
                    }
                }
            }

            return list;
        }

        private static IEnumerable<string?> PosList(JsonElement sense)
        {
            if (!sense.TryGetProperty("pos", out var pos))
            {
                yield break;
            }

            if (pos.ValueKind == JsonValueKind.String)
            {
                yield return pos.GetString();
            }
            else if (pos.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pos.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        yield return item.GetString();
                    }
                }
            }
        }

        private static string? Ipa(string? value)
        {
            if (value == null)
            {
                return null;
            }

            var core = value.Trim('/', ' ');
            return core.Length == 0 ? null : "/" + core + "/";
        }

        private static string Topic(string? value)
            => value != null && SetTopics.All.Contains(value) ? value : "Khác";

        private static string? SingleGrapheme(string? value)
            => value != null && new StringInfo(value).LengthInTextElements == 1 ? value : null;
    }
}
