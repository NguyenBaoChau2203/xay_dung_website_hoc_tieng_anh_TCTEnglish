using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using Xunit;

namespace TCTEnglish.Tests;

public class CardInputNormalizerTests
{
    private static CardInputDto Valid() => new() { Term = "  resilient ", Definition = " kiên cường " };

    [Fact]
    public void TrimsAndAcceptsMinimalCard()
    {
        var result = CardInputNormalizer.Normalize(Valid());
        Assert.True(result.IsValid);
        Assert.Equal("resilient", result.Data.Term);
        Assert.Equal("kiên cường", result.Data.Definition);
        Assert.Null(result.Data.DetailsJson);
        Assert.Null(result.Data.Example);
    }

    [Theory]
    [InlineData(null, "nghĩa", "Term", "Nhập từ")]
    [InlineData("   ", "nghĩa", "Term", "Nhập từ")]
    [InlineData("word", null, "Definition", "Nhập nghĩa tiếng Việt")]
    [InlineData("word", "  ", "Definition", "Nhập nghĩa tiếng Việt")]
    public void MissingTermOrDefinitionIsReportedPerField(string? term, string? definition, string field, string message)
    {
        var result = CardInputNormalizer.Normalize(new CardInputDto { Term = term, Definition = definition });
        Assert.False(result.IsValid);
        Assert.Equal(message, result.FieldErrors[field]);
    }

    [Fact]
    public void LongTermAndDefinitionAreRejectedWithLimitMessage()
    {
        var result = CardInputNormalizer.Normalize(new CardInputDto { Term = new string('a', 256), Definition = new string('b', 1001) });
        Assert.Equal("Tối đa 255 ký tự", result.FieldErrors["Term"]);
        Assert.Equal("Tối đa 1000 ký tự", result.FieldErrors["Definition"]);
    }

    [Fact]
    public void ListsAreClampedToDocumentedLimitsAndFirstExampleMirrorsLegacyColumns()
    {
        var dto = Valid();
        dto.Examples = Enumerable.Range(1, 9).Select(i => new CardExample { Text = $"Example {i}", Vi = $"Ví dụ {i}" }).ToList();
        dto.Synonyms = Enumerable.Range(1, 15).Select(i => $"syn{i}").Concat(new[] { "SYN1", " " }).ToList();
        dto.Collocations = Enumerable.Range(1, 12).Select(i => new CardCollocation { Text = $"col{i}" }).ToList();
        dto.WordFamily = Enumerable.Range(1, 12).Select(i => new CardWordFamilyItem { Text = $"wf{i}", Pos = "noun" }).ToList();

        var result = CardInputNormalizer.Normalize(dto);
        var details = CardDetailsCodec.Parse(result.Data.DetailsJson, out var invalid);

        Assert.False(invalid);
        Assert.Equal(6, details.Examples.Count);
        Assert.Equal(10, details.Synonyms.Count);
        Assert.Equal(8, details.Collocations.Count);
        Assert.Equal(8, details.WordFamily.Count);
        Assert.Equal("Example 1", result.Data.Example);
        Assert.Equal("Ví dụ 1", result.Data.ExampleTranslation);
    }

    [Fact]
    public void ExampleWithoutTextIsDroppedAndNoExampleLeavesLegacyColumnsNull()
    {
        var dto = Valid();
        dto.Examples = new() { new CardExample { Text = "  ", Vi = "chỉ có nghĩa" } };
        var result = CardInputNormalizer.Normalize(dto);
        Assert.Null(result.Data.Example);
        Assert.Null(result.Data.ExampleTranslation);
        Assert.Null(result.Data.DetailsJson);
    }

