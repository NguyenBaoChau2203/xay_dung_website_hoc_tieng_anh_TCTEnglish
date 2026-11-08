namespace TCTEnglish.Services.AI.Rag.Corpus;

public static class CorpusChunker
{
    public static PreparedCorpusDocument Prepare(CorpusDocument document)
    {
        var sentences = SplitSentences(document);
        var chunks = Pack(document, sentences);
        return new PreparedCorpusDocument(document, sentences, chunks);
    }

    public static IReadOnlyList<CorpusSentence> SplitSentences(CorpusDocument document)
    {
        var sentences = new List<CorpusSentence>();
        var body = document.Body ?? string.Empty;
        if (body.Length > CorpusVersions.MaxBodyCharacters)
        {
            body = body[..CorpusVersions.MaxBodyCharacters];
        }

        var paragraphIndex = 0;
        foreach (var paragraph in SplitParagraphs(body))
        {
            var heading = IsHeading(paragraph);
            foreach (var slice in SplitParagraphSentences(paragraph))
            {
                if (sentences.Count >= CorpusVersions.MaxSentencesPerDocument)
                {
                    return sentences;
                }

                var number = sentences.Count + 1;
                sentences.Add(new CorpusSentence(
                    $"{document.SourceKey}#s{number}",
                    number,
                    slice,
                    CorpusHash.Sha256(slice),
                    paragraphIndex,
                    heading && number == sentences.Count + 1));
            }

            paragraphIndex++;
        }

        for (var index = 0; index < sentences.Count; index++)
        {
            var sentence = sentences[index];
            var heading = IsHeading(sentence.Text) && sentence.ParagraphIndex >= 0
                && (index == 0 || sentences[index - 1].ParagraphIndex != sentence.ParagraphIndex);
            if (heading != sentence.IsHeading)
            {
                sentences[index] = sentence with { IsHeading = heading };
            }
        }

        return ExpandSpans(sentences);
    }

    private static List<CorpusSentence> ExpandSpans(List<CorpusSentence> sentences)
    {
        var expanded = new List<CorpusSentence>(sentences.Count);
        foreach (var sentence in sentences)
        {
            if (expanded.Count >= CorpusVersions.MaxSentencesPerDocument)
            {
                break;
            }

            var spans = SplitSpans(sentence.Text);
            for (var part = 0; part < spans.Count && expanded.Count < CorpusVersions.MaxSentencesPerDocument; part++)
            {
                var span = spans[part];
                expanded.Add(sentence with
                {
                    SentenceId = spans.Count == 1 ? sentence.SentenceId : $"{sentence.SentenceId}p{part + 1}",
                    Number = expanded.Count + 1,
                    Text = span,
                    ContentHash = CorpusHash.Sha256(span),
                    IsHeading = sentence.IsHeading && part == 0
                });
            }
        }

        return expanded;
    }

    private static List<string> SplitSpans(string text)
    {
        if (ApproximateTokenCounter.Count(text) <= CorpusVersions.MaxChunkTokens)
        {
            return [text];
        }

        var spans = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var best = start;
            for (var end = start + 1; end <= text.Length; end++)
            {
                if (ApproximateTokenCounter.Count(text[start..end]) > CorpusVersions.MaxChunkTokens)
                {
                    break;
                }

                best = end;
            }

            var cut = best;
            if (best < text.Length)
            {
                var boundary = -1;
                for (var index = start + 1; index <= best; index++)
                {
                    if (char.IsWhiteSpace(text[index - 1]))
                    {
                        boundary = index;
                    }
                }

                if (boundary > start)
                {
                    cut = boundary;
                }
            }

            if (cut <= start)
            {
                cut = Math.Min(text.Length, start + 1);
            }

            spans.Add(text[start..cut]);
            start = cut;
        }

