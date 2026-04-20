using System;
using System.IO;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingAdminControllerStructureTests
{
    [Fact]
    public void WritingAdmin_CreateAndEdit_UseTransactionHelperInsteadOfDirectTransactions()
    {
        var source = ReadControllerSource();
        var createSlice = SliceBetween(
            source,
            "public async Task<IActionResult> Create([FromForm] WritingExerciseCreateViewModel model)",
            "public async Task<IActionResult> Edit(int id)");
        var editSlice = SliceBetween(
            source,
            "public async Task<IActionResult> Edit(int id, [FromForm] WritingExerciseEditViewModel model)",
            "[HttpPost]");

        Assert.Contains("ExecuteWritingTransactionAsync", createSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginTransactionAsync", createSlice, StringComparison.Ordinal);

        Assert.Contains("ExecuteWritingTransactionAsync", editSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginTransactionAsync", editSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingAdmin_TransactionHelper_WrapsManualTransactionInsideExecutionStrategy()
    {
        var source = ReadControllerSource();
        var helperSlice = SliceBetween(
            source,
            "private async Task ExecuteWritingTransactionAsync(Func<Task> action)",
            "private void SyncWritingExerciseSentences(");

        Assert.Contains("_context.Database.CreateExecutionStrategy()", helperSlice, StringComparison.Ordinal);
        Assert.Contains("strategy.ExecuteAsync", helperSlice, StringComparison.Ordinal);
        Assert.Contains("_context.Database.BeginTransactionAsync()", helperSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingAdmin_CreateAndEdit_LogFailuresWithActionAndResourceContext()
    {
        var source = ReadControllerSource();
        var createSlice = SliceBetween(
            source,
            "public async Task<IActionResult> Create([FromForm] WritingExerciseCreateViewModel model)",
            "public async Task<IActionResult> Edit(int id)");
        var editSlice = SliceBetween(
            source,
            "public async Task<IActionResult> Edit(int id, [FromForm] WritingExerciseEditViewModel model)",
            "[HttpPost]");

        Assert.Contains("nameof(WritingExerciseManagementController)", createSlice, StringComparison.Ordinal);
        Assert.Contains("nameof(Create)", createSlice, StringComparison.Ordinal);
        Assert.Contains("adminId", createSlice, StringComparison.Ordinal);
        Assert.Contains("model.Title", createSlice, StringComparison.Ordinal);

        Assert.Contains("nameof(WritingExerciseManagementController)", editSlice, StringComparison.Ordinal);
        Assert.Contains("nameof(Edit)", editSlice, StringComparison.Ordinal);
        Assert.Contains("adminId", editSlice, StringComparison.Ordinal);
        Assert.Contains("id", editSlice, StringComparison.Ordinal);
        Assert.Contains("model.Title", editSlice, StringComparison.Ordinal);
    }

    private static string ReadControllerSource()
    {
        return File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "Areas",
            "Admin",
            "Controllers",
            "WritingExerciseManagementController.cs"));
    }

    private static string SliceBetween(string source, string startMarker, string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Expected to find start marker: {startMarker}");

        var searchFrom = startIndex + startMarker.Length;
        var endIndex = source.IndexOf(endMarker, searchFrom, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, $"Expected to find end marker: {endMarker}");

        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string GetWorkspaceRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