    [Theory]
    [InlineData("😀", "😀")]
    [InlineData("👨‍👩‍👧", "👨‍👩‍👧")]
    [InlineData("😀😀", null)]
    [InlineData("ab", null)]
    [InlineData("  ", null)]
    public void EmojiMustBeExactlyOneGrapheme_OtherwiseDroppedSilently(string value, string? expected)
    {
        var dto = Valid();
        dto.Emoji = value;
        var result = CardInputNormalizer.Normalize(dto);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Data.Emoji);
    }

    [Theory]
    [InlineData("b2", "B2", true)]
    [InlineData("C1", "C1", true)]
    [InlineData("", null, true)]
    [InlineData("D1", null, false)]
    public void CefrMustBeEmptyOrAKnownLevel(string value, string? expected, bool valid)
    {
        var dto = Valid();
        dto.Cefr = value;
        var result = CardInputNormalizer.Normalize(dto);
        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            Assert.Equal(expected, result.Data.Cefr);
        }
        else
        {
            Assert.True(result.FieldErrors.ContainsKey("Cefr"));
        }
    }

    [Fact]
    public void UnknownPartsOfSpeechAreFilteredDuplicatesRemovedAndCappedAtSix()
    {
        var dto = Valid();
        dto.PartsOfSpeech = new() { "noun", "VERB", "noun", "bogus", "adj", "adv", "pron", "prep", "conj" };
        var result = CardInputNormalizer.Normalize(dto);
        Assert.Equal("noun,verb,adj,adv,pron,prep", result.Data.PartsOfSpeech);
    }

    [Theory]
    [InlineData("/images/cards/abc.jpg", true)]
    [InlineData("/uploads/shared/abc.png", true)]
    [InlineData("https://pixabay.com/get/x.jpg", true)]
    [InlineData("https://cdn.pixabay.com/photo/x.jpg", true)]
    [InlineData("http://pixabay.com/x.jpg", false)]
    [InlineData("https://evilpixabay.com/x.jpg", false)]
    [InlineData("https://pixabay.com.evil.test/x.jpg", false)]
    [InlineData("https://user@pixabay.com/x.jpg", false)]
    [InlineData("/images/cards/../../secret.png", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://example.com/a.jpg", false)]
    public void ImageUrlIsLimitedToOwnStorageOrPixabayHosts(string url, bool allowed)
    {
        var dto = Valid();
        dto.ImageUrl = url;
        var result = CardInputNormalizer.Normalize(dto);
        Assert.Equal(allowed, result.IsValid);
        if (!allowed)
        {
            Assert.Equal("Ảnh không hợp lệ", result.FieldErrors["ImageUrl"]);
        }
    }

    [Fact]
    public void ImageSourceKeepsKnownProviderAndDropsUnsafePageUrl()
    {
        var dto = Valid();
        dto.ImageUrl = "https://pixabay.com/get/x.jpg";
        dto.ImageSource = new CardImageSource { Provider = "pixabay", PageUrl = "javascript:alert(1)", Author = " Ann " };
        var json = CardInputNormalizer.Normalize(dto).Data.ImageSourceJson;
        var source = CardDetailsCodec.ParseImageSource(json);
        Assert.Equal("pixabay", source!.Provider);
        Assert.Null(source.PageUrl);
        Assert.Equal("Ann", source.Author);

        dto.ImageSource = new CardImageSource { Provider = "other" };
        Assert.Null(CardInputNormalizer.Normalize(dto).Data.ImageSourceJson);
    }

    [Fact]
    public void BadDetailsJsonIsTreatedAsEmpty()
    {
        var details = CardDetailsCodec.Parse("{not json", out var invalid);
        Assert.True(invalid);
        Assert.Empty(details.Examples);
    }

    [Fact]
    public void DetailsRoundTripWithCamelCaseAndOmitNulls()
    {
        var dto = Valid();
        dto.VerbForms = new CardVerbForms { Past = "ran" };
        dto.Plural = "runs";
        var json = CardInputNormalizer.Normalize(dto).Data.DetailsJson!;
        Assert.Contains("\"verbForms\"", json);
        Assert.Contains("\"past\":\"ran\"", json);
        Assert.DoesNotContain("pastParticiple", json);
        var parsed = CardDetailsCodec.Parse(json, out _);
        Assert.Equal("ran", parsed.VerbForms!.Past);
        Assert.Equal("runs", parsed.Plural);
    }

    [Fact]
    public void CoverColorIsStableAndWithinSixHues()
    {
        Assert.Equal(SetCoverColor.For(301), SetCoverColor.For(301));
        foreach (var id in Enumerable.Range(1, 200))
        {
            Assert.Contains(SetCoverColor.For(id), SetCoverColor.Hues);
        }
        Assert.True(Enumerable.Range(1, 200).Select(SetCoverColor.For).Distinct().Count() > 3);
    }

    [Fact]
    public void StatusPriorityIsSuspendedHardLearnedLearningNew()
    {
        Assert.Equal(CardStatus.New, CardStatus.Classify(false, null, null, hasProgress: false));
        Assert.Equal(CardStatus.Suspended, CardStatus.Classify(true, 9, "Mastered", true));
        Assert.Equal(CardStatus.Hard, CardStatus.Classify(false, 3, "Mastered", true));
        Assert.Equal(CardStatus.Learned, CardStatus.Classify(false, 2, "Mastered", true));
        Assert.Equal(CardStatus.Learning, CardStatus.Classify(false, 0, "Reviewing", true));
        Assert.Equal(CardStatus.New, CardStatus.Classify(false, 0, null, true));
    }
}