        return spans;
    }

    private static List<CorpusChunk> Pack(CorpusDocument document, IReadOnlyList<CorpusSentence> sentences)
    {
        var titleTokens = LexicalNormalizer.Unigrams(document.Title);
        var keywordTokens = LexicalNormalizer.KeywordTokens(document.Keywords);
        var chunks = new List<CorpusChunk>();
        var current = new List<CorpusSentence>();
        var currentTokens = 0;

        void Flush(bool overlap)
        {
            if (current.Count == 0)
            {
                return;
            }

            var overlapSentence = overlap ? current[^1] : null;
            chunks.Add(CreateChunk(document.SourceKey, chunks.Count + 1, current, titleTokens, keywordTokens));
            current = [];
            currentTokens = 0;
            if (overlapSentence is not null && ApproximateTokenCounter.Count(overlapSentence.Text) <= CorpusVersions.MaxOverlapTokens)
            {
                current.Add(overlapSentence);
                currentTokens = ApproximateTokenCounter.Count(overlapSentence.Text);
            }
        }

        foreach (var sentence in sentences)
        {
            var tokens = ApproximateTokenCounter.Count(sentence.Text);
            var separator = current.Count == 0 ? 0 : 1;
            var headingBreak = sentence.IsHeading && current.Count > 0;
            var lengthBreak = current.Count > 0 && currentTokens + separator + tokens > CorpusVersions.MaxChunkTokens;
            if (headingBreak || lengthBreak)
            {
                Flush(overlap: lengthBreak && !headingBreak);
            }

            separator = current.Count == 0 ? 0 : 1;
            if (current.Count > 0 && currentTokens + separator + tokens > CorpusVersions.MaxChunkTokens)
            {
                current.Clear();
                currentTokens = 0;
                separator = 0;
            }

            current.Add(sentence);
            currentTokens += separator + tokens;
            if (currentTokens >= CorpusVersions.MaxChunkTokens && current.Count > 1)
            {
                Flush(overlap: false);
            }
        }

        Flush(overlap: false);
        if (chunks.Count == 0)
        {
            chunks.Add(CreateChunk(document.SourceKey, 1, [], titleTokens, keywordTokens));
        }

        return chunks;
    }

    private static CorpusChunk CreateChunk(
        string sourceKey,
        int number,
        IReadOnlyList<CorpusSentence> sentences,
        IReadOnlyList<string> titleTokens,
        IReadOnlyList<string> keywordTokens)
    {
        var bodyTokens = new List<string>();
        foreach (var sentence in sentences)
        {
            bodyTokens.AddRange(LexicalNormalizer.Unigrams(sentence.Text));
        }

        return new CorpusChunk(
            $"{sourceKey}#c{number}",
            sourceKey,
            number,
            sentences.Select(sentence => sentence.Number).Distinct().ToList(),
            titleTokens,
            keywordTokens,
            bodyTokens);
    }

    private static List<string> SplitParagraphs(string body)
    {
        var paragraphs = new List<string>();
        var start = 0;
        for (var index = 0; index < body.Length; index++)
        {
            if (body[index] != '\n' || index + 1 >= body.Length || body[index + 1] != '\n')
            {
                continue;
            }

            AddParagraph(paragraphs, body[start..index]);
            index++;
            while (index + 1 < body.Length && body[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        if (start <= body.Length)
        {
            AddParagraph(paragraphs, body[start..]);
        }

        return paragraphs;
    }

    private static void AddParagraph(List<string> paragraphs, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            paragraphs.Add(value.Trim('\r'));
        }
    }

    private static bool IsHeading(string paragraph)
    {
        var trimmed = paragraph.TrimStart();
        return trimmed.StartsWith("#", StringComparison.Ordinal);
    }

    private static List<string> SplitParagraphSentences(string paragraph)
    {
        var sentences = new List<string>();
        var start = 0;
        for (var index = 0; index < paragraph.Length; index++)
        {
            var character = paragraph[index];
            if (character == '\n')
            {
                AddSentence(sentences, paragraph[start..index]);
                start = index + 1;
                continue;
            }

            if (!IsSentenceBoundary(paragraph, index))
            {
                continue;
            }

            AddSentence(sentences, paragraph[start..(index + 1)]);
            var next = index + 1;
            while (next < paragraph.Length && paragraph[next] == ' ')
            {
                next++;
            }

            start = next;
            index = next - 1;
        }

        if (start < paragraph.Length)
        {
            AddSentence(sentences, paragraph[start..]);
        }

        return sentences;
    }

    private static void AddSentence(List<string> sentences, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sentences.Add(value.Trim());
        }
    }

    private static bool IsSentenceBoundary(string text, int index)
    {
        var character = text[index];
        if (character is not ('.' or '!' or '?' or '…'))
        {
            return false;
        }

        var next = index + 1 < text.Length ? text[index + 1] : '\0';
        if (next != '\0' && !char.IsWhiteSpace(next))
        {
            return false;
        }

        if (character == '.' && index > 0 && char.IsDigit(text[index - 1]))
        {
            var cursor = index - 1;
            while (cursor > 0 && char.IsDigit(text[cursor - 1]))
            {
                cursor--;
            }

            if (cursor == 0 || char.IsWhiteSpace(text[cursor - 1]))
            {
                return false;
            }
        }

        return true;
    }
}
