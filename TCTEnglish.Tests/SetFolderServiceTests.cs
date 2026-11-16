using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

/// <summary>Folder rules of the personal-set workspace: names, access, delete, move and the list read models.</summary>
public sealed class SetFolderServiceTests : IAsyncLifetime
{
    private const int Owner = 1, Outsider = 2, Saver = 3, ClassOwner = 4, ClassMember = 5, Admin = 6;

    private SqliteConnection _connection = null!;
    private DbflashcardContext _db = null!;
    private PersonalSetService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        foreach (var (id, role) in new[] { (Owner, "Standard"), (Outsider, "Standard"), (Saver, "Standard"), (ClassOwner, "Standard"), (ClassMember, "Standard"), (Admin, "Admin") })
        {
            _db.Users.Add(new User { UserId = id, Email = $"u{id}@test.local", PasswordHash = "x", FullName = $"User {id}", Role = role, CreatedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync();
        _service = new PersonalSetService(_db, TimeProvider.System, new StubFactory(), new StubEnvironment(), NullLogger<PersonalSetService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ------------------------------------------------------------------ seed helpers

    private async Task<Folder> AddFolderAsync(int owner, string name, int? parent = null)
    {
        var folder = new Folder { FolderName = name, UserId = owner, ParentFolderId = parent };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    private async Task<Set> AddSetAsync(int owner, string name, int? folderId = null, params string[] terms)
    {
        var set = new Set { SetName = name, OwnerId = owner, Visibility = "Private", FolderId = folderId, CreatedAt = DateTime.UtcNow };
        foreach (var term in terms)
        {
            set.Cards.Add(new Card { Term = term, Definition = "d" });
        }

        _db.Sets.Add(set);
        await _db.SaveChangesAsync();
        return set;
    }

    private async Task AddProgressAsync(int userId, int cardId, DateTime? next, bool suspended = false)
    {
        _db.LearningProgresses.Add(new LearningProgress { UserId = userId, CardId = cardId, Status = "Learning", IsSuspended = suspended, LastReviewedDate = DateTime.UtcNow, NextReviewDate = next });
        await _db.SaveChangesAsync();
    }

    private Task<int> FolderIdOf(Set set) => _db.Sets.AsNoTracking().Where(s => s.SetId == set.SetId).Select(s => s.FolderId ?? 0).SingleAsync();

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_TrimsTheNameAndStoresTheFolderForTheOwner()
    {
        var result = await _service.CreateFolderAsync(Owner, "  Công việc  ");

        Assert.Equal(OperationStatus.Success, result.Status);
        var folder = await _db.Folders.AsNoTracking().SingleAsync(f => f.FolderId == result.Value);
        Assert.Equal("Công việc", folder.FolderName);
        Assert.Equal(Owner, folder.UserId);
        Assert.Null(folder.ParentFolderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsAnEmptyName(string? name)
    {
        var result = await _service.CreateFolderAsync(Owner, name);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Equal("Hãy nhập tên thư mục.", result.ErrorMessage);
        Assert.Equal("Hãy nhập tên thư mục.", result.FieldErrors!["Name"]);
        Assert.Empty(_db.Folders);
    }

    [Fact]
    public async Task Create_AcceptsExactlyHundredCharactersAndRejectsMore()
    {
        var ok = await _service.CreateFolderAsync(Owner, new string('a', 100));
        var tooLong = await _service.CreateFolderAsync(Owner, new string('b', 101));
        var padded = await _service.CreateFolderAsync(Owner, "  " + new string('c', 100) + "  ");

        Assert.Equal(OperationStatus.Success, ok.Status);
        Assert.Equal(OperationStatus.Invalid, tooLong.Status);
        Assert.Equal("Tên thư mục tối đa 100 ký tự.", tooLong.ErrorMessage);
        Assert.Equal(OperationStatus.Success, padded.Status); // the limit counts the trimmed name
        Assert.Equal(2, await _db.Folders.CountAsync());
    }

    [Fact]
    public async Task Create_RejectsADuplicateOfTheSameOwnerIgnoringCaseAndSpacesButNotOfAnotherOwner()
    {
        await _service.CreateFolderAsync(Owner, "Công việc");

        var duplicate = await _service.CreateFolderAsync(Owner, "  CÔNG VIỆC ");
        var otherOwner = await _service.CreateFolderAsync(Outsider, "Công việc");

        Assert.Equal(OperationStatus.Invalid, duplicate.Status);
        Assert.Equal("Bạn đã có thư mục tên này.", duplicate.ErrorMessage);
        Assert.Equal(OperationStatus.Success, otherOwner.Status);
    }

    [Fact]
    public async Task Create_ForAnUnknownUserIsNotFound()
    {
        var result = await _service.CreateFolderAsync(999, "Công việc");

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.Empty(_db.Folders);
    }

    // ------------------------------------------------------------------ rename

    [Fact]
    public async Task Rename_ChangesTheNameAndAllowsACaseOnlyChange()
    {
        var folder = await AddFolderAsync(Owner, "công việc");

        var result = await _service.RenameFolderAsync(folder.FolderId, Owner, false, "  Công việc ");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Công việc", (await _db.Folders.AsNoTracking().SingleAsync()).FolderName);
    }

    [Fact]
    public async Task Rename_ToTheSameNameIsASuccessWithoutChange()
    {
        var folder = await AddFolderAsync(Owner, "Du lịch");

        var result = await _service.RenameFolderAsync(folder.FolderId, Owner, false, "Du lịch");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Du lịch", (await _db.Folders.AsNoTracking().SingleAsync()).FolderName);
    }

    [Fact]
    public async Task Rename_RejectsEmptyTooLongAndDuplicateNames()
    {
        var a = await AddFolderAsync(Owner, "A");
        await AddFolderAsync(Owner, "B");

        var empty = await _service.RenameFolderAsync(a.FolderId, Owner, false, " ");
        var tooLong = await _service.RenameFolderAsync(a.FolderId, Owner, false, new string('x', 101));
        var duplicate = await _service.RenameFolderAsync(a.FolderId, Owner, false, " b ");

        Assert.Equal("Hãy nhập tên thư mục.", empty.ErrorMessage);
        Assert.Equal("Tên thư mục tối đa 100 ký tự.", tooLong.ErrorMessage);
        Assert.Equal("Bạn đã có thư mục tên này.", duplicate.ErrorMessage);
        Assert.All(new[] { empty, tooLong, duplicate }, r => Assert.Equal(OperationStatus.Invalid, r.Status));
        Assert.Equal("A", (await _db.Folders.AsNoTracking().SingleAsync(f => f.FolderId == a.FolderId)).FolderName);
    }

    [Fact]
    public async Task Rename_OfAForeignFolderIsNotFoundButAnAdminMay()
    {
        var folder = await AddFolderAsync(Owner, "Riêng");

        var stranger = await _service.RenameFolderAsync(folder.FolderId, Outsider, false, "Hack");
        var saverResult = await _service.RenameFolderAsync(folder.FolderId, Saver, false, "Hack");
        var missing = await _service.RenameFolderAsync(12345, Owner, false, "Hack");
        var admin = await _service.RenameFolderAsync(folder.FolderId, Admin, true, "Admin sửa");

        Assert.Equal(OperationStatus.NotFound, stranger.Status);
        Assert.Equal(OperationStatus.NotFound, saverResult.Status);
        Assert.Equal(OperationStatus.NotFound, missing.Status);
        Assert.Equal(OperationStatus.Success, admin.Status);
        Assert.Equal("Admin sửa", (await _db.Folders.AsNoTracking().SingleAsync()).FolderName);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_KeepsTheSetsAndRemovesTheFolderItsSavesAndItsClassLinks()
    {
        var folder = await AddFolderAsync(Owner, "Xóa tôi");
        var other = await AddFolderAsync(Owner, "Giữ lại");
        var inside = await AddSetAsync(Owner, "Trong thư mục", folder.FolderId, "a", "b");
        var elsewhere = await AddSetAsync(Owner, "Thư mục khác", other.FolderId, "c");
        _db.SavedFolders.Add(new SavedFolder { UserId = Saver, FolderId = folder.FolderId });
        var cls = new Class { ClassName = "C", OwnerId = ClassOwner, CreatedAt = DateTime.UtcNow };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync();
        _db.ClassFolders.Add(new ClassFolder { ClassId = cls.ClassId, FolderId = folder.FolderId, AddedByUserId = Owner });
        await _db.SaveChangesAsync();

        var result = await _service.DeleteFolderAsync(folder.FolderId, Owner, false);

        Assert.Equal(OperationStatus.Success, result.Status);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.Folders.AnyAsync(f => f.FolderId == folder.FolderId));
        Assert.True(await _db.Folders.AnyAsync(f => f.FolderId == other.FolderId));
        Assert.Null((await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == inside.SetId)).FolderId);
        Assert.Equal(2, await _db.Cards.CountAsync(c => c.SetId == inside.SetId));
        Assert.Equal(other.FolderId, await FolderIdOf(elsewhere));
        Assert.Empty(_db.SavedFolders);
        Assert.Empty(_db.ClassFolders);
    }

    [Fact]
    public async Task Delete_WithChildFoldersIsRefusedAndChangesNothing()
    {
        var parent = await AddFolderAsync(Owner, "Cha");
        await AddFolderAsync(Owner, "Con", parent.FolderId);
        var set = await AddSetAsync(Owner, "Trong cha", parent.FolderId, "a");

        var result = await _service.DeleteFolderAsync(parent.FolderId, Owner, false);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors!.ContainsKey("ChildFolders"));
        Assert.Contains("thư mục con", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(2, await _db.Folders.CountAsync());
        Assert.Equal(parent.FolderId, await FolderIdOf(set));
    }

    [Fact]
    public async Task Delete_OfAForeignFolderIsNotFoundButAnAdminMay()
    {
        var folder = await AddFolderAsync(Owner, "Riêng");

        var stranger = await _service.DeleteFolderAsync(folder.FolderId, Outsider, false);
        var missing = await _service.DeleteFolderAsync(777, Owner, false);
        Assert.Equal(OperationStatus.NotFound, stranger.Status);
        Assert.Equal(OperationStatus.NotFound, missing.Status);
        Assert.True(await _db.Folders.AnyAsync());

        var admin = await _service.DeleteFolderAsync(folder.FolderId, Admin, true);
        Assert.Equal(OperationStatus.Success, admin.Status);
        Assert.Empty(_db.Folders);
    }

    // ------------------------------------------------------------------ move

    [Fact]
    public async Task Move_PutsAnOwnSetIntoAnOwnFolderAndTakesItOutAgain()
    {
        var folder = await AddFolderAsync(Owner, "Công việc");
        var set = await AddSetAsync(Owner, "Từ vựng", null, "a");

        var into = await _service.MoveSetToFolderAsync(set.SetId, folder.FolderId, Owner, false);
        Assert.Equal(OperationStatus.Success, into.Status);
        Assert.Equal("Công việc", into.Value);
        Assert.Equal(folder.FolderId, await FolderIdOf(set));

        var outOf = await _service.MoveSetToFolderAsync(set.SetId, null, Owner, false);
        Assert.Equal(OperationStatus.Success, outOf.Status);
        Assert.Null(outOf.Value);
        Assert.Equal(0, await FolderIdOf(set));
    }

    [Fact]
    public async Task Move_RefusesAForeignSetAForeignFolderAndUnknownIds()
    {
        var mine = await AddFolderAsync(Owner, "Của tôi");
        var theirs = await AddFolderAsync(Outsider, "Của họ");
        var mySet = await AddSetAsync(Owner, "Bộ của tôi", null, "a");
        var theirSet = await AddSetAsync(Outsider, "Bộ của họ", null, "b");

        Assert.Equal(OperationStatus.NotFound, (await _service.MoveSetToFolderAsync(theirSet.SetId, mine.FolderId, Owner, false)).Status);
        Assert.Equal(OperationStatus.NotFound, (await _service.MoveSetToFolderAsync(mySet.SetId, theirs.FolderId, Owner, false)).Status);
        Assert.Equal(OperationStatus.NotFound, (await _service.MoveSetToFolderAsync(mySet.SetId, 9999, Owner, false)).Status);
        Assert.Equal(OperationStatus.NotFound, (await _service.MoveSetToFolderAsync(9999, mine.FolderId, Owner, false)).Status);
        Assert.Equal(0, await FolderIdOf(mySet));
        Assert.Equal(0, await FolderIdOf(theirSet));
    }

    [Fact]
    public async Task Move_NeverMixesOwnersEvenForAnAdmin()
    {
        var theirs = await AddFolderAsync(Outsider, "Của họ");
        var mySet = await AddSetAsync(Owner, "Bộ của tôi", null, "a");

        var result = await _service.MoveSetToFolderAsync(mySet.SetId, theirs.FolderId, Admin, true);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Equal(0, await FolderIdOf(mySet));
    }

    // ------------------------------------------------------------------ unsave

    [Fact]
    public async Task Unsave_RemovesOnlyTheCallersSaveAndIsNotFoundWhenNothingWasSaved()
    {
        var folder = await AddFolderAsync(Owner, "Chia sẻ");
        _db.SavedFolders.Add(new SavedFolder { UserId = Saver, FolderId = folder.FolderId });
        _db.SavedFolders.Add(new SavedFolder { UserId = Outsider, FolderId = folder.FolderId });
        await _db.SaveChangesAsync();

        var result = await _service.UnsaveFolderAsync(folder.FolderId, Saver);
        var again = await _service.UnsaveFolderAsync(folder.FolderId, Saver);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(OperationStatus.NotFound, again.Status);
        Assert.Equal(new[] { Outsider }, await _db.SavedFolders.Select(s => s.UserId).ToArrayAsync());
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_ShowsOwnAndSavedFoldersOrderedByNameWithCounts()
    {
        var work = await AddFolderAsync(Owner, "công việc");
        var travel = await AddFolderAsync(Owner, "Du lịch");
        var saved = await AddFolderAsync(Saver, "Ảnh bạn");
        await AddFolderAsync(Outsider, "Không liên quan");
        _db.SavedFolders.Add(new SavedFolder { UserId = Owner, FolderId = saved.FolderId });
        await _db.SaveChangesAsync();
        var s1 = await AddSetAsync(Owner, "S1", work.FolderId, "a", "b", "c");
        await AddSetAsync(Owner, "S2", work.FolderId, "d");
        await AddSetAsync(Saver, "S3", saved.FolderId, "e", "f");

        var folders = await _service.ListFoldersAsync(Owner);

        Assert.Equal(new[] { "Ảnh bạn", "công việc", "Du lịch" }, folders.Select(f => f.Name).ToArray());
        var workTile = folders.Single(f => f.FolderId == work.FolderId);
        Assert.True(workTile.IsOwn);
        Assert.Null(workTile.OwnerName);
        Assert.Equal(2, workTile.SetCount);
        Assert.Equal(4, workTile.WordCount);
        var savedTile = folders.Single(f => f.FolderId == saved.FolderId);
        Assert.False(savedTile.IsOwn);
        Assert.Equal("User 3", savedTile.OwnerName);
        Assert.Equal(1, savedTile.SetCount);
        Assert.Equal(2, savedTile.WordCount);
        var empty = folders.Single(f => f.FolderId == travel.FolderId);
        Assert.Equal((0, 0, 0), (empty.SetCount, empty.WordCount, empty.DueCount));
        Assert.All(folders, f => Assert.Contains(f.Hue, new[] { "warning", "primary", "success", "info" }));
        Assert.NotEqual(0, s1.SetId);
    }

    [Fact]
    public async Task List_ListsAFolderOnlyOnceWhenItIsBothOwnAndSaved()
    {
        var folder = await AddFolderAsync(Owner, "Hai lần");
        _db.SavedFolders.Add(new SavedFolder { UserId = Owner, FolderId = folder.FolderId });
        await _db.SaveChangesAsync();

        var folders = await _service.ListFoldersAsync(Owner);

        Assert.Single(folders);
        Assert.True(folders[0].IsOwn);
    }

    [Fact]
    public async Task List_CountsDueWordsOfTheCallerOnly()
    {
        var folder = await AddFolderAsync(Owner, "Ôn tập");
        var set = await AddSetAsync(Owner, "S", folder.FolderId, "a", "b", "c", "d");
        var cardIds = await _db.Cards.Where(c => c.SetId == set.SetId).OrderBy(c => c.CardId).Select(c => c.CardId).ToListAsync();
        await AddProgressAsync(Owner, cardIds[0], DateTime.UtcNow.AddMinutes(-5));          // due
        await AddProgressAsync(Owner, cardIds[1], DateTime.UtcNow.AddDays(2));              // later
        await AddProgressAsync(Owner, cardIds[2], DateTime.UtcNow.AddMinutes(-5), true);    // suspended
        await AddProgressAsync(Outsider, cardIds[3], DateTime.UtcNow.AddMinutes(-5));       // someone else

        var folders = await _service.ListFoldersAsync(Owner);

        Assert.Equal(1, folders.Single().DueCount);
    }

    [Fact]
    public async Task SetIndex_ListsFoldersPerFilterAndNamesTheFolderOnTheSetTile()
    {
        var work = await AddFolderAsync(Owner, "Công việc");
        var saved = await AddFolderAsync(Saver, "Của bạn");
        _db.SavedFolders.Add(new SavedFolder { UserId = Owner, FolderId = saved.FolderId });
        await _db.SaveChangesAsync();
        await AddSetAsync(Owner, "Trong thư mục", work.FolderId, "a");
        await AddSetAsync(Owner, "Không thư mục", null, "b");

        var all = await _service.GetSetIndexAsync(Owner, false, null);
        var own = await _service.GetSetIndexAsync(Owner, false, "own");
        var shared = await _service.GetSetIndexAsync(Owner, false, "shared");
        var subscribed = await _service.GetSetIndexAsync(Owner, false, "subscribed");

        Assert.Equal(new[] { "Công việc", "Của bạn" }, all.Folders.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "Công việc" }, own.Folders.Select(f => f.Name).ToArray());
        Assert.Equal(new[] { "Của bạn" }, shared.Folders.Select(f => f.Name).ToArray());
        Assert.Empty(subscribed.Folders);
        Assert.Equal("Công việc", all.Own.Single(t => t.SetName == "Trong thư mục").FolderName);
        Assert.Equal(work.FolderId, all.Own.Single(t => t.SetName == "Trong thư mục").FolderId);
        Assert.Null(all.Own.Single(t => t.SetName == "Không thư mục").FolderName);
    }

    [Fact]
    public async Task SetIndex_NeverNamesAFolderTheViewerCannotOpen()
    {
        var theirs = await AddFolderAsync(Outsider, "Bí mật của họ");
        var set = await AddSetAsync(Outsider, "Bộ công khai", theirs.FolderId, "a");
        set.Visibility = "Public";
        _db.SetSubscriptions.Add(new SetSubscription { SetId = set.SetId, UserId = Owner, SubscribedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var index = await _service.GetSetIndexAsync(Owner, false, null);

        var tile = index.Subscribed.Single();
        Assert.Null(tile.FolderName);
        Assert.Null(tile.FolderId);
    }

    // ------------------------------------------------------------------ folder page

    [Fact]
    public async Task Page_ForTheOwnerCanManageListsSetsAndMovableSets()
    {
        var folder = await AddFolderAsync(Owner, "Công việc");
        var other = await AddFolderAsync(Owner, "Du lịch");
        await AddSetAsync(Owner, "Trong", folder.FolderId, "a", "b");
        var loose = await AddSetAsync(Owner, "Tự do", null, "c");
        var elsewhere = await AddSetAsync(Owner, "Ở thư mục khác", other.FolderId, "d");
        await AddSetAsync(Outsider, "Của người khác", null, "e");

        var page = await _service.GetFolderPageAsync(folder.FolderId, Owner, false);

        Assert.NotNull(page);
        Assert.True(page!.CanManage);
        Assert.False(page.IsSaved);
        Assert.Equal("Công việc", page.Name);
        Assert.Equal(1, page.SetCount);
        Assert.Equal(2, page.WordCount);
        Assert.Equal("Trong", page.Sets.Single().SetName);
        Assert.Equal("own", page.Sets.Single().Relation);
        Assert.False(page.HasChildFolders);
        Assert.Equal(new[] { "Tự do", "Ở thư mục khác" }, page.MovableSets.Select(m => m.SetName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal("Du lịch", page.MovableSets.Single(m => m.SetId == elsewhere.SetId).CurrentFolderName);
        Assert.Null(page.MovableSets.Single(m => m.SetId == loose.SetId).CurrentFolderName);
    }

    [Fact]
    public async Task Page_ForASavedFollowerIsReadOnlyWithTheOwnerNameAndNoMovableSets()
    {
        var folder = await AddFolderAsync(Owner, "Chia sẻ");
        await AddSetAsync(Owner, "Bộ", folder.FolderId, "a");
        await AddSetAsync(Saver, "Bộ của người lưu", null, "b");
        _db.SavedFolders.Add(new SavedFolder { UserId = Saver, FolderId = folder.FolderId });
        await _db.SaveChangesAsync();

        var page = await _service.GetFolderPageAsync(folder.FolderId, Saver, false);

        Assert.NotNull(page);
        Assert.False(page!.CanManage);
        Assert.True(page.IsSaved);
        Assert.Equal("User 1", page.OwnerName);
        Assert.Equal("shared", page.Sets.Single().Relation);
        Assert.Empty(page.MovableSets);
    }

    [Fact]
    public async Task Page_ForAClassMemberIsReadOnlyAndForAStrangerIsMissing()
    {
        var folder = await AddFolderAsync(Owner, "Lớp học");
        await AddSetAsync(Owner, "Bộ", folder.FolderId, "a");
        var cls = new Class { ClassName = "C", OwnerId = ClassOwner, CreatedAt = DateTime.UtcNow };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync();
        _db.ClassFolders.Add(new ClassFolder { ClassId = cls.ClassId, FolderId = folder.FolderId, AddedByUserId = Owner });
        _db.ClassMembers.Add(new TCTVocabulary.Models.ClassMember { ClassId = cls.ClassId, UserId = ClassMember });
        await _db.SaveChangesAsync();

        var member = await _service.GetFolderPageAsync(folder.FolderId, ClassMember, false);
        var classOwner = await _service.GetFolderPageAsync(folder.FolderId, ClassOwner, false);
        var stranger = await _service.GetFolderPageAsync(folder.FolderId, Outsider, false);
        var missing = await _service.GetFolderPageAsync(4242, Owner, false);
        var admin = await _service.GetFolderPageAsync(folder.FolderId, Admin, true);

        Assert.NotNull(member);
        Assert.False(member!.CanManage);
        Assert.False(member.IsSaved);
        Assert.NotNull(classOwner);
        Assert.Null(stranger);
        Assert.Null(missing);
        Assert.True(admin!.CanManage);
    }

    [Fact]
    public async Task Page_ReportsChildFoldersAndTheCallersDueWords()
    {
        var folder = await AddFolderAsync(Owner, "Cha");
        await AddFolderAsync(Owner, "Con", folder.FolderId);
        var set = await AddSetAsync(Owner, "Bộ", folder.FolderId, "a", "b");
        var cardIds = await _db.Cards.Where(c => c.SetId == set.SetId).Select(c => c.CardId).ToListAsync();
        await AddProgressAsync(Owner, cardIds[0], DateTime.UtcNow.AddMinutes(-1));

        var page = await _service.GetFolderPageAsync(folder.FolderId, Owner, false);

        Assert.True(page!.HasChildFolders);
        Assert.Equal(1, page.DueCount);
        Assert.Equal(1, page.Sets.Single().DueCount);
    }

    // ------------------------------------------------------------------ stubs

    private sealed class StubFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }
}
