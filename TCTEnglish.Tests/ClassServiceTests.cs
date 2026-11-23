using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class ClassServiceTests : IAsyncLifetime
{
    private const int OwnerId = 1;
    private const int MemberId = 2;
    private const int OutsiderId = 3;
    private const int AssistantId = 4;

    private SqliteConnection _connection = null!;
    private DbflashcardContext _db = null!;
    private ClassService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        foreach (var (id, name) in new[]
                 {
                     (OwnerId, "Owner"),
                     (MemberId, "Member"),
                     (OutsiderId, "Outsider"),
                     (AssistantId, "Assistant")
                 })
        {
            _db.Users.Add(new User
            {
                UserId = id,
                Email = $"u{id}@test.local",
                PasswordHash = "x",
                FullName = name,
                Role = "Standard",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        var files = new Mock<IFileStorageService>();
        _service = new ClassService(_db, files.Object, NullLogger<ClassService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Class> SeedClassAsync(
        bool requiresApproval = false,
        bool isChatLocked = false,
        bool allowMemberToPost = false,
        bool isPublic = true,
        string? joinCode = null)
    {
        var cls = new Class
        {
            ClassName = "Test Class",
            OwnerId = OwnerId,
            RequiresApproval = requiresApproval,
            IsChatLocked = isChatLocked,
            AllowMemberToPost = allowMemberToPost,
            IsPublic = isPublic,
            JoinCode = joinCode ?? ClassJoinCode.Generate(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync();

        _db.ClassMembers.Add(new ClassMember
        {
            ClassId = cls.ClassId,
            UserId = OwnerId,
            Role = ClassRole.Owner,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return cls;
    }

    private async Task AddMemberAsync(int classId, int userId, ClassRole role, bool muted = false)
    {
        _db.ClassMembers.Add(new ClassMember
        {
            ClassId = classId,
            UserId = userId,
            Role = role,
            IsMuted = muted,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task JoinClass_WhenRequiresApproval_ReturnsPendingSuccess()
    {
        var cls = await SeedClassAsync(requiresApproval: true);

        var result = await _service.JoinClassAsync(cls.ClassId, null, OutsiderId);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(JoinClassOutcome.Pending, result.Value!.Outcome);
        Assert.True(await _db.ClassJoinRequests.AnyAsync(r =>
            r.ClassId == cls.ClassId &&
            r.UserId == OutsiderId &&
            r.Status == JoinRequestStatus.Pending));
        Assert.False(await _db.ClassMembers.AnyAsync(m => m.ClassId == cls.ClassId && m.UserId == OutsiderId));
    }

    [Fact]
    public async Task JoinClass_WhenOpen_ReturnsJoined()
    {
        var cls = await SeedClassAsync();

        var result = await _service.JoinClassAsync(cls.ClassId, null, OutsiderId);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(JoinClassOutcome.Joined, result.Value!.Outcome);
        Assert.True(await _db.ClassMembers.AnyAsync(m => m.ClassId == cls.ClassId && m.UserId == OutsiderId));
    }

    [Fact]
    public async Task SearchClasses_SuggestsPublicOnly_ExcludesPrivateAndJoined()
    {
        var publicOpen = await SeedClassAsync(joinCode: "PUBLIC01");
        publicOpen.ClassName = "Public Alpha";
        var privateCls = await SeedClassAsync(isPublic: false, joinCode: "PRIV0001");
        privateCls.ClassName = "Secret Beta";
        await _db.SaveChangesAsync();

        var ownedByOutsider = new Class
        {
            ClassName = "Outsider Own",
            OwnerId = OutsiderId,
            IsPublic = true,
            JoinCode = "OUTSIDER",
            CreatedAt = DateTime.UtcNow
        };
        _db.Classes.Add(ownedByOutsider);
        await _db.SaveChangesAsync();

        var suggest = await _service.SearchClassesAsync(null, OutsiderId, Roles.Standard);
        Assert.Contains(suggest, c => c.ClassId == publicOpen.ClassId);
        Assert.DoesNotContain(suggest, c => c.ClassId == privateCls.ClassId);
        Assert.DoesNotContain(suggest, c => c.ClassId == ownedByOutsider.ClassId);

        var named = await _service.SearchClassesAsync("Alpha", OutsiderId, Roles.Standard);
        Assert.Single(named);
        Assert.Equal(publicOpen.ClassId, named[0].ClassId);
    }

    [Fact]
    public async Task BrowseClasses_TeacherSeesOwnPrivateAndOthersPublic_NotOthersPrivate()
    {
        var publicOpen = await SeedClassAsync(joinCode: "PUBTEACH");
        publicOpen.ClassName = "Public Teach";
        var othersPrivate = await SeedClassAsync(isPublic: false, joinCode: "PRIVOTHR");
        othersPrivate.ClassName = "Other Private";

        var ownPrivate = new Class
        {
            ClassName = "My Private",
            OwnerId = OutsiderId,
            IsPublic = false,
            JoinCode = "MYPRIV01",
            CreatedAt = DateTime.UtcNow
        };
        _db.Classes.Add(ownPrivate);
        await _db.SaveChangesAsync();

        var browse = await _service.BrowseClassesAsync(null, OutsiderId, Roles.Teacher);
        Assert.Contains(browse, c => c.ClassId == publicOpen.ClassId);
        Assert.Contains(browse, c => c.ClassId == ownPrivate.ClassId);
        Assert.DoesNotContain(browse, c => c.ClassId == othersPrivate.ClassId);
    }

    [Fact]
    public async Task BrowseClasses_AdminSeesAllPublicAndPrivate()
    {
        var publicOpen = await SeedClassAsync(joinCode: "PUBADMIN");
        var privateCls = await SeedClassAsync(isPublic: false, joinCode: "PRIVADMN");

        var browse = await _service.BrowseClassesAsync(null, OutsiderId, Roles.Admin);
        Assert.Contains(browse, c => c.ClassId == publicOpen.ClassId);
        Assert.Contains(browse, c => c.ClassId == privateCls.ClassId);
    }

    [Fact]
    public async Task JoinClassByCode_RespectsPasswordAndApproval()
    {
        var cls = await SeedClassAsync(requiresApproval: true, joinCode: "JOINCODE");
        cls.HasPassword = true;
        cls.PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret");
        await _db.SaveChangesAsync();

        var needsPassword = await _service.JoinClassByCodeAsync("JOINCODE", null, OutsiderId);
        Assert.Equal(JoinClassOutcome.NeedsPassword, needsPassword.Value!.Outcome);

        var wrong = await _service.JoinClassByCodeAsync("JOINCODE", "nope", OutsiderId);
        Assert.Equal(OperationStatus.Invalid, wrong.Status);

        var pending = await _service.JoinClassByCodeAsync("joincode", "secret", OutsiderId);
        Assert.Equal(JoinClassOutcome.Pending, pending.Value!.Outcome);
        Assert.Equal(cls.ClassId, pending.Value.ClassId);
    }

    [Fact]
    public async Task CreateClass_AssignsJoinCodeAndDefaultsPublic()
    {
        var id = await _service.CreateClassAsync(new CreateClassViewModel
        {
            ClassName = "Created",
            IsPublic = true
        }, OwnerId, Roles.Teacher);

        var cls = await _db.Classes.SingleAsync(c => c.ClassId == id);
        Assert.True(cls.IsPublic);
        Assert.True(ClassJoinCode.IsValidFormat(cls.JoinCode));
    }

    [Fact]
    public async Task CreateClass_RejectsLearnerRoles()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateClassAsync(new CreateClassViewModel
            {
                ClassName = "Nope",
                IsPublic = true
            }, OutsiderId, Roles.Standard));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateClassAsync(new CreateClassViewModel
            {
                ClassName = "Nope Premium",
                IsPublic = true
            }, OutsiderId, Roles.Premium));
    }

    [Fact]
    public void Roles_NormalizeAndCanCreateClass_Teacher()
    {
        Assert.Equal(Roles.Teacher, Roles.Normalize("teacher"));
        Assert.True(Roles.CanCreateClass(Roles.Teacher));
        Assert.True(Roles.CanCreateClass(Roles.Admin));
        Assert.False(Roles.CanCreateClass(Roles.Standard));
        Assert.False(Roles.CanCreateClass(Roles.Premium));
        Assert.True(Roles.IsProtectedFromBillingRoleSync(Roles.Teacher));
        Assert.True(Roles.IsLearner(Roles.Premium));
    }

    [Fact]
    public async Task GetClassPage_CreatorRole_TitlesAndOwnedSection()
    {
        await SeedClassAsync(joinCode: "OWNPAGE1");
        var page = await _service.GetClassPageAsync(OwnerId, Roles.Teacher);
        Assert.True(page.CanCreateClass);
        Assert.Equal("Lớp tôi tạo", page.PageTitle);
        Assert.Single(page.ManagedClasses);

        var learnerPage = await _service.GetClassPageAsync(OutsiderId, Roles.Standard);
        Assert.False(learnerPage.CanCreateClass);
        Assert.Equal("Lớp học của tôi", learnerPage.PageTitle);
    }

    [Fact]
    public async Task RegenerateJoinCode_ChangesCode()
    {
        var cls = await SeedClassAsync(joinCode: "OLDCODE1");
        var old = cls.JoinCode;

        var result = await _service.RegenerateJoinCodeAsync(cls.ClassId, OwnerId, isAdmin: false);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotEqual(old, result.Value);
        Assert.True(ClassJoinCode.IsValidFormat(result.Value));
    }

    [Fact]
    public async Task PrivateClass_RequiresAnInviteForOutsiders()
    {
        var cls = await SeedClassAsync(isPublic: false, joinCode: "PRIVATE2");

        Assert.Null(await _service.GetClassDetailAsync(cls.ClassId, OutsiderId, isAdmin: false));
        Assert.Equal(OperationStatus.NotFound, (await _service.JoinClassAsync(cls.ClassId, null, OutsiderId)).Status);
        Assert.Equal(JoinClassOutcome.Joined, (await _service.JoinClassByCodeAsync(cls.JoinCode, null, OutsiderId)).Value!.Outcome);
        Assert.NotNull(await _service.GetClassDetailAsync(cls.ClassId, OutsiderId, isAdmin: false));
    }

    [Fact]
    public async Task UpdateClass_RotatesTheCodeWhenMadePrivateAndPreservesOmittedVisibility()
    {
        var cls = await SeedClassAsync(isPublic: true, joinCode: "PUBLIC22");
        var oldCode = cls.JoinCode;

        Assert.Equal(OperationStatus.Success, (await _service.UpdateClassAsync(
            cls.ClassId, cls.ClassName, null, false, false, true, false, OwnerId, false)).Status);
        var privateCode = cls.JoinCode;
        Assert.NotEqual(oldCode, privateCode);
        Assert.False(cls.IsPublic);

        Assert.Equal(OperationStatus.Success, (await _service.UpdateClassAsync(
            cls.ClassId, cls.ClassName, null, false, false, true, null, OwnerId, false)).Status);
        Assert.False(cls.IsPublic);
        Assert.Equal(privateCode, cls.JoinCode);
    }

    [Fact]
    public async Task GetClassDetail_HidesJoinCodeFromNonManagers()
    {
        var cls = await SeedClassAsync(joinCode: "HIDECODE");
        await AddMemberAsync(cls.ClassId, MemberId, ClassRole.Member);

        var memberVm = await _service.GetClassDetailAsync(cls.ClassId, MemberId, isAdmin: false);
        Assert.Null(memberVm!.Class.JoinCode);

        var ownerVm = await _service.GetClassDetailAsync(cls.ClassId, OwnerId, isAdmin: false);
        Assert.Equal("HIDECODE", ownerVm!.Class.JoinCode);
    }

    [Fact]
    public async Task EnsureCanSendClassChat_BlocksMutedMember()
    {
        var cls = await SeedClassAsync();
        await AddMemberAsync(cls.ClassId, MemberId, ClassRole.Member, muted: true);

        var result = await _service.EnsureCanSendClassChatAsync(cls.ClassId, MemberId, isAdmin: false);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Contains("mute", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureCanSendClassChat_BlocksMemberWhenChatLocked()
    {
        var cls = await SeedClassAsync(isChatLocked: true);
        await AddMemberAsync(cls.ClassId, MemberId, ClassRole.Member);

        var result = await _service.EnsureCanSendClassChatAsync(cls.ClassId, MemberId, isAdmin: false);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Contains("khóa", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureCanSendClassChat_AllowsOwnerWhenChatLocked()
    {
        var cls = await SeedClassAsync(isChatLocked: true);

        var result = await _service.EnsureCanSendClassChatAsync(cls.ClassId, OwnerId, isAdmin: false);

        Assert.Equal(OperationStatus.Success, result.Status);
    }

    [Fact]
    public async Task AddFolderToClass_BlocksMemberWhenAllowMemberToPostFalse()
    {
        var cls = await SeedClassAsync(allowMemberToPost: false);
        await AddMemberAsync(cls.ClassId, MemberId, ClassRole.Member);

        var folder = new Folder { FolderName = "Mine", UserId = MemberId };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();

        var result = await _service.AddFolderToClassAsync(cls.ClassId, folder.FolderId, MemberId, isAdmin: false);

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.False(await _db.ClassFolders.AnyAsync(cf => cf.ClassId == cls.ClassId && cf.FolderId == folder.FolderId));
    }

    [Fact]
    public async Task AddFolderToClass_AllowsMemberWhenAllowMemberToPostTrue()
    {
        var cls = await SeedClassAsync(allowMemberToPost: true);
        await AddMemberAsync(cls.ClassId, MemberId, ClassRole.Member);

        var folder = new Folder { FolderName = "Mine", UserId = MemberId };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();

        var result = await _service.AddFolderToClassAsync(cls.ClassId, folder.FolderId, MemberId, isAdmin: false);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(await _db.ClassFolders.AnyAsync(cf => cf.ClassId == cls.ClassId && cf.FolderId == folder.FolderId));
    }

    [Fact]
    public async Task GetClassDetail_SetsCanSendChatFalseWhenMuted()
    {
        var cls = await SeedClassAsync();
        await AddMemberAsync(cls.ClassId, MemberId, ClassRole.Member, muted: true);

        var vm = await _service.GetClassDetailAsync(cls.ClassId, MemberId, isAdmin: false);

        Assert.NotNull(vm);
        Assert.True(vm!.IsMutedForCurrentUser);
        Assert.False(vm.CanSendChat);
    }

    [Fact]
    public async Task GetClassMessages_PagesOlderMessages()
    {
        var cls = await SeedClassAsync();
        for (var i = 0; i < 5; i++)
        {
            _db.ClassMessages.Add(new ClassMessage
            {
                ClassId = cls.ClassId,
                UserId = OwnerId,
                Content = $"m{i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        await _db.SaveChangesAsync();

        var first = await _service.GetClassMessagesAsync(cls.ClassId, OwnerId, false, null, 2);
        Assert.Equal(2, first.Messages.Count);
        Assert.True(first.HasMore);

        var oldestOnPage = first.Messages[0].MessageId;
        var older = await _service.GetClassMessagesAsync(cls.ClassId, OwnerId, false, oldestOnPage, 2);
        Assert.Equal(2, older.Messages.Count);
        Assert.True(older.Messages.All(m => m.MessageId < oldestOnPage));
    }
}
