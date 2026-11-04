using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class CatalogQueryRegressionTests
{
    [Fact]
    public async Task WritingList_PagesBeforeMaterializingAttempts_AndKeepsHistoricalPass()
    {
        await using var factory = new TestWebApplicationFactory();
        var counter = new RowCountingCommandInterceptor(); factory.SaveInterceptors.Add(counter);
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            for (int i = 0; i < 40; i++)
            {
                var exercise = new WritingExercise { Title = "Catalog " + i, Level = "beginner", ContentType = "articles", Topic = "catalog", SourceType = "manual", PreviewText = "Sample", IsPublished = true, CreatedAt = DateTime.UtcNow,
                    WritingExerciseSentences = [new() { SortOrder = 1, VietnameseText = "Học", EnglishMeaning = "Study" }] };
                db.WritingExercises.Add(exercise); await db.SaveChangesAsync();
                for (int attempt = 0; attempt < 8; attempt++) db.UserWritingAttempts.Add(new UserWritingAttempt
                { UserId = TestDataIds.UserId, WritingExerciseId = exercise.Id, WritingExerciseSentenceId = exercise.WritingExerciseSentences.Single().Id,
                    SubmittedAnswer = "Study", Passed = attempt == 0, EvaluationSource = "offline", ReviewText = new string('x', 1000), CreatedAtUtc = DateTime.UtcNow.AddMinutes(attempt) });
            }
            await db.SaveChangesAsync();
        }
        using var read = factory.Services.CreateScope();
        counter.Reset();
        var vm = await read.ServiceProvider.GetRequiredService<IWritingService>().GetWritingExerciseListViewModelAsync("beginner", "articles", "catalog", 1, TestDataIds.UserId, "completed");
        Assert.Equal(40, vm.TotalCount);
        Assert.Equal(6, vm.Exercises.Count);
        Assert.All(vm.Exercises, item => { Assert.Equal(8, item.AttemptCount); Assert.Equal(1, item.CompletedSentenceCount); });
        Assert.True(counter.Rows < 50, $"List materialized {counter.Rows} rows instead of SQL page/aggregate results.");
    }
}
