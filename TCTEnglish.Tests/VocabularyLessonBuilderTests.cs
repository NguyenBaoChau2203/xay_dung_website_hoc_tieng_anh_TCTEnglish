using TCTVocabulary.Services.Vocabulary;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class VocabularyLessonBuilderTests
{
    private static List<LessonCard> Cards(string? topic, int count, int startId = 1) =>
        Enumerable.Range(startId, count).Select(id => new LessonCard(id, topic)).ToList();

    [Fact]
    public void Build_WithNoCards_ReturnsNoLessons()
    {
        Assert.Empty(VocabularyLessonBuilder.Build(Array.Empty<LessonCard>()));
    }

    [Theory]
    [InlineData(3, new[] { 3 })]
    [InlineData(4, new[] { 4 })]
    [InlineData(10, new[] { 10 })]
    [InlineData(13, new[] { 13 })]
    [InlineData(14, new[] { 10, 4 })]
    [InlineData(20, new[] { 10, 10 })]
    [InlineData(23, new[] { 10, 13 })]
    [InlineData(24, new[] { 10, 10, 4 })]
    public void Build_SplitsOneTopicIntoPartsAndMergesAShortTail(int cardCount, int[] expectedSizes)
    {
        var lessons = VocabularyLessonBuilder.Build(Cards("Animals", cardCount));

        Assert.Equal(expectedSizes, lessons.Select(lesson => lesson.CardIds.Count).ToArray());
        Assert.All(lessons, lesson => Assert.Equal(expectedSizes.Length, lesson.PartCount));
        Assert.Equal(Enumerable.Range(1, expectedSizes.Length), lessons.Select(lesson => lesson.Part));
        Assert.Equal(Enumerable.Range(1, cardCount), lessons.SelectMany(lesson => lesson.CardIds));
    }

    [Fact]
    public void Build_KeepsTopicsInFirstAppearanceOrderAndNumbersAcrossTopics()
    {
        var cards = new List<LessonCard>
        {
            new(1, "Colors"), new(2, "Animals"), new(3, "Colors"), new(4, "Animals"),
            new(5, "Colors"), new(6, "Colors"), new(7, "Animals"), new(8, "Animals")
        };

        var lessons = VocabularyLessonBuilder.Build(cards);

        Assert.Equal(new[] { "Colors", "Animals" }, lessons.Select(lesson => lesson.Topic).ToArray());
        Assert.Equal(new[] { 1, 2 }, lessons.Select(lesson => lesson.Number).ToArray());
        Assert.Equal(new[] { 1, 3, 5, 6 }, lessons[0].CardIds.ToArray());
        Assert.Equal(new[] { 2, 4, 7, 8 }, lessons[1].CardIds.ToArray());
    }

    [Fact]
    public void Build_OrdersCardsByIdBeforeGrouping()
    {
        var cards = new List<LessonCard> { new(9, "B"), new(2, "A"), new(5, "B"), new(1, "A") };

        var lessons = VocabularyLessonBuilder.Build(cards);

        Assert.Equal(new[] { "A", "B" }, lessons.Select(lesson => lesson.Topic).ToArray());
        Assert.Equal(new[] { 1, 2 }, lessons[0].CardIds.ToArray());
        Assert.Equal(new[] { 5, 9 }, lessons[1].CardIds.ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_UsesTheGeneralTopicForBlankTopics(string? topic)
    {
        var lessons = VocabularyLessonBuilder.Build(Cards(topic, 5));

        var lesson = Assert.Single(lessons);
        Assert.Equal(VocabularyLessonBuilder.DefaultTopic, lesson.Topic);
        Assert.Equal("Từ vựng chung", lesson.Topic);
    }

    [Fact]
    public void Build_TrimsTopicsSoPaddedNamesShareOneLesson()
    {
        var cards = new List<LessonCard> { new(1, "Animals"), new(2, " Animals "), new(3, "Animals") , new(4, "Animals") };

        var lesson = Assert.Single(VocabularyLessonBuilder.Build(cards));

        Assert.Equal("Animals", lesson.Topic);
        Assert.Equal(4, lesson.CardIds.Count);
    }

    [Fact]
    public void Build_NumbersLessonsAcrossTopicsAndParts()
    {
        var cards = Cards("A", 14).Concat(Cards("B", 5, startId: 100)).ToList();

        var lessons = VocabularyLessonBuilder.Build(cards);

        Assert.Equal(new[] { 1, 2, 3 }, lessons.Select(lesson => lesson.Number).ToArray());
        Assert.Equal(new[] { "A", "A", "B" }, lessons.Select(lesson => lesson.Topic).ToArray());
        Assert.Equal(new[] { 1, 2, 1 }, lessons.Select(lesson => lesson.Part).ToArray());
        Assert.Equal(new[] { 2, 2, 1 }, lessons.Select(lesson => lesson.PartCount).ToArray());
    }

    [Fact]
    public void Title_AddsThePartOnlyWhenTheTopicHasSeveralParts()
    {
        var lessons = VocabularyLessonBuilder.Build(Cards("Animals", 14).Concat(Cards("Colors", 5, 50)).ToList());

        Assert.Equal("Bài 1 · Animals · phần 1", lessons[0].Title);
        Assert.Equal("Bài 2 · Animals · phần 2", lessons[1].Title);
        Assert.Equal("Bài 3 · Colors", lessons[2].Title);
    }

    [Fact]
    public void Build_HonoursACustomSizeAndTail()
    {
        var lessons = VocabularyLessonBuilder.Build(Cards("A", 7), size: 3, minTail: 2);

        Assert.Equal(new[] { 3, 4 }, lessons.Select(lesson => lesson.CardIds.Count).ToArray());
    }

    [Fact]
    public void Build_IsDeterministicForTheSameInput()
    {
        var cards = Cards("A", 17).Concat(Cards("B", 6, 40)).Concat(Cards(null, 3, 80)).ToList();

        var first = VocabularyLessonBuilder.Build(cards);
        var second = VocabularyLessonBuilder.Build(cards.AsEnumerable().Reverse().ToList());

        Assert.Equal(
            first.Select(lesson => (lesson.Number, lesson.Topic, lesson.Part, lesson.PartCount, string.Join(",", lesson.CardIds))),
            second.Select(lesson => (lesson.Number, lesson.Topic, lesson.Part, lesson.PartCount, string.Join(",", lesson.CardIds))));
    }
}
