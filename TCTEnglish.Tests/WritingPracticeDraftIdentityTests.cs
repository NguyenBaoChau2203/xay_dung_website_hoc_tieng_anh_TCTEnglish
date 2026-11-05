using System;
using System.IO;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingPracticeDraftIdentityTests
{
    [Fact]
    public void WritingPractice_ExposesServerVerifiedOwnerIdForDraftNamespace()
    {
        var viewModelSource = File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "ViewModels",
            "WritingIndexViewModel.cs"));
        Assert.Contains("public int CurrentOwnerUserId { get; set; }", viewModelSource, StringComparison.Ordinal);

        var serviceSource = File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "Services",
            "WritingService.cs"));
        Assert.Contains(
            "BuildWritingPracticeViewModel(exerciseListViewModel, practiceData, selectedPage, userId)",
            serviceSource,
            StringComparison.Ordinal);
        Assert.Contains("CurrentOwnerUserId = currentOwnerUserId", serviceSource, StringComparison.Ordinal);

        var viewSource = File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "Views",
            "Study",
            "WritingPractice.cshtml"));
        Assert.Contains("data-owner-user-id=\"@Model.CurrentOwnerUserId\"", viewSource, StringComparison.Ordinal);

        var jsSource = File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "wwwroot",
            "js",
            "writing.js"));
        Assert.Contains("data-owner-user-id", jsSource, StringComparison.Ordinal);
        Assert.Contains("wp_draft_u", jsSource, StringComparison.Ordinal);
        Assert.Contains("hasOwnerScope", jsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("wp_draft_ex' + exerciseId", jsSource, StringComparison.Ordinal);
    }

    private static string GetWorkspaceRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
