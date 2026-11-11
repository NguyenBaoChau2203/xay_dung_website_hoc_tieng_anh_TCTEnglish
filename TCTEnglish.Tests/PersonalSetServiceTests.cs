using System.Net;
using System.Reflection;
using System.Text;
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

public sealed class PersonalSetServiceTests : IAsyncLifetime
{
    private const int Owner = 1, Outsider = 2, Subscriber = 3, FolderOwner = 4, Saver = 5, ClassOwner = 6, ClassMember = 7, Admin = 8, SystemUser = 9;

    private SqliteConnection _connection = null!;
    private DbflashcardContext _db = null!;
    private PersonalSetService _service = null!;
    private string _webRoot = null!;
    private StubHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        foreach (var (id, role) in new[] { (Owner, "Standard"), (Outsider, "Standard"), (Subscriber, "Standard"), (FolderOwner, "Standard"),
                     (Saver, "Standard"), (ClassOwner, "Standard"), (ClassMember, "Standard"), (Admin, "Admin") })
        {
            _db.Users.Add(new User { UserId = id, Email = $"u{id}@test.local", PasswordHash = "x", FullName = $"User {id}", Role = role, CreatedAt = DateTime.UtcNow });
        }

        _db.Users.Add(new User { UserId = SystemUser, Email = PersonalSetService.SystemOwnerEmail, PasswordHash = "x", FullName = "TCT System", Role = "System", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        _webRoot = Path.Combine(Path.GetTempPath(), "tct-sets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);
        _handler = new StubHandler();
        _service = NewService(_db);
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

    private PersonalSetService NewService(DbflashcardContext db)
        => new(db, TimeProvider.System, new StubFactory(_handler), new StubEnvironment(_webRoot), NullLogger<PersonalSetService>.Instance);

    // ------------------------------------------------------------------ seed helpers

    private async Task<Set> AddSetAsync(int owner, string name, string visibility = "Private", int? folderId = null, params (string Term, string Def)[] cards)
    {
        var set = new Set { SetName = name, OwnerId = owner, Visibility = visibility, FolderId = folderId, CreatedAt = DateTime.UtcNow };
        foreach (var (term, def) in cards)
        {
            set.Cards.Add(new Card { Term = term, Definition = def });
        }

        _db.Sets.Add(set);
        await _db.SaveChangesAsync();
        return set;
    }

    private async Task<Folder> AddFolderAsync(int owner, string name)
    {
        var folder = new Folder { FolderName = name, UserId = owner };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    private async Task AddProgressAsync(int userId, int cardId, string? status, int wrong = 0, bool suspended = false, DateTime? next = null)
    {
        _db.LearningProgresses.Add(new LearningProgress
        {
            UserId = userId,
            CardId = cardId,
            Status = status,
            WrongCount = wrong,
            RepetitionCount = status == "Mastered" ? 1 : 0,
            IsSuspended = suspended,
            LastReviewedDate = DateTime.UtcNow,
            NextReviewDate = next,
        });
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ access

    [Fact]
    public async Task OwnerAndAdminManage_OutsiderOnlyViewsPublic_SubscriberViewsPrivate()
    {
        var priv = await AddSetAsync(Owner, "Private deck", "Private", null, ("a", "b"));
        var pub = await AddSetAsync(Owner, "Public deck", "Public", null, ("c", "d"));

        Assert.True(await _service.CanViewSetAsync(priv.SetId, Owner, false));
        Assert.True(await _service.CanEditSetAsync(priv.SetId, Owner, false));
        Assert.True(await _service.CanViewSetAsync(priv.SetId, Admin, true));
        Assert.True(await _service.CanEditSetAsync(priv.SetId, Admin, true));

        Assert.False(await _service.CanViewSetAsync(priv.SetId, Outsider, false));
        Assert.True(await _service.CanViewSetAsync(pub.SetId, Outsider, false));
        Assert.False(await _service.CanEditSetAsync(pub.SetId, Outsider, false));

        Assert.False(await _service.CanViewSetAsync(priv.SetId, Subscriber, false));
        _db.SetSubscriptions.Add(new SetSubscription { SetId = priv.SetId, UserId = Subscriber, SubscribedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        Assert.True(await _service.CanViewSetAsync(priv.SetId, Subscriber, false));
        Assert.False(await _service.CanEditSetAsync(priv.SetId, Subscriber, false));
    }

    [Fact]
    public async Task ViewRuleIsASupersetOfTheChatCardAccessRule_ForEveryBranch()
    {
        var folderForOwner = await AddFolderAsync(FolderOwner, "mine");
        var savedFolder = await AddFolderAsync(Owner, "saved");
        var classFolder = await AddFolderAsync(Owner, "class");
        _db.SavedFolders.Add(new SavedFolder { UserId = Saver, FolderId = savedFolder.FolderId });
        var cls = new Class { ClassName = "C", OwnerId = ClassOwner, CreatedAt = DateTime.UtcNow };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync();
        _db.ClassFolders.Add(new ClassFolder { ClassId = cls.ClassId, FolderId = classFolder.FolderId, AddedByUserId = Owner });
        _db.ClassMembers.Add(new TCTVocabulary.Models.ClassMember { ClassId = cls.ClassId, UserId = ClassMember });
        await _db.SaveChangesAsync();

        var inFolderOwnerSet = await AddSetAsync(Owner, "folder owner branch", "Private", folderForOwner.FolderId, ("x1", "y"));
        var inSavedSet = await AddSetAsync(Owner, "saved branch", "Private", savedFolder.FolderId, ("x2", "y"));
        var inClassSet = await AddSetAsync(Owner, "class branch", "Private", classFolder.FolderId, ("x3", "y"));
        var ownSet = await AddSetAsync(Owner, "own branch", "Private", null, ("x4", "y"));

        var authorized = typeof(TCTEnglish.Services.AI.Internal.Retrievers.CardLookupRetriever).Assembly
            .GetType("TCTEnglish.Services.AI.Internal.Retrievers.LearnerCardAccess")!
            .GetMethod("Authorized", BindingFlags.Public | BindingFlags.Static)!;

        var cases = new (int User, Set Set)[]
        {
            (Owner, ownSet),
            (FolderOwner, inFolderOwnerSet),
            (Saver, inSavedSet),
            (ClassOwner, inClassSet),
            (ClassMember, inClassSet),
        };

        foreach (var (user, set) in cases)
        {
            var allowedCards = ((IQueryable<Card>)authorized.Invoke(null, new object[] { _db, user })!).Select(c => c.SetId).Distinct().ToList();
            Assert.Contains(set.SetId, allowedCards);
            Assert.True(await _service.CanViewSetAsync(set.SetId, user, false), $"user {user} must view set {set.SetName}");
        }

        // Every set the chat rule authorizes must also be viewable.
        foreach (var user in new[] { Owner, Outsider, FolderOwner, Saver, ClassOwner, ClassMember })
        {
            var chatSets = ((IQueryable<Card>)authorized.Invoke(null, new object[] { _db, user })!).Select(c => c.SetId).Distinct().ToList();
            foreach (var setId in chatSets)
            {
                Assert.True(await _service.CanViewSetAsync(setId, user, false));
            }
        }

        Assert.False(await _service.CanViewSetAsync(inClassSet.SetId, Outsider, false));
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task Index_GroupsOwnSubscribedAndFolderSharedSets_WithDueAndNewCounts()
    {
        var mine = await AddSetAsync(Owner, "Mine", "Private", null, ("a", "1"), ("b", "2"), ("c", "3"), ("d", "4"));
        var theirs = await AddSetAsync(Outsider, "Theirs", "Public", null, ("e", "5"));
        var cardIds = mine.Cards.OrderBy(c => c.CardId).Select(c => c.CardId).ToList();
        await AddProgressAsync(Owner, cardIds[0], "Mastered", 0, false, DateTime.UtcNow.AddDays(3));
        await AddProgressAsync(Owner, cardIds[1], "Learning", 1, false, DateTime.UtcNow.AddMinutes(-5)); // due
        await AddProgressAsync(Owner, cardIds[2], "Learning", 1, true, DateTime.UtcNow.AddMinutes(-5)); // suspended: not due
        _db.SetSubscriptions.Add(new SetSubscription { SetId = theirs.SetId, UserId = Owner, SubscribedAt = DateTime.UtcNow });

        var folder = await AddFolderAsync(Saver, "f");
        var classSet = await AddSetAsync(Saver, "Saved folder deck", "Private", folder.FolderId, ("z", "z"));
        _db.SavedFolders.Add(new SavedFolder { UserId = Owner, FolderId = folder.FolderId });
        await _db.SaveChangesAsync();

        var model = await _service.GetSetIndexAsync(Owner, false, null);

        var tile = Assert.Single(model.Own);
        Assert.Equal("Mine", tile.SetName);
        Assert.Equal(4, tile.WordCount);
        Assert.Equal(1, tile.DueCount);
        Assert.Equal(1, tile.NewCount);
        Assert.Equal(75, tile.ProgressPercent);
        Assert.Equal("a", tile.SampleTerm);
        Assert.Equal("Private", tile.Visibility);
        Assert.Equal("Theirs", Assert.Single(model.Subscribed).SetName);
        Assert.Equal(classSet.SetId, Assert.Single(model.Shared).SetId);

        var onlyShared = await _service.GetSetIndexAsync(Owner, false, "shared");
        Assert.Empty(onlyShared.Own);
        Assert.Single(onlyShared.Shared);
    }

    [Fact]
    public async Task Index_IgnoresUnknownFilterAndShowsNothingForAnotherUser()
    {
        await AddSetAsync(Owner, "Mine", "Private", null, ("a", "b"));
        var model = await _service.GetSetIndexAsync(Outsider, false, "bogus");
        Assert.True(model.IsEmpty);
        Assert.Null(model.Filter);
    }

    // ------------------------------------------------------------------ search

    [Fact]
    public async Task SearchCards_FiltersByStatusQueryPosAndSortsAndPages()
    {
        var set = await AddSetAsync(Owner, "Search", "Private", null);
        var cards = new List<Card>
        {
            new() { SetId = set.SetId, Term = "apple", Definition = "quả táo", Phonetic = "/ap/", PartsOfSpeech = "noun", Cefr = "A1", Topic = "Ăn uống" },
            new() { SetId = set.SetId, Term = "banana", Definition = "chuối", PartsOfSpeech = "noun,adj", Cefr = "A2" },
            new() { SetId = set.SetId, Term = "cherry 100%", Definition = "anh đào", PartsOfSpeech = "verb" },
            new() { SetId = set.SetId, Term = "date_fruit", Definition = "chà là" },
        };
        _db.Cards.AddRange(cards);
        await _db.SaveChangesAsync();
        await AddProgressAsync(Owner, cards[0].CardId, "Mastered");
        await AddProgressAsync(Owner, cards[1].CardId, "Learning", 4); // hard
        await AddProgressAsync(Owner, cards[2].CardId, "Learning", 0, true); // suspended

        async Task<List<string>> Terms(CardSearchQuery q) => (await _service.SearchCardsAsync(set.SetId, q, Owner, false))!.Rows.Select(r => r.Term).ToList();

        Assert.Equal(new[] { "apple" }, await Terms(new() { Status = "learned" }));
        Assert.Equal(new[] { "banana" }, await Terms(new() { Status = "hard" }));
        Assert.Equal(new[] { "cherry 100%" }, await Terms(new() { Status = "suspended" }));
        Assert.Equal(new[] { "date_fruit" }, await Terms(new() { Status = "new" }));
        Assert.Equal(new[] { "apple", "banana" }, await Terms(new() { Pos = "noun", Sort = "az" }));
        Assert.Equal(new[] { "banana" }, await Terms(new() { Pos = "adj" }));
        Assert.Equal(new[] { "apple" }, await Terms(new() { Level = "A1" }));
        Assert.Equal(new[] { "apple" }, await Terms(new() { Q = "quả" }));
        Assert.Equal(new[] { "apple" }, await Terms(new() { Q = "/ap/" }));
        Assert.Equal(new[] { "cherry 100%" }, await Terms(new() { Q = "100%" }));
        Assert.Equal(new[] { "date_fruit" }, await Terms(new() { Q = "e_f" }));
        Assert.Empty(await Terms(new() { Q = "%" , Topic = "none" }));
        Assert.Equal(new[] { "banana", "apple", "date_fruit", "cherry 100%" }.Length, (await Terms(new() { Sort = "hard" })).Count);
        Assert.Equal("banana", (await Terms(new() { Sort = "hard" })).First());

        var page = (await _service.SearchCardsAsync(set.SetId, new() { Status = "learned" }, Owner, false))!;
        Assert.Equal(1, page.Total);
        Assert.Equal("learned", page.Rows.Single().Status.Key);
        Assert.Equal("Đã thuộc", page.Rows.Single().Status.Label);
    }

    [Fact]
    public async Task SearchCards_PagesInFifties_AndHidesPrivateSetFromOutsider()
    {
        var set = await AddSetAsync(Owner, "Big", "Private", null);
        _db.Cards.AddRange(Enumerable.Range(1, 120).Select(i => new Card { SetId = set.SetId, Term = $"w{i:000}", Definition = "d" }));
        await _db.SaveChangesAsync();

        var first = (await _service.SearchCardsAsync(set.SetId, new() { Sort = "az" }, Owner, false))!;
        Assert.Equal(50, first.Rows.Count);
        Assert.Equal(50, first.NextOffset);
        Assert.Equal(120, first.Total);
        var last = (await _service.SearchCardsAsync(set.SetId, new() { Sort = "az", Offset = 100 }, Owner, false))!;
        Assert.Equal(20, last.Rows.Count);
        Assert.Null(last.NextOffset);

        Assert.Null(await _service.SearchCardsAsync(set.SetId, new(), Outsider, false));
    }

    [Fact]
    public async Task ProgressIsPerViewer_SoAnotherUsersStatusNeverLeaks()
    {
        var set = await AddSetAsync(Owner, "Shared", "Public", null, ("only", "một"));
        await AddProgressAsync(Owner, set.Cards.Single().CardId, "Mastered");
        var forOutsider = (await _service.SearchCardsAsync(set.SetId, new(), Outsider, false))!;
        Assert.Equal("new", forOutsider.Rows.Single().Status.Key);
    }

    // ------------------------------------------------------------------ export

    [Fact]
    public async Task Export_WritesBomHeaderEscapesFormulasAndHidesNotesFromNonOwners()
    {
        var set = await AddSetAsync(Owner, "Thẻ: Ăn/Uống?", "Public", null);
        _db.Cards.Add(new Card { SetId = set.SetId, Term = "=1+1", Definition = "a, \"b\"", PersonalNote = "secret", Example = "line\nbreak", PartsOfSpeech = "noun" });
        await _db.SaveChangesAsync();

        var owner = (await _service.ExportCsvAsync(set.SetId, Owner, false))!;
        var other = (await _service.ExportCsvAsync(set.SetId, Outsider, false))!;

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, owner.Content.Take(3).ToArray());
        var ownerText = Encoding.UTF8.GetString(owner.Content, 3, owner.Content.Length - 3);
        Assert.StartsWith("term,phonetic,meaning_vi,meaning_en,parts_of_speech,topic,example,example_vi,synonyms,antonyms,cefr,note\r\n", ownerText);
        Assert.Contains("'=1+1", ownerText);
        Assert.Contains("\"a, \"\"b\"\"\"", ownerText);
        Assert.Contains("secret", ownerText);
        Assert.DoesNotContain("secret", Encoding.UTF8.GetString(other.Content));
        Assert.Equal("Thẻ ĂnUống.csv", owner.FileName);

        var priv = await AddSetAsync(Owner, "Private", "Private", null, ("a", "b"));
        Assert.Null(await _service.ExportCsvAsync(priv.SetId, Outsider, false));
        Assert.Equal("bo-the", SetNaming.ToFileName("???"));
    }

    // ------------------------------------------------------------------ create / delete

    [Fact]
    public async Task CreateSet_DefaultsToPrivate_RejectsForeignFolder_AndCapsAtTwoHundred()
    {
        var foreignFolder = await AddFolderAsync(Outsider, "theirs");
        var denied = await _service.CreateSetAsync(new SetInfoFormViewModel { SetName = "x", FolderId = foreignFolder.FolderId }, Owner, false);
        Assert.Equal(OperationStatus.NotFound, denied.Status);

        var ok = await _service.CreateSetAsync(new SetInfoFormViewModel { SetName = "  Mine  ", Visibility = "weird" }, Owner, false);
        var created = await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == ok.Value);
        Assert.Equal("Mine", created.SetName);
        Assert.Equal("Private", created.Visibility);
        Assert.NotNull(created.UpdatedAt);

        var blank = await _service.CreateSetAsync(new SetInfoFormViewModel { SetName = "   " }, Owner, false);
        Assert.Equal(OperationStatus.Invalid, blank.Status);
        Assert.Equal("Nhập tên bộ thẻ", blank.FieldErrors!["SetName"]);

        _db.Sets.AddRange(Enumerable.Range(0, 199).Select(i => new Set { SetName = $"s{i}", OwnerId = Owner, CreatedAt = DateTime.UtcNow }));
        await _db.SaveChangesAsync();
        var capped = await _service.CreateSetAsync(new SetInfoFormViewModel { SetName = "one too many" }, Owner, false);
        Assert.Equal(OperationStatus.Invalid, capped.Status);
        Assert.Contains("200", capped.ErrorMessage);
    }

    [Fact]
    public async Task DeleteSet_WithProgressAndSubscribers_RemovesEverythingAndRequiresExactName()
    {
        var set = await AddSetAsync(Owner, "Delete me", "Public", null, ("a", "b"), ("c", "d"));
        foreach (var card in set.Cards)
        {
            await AddProgressAsync(Owner, card.CardId, "Mastered");
            await AddProgressAsync(Outsider, card.CardId, "Learning", 2);
        }

        _db.SetSubscriptions.Add(new SetSubscription { SetId = set.SetId, UserId = Outsider, SubscribedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        Assert.Equal(OperationStatus.NotFound, (await _service.DeleteSetAsync(set.SetId, Outsider, false)).Status);
        Assert.Equal(OperationStatus.Invalid, (await _service.DeleteSetAsync(set.SetId, Owner, false, "delete me")).Status);
        Assert.True(await _db.Sets.AnyAsync(s => s.SetId == set.SetId));

        var result = await _service.DeleteSetAsync(set.SetId, Owner, false, "  Delete me ");
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.False(await _db.Sets.AnyAsync(s => s.SetId == set.SetId));
        Assert.Empty(await _db.Cards.Where(c => c.SetId == set.SetId).ToListAsync());
        Assert.Empty(await _db.LearningProgresses.ToListAsync());
        Assert.Empty(await _db.SetSubscriptions.ToListAsync());
    }

    // ------------------------------------------------------------------ batch create

    [Fact]
    public async Task CreateCardsBatch_SkipsDuplicates_FlagsInvalid_AndRejectsOverThirty()
    {
        var set = await AddSetAsync(Owner, "Batch", "Private", null, ("Apple", "táo"));
        var result = await _service.CreateCardsBatchAsync(set.SetId, new List<CardInputDto>
        {
            new() { Term = "apple", Definition = "táo 2" },           // exists (case-insensitive)
            new() { Term = "Pear", Definition = "lê" },
            new() { Term = " pear ", Definition = "lê 2" },            // in-batch duplicate
            new() { Term = "Fig", Definition = "" },                   // invalid
            new() { Term = "Plum", Definition = "mận", PartsOfSpeech = new() { "noun" }, Examples = new() { new() { Text = "A plum.", Vi = "Một quả mận." } } },
        }, Owner, false);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(new[] { "Pear", "Plum" }, result.Value!.Inserted.Select(i => i.Term).ToArray());
        Assert.Equal(new[] { "pear", "apple" }.OrderBy(x => x), result.Value.Skipped.Select(s => s.Trim().ToLowerInvariant()).OrderBy(x => x));
        Assert.Equal(new[] { "Fig" }, result.Value.Invalid.ToArray());

        var plum = await _db.Cards.AsNoTracking().SingleAsync(c => c.Term == "Plum");
        Assert.Equal("A plum.", plum.Example);
        Assert.Equal("Một quả mận.", plum.ExampleTranslation);
        Assert.NotNull(plum.CreatedAt);
        Assert.Equal("noun", plum.PartsOfSpeech);

        var tooMany = await _service.CreateCardsBatchAsync(set.SetId, Enumerable.Range(0, 31).Select(i => new CardInputDto { Term = $"t{i}", Definition = "d" }).ToList(), Owner, false);
        Assert.Equal(OperationStatus.Invalid, tooMany.Status);

        var foreign = await _service.CreateCardsBatchAsync(set.SetId, new List<CardInputDto> { new() { Term = "x", Definition = "y" } }, Outsider, false);
        Assert.Equal(OperationStatus.NotFound, foreign.Status);
    }

    // ------------------------------------------------------------------ bulk

    [Fact]
    public async Task BulkMove_KeepsProgress_SkipsTermsTheTargetAlreadyHas_AndNeverMovesIntoSomeoneElsesSet()
    {
        var source = await AddSetAsync(Owner, "Source", "Private", null, ("a", "1"), ("b", "2"), ("c", "3"));
        var target = await AddSetAsync(Owner, "Target", "Private", null, ("B", "dup"));
        var foreignTarget = await AddSetAsync(Outsider, "Foreign", "Public", null);
        var ids = source.Cards.Select(c => c.CardId).ToList();
        await AddProgressAsync(Owner, ids[0], "Mastered");

        var denied = await _service.BulkCardsAsync(source.SetId, new CardBulkRequest { Action = "move", CardIds = ids, TargetSetId = foreignTarget.SetId }, Owner, false);
        Assert.Equal(OperationStatus.NotFound, denied.Status);

        var moved = await _service.BulkCardsAsync(source.SetId, new CardBulkRequest { Action = "move", CardIds = ids, TargetSetId = target.SetId }, Owner, false);
        Assert.Equal(OperationStatus.Success, moved.Status);
        Assert.Equal(2, moved.Value!.Count);
        Assert.Equal(1, moved.Value.Skipped);
        Assert.Equal(2, await _db.Cards.CountAsync(c => c.SetId == target.SetId && (c.Term == "a" || c.Term == "c")));
        Assert.Equal("Mastered", (await _db.LearningProgresses.SingleAsync(p => p.CardId == ids[0])).Status);

        var again = await _service.BulkCardsAsync(source.SetId, new CardBulkRequest { Action = "move", CardIds = new() { ids[1] }, TargetSetId = target.SetId }, Owner, false);
        Assert.Equal(OperationStatus.Invalid, again.Status);
        Assert.Contains("Target", again.ErrorMessage);
    }

    [Fact]
    public async Task BulkSuspend_IsPerUser_AndAllowedForViewersOnly()
    {
        var set = await AddSetAsync(Owner, "Public", "Public", null, ("a", "1"));
        var cardId = set.Cards.Single().CardId;

        var result = await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "suspend", CardIds = new() { cardId }, Suspended = true }, Outsider, false);
        Assert.Equal(OperationStatus.Success, result.Status);

        var mine = await _db.LearningProgresses.AsNoTracking().SingleAsync(p => p.UserId == Outsider && p.CardId == cardId);
        Assert.True(mine.IsSuspended);
        Assert.Null(mine.Status);
        Assert.False(await _db.LearningProgresses.AnyAsync(p => p.UserId == Owner));

        var edit = await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "delete", CardIds = new() { cardId } }, Outsider, false);
        Assert.Equal(OperationStatus.NotFound, edit.Status);
        Assert.True(await _db.Cards.AnyAsync(c => c.CardId == cardId));

        var privateSet = await AddSetAsync(Owner, "Priv", "Private", null, ("p", "q"));
        var hidden = await _service.BulkCardsAsync(privateSet.SetId, new CardBulkRequest { Action = "suspend", CardIds = new() { privateSet.Cards.Single().CardId }, Suspended = true }, Outsider, false);
        Assert.Equal(OperationStatus.NotFound, hidden.Status);
    }

    [Fact]
    public async Task BulkTopicAndDelete_AffectOnlyCardsInThatSet_AndDeleteRemovesProgress()
    {
        var set = await AddSetAsync(Owner, "Bulk", "Private", null, ("a", "1"), ("b", "2"));
        var other = await AddSetAsync(Owner, "Other", "Private", null, ("z", "9"));
        var ids = set.Cards.Select(c => c.CardId).Append(other.Cards.Single().CardId).ToList();
        await AddProgressAsync(Owner, ids[0], "Mastered");

        var topic = await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "topic", CardIds = ids, Topic = " Công việc " }, Owner, false);
        Assert.Equal(2, topic.Value!.Count);
        Assert.Equal("Công việc", (await _db.Cards.AsNoTracking().FirstAsync(c => c.CardId == ids[0])).Topic);
        Assert.Null((await _db.Cards.AsNoTracking().FirstAsync(c => c.CardId == ids[2])).Topic);

        var clear = await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "topic", CardIds = ids, Topic = null }, Owner, false);
        Assert.Null((await _db.Cards.AsNoTracking().FirstAsync(c => c.CardId == ids[0])).Topic);
        Assert.Equal(2, clear.Value!.Count);

        var deleted = await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "delete", CardIds = ids }, Owner, false);
        Assert.Equal(2, deleted.Value!.Count);
        Assert.Empty(await _db.LearningProgresses.ToListAsync());
        Assert.True(await _db.Cards.AnyAsync(c => c.CardId == ids[2]));

        var none = await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "delete", CardIds = ids }, Owner, false);
        Assert.Equal(OperationStatus.NotFound, none.Status);
        Assert.Equal("Không có từ nào được cập nhật.", none.ErrorMessage);

        Assert.Equal(OperationStatus.Invalid, (await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "explode", CardIds = ids }, Owner, false)).Status);
        Assert.Equal(OperationStatus.Invalid, (await _service.BulkCardsAsync(set.SetId, new CardBulkRequest { Action = "delete", CardIds = new() }, Owner, false)).Status);
    }

    // ------------------------------------------------------------------ card update / delete / suspend

    [Fact]
    public async Task UpdateCard_PersistsFields_RejectsDuplicateTerm_AndIsOwnerOnly()
    {
        var set = await AddSetAsync(Owner, "Edit", "Private", null, ("alpha", "a"), ("beta", "b"));
        var alpha = set.Cards.First(c => c.Term == "alpha");

        var ok = await _service.UpdateCardAsync(alpha.CardId, new CardInputDto
        {
            Term = "Alpha",
            Definition = "chữ cái đầu",
            Cefr = "b1",
            PersonalNote = "mẹo",
            Synonyms = new() { "first" },
        }, Owner, false);
        Assert.Equal(OperationStatus.Success, ok.Status);
        var saved = await _db.Cards.AsNoTracking().SingleAsync(c => c.CardId == alpha.CardId);
        Assert.Equal("Alpha", saved.Term);
        Assert.Equal("B1", saved.Cefr);
        Assert.Equal("mẹo", saved.PersonalNote);
        Assert.Contains("first", saved.DetailsJson);

        var dup = await _service.UpdateCardAsync(alpha.CardId, new CardInputDto { Term = " BETA ", Definition = "x" }, Owner, false);
        Assert.Equal(OperationStatus.Invalid, dup.Status);
        Assert.Equal("Đã có trong bộ này", dup.FieldErrors!["Term"]);

        var bad = await _service.UpdateCardAsync(alpha.CardId, new CardInputDto { Term = "", Definition = "" }, Owner, false);
        Assert.Equal("Nhập từ", bad.FieldErrors!["Term"]);

        Assert.Equal(OperationStatus.NotFound, (await _service.UpdateCardAsync(alpha.CardId, new CardInputDto { Term = "x", Definition = "y" }, Outsider, false)).Status);
    }

    [Fact]
    public async Task DeleteCard_RemovesProgressForEveryone_AndSuspendIsPerViewer()
    {
        var set = await AddSetAsync(Owner, "Cards", "Public", null, ("a", "1"));
        var cardId = set.Cards.Single().CardId;
        await AddProgressAsync(Owner, cardId, "Mastered");
        await AddProgressAsync(Outsider, cardId, "Learning");

        Assert.Equal(OperationStatus.NotFound, (await _service.DeleteCardAsync(cardId, Outsider, false)).Status);
        var suspended = await _service.SetSuspendedAsync(cardId, true, Outsider, false);
        Assert.True(suspended.Value);
        Assert.True((await _db.LearningProgresses.AsNoTracking().SingleAsync(p => p.UserId == Outsider)).IsSuspended);
        Assert.False((await _db.LearningProgresses.AsNoTracking().SingleAsync(p => p.UserId == Owner)).IsSuspended);

        var deleted = await _service.DeleteCardAsync(cardId, Owner, false);
        Assert.Equal(set.SetId, deleted.Value);
        Assert.Empty(await _db.LearningProgresses.ToListAsync());
        Assert.Empty(await _db.Cards.ToListAsync());
    }

    // ------------------------------------------------------------------ share link / follow / clone / samples

    [Fact]
    public async Task ShareLink_EnableRegenerateDisable_AndOnlyTheLiveSlugResolves()
    {
        var set = await AddSetAsync(Owner, "Shareable", "Private", null, ("a", "1"));

        Assert.Equal(OperationStatus.NotFound, (await _service.SetShareLinkAsync(set.SetId, true, false, Outsider, false)).Status);
        var enabled = await _service.SetShareLinkAsync(set.SetId, true, false, Owner, false);
        var slug = enabled.Value!;
        Assert.Matches("^[a-km-z2-9]{10}$", slug);
        Assert.Equal(slug, (await _service.SetShareLinkAsync(set.SetId, true, false, Owner, false)).Value);
        Assert.NotNull(await _service.GetSharedSetAsync(slug, Outsider));

        var regenerated = (await _service.SetShareLinkAsync(set.SetId, true, true, Owner, false)).Value!;
        Assert.NotEqual(slug, regenerated);
        Assert.Null(await _service.GetSharedSetAsync(slug, Outsider));
        Assert.NotNull(await _service.GetSharedSetAsync(regenerated, null));

        await _service.SetShareLinkAsync(set.SetId, false, false, Owner, false);
        Assert.Null(await _service.GetSharedSetAsync(regenerated, Outsider));
        Assert.Null(await _service.GetSharedSetAsync("not a slug!", Outsider));
    }

    [Fact]
    public async Task Subscribe_IsIdempotent_OwnerNoOp_AndDisabledSlugIsNotFound()
    {
        var set = await AddSetAsync(Owner, "Follow", "Private", null, ("a", "1"));
        var slug = (await _service.SetShareLinkAsync(set.SetId, true, false, Owner, false)).Value!;

        Assert.Equal(OperationStatus.NotFound, (await _service.SubscribeAsync(set.SetId, null, Outsider, false)).Status);
        Assert.Equal(OperationStatus.Success, (await _service.SubscribeAsync(null, slug, Outsider, false)).Status);
        Assert.Equal(OperationStatus.Success, (await _service.SubscribeAsync(null, slug, Outsider, false)).Status);
        Assert.Equal(1, await _db.SetSubscriptions.CountAsync());
        Assert.True(await _service.CanViewSetAsync(set.SetId, Outsider, false));

        Assert.Equal(OperationStatus.Success, (await _service.SubscribeAsync(null, slug, Owner, false)).Status);
        Assert.Equal(1, await _db.SetSubscriptions.CountAsync());

        await _service.UnsubscribeAsync(set.SetId, Outsider);
        Assert.Equal(0, await _db.SetSubscriptions.CountAsync());
        Assert.False(await _service.CanViewSetAsync(set.SetId, Outsider, false));

        await _service.SetShareLinkAsync(set.SetId, false, false, Owner, false);
        Assert.Equal(OperationStatus.NotFound, (await _service.SubscribeAsync(null, slug, Subscriber, false)).Status);
    }

    [Fact]
    public async Task Clone_CopiesContentFields_NotProgress_NotForeignNotes_AndStartsPrivateWithoutFolder()
    {
        var folder = await AddFolderAsync(Owner, "F");
        var set = await AddSetAsync(Owner, "Original", "Public", folder.FolderId);
        _db.Cards.Add(new Card
        {
            SetId = set.SetId, Term = "run", Definition = "chạy", Phonetic = "/rʌn/", PhoneticUk = "/rʌn/", MeaningEn = "move fast", PartsOfSpeech = "verb",
            Cefr = "A1", Emoji = "🏃", Topic = "Thể thao", Example = "I run.", ExampleTranslation = "Tôi chạy.", DetailsJson = "{\"synonyms\":[\"jog\"]}",
            PersonalNote = "owner secret", AiGenerated = true,
        });
        await _db.SaveChangesAsync();
        await AddProgressAsync(Owner, (await _db.Cards.FirstAsync()).CardId, "Mastered");

        var clone = await _service.CloneSetAsync(set.SetId, null, Outsider, false);
        Assert.Equal(OperationStatus.Success, clone.Status);
        var copy = await _db.Sets.Include(s => s.Cards).AsNoTracking().SingleAsync(s => s.SetId == clone.Value);
        Assert.Equal(Outsider, copy.OwnerId);
        Assert.Equal("Private", copy.Visibility);
        Assert.Null(copy.FolderId);
        Assert.Null(copy.ShareSlug);
        Assert.Equal(set.SetId, copy.ClonedFromSetId);
        var card = Assert.Single(copy.Cards);
        Assert.Equal("/rʌn/", card.Phonetic);
        Assert.Equal("🏃", card.Emoji);
        Assert.Equal("A1", card.Cefr);
        Assert.Contains("jog", card.DetailsJson);
        Assert.True(card.AiGenerated);
        Assert.Null(card.PersonalNote);
        Assert.False(await _db.LearningProgresses.AnyAsync(p => p.CardId == card.CardId));

        var ownerCopy = await _service.CloneSetAsync(set.SetId, null, Owner, false);
        var ownerCard = await _db.Cards.AsNoTracking().SingleAsync(c => c.SetId == ownerCopy.Value);
        Assert.Equal("owner secret", ownerCard.PersonalNote);
        Assert.Equal("Original (bản sao)", (await _db.Sets.AsNoTracking().SingleAsync(s => s.SetId == ownerCopy.Value)).SetName);

        var priv = await AddSetAsync(Owner, "Hidden", "Private", null, ("a", "b"));
        Assert.Equal(OperationStatus.NotFound, (await _service.CloneSetAsync(priv.SetId, null, Outsider, false)).Status);
    }

    [Fact]
    public async Task AddSamples_ClonesTwoLargestSystemSetsOnce_AndIsIdempotent()
    {
        Assert.Equal(OperationStatus.Invalid, (await _service.AddSampleSetsAsync(Owner)).Status);

        Card[] Cards(int n) => Enumerable.Range(0, n).Select(i => new Card { Term = $"t{i}", Definition = "d" }).ToArray();
        _db.Sets.Add(new Set { SetName = "Big", OwnerId = SystemUser, CreatedAt = DateTime.UtcNow, Cards = Cards(9) });
        _db.Sets.Add(new Set { SetName = "Medium", OwnerId = SystemUser, CreatedAt = DateTime.UtcNow, Cards = Cards(6) });
        _db.Sets.Add(new Set { SetName = "Small", OwnerId = SystemUser, CreatedAt = DateTime.UtcNow, Cards = Cards(2) });
        _db.Sets.Add(new Set { SetName = "Huge but private", OwnerId = SystemUser, Visibility = "Private", CreatedAt = DateTime.UtcNow, Cards = Cards(30) });
        await _db.SaveChangesAsync();

        var first = await _service.AddSampleSetsAsync(Owner);
        Assert.Equal(new SampleSetsResult(2, 15), first.Value);
        Assert.Equal(new[] { "Big", "Medium" }, (await _db.Sets.Where(s => s.OwnerId == Owner).OrderBy(s => s.SetName).Select(s => s.SetName).ToListAsync()).ToArray());

        var second = await _service.AddSampleSetsAsync(Owner);
        Assert.Equal(new SampleSetsResult(0, 0), second.Value);
        Assert.Equal(2, await _db.Sets.CountAsync(s => s.OwnerId == Owner));
    }

    // ------------------------------------------------------------------ legacy edit (B1) and images (B3)

    [Fact]
    public async Task LegacyEdit_KeepsProgressAndCardDetails_DeletesSurplus_AndInsertsNew()
    {
        var set = await AddSetAsync(Owner, "Legacy", "Public", null);
        _db.Cards.AddRange(
            new Card { SetId = set.SetId, Term = "alpha", Definition = "a", Phonetic = "/a/", Example = "An alpha.", Topic = "T", Cefr = "A2" },
            new Card { SetId = set.SetId, Term = "beta", Definition = "b", Phonetic = "/b/" },
            new Card { SetId = set.SetId, Term = "gamma", Definition = "c" });
        await _db.SaveChangesAsync();
        var cards = await _db.Cards.OrderBy(c => c.CardId).ToListAsync();
        await AddProgressAsync(Owner, cards[0].CardId, "Mastered");
        await AddProgressAsync(Owner, cards[2].CardId, "Learning", 2);

        var result = await _service.ApplyLegacyEditAsync(set.SetId, "Renamed", "New description", new List<LegacyCardRow>
        {
            new() { Term = "gamma", Definition = "c updated" },   // reordered, same term -> keeps progress
            new() { Term = "alpha", Definition = "a" },
            new() { Term = "delta", Definition = "new" },         // pairs with beta by position (typo fix keeps progress slot)
            new() { Term = "epsilon", Definition = "brand new" },
        }, Owner, false);

        Assert.Equal(OperationStatus.Success, result.Status);
        _db.ChangeTracker.Clear();
        var saved = await _db.Sets.Include(s => s.Cards).AsNoTracking().SingleAsync(s => s.SetId == set.SetId);
        Assert.Equal("Renamed", saved.SetName);
        Assert.Equal(4, saved.Cards.Count);

        var alpha = saved.Cards.Single(c => c.Term == "alpha");
        Assert.Equal(cards[0].CardId, alpha.CardId);
        Assert.Equal("/a/", alpha.Phonetic);
        Assert.Equal("An alpha.", alpha.Example);
        Assert.Equal("A2", alpha.Cefr);
        Assert.Equal("Mastered", (await _db.LearningProgresses.SingleAsync(p => p.CardId == alpha.CardId)).Status);

        var gamma = saved.Cards.Single(c => c.Term == "gamma");
        Assert.Equal(cards[2].CardId, gamma.CardId);
        Assert.Equal("c updated", gamma.Definition);
        Assert.Equal(2, (await _db.LearningProgresses.SingleAsync(p => p.CardId == gamma.CardId)).WrongCount);

        Assert.Equal(cards[1].CardId, saved.Cards.Single(c => c.Term == "delta").CardId);
        Assert.Contains(saved.Cards, c => c.Term == "epsilon" && c.CreatedAt != null);

        var shrink = await _service.ApplyLegacyEditAsync(set.SetId, "Renamed", null, new List<LegacyCardRow> { new() { Term = "alpha", Definition = "a" } }, Owner, false);
        Assert.Equal(OperationStatus.Success, shrink.Status);
        _db.ChangeTracker.Clear();
        Assert.Equal(1, await _db.Cards.CountAsync(c => c.SetId == set.SetId));
        Assert.Equal(1, await _db.LearningProgresses.CountAsync());

        Assert.Equal(OperationStatus.NotFound, (await _service.ApplyLegacyEditAsync(set.SetId, "x", null, new List<LegacyCardRow>(), Outsider, false)).Status);
    }

    [Theory]
    [InlineData("http://pixabay.com/a.jpg")]
    [InlineData("https://example.com/a.jpg")]
    [InlineData("https://127.0.0.1/a.jpg")]
    [InlineData("https://pixabay.com.evil.test/a.jpg")]
    [InlineData("file:///etc/passwd")]
    public async Task ImportExternalImage_RejectsNonPixabayHostsWithoutAnyRequest(string url)
    {
        Assert.Null(await _service.ImportExternalImageAsync(url));
        Assert.Equal(0, _handler.Requests);
    }

    [Fact]
    public async Task ImportExternalImage_SavesOnlyBoundedImagesWithMatchingSignature()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Bytes(png, "image/png") };
        var saved = await _service.ImportExternalImageAsync("https://cdn.pixabay.com/photo/a.png");
        Assert.StartsWith("/images/cards/", saved);
        Assert.EndsWith(".png", saved);
        Assert.True(File.Exists(Path.Combine(_webRoot, "images", "cards", Path.GetFileName(saved!))));

        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Bytes(Encoding.UTF8.GetBytes("<html>nope</html>"), "text/html") };
        Assert.Null(await _service.ImportExternalImageAsync("https://pixabay.com/a.png"));

        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Bytes(Encoding.UTF8.GetBytes("<html>nope</html>"), "image/png") };
        Assert.Null(await _service.ImportExternalImageAsync("https://pixabay.com/a.png"));

        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Bytes(new byte[5 * 1024 * 1024 + 10], "image/png") };
        Assert.Null(await _service.ImportExternalImageAsync("https://pixabay.com/big.png"));

        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        Assert.Null(await _service.ImportExternalImageAsync("https://pixabay.com/missing.png"));
        Assert.Single(Directory.GetFiles(Path.Combine(_webRoot, "images", "cards")));
    }

    [Fact]
    public async Task BatchCreate_StoresNoExternalUrlWhenDownloadFails()
    {
        var set = await AddSetAsync(Owner, "Images", "Private", null);
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var result = await _service.CreateCardsBatchAsync(set.SetId, new List<CardInputDto>
        {
            new() { Term = "pic", Definition = "ảnh", ImageUrl = "https://pixabay.com/get/x.jpg", ImageSource = new CardImageSource { Provider = "pixabay" } },
            new() { Term = "evil", Definition = "x", ImageUrl = "https://evil.example/x.jpg" },
        }, Owner, false);

        Assert.Equal(new[] { "pic" }, result.Value!.Inserted.Select(i => i.Term).ToArray());
        Assert.Equal(new[] { "evil" }, result.Value.Invalid.ToArray());
        var pic = await _db.Cards.AsNoTracking().SingleAsync(c => c.Term == "pic");
        Assert.Null(pic.ImageUrl);
        Assert.Null(pic.ImageSourceJson);
    }

    // ------------------------------------------------------------------ detail

    [Fact]
    public async Task Detail_ReportsRoleFlags_AndHidesShareSlugFromViewers()
    {
        var set = await AddSetAsync(Owner, "Detail", "Public", null, ("a", "1"), ("b", "2"));
        var slug = (await _service.SetShareLinkAsync(set.SetId, true, false, Owner, false)).Value;
        await AddProgressAsync(Owner, set.Cards.First().CardId, "Mastered");

        var asOwner = (await _service.GetSetDetailAsync(set.SetId, Owner, false))!;
        Assert.True(asOwner.IsOwner && asOwner.CanEdit && asOwner.CanManage && asOwner.IsStudying);
        Assert.Equal(slug, asOwner.ShareSlug);
        Assert.Equal(50, asOwner.MasteredPercent);
        Assert.Equal(2, asOwner.WordCount);

        var asViewer = (await _service.GetSetDetailAsync(set.SetId, Outsider, false))!;
        Assert.False(asViewer.CanEdit || asViewer.CanManage || asViewer.IsStudying || asViewer.IsSubscribed);
        Assert.Null(asViewer.ShareSlug);
        Assert.Empty(asViewer.MoveTargets);
        Assert.Equal("User 1", asViewer.OwnerName);

        var priv = await AddSetAsync(Owner, "Hidden", "Private", null);
        Assert.Null(await _service.GetSetDetailAsync(priv.SetId, Outsider, false));
        Assert.Null(await _service.GetSetInfoFormAsync(set.SetId, Outsider, false));
        Assert.NotNull(await _service.GetSetInfoFormAsync(set.SetId, Owner, false));
    }

    [Fact]
    public async Task CardDetail_ShowsLegacyExampleAsOne_RelatedLinks_AndHidesNotesFromViewers()
    {
        var set = await AddSetAsync(Owner, "Words", "Public", null);
        _db.Cards.AddRange(
            new Card { SetId = set.SetId, Term = "big", Definition = "to", Example = "A big dog.", ExampleTranslation = "Con chó to.", PersonalNote = "note", DetailsJson = "{\"synonyms\":[\"large\",\"huge\"]}" },
            new Card { SetId = set.SetId, Term = "Large", Definition = "lớn" });
        await _db.SaveChangesAsync();
        var big = await _db.Cards.AsNoTracking().SingleAsync(c => c.Term == "big");

        var owner = (await _service.GetCardDetailAsync(big.CardId, Owner, false))!;
        Assert.Equal("note", owner.PersonalNote);
        Assert.Equal("A big dog.", Assert.Single(owner.Details.Examples).Text);
        Assert.NotNull(owner.RelatedSynonyms.Single(s => s.Text == "large").CardId);
        Assert.Null(owner.RelatedSynonyms.Single(s => s.Text == "huge").CardId);
        Assert.True(owner.CanEdit);
        Assert.False(owner.Progress.HasReviewed);

        var viewer = (await _service.GetCardDetailAsync(big.CardId, Outsider, false))!;
        Assert.Null(viewer.PersonalNote);
        Assert.False(viewer.CanEdit);

        var priv = await AddSetAsync(Owner, "Hidden", "Private", null, ("q", "r"));
        Assert.Null(await _service.GetCardDetailAsync(priv.Cards.Single().CardId, Outsider, false));
        Assert.Null(await _service.GetCardFormAsync(big.CardId, Outsider, false));
    }

    [Fact]
    public async Task FindDuplicates_LooksOnlyInOwnedAndFollowedSets()
    {
        var mine = await AddSetAsync(Owner, "Mine", "Private", null, ("Apple", "táo"));
        var followed = await AddSetAsync(Outsider, "Followed", "Public", null, ("apple", "quả táo"));
        await AddSetAsync(Outsider, "Not followed", "Public", null, ("APPLE", "x"));
        _db.SetSubscriptions.Add(new SetSubscription { SetId = followed.SetId, UserId = Owner, SubscribedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var hits = await _service.FindDuplicatesAsync(" apple ", Owner, null);
        Assert.Equal(2, hits.Count);
        Assert.DoesNotContain(hits, h => h.SetName == "Not followed");

        var excluding = await _service.FindDuplicatesAsync("apple", Owner, mine.Cards.Single().CardId);
        Assert.Single(excluding);
        Assert.Empty(await _service.FindDuplicatesAsync("   ", Owner, null));
    }

    // ------------------------------------------------------------------ doubles

    private static ByteArrayContent Bytes(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return content;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var response = Respond(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
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
