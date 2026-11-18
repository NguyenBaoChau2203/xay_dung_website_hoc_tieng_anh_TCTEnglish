using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Admin;
using TCTEnglish.Services.Sets;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class AdminVocabularyServiceTests : IAsyncLifetime
{
    private const int Owner = 1, Outsider = 2, Subscriber = 3, Admin = 8, SystemUser = 9;

    private SqliteConnection _connection = null!;
    private DbflashcardContext _db = null!;
    private AdminVocabularyService _service = null!;
    private PersonalSetService _personal = null!;
    private string _webRoot = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        foreach (var (id, role) in new[] { (Owner, "Standard"), (Outsider, "Standard"), (Subscriber, "Standard"), (Admin, "Admin") })
        {
            _db.Users.Add(new User { UserId = id, Email = $"u{id}@test.local", PasswordHash = "x", FullName = $"User {id}", Role = role, CreatedAt = DateTime.UtcNow });
        }

        _db.Users.Add(new User { UserId = SystemUser, Email = PersonalSetService.SystemOwnerEmail, PasswordHash = "x", FullName = "TCT System", Role = "System", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        _webRoot = Path.Combine(Path.GetTempPath(), "tct-admin-vocab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);
        _personal = new PersonalSetService(_db, TimeProvider.System, new StubFactory(), new StubEnvironment(_webRoot), NullLogger<PersonalSetService>.Instance);
        _service = new AdminVocabularyService(_db, _personal, NullLogger<AdminVocabularyService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, true);
        }
    }

    // ------------------------------------------------------------------ seed helpers

    private async Task<Folder> AddFolderAsync(int owner, string name)
    {
        var folder = new Folder { FolderName = name, UserId = owner };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    private async Task<Set> AddSetAsync(int owner, string name, string visibility = "Public", int? folderId = null, string? slug = null, DateTime? created = null, int views = 0, params Card[] cards)
    {
        var set = new Set
        {
            SetName = name, OwnerId = owner, Visibility = visibility, FolderId = folderId, ShareSlug = slug,
            CreatedAt = created ?? DateTime.UtcNow, ViewCount = views,
        };
        foreach (var card in cards)
        {
            set.Cards.Add(card);
        }

        _db.Sets.Add(set);
        await _db.SaveChangesAsync();
        return set;
    }

    private static Card NewCard(string term, string definition = "nghĩa", string? topic = null, string? example = "Ví dụ.", string? phonetic = "/x/", string? emoji = "🙂")
        => new() { Term = term, Definition = definition, Topic = topic, Example = example, Phonetic = phonetic, Emoji = emoji };

    private static AdminCardInput ValidCard(string term = "Dog") => new()
    {
        Term = term, Definition = "Con chó", Phonetic = "/dɒɡ/", Example = "My dog is friendly.", Topic = "Animals", Emoji = "🐶",
    };

    // ------------------------------------------------------------------ S1: folder / owner

    [Fact]
    public async Task S1_TheSharingRule_GrantsViewToWhoeverOwnsTheFolder_SoTheFolderMustBelongToTheSetOwner()
    {
        // Characterization of the rule the admin used to break: a private set placed in someone else's folder becomes visible to that user.
        var theirFolder = await AddFolderAsync(Outsider, "Của người khác");
        var secret = await AddSetAsync(Owner, "Riêng tư", "Private", theirFolder.FolderId);

        Assert.True(await _personal.CanViewSetAsync(secret.SetId, Outsider, isAdmin: false));
    }

    [Fact]
    public async Task S1_CreateSystemSet_RejectsAFolderOfAnotherUser()
    {
        var theirFolder = await AddFolderAsync(Outsider, "Của người khác");
        await AddSystemUserFolderAsync();

        var result = await _service.CreateSystemSetAsync(new AdminSetInput { Name = "Bộ mới", FolderId = theirFolder.FolderId }, Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminVocabularyMessages.FolderNotOwned, result.FieldErrors["FolderId"]);
        Assert.False(await _db.Sets.AnyAsync(s => s.SetName == "Bộ mới"));
    }

    [Fact]
    public async Task S1_UpdateSystemSet_RejectsAFolderOfAnotherUser_AndAMissingFolder()
    {
        var theirFolder = await AddFolderAsync(Outsider, "Của người khác");
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var foreign = await _service.UpdateSetAsync(set.SetId, new AdminSetInput { Name = "Bộ hệ thống", FolderId = theirFolder.FolderId }, Admin);
        var missing = await _service.UpdateSetAsync(set.SetId, new AdminSetInput { Name = "Bộ hệ thống", FolderId = 9999 }, Admin);

        Assert.Equal(AdminResultStatus.Invalid, foreign.Status);
        Assert.Equal(AdminResultStatus.Invalid, missing.Status);
        Assert.Null((await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).FolderId);
    }

    // ------------------------------------------------------------------ S2: create

    [Fact]
    public async Task S2_CreateSystemSet_IsOwnedBySystemUser_AndPublic_AndStamped()
    {
        var folder = await AddSystemUserFolderAsync();

        var result = await _service.CreateSystemSetAsync(
            new AdminSetInput { Name = "  Bộ   từ vựng\tmới ", Description = "  #tag  ", FolderId = folder.FolderId }, Admin);

        Assert.True(result.IsSuccess);
        var set = await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == result.Value);
        Assert.Equal(SystemUser, set.OwnerId);
        Assert.Equal("Public", set.Visibility);
        Assert.Equal("Bộ từ vựng mới", set.SetName);
        Assert.Equal("#tag", set.Description);
        Assert.Equal(folder.FolderId, set.FolderId);
        Assert.NotNull(set.CreatedAt);
        Assert.NotNull(set.UpdatedAt);
        Assert.Equal(0, set.ViewCount);
    }

    [Fact]
    public async Task S2_CreateSystemSet_WithoutAFolder_IsAllowed()
    {
        await AddSystemUserFolderAsync();
        var result = await _service.CreateSystemSetAsync(new AdminSetInput { Name = "Không thư mục" }, Admin);

        Assert.True(result.IsSuccess);
        Assert.Null((await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == result.Value)).FolderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task S2_CreateSystemSet_RejectsABlankName(string? name)
    {
        await AddSystemUserFolderAsync();
        var result = await _service.CreateSystemSetAsync(new AdminSetInput { Name = name }, Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors.ContainsKey("Name"));
    }

    [Fact]
    public async Task S2_CreateSystemSet_RejectsALongNameAndDescription_AtTheColumnLimits()
    {
        await AddSystemUserFolderAsync();

        var longName = await _service.CreateSystemSetAsync(new AdminSetInput { Name = new string('a', 256) }, Admin);
        var okName = await _service.CreateSystemSetAsync(new AdminSetInput { Name = new string('b', 255) }, Admin);
        var longDescription = await _service.CreateSystemSetAsync(new AdminSetInput { Name = "Mô tả dài", Description = new string('c', 1001) }, Admin);

        Assert.True(longName.FieldErrors.ContainsKey("Name"));
        Assert.True(okName.IsSuccess);
        Assert.True(longDescription.FieldErrors.ContainsKey("Description"));
    }

    [Fact]
    public async Task S2_CreateSystemSet_RejectsADuplicateSystemName_IgnoringCase_ButNotALearnerName()
    {
        await AddSystemUserFolderAsync();
        await AddSetAsync(SystemUser, "Từ vựng Oxford");
        await AddSetAsync(Owner, "Tên của học viên");

        var duplicate = await _service.CreateSystemSetAsync(new AdminSetInput { Name = "TỪ VỰNG OXFORD" }, Admin);
        var sameAsLearner = await _service.CreateSystemSetAsync(new AdminSetInput { Name = "Tên của học viên" }, Admin);

        Assert.Equal(AdminResultStatus.Invalid, duplicate.Status);
        Assert.True(duplicate.FieldErrors.ContainsKey("Name"));
        Assert.True(sameAsLearner.IsSuccess);
    }

    [Fact]
    public async Task S2_CreateSystemSet_FailsClearlyWhenThereIsNoSystemUser()
    {
        _db.Users.Remove(await _db.Users.SingleAsync(u => u.UserId == SystemUser));
        await _db.SaveChangesAsync();

        var result = await _service.CreateSystemSetAsync(new AdminSetInput { Name = "Bộ mới" }, Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    // ------------------------------------------------------------------ S3: delete

    [Fact]
    public async Task S3_Delete_GoesThroughThePersonalSetService_RemovingCardsProgressAndSubscriptions()
    {
        var set = await AddSetAsync(Owner, "Bộ của học viên", cards: new[] { NewCard("a"), NewCard("b") });
        _db.SetSubscriptions.Add(new SetSubscription { SetId = set.SetId, UserId = Subscriber, SubscribedAt = DateTime.UtcNow });
        var cardId = set.Cards.First().CardId;
        _db.LearningProgresses.Add(new LearningProgress { UserId = Subscriber, CardId = cardId, Status = "Learning", RepetitionCount = 1 });
        await _db.SaveChangesAsync();

        var result = await _service.DeleteSetAsync(set.SetId, "Bộ của học viên", Admin);

        Assert.True(result.IsSuccess);
        Assert.False(await _db.Sets.AnyAsync(s => s.SetId == set.SetId));
        Assert.False(await _db.Cards.AnyAsync(c => c.SetId == set.SetId));
        Assert.False(await _db.SetSubscriptions.AnyAsync(x => x.SetId == set.SetId));
        Assert.False(await _db.LearningProgresses.AnyAsync(p => p.CardId == cardId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sai tên")]
    [InlineData("bộ của học viên")]
    public async Task S3_Delete_NeedsTheExactName_AndChangesNothingOtherwise(string? typed)
    {
        var set = await AddSetAsync(Owner, "Bộ của học viên", cards: new[] { NewCard("a") });

        var result = await _service.DeleteSetAsync(set.SetId, typed, Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminVocabularyMessages.ConfirmNameMismatch, result.Message);
        Assert.True(await _db.Sets.AnyAsync(s => s.SetId == set.SetId));
        Assert.True(await _db.Cards.AnyAsync(c => c.SetId == set.SetId));
    }

    [Fact]
    public async Task S3_Delete_ReportsAMissingSet()
    {
        var result = await _service.DeleteSetAsync(9999, "x", Admin);
        Assert.Equal(AdminResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task S3_Delete_AlsoWorksForASystemSet()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống", cards: new[] { NewCard("a") });

        var result = await _service.DeleteSetAsync(set.SetId, "Bộ hệ thống", Admin);

        Assert.True(result.IsSuccess);
        Assert.False(await _db.Sets.AnyAsync(s => s.SetId == set.SetId));
    }

    // ------------------------------------------------------------------ S4 / D3: system set name

    [Fact]
    public async Task S4_RenamingASystemSet_IsBlocked_BecauseTheSeederMatchesByName()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.UpdateSetAsync(set.SetId, new AdminSetInput { Name = "Tên khác" }, Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminVocabularyMessages.SystemRenameBlocked, result.FieldErrors["Name"]);
        Assert.Equal("Bộ hệ thống", (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).SetName);
    }

    [Fact]
    public async Task S4_ASystemSet_KeepsItsNameWhileTheDescriptionChanges()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.UpdateSetAsync(set.SetId, new AdminSetInput { Name = "  Bộ hệ thống ", Description = "  mô tả mới " }, Admin);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Warnings);
        var saved = await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId);
        Assert.Equal("Bộ hệ thống", saved.SetName);
        Assert.Equal("mô tả mới", saved.Description);
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal("Public", saved.Visibility);
        Assert.Equal(SystemUser, saved.OwnerId);
    }

    [Fact]
    public async Task S4_MovingASystemSetToAnotherSystemFolder_Works_WithAWarningAboutTheSeederFile()
    {
        var from = await AddFolderAsync(SystemUser, "Thư mục cũ");
        var to = await AddFolderAsync(SystemUser, "Thư mục mới");
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống", folderId: from.FolderId);

        var result = await _service.UpdateSetAsync(set.SetId, new AdminSetInput { Name = "Bộ hệ thống", FolderId = to.FolderId }, Admin);

        Assert.True(result.IsSuccess);
        Assert.Contains(AdminVocabularyMessages.SystemFolderChangeWarning, result.Warnings);
        Assert.Equal(to.FolderId, (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).FolderId);
    }

    [Fact]
    public async Task S4_UpdateSet_ReportsAMissingSet()
    {
        var result = await _service.UpdateSetAsync(9999, new AdminSetInput { Name = "x" }, Admin);
        Assert.Equal(AdminResultStatus.NotFound, result.Status);
    }

    // ------------------------------------------------------------------ D2: learner content

    [Fact]
    public async Task D2_UpdatingALearnerSet_IsForbidden()
    {
        var set = await AddSetAsync(Owner, "Bộ của học viên", "Private");

        var result = await _service.UpdateSetAsync(set.SetId, new AdminSetInput { Name = "Đổi tên" }, Admin);

        Assert.Equal(AdminResultStatus.Forbidden, result.Status);
        Assert.Equal(AdminVocabularyMessages.LearnerContentReadOnly, result.Message);
        Assert.Equal("Bộ của học viên", (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).SetName);
    }

    [Fact]
    public async Task D2_MakingALearnerSetPrivate_TurnsOffTheShareLink()
    {
        var set = await AddSetAsync(Owner, "Bộ công khai", "Public", slug: "abcdefghij");

        var result = await _service.SetVisibilityAsync(set.SetId, "Private", Admin);

        Assert.True(result.IsSuccess);
        var saved = await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId);
        Assert.Equal("Private", saved.Visibility);
        Assert.Null(saved.ShareSlug);
        Assert.NotNull(saved.UpdatedAt);
        // The outsider can no longer reach it by the old link or by browsing.
        Assert.False(await _personal.CanViewSetAsync(set.SetId, Outsider, isAdmin: false));
        var subscribe = await _personal.SubscribeAsync(null, "abcdefghij", Outsider, false);
        Assert.False(subscribe.IsSuccess);
    }

    [Fact]
    public async Task D2_ALearnerSet_CannotBeMadePublicByAnAdmin()
    {
        var set = await AddSetAsync(Owner, "Bộ riêng", "Private");

        var result = await _service.SetVisibilityAsync(set.SetId, "Public", Admin);

        Assert.Equal(AdminResultStatus.Forbidden, result.Status);
        Assert.Equal("Private", (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).Visibility);
    }

    [Fact]
    public async Task D2_ASystemSet_StaysPublic_WhateverIsAsked()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.SetVisibilityAsync(set.SetId, "Private", Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminVocabularyMessages.SystemAlwaysPublic, result.Message);
        Assert.Equal("Public", (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).Visibility);
    }

    [Theory]
    [InlineData("Hidden")]
    [InlineData("")]
    [InlineData(null)]
    public async Task D2_SetVisibility_RejectsUnknownValues(string? value)
    {
        var set = await AddSetAsync(Owner, "Bộ riêng", "Public");
        var result = await _service.SetVisibilityAsync(set.SetId, value!, Admin);
        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal("Public", (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == set.SetId)).Visibility);
    }

    [Fact]
    public async Task D2_LearnerCards_AreReadOnly()
    {
        var set = await AddSetAsync(Owner, "Bộ của học viên", cards: new[] { NewCard("a") });
        var cardId = set.Cards.Single().CardId;

        var add = await _service.AddCardAsync(set.SetId, ValidCard(), Admin);
        var update = await _service.UpdateCardAsync(cardId, ValidCard("b"), Admin);
        var remove = await _service.DeleteCardAsync(cardId, Admin);

        Assert.Equal(AdminResultStatus.Forbidden, add.Status);
        Assert.Equal(AdminResultStatus.Forbidden, update.Status);
        Assert.Equal(AdminResultStatus.Forbidden, remove.Status);
        Assert.Equal(1, await _db.Cards.CountAsync(c => c.SetId == set.SetId));
        Assert.Equal("a", (await _db.Cards.AsNoTracking().SingleAsync(c => c.CardId == cardId)).Term);
    }

    // ------------------------------------------------------------------ S5: list and paging

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task S5_ListSets_ClampsAPageBelowOne(int page)
    {
        await AddSetAsync(SystemUser, "Một");

        var result = await _service.ListSetsAsync(new AdminSetQuery(Page: page));

        Assert.Equal(1, result.Page);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task S5_ListSets_ShowsTheLastPage_WhenThePageIsBeyondTheEnd()
    {
        for (var i = 0; i < 45; i++)
        {
            await AddSetAsync(Owner, $"Bộ {i:00}", created: new DateTime(2026, 1, 1).AddMinutes(i));
        }

        var result = await _service.ListSetsAsync(new AdminSetQuery(Page: 99));

        Assert.Equal(45, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.Rows.Count);
    }

    [Fact]
    public async Task S5_ListSets_OnAnEmptyDatabase_ReturnsOnePageWithNoRows()
    {
        var result = await _service.ListSetsAsync(new AdminSetQuery(Page: 7));

        Assert.Equal(1, result.Page);
        Assert.Equal(0, result.TotalItems);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task ListSets_FiltersBySourceFolderVisibilityAndText()
    {
        var sysFolder = await AddFolderAsync(SystemUser, "Hệ thống A");
        var userFolder = await AddFolderAsync(Owner, "Của tôi");
        await AddSetAsync(SystemUser, "Oxford A1", folderId: sysFolder.FolderId);
        await AddSetAsync(SystemUser, "Giao tiếp", folderId: sysFolder.FolderId);
        await AddSetAsync(Owner, "Du lịch", "Private", userFolder.FolderId);
        await AddSetAsync(Owner, "Công nghệ", "Public", slug: "abcdefghij");
        await AddSetAsync(Outsider, "Riêng tư hẳn", "Private");

        var system = await _service.ListSetsAsync(new AdminSetQuery(Source: "system"));
        var user = await _service.ListSetsAsync(new AdminSetQuery(Source: "user"));
        var all = await _service.ListSetsAsync(new AdminSetQuery());
        var inFolder = await _service.ListSetsAsync(new AdminSetQuery(Source: "system", FolderId: sysFolder.FolderId));
        var searched = await _service.ListSetsAsync(new AdminSetQuery(Q: "Oxford"));
        var publicOnly = await _service.ListSetsAsync(new AdminSetQuery(Source: "user", Visibility: "Public"));
        var linkOnly = await _service.ListSetsAsync(new AdminSetQuery(Visibility: "Link"));
        var privateOnly = await _service.ListSetsAsync(new AdminSetQuery(Source: "user", Visibility: "Private"));

        Assert.Equal(2, system.TotalItems);
        Assert.All(system.Rows, r => Assert.True(r.IsSystem));
        Assert.Equal(3, user.TotalItems);
        Assert.Equal(5, all.TotalItems);
        Assert.Equal(2, inFolder.TotalItems);
        Assert.Equal("Oxford A1", Assert.Single(searched.Rows).Name);
        Assert.Equal("Công nghệ", Assert.Single(publicOnly.Rows).Name);
        Assert.Equal(0, linkOnly.TotalItems);
        Assert.Equal(2, privateOnly.TotalItems);
        Assert.Equal(new[] { "Hệ thống A" }, all.SystemFolders.Select(f => f.Name));
    }

    [Fact]
    public async Task ListSets_ComputesTheExposureOfEveryRow()
    {
        await AddSetAsync(Owner, "Công khai", "Public");
        await AddSetAsync(Owner, "Có liên kết", "Private", slug: "abcdefghij");
        await AddSetAsync(Owner, "Riêng tư", "Private");

        var rows = (await _service.ListSetsAsync(new AdminSetQuery())).Rows.ToDictionary(r => r.Name, r => r.Exposure);
        var link = await _service.ListSetsAsync(new AdminSetQuery(Visibility: "Link"));

        Assert.Equal("Public", rows["Công khai"]);
        Assert.Equal("Link", rows["Có liên kết"]);
        Assert.Equal("Private", rows["Riêng tư"]);
        Assert.Equal("Có liên kết", Assert.Single(link.Rows).Name);
    }

    [Fact]
    public async Task ListSets_SortsAndCountsCardsAndTopics()
    {
        await AddSetAsync(SystemUser, "Beta", created: new DateTime(2026, 1, 1), views: 5, cards: new[] { NewCard("a", topic: "T1"), NewCard("b", topic: "T1"), NewCard("c", topic: "T2") });
        await AddSetAsync(SystemUser, "Alpha", created: new DateTime(2026, 2, 1), views: 50, cards: new[] { NewCard("d", topic: "T1") });

        var newest = await _service.ListSetsAsync(new AdminSetQuery(Sort: "newest"));
        var byName = await _service.ListSetsAsync(new AdminSetQuery(Sort: "name"));
        var byCards = await _service.ListSetsAsync(new AdminSetQuery(Sort: "cards"));
        var byViews = await _service.ListSetsAsync(new AdminSetQuery(Sort: "views"));

        Assert.Equal(new[] { "Alpha", "Beta" }, newest.Rows.Select(r => r.Name));
        Assert.Equal(new[] { "Alpha", "Beta" }, byName.Rows.Select(r => r.Name));
        Assert.Equal(new[] { "Beta", "Alpha" }, byCards.Rows.Select(r => r.Name));
        Assert.Equal(new[] { "Alpha", "Beta" }, byViews.Rows.Select(r => r.Name));
        var beta = byCards.Rows.First();
        Assert.Equal(3, beta.CardCount);
        Assert.Equal(2, beta.TopicCount);
        Assert.Equal("TCT System", beta.OwnerName);
    }

    [Fact]
    public async Task GetFigures_CountsSystemAndUserSets_AndIncompleteSystemCards()
    {
        await AddSetAsync(SystemUser, "S1", cards: new[] { NewCard("a"), NewCard("b", example: null), NewCard("c", phonetic: " "), NewCard("d", example: "", phonetic: null) });
        await AddSetAsync(SystemUser, "S2", cards: new[] { NewCard("e") });
        await AddSetAsync(Owner, "U1", cards: new[] { NewCard("f", example: null) });

        var figures = await _service.GetFiguresAsync();

        Assert.Equal(2, figures.SystemSets);
        Assert.Equal(5, figures.SystemCards);
        Assert.Equal(1, figures.UserSets);
        Assert.Equal(3, figures.SystemCardsMissingExampleOrPhonetic);
    }

    // ------------------------------------------------------------------ forms

    [Fact]
    public async Task GetSetForm_ForANewSet_OffersSystemFoldersOnly()
    {
        await AddFolderAsync(SystemUser, "Hệ thống");
        await AddFolderAsync(Owner, "Của học viên");

        var form = await _service.GetSetFormAsync(null);

        Assert.NotNull(form);
        Assert.True(form!.IsSystem);
        Assert.Null(form.SetId);
        Assert.Equal(new[] { "Hệ thống" }, form.Folders.Select(f => f.Name));
    }

    [Fact]
    public async Task GetSetForm_ForAnExistingSet_ReportsItsOwnerKind()
    {
        var folder = await AddFolderAsync(SystemUser, "Hệ thống");
        var system = await AddSetAsync(SystemUser, "Bộ hệ thống", folderId: folder.FolderId);
        var learner = await AddSetAsync(Owner, "Bộ học viên", "Private");

        var systemForm = await _service.GetSetFormAsync(system.SetId);
        var learnerForm = await _service.GetSetFormAsync(learner.SetId);

        Assert.True(systemForm!.IsSystem);
        Assert.Equal("Bộ hệ thống", systemForm.Name);
        Assert.Equal(folder.FolderId, systemForm.FolderId);
        Assert.False(learnerForm!.IsSystem);
        Assert.Equal("User 1", learnerForm.OwnerName);
        Assert.Null(await _service.GetSetFormAsync(9999));
    }

    // ------------------------------------------------------------------ S6 / S7: cards

    [Fact]
    public async Task S6_AddCard_TrimsCollapsesAndSavesEveryField()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.AddCardAsync(set.SetId, new AdminCardInput
        {
            Term = "  ice   cream ", Definition = " kem ", Phonetic = " /aɪs/ ", PhoneticUk = " /aɪs/ ", PartsOfSpeech = "noun, adj",
            Cefr = "B1", Emoji = "🍦", ImageUrl = " https://cdn.example.com/a.png ", Topic = " Food ", Example = "I love ice cream.",
            ExampleTranslation = "Tôi thích kem.", MeaningEn = "a frozen dessert",
        }, Admin);

        Assert.True(result.IsSuccess);
        var card = await _db.Cards.AsNoTracking().SingleAsync(c => c.CardId == result.Value);
        Assert.Equal(set.SetId, card.SetId);
        Assert.Equal("ice cream", card.Term);
        Assert.Equal("kem", card.Definition);
        Assert.Equal("/aɪs/", card.Phonetic);
        Assert.Equal("/aɪs/", card.PhoneticUk);
        Assert.Equal("noun,adj", card.PartsOfSpeech);
        Assert.Equal("B1", card.Cefr);
        Assert.Equal("🍦", card.Emoji);
        Assert.Equal("https://cdn.example.com/a.png", card.ImageUrl);
        Assert.Equal("Food", card.Topic);
        Assert.Equal("I love ice cream.", card.Example);
        Assert.Equal("Tôi thích kem.", card.ExampleTranslation);
        Assert.Equal("a frozen dessert", card.MeaningEn);
        Assert.NotNull(card.CreatedAt);
    }

    [Fact]
    public async Task S6_AddCard_StoresBlankOptionalFieldsAsNull()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.AddCardAsync(set.SetId, new AdminCardInput { Term = "Cat", Definition = "Con mèo", Example = "  ", ImageUrl = "", Cefr = "" }, Admin);

        Assert.True(result.IsSuccess);
        var card = await _db.Cards.AsNoTracking().SingleAsync(c => c.CardId == result.Value);
        Assert.Null(card.Example);
        Assert.Null(card.ImageUrl);
        Assert.Null(card.Cefr);
        Assert.Null(card.Phonetic);
    }

    public static IEnumerable<object[]> InvalidCards() => new[]
    {
        new object[] { "Term", new AdminCardInput { Term = "", Definition = "nghĩa" } },
        new object[] { "Term", new AdminCardInput { Term = null, Definition = "nghĩa" } },
        new object[] { "Term", new AdminCardInput { Term = new string('a', 256), Definition = "nghĩa" } },
        new object[] { "Definition", new AdminCardInput { Term = "a", Definition = "  " } },
        new object[] { "Definition", new AdminCardInput { Term = "a", Definition = new string('d', 1001) } },
        new object[] { "Phonetic", new AdminCardInput { Term = "a", Definition = "b", Phonetic = new string('p', 101) } },
        new object[] { "PhoneticUk", new AdminCardInput { Term = "a", Definition = "b", PhoneticUk = new string('p', 101) } },
        new object[] { "Emoji", new AdminCardInput { Term = "a", Definition = "b", Emoji = new string('x', 17) } },
        new object[] { "Topic", new AdminCardInput { Term = "a", Definition = "b", Topic = new string('t', 101) } },
        new object[] { "Example", new AdminCardInput { Term = "a", Definition = "b", Example = new string('e', 1001) } },
        new object[] { "ExampleTranslation", new AdminCardInput { Term = "a", Definition = "b", ExampleTranslation = new string('e', 1001) } },
        new object[] { "MeaningEn", new AdminCardInput { Term = "a", Definition = "b", MeaningEn = new string('m', 401) } },
        new object[] { "Cefr", new AdminCardInput { Term = "a", Definition = "b", Cefr = "D1" } },
        new object[] { "PartsOfSpeech", new AdminCardInput { Term = "a", Definition = "b", PartsOfSpeech = "noun, flying" } },
        new object[] { "ImageUrl", new AdminCardInput { Term = "a", Definition = "b", ImageUrl = "http://cdn.example.com/a.png" } },
        new object[] { "ImageUrl", new AdminCardInput { Term = "a", Definition = "b", ImageUrl = "javascript:alert(1)" } },
        new object[] { "ImageUrl", new AdminCardInput { Term = "a", Definition = "b", ImageUrl = new string('u', 501) } },
    };

    [Theory]
    [MemberData(nameof(InvalidCards))]
    public async Task S6_AddCard_RejectsBadInput_WithAFieldMessage_AndSavesNothing(string field, AdminCardInput input)
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.AddCardAsync(set.SetId, input, Admin);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors.ContainsKey(field), $"expected an error on {field}, got {string.Join(",", result.FieldErrors.Keys)}");
        Assert.Equal(0, await _db.Cards.CountAsync(c => c.SetId == set.SetId));
    }

    [Fact]
    public async Task S6_AddCard_AcceptsTheValuesAtTheLimits()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống");

        var result = await _service.AddCardAsync(set.SetId, new AdminCardInput
        {
            Term = new string('a', 255), Definition = new string('d', 1000), Phonetic = new string('p', 100), PhoneticUk = new string('p', 100),
            Emoji = new string('x', 16), Topic = new string('t', 100), Example = new string('e', 1000), ExampleTranslation = new string('e', 1000),
            MeaningEn = new string('m', 400), ImageUrl = "/" + new string('u', 499),
        }, Admin);

        Assert.True(result.IsSuccess, string.Join(";", result.FieldErrors.Select(kv => kv.Key + "=" + kv.Value)));
    }

    [Fact]
    public async Task S6_AddCard_ReportsAMissingSet()
    {
        var result = await _service.AddCardAsync(9999, ValidCard(), Admin);
        Assert.Equal(AdminResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task S7_UpdateCard_ReturnsTheCardsRealSet_NotAPostedOne_AndSavesTheExtraFields()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống", cards: new[] { NewCard("Dog") });
        var other = await AddSetAsync(SystemUser, "Bộ khác");
        var cardId = set.Cards.Single().CardId;

        var result = await _service.UpdateCardAsync(cardId, new AdminCardInput
        {
            Term = "Dog", Definition = "Con chó", Emoji = "🐶", PhoneticUk = "/dɒɡ/", Cefr = "A1", PartsOfSpeech = "noun", MeaningEn = "a pet",
        }, Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(set.SetId, result.Value);
        Assert.NotEqual(other.SetId, result.Value);
        var card = await _db.Cards.AsNoTracking().SingleAsync(c => c.CardId == cardId);
        Assert.Equal("🐶", card.Emoji);
        Assert.Equal("/dɒɡ/", card.PhoneticUk);
        Assert.Equal("A1", card.Cefr);
        Assert.Equal("noun", card.PartsOfSpeech);
        Assert.Equal("a pet", card.MeaningEn);
        Assert.NotNull(card.UpdatedAt);
        Assert.Equal(set.SetId, card.SetId);
    }

    [Fact]
    public async Task S7_UpdateCard_RejectsBadInput_AndReportsAMissingCard()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống", cards: new[] { NewCard("Dog") });
        var cardId = set.Cards.Single().CardId;

        var bad = await _service.UpdateCardAsync(cardId, new AdminCardInput { Term = "", Definition = "x" }, Admin);
        var missing = await _service.UpdateCardAsync(9999, ValidCard(), Admin);

        Assert.Equal(AdminResultStatus.Invalid, bad.Status);
        Assert.Equal("Dog", (await _db.Cards.AsNoTracking().SingleAsync(c => c.CardId == cardId)).Term);
        Assert.Equal(AdminResultStatus.NotFound, missing.Status);
    }

    [Fact]
    public async Task DeleteCard_RemovesItsProgress_AndReturnsTheSetId()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống", cards: new[] { NewCard("a"), NewCard("b") });
        var cardId = set.Cards.First().CardId;
        _db.LearningProgresses.Add(new LearningProgress { UserId = Subscriber, CardId = cardId, Status = "Learning", RepetitionCount = 1 });
        await _db.SaveChangesAsync();

        var result = await _service.DeleteCardAsync(cardId, Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(set.SetId, result.Value);
        Assert.False(await _db.Cards.AnyAsync(c => c.CardId == cardId));
        Assert.False(await _db.LearningProgresses.AnyAsync(p => p.CardId == cardId));
        Assert.Equal(1, await _db.Cards.CountAsync(c => c.SetId == set.SetId));
        Assert.Equal(AdminResultStatus.NotFound, (await _service.DeleteCardAsync(cardId, Admin)).Status);
    }

    [Fact]
    public async Task GetCardForm_LoadsACardOrAnEmptyFormForASet()
    {
        var set = await AddSetAsync(SystemUser, "Bộ hệ thống", cards: new[] { NewCard("Dog", "Con chó", "Animals") });
        var cardId = set.Cards.Single().CardId;

        var edit = await _service.GetCardFormAsync(cardId, null);
        var add = await _service.GetCardFormAsync(null, set.SetId);

        Assert.Equal("Dog", edit!.Input.Term);
        Assert.Equal(set.SetId, edit.SetId);
        Assert.True(edit.IsSystem);
        Assert.Equal("Bộ hệ thống", edit.SetName);
        Assert.Null(add!.CardId);
        Assert.Equal(set.SetId, add.SetId);
        Assert.Equal("", add.Input.Term ?? "");
        Assert.Null(await _service.GetCardFormAsync(9999, null));
        Assert.Null(await _service.GetCardFormAsync(null, 9999));
        Assert.Null(await _service.GetCardFormAsync(null, null));
    }

    // ------------------------------------------------------------------ S8: cards page

    private async Task<Set> AddBigSystemSetAsync()
    {
        var cards = new List<Card>();
        for (var i = 1; i <= 60; i++)
        {
            cards.Add(NewCard($"w{i:00}", $"nghĩa {i}", i <= 30 ? "Animals" : "Food"));
        }

        cards[0].Example = null;                       // missing example
        cards[1].Phonetic = null;                      // missing phonetic
        cards[2].Emoji = null;                         // missing picture (no image either)
        cards[3].Emoji = null; cards[3].ImageUrl = "/images/x.png"; // has an image: picture present
        return await AddSetAsync(SystemUser, "Bộ lớn", cards: cards.ToArray());
    }

    [Fact]
    public async Task S8_ListCards_PagesAtFifty_InOrder()
    {
        var set = await AddBigSystemSetAsync();

        var first = await _service.ListCardsAsync(set.SetId, new AdminCardQuery());
        var second = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Page: 2));
        var beyond = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Page: 99));
        var below = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Page: 0));

        Assert.Equal(60, first!.TotalItems);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(50, first.Rows.Count);
        Assert.Equal("w01", first.Rows[0].Term);
        Assert.Equal(10, second!.Rows.Count);
        Assert.Equal("w60", second.Rows[^1].Term);
        Assert.Equal(2, beyond!.Page);
        Assert.Equal(1, below!.Page);
    }

    [Fact]
    public async Task S8_ListCards_FlagsAndFiltersTheQualityOfEachCard()
    {
        var set = await AddBigSystemSetAsync();

        var all = await _service.ListCardsAsync(set.SetId, new AdminCardQuery());
        var noExample = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Quality: "missing-example"));
        var noPhonetic = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Quality: "missing-phonetic"));
        var noPicture = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Quality: "missing-picture"));
        var complete = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Quality: "complete"));

        Assert.True(all!.Rows[0].MissingExample);
        Assert.True(all.Rows[1].MissingPhonetic);
        Assert.True(all.Rows[2].MissingPicture);
        Assert.False(all.Rows[3].MissingPicture);
        Assert.Equal("w01", Assert.Single(noExample!.Rows).Term);
        Assert.Equal("w02", Assert.Single(noPhonetic!.Rows).Term);
        Assert.Equal("w03", Assert.Single(noPicture!.Rows).Term);
        Assert.Equal(57, complete!.TotalItems);
        Assert.All(complete.Rows, r => Assert.True(r.IsComplete));
    }

    [Fact]
    public async Task S8_ListCards_SearchesAndFiltersByTopic_AndReportsTheSetSummary()
    {
        var set = await AddBigSystemSetAsync();
        var learner = await AddSetAsync(Owner, "Bộ học viên", "Private");
        _db.LearningProgresses.Add(new LearningProgress { UserId = Subscriber, CardId = set.Cards.First().CardId, Status = "Learning", RepetitionCount = 1 });
        _db.LearningProgresses.Add(new LearningProgress { UserId = Subscriber, CardId = set.Cards.Last().CardId, Status = "Learning", RepetitionCount = 1 });
        _db.LearningProgresses.Add(new LearningProgress { UserId = Outsider, CardId = set.Cards.Last().CardId, Status = "Learning", RepetitionCount = 1 });
        await _db.SaveChangesAsync();

        var food = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Topic: "Food"));
        var search = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Q: "w17"));
        var byMeaning = await _service.ListCardsAsync(set.SetId, new AdminCardQuery(Q: "nghĩa 42"));
        var learnerPage = await _service.ListCardsAsync(learner.SetId, new AdminCardQuery());

        Assert.Equal(30, food!.TotalItems);
        Assert.Equal("w17", Assert.Single(search!.Rows).Term);
        Assert.Equal("w42", Assert.Single(byMeaning!.Rows).Term);
        Assert.Equal(60, food.TotalCards);
        Assert.Equal(new[] { "Animals", "Food" }, food.Topics);
        Assert.Equal(6, food.LessonCount);
        Assert.Equal(2, food.MissingExampleOrPhonetic);
        Assert.Equal(2, food.LearnerCount);
        Assert.True(food.IsSystem);
        Assert.Equal("Bộ lớn", food.SetName);
        Assert.Equal("Public", food.Exposure);
        Assert.False(learnerPage!.IsSystem);
        Assert.Equal("User 1", learnerPage.OwnerName);
        Assert.Empty(learnerPage.Rows);
        Assert.Null(await _service.ListCardsAsync(9999, new AdminCardQuery()));
    }

    // ------------------------------------------------------------------ export

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    private static List<string> Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToList();

    [Fact]
    public async Task Export_HasTheExactShapeOfTheSystemVocabularyFile()
    {
        var folder = await AddFolderAsync(SystemUser, "Từ Vựng Theo Chủ Đề");
        var set = await AddSetAsync(SystemUser, "Màu sắc & Hình dạng", folderId: folder.FolderId, cards: new[]
        {
            new Card { Term = "Red", Definition = "Màu đỏ", Topic = "Colors", Phonetic = "/red/", Example = "A red car.", ExampleTranslation = "Một chiếc xe đỏ.", ImageUrl = "/images/red.png" },
            new Card { Term = "Blue", Definition = "Màu xanh", Topic = "Colors" },
        });
        set.Description = "#colors #a1";
        await _db.SaveChangesAsync();

        var export = await _service.ExportSystemSetAsync(set.SetId);

        Assert.NotNull(export);
        Assert.Equal("system-vocabulary-mau-sac-hinh-dang.json", export!.FileName);
        using var exported = JsonDocument.Parse(export.Json);
        using var real = JsonDocument.Parse(File.ReadAllText(RepoFile("TCTEnglish", "wwwroot", "data", "system-vocabulary.json")));

        var realFolder = real.RootElement[0];
        var realSet = realFolder.GetProperty("Sets")[0];
        var realCard = realSet.GetProperty("Cards")[0];
        var folderOut = exported.RootElement[0];
        var setOut = folderOut.GetProperty("Sets")[0];
        var cardOut = setOut.GetProperty("Cards")[0];

        Assert.Equal(JsonValueKind.Array, exported.RootElement.ValueKind);
        Assert.Equal(1, exported.RootElement.GetArrayLength());
        Assert.Equal(1, folderOut.GetProperty("Sets").GetArrayLength());
        Assert.Equal(Keys(realFolder), Keys(folderOut));
        Assert.Equal(Keys(realSet), Keys(setOut));
        Assert.Equal(Keys(realCard), Keys(cardOut));
        Assert.Equal("Từ Vựng Theo Chủ Đề", folderOut.GetProperty("FolderName").GetString());
        Assert.Equal("Màu sắc & Hình dạng", setOut.GetProperty("SetName").GetString());
        Assert.Equal("#colors #a1", setOut.GetProperty("Description").GetString());
        Assert.Equal("Red", cardOut.GetProperty("Term").GetString());
        Assert.Equal("Màu đỏ", cardOut.GetProperty("Definition").GetString());
        Assert.Equal("/images/red.png", cardOut.GetProperty("ImageUrl").GetString());
        Assert.Equal(2, setOut.GetProperty("Cards").GetArrayLength());
        // Vietnamese stays readable in the file (no \u escapes).
        Assert.Contains("Màu đỏ", export.Json);
        Assert.DoesNotContain("\\u", export.Json);
    }

    [Fact]
    public async Task Export_IsOnlyForSystemSets()
    {
        var learner = await AddSetAsync(Owner, "Bộ học viên", "Private", cards: new[] { NewCard("a") });

        Assert.Null(await _service.ExportSystemSetAsync(learner.SetId));
        Assert.Null(await _service.ExportSystemSetAsync(9999));
    }

    [Fact]
    public async Task Export_OfASetWithoutAFolder_StillNamesAFolderForTheSeeder()
    {
        var set = await AddSetAsync(SystemUser, "Bộ lẻ", cards: new[] { NewCard("a") });

        var export = await _service.ExportSystemSetAsync(set.SetId);

        using var doc = JsonDocument.Parse(export!.Json);
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement[0].GetProperty("FolderName").GetString()));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Folder> AddSystemUserFolderAsync() => await AddFolderAsync(SystemUser, "Thư mục hệ thống");

    private sealed class StubFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new NotFoundHandler(), disposeHandler: true);

        private sealed class NotFoundHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class StubEnvironment(string webRoot) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Development";
    }
}
