using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TCTVocabulary.Models;
using TCTVocabulary.Workers;

namespace TCTEnglish.Tests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _sqliteConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish")));
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureServices(services =>
        {
            var dataProtectionDirectory = new DirectoryInfo(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".appdata", "DataProtection-Keys"));
            dataProtectionDirectory.Create();

            services.RemoveAll<DbContextOptions<DbflashcardContext>>();
            services.RemoveAll<DbflashcardContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<DbflashcardContext>>();

            _sqliteConnection?.Dispose();
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            _sqliteConnection.Open();

            var hostedServiceDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(AutoUnlockWorker))
                .ToList();

            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DbflashcardContext>(options =>
            {
                options.UseSqlite(_sqliteConnection);
                options.ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>();
            });

            services.AddDataProtection()
                .PersistKeysToFileSystem(dataProtectionDirectory)
                .SetApplicationName("TCTEnglish.Tests");

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        await SeedAsync(context);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        _sqliteConnection?.Dispose();
        _sqliteConnection = null;
    }

    private static async Task SeedAsync(DbflashcardContext context)
    {
        var standardUser = new User
        {
            UserId = TestDataIds.UserId,
            Email = "student@test.local",
            PasswordHash = "hash",
            FullName = "Sprint One Student",
            Role = Roles.Standard,
            Status = UserStatus.Online,
            CreatedAt = DateTime.UtcNow
        };

        var adminUser = new User
        {
            UserId = TestDataIds.AdminUserId,
            Email = "admin@test.local",
            PasswordHash = "hash",
            FullName = "Sprint One Admin",
            Role = Roles.Admin,
            Status = UserStatus.Online,
            CreatedAt = DateTime.UtcNow
        };

        var outsiderUser = new User
        {
            UserId = TestDataIds.OutsiderUserId,
            Email = "outsider@test.local",
            PasswordHash = "hash",
            FullName = "Sprint One Outsider",
            Role = Roles.Standard,
            Status = UserStatus.Online,
            CreatedAt = DateTime.UtcNow
        };

        var memberUser = new User
        {
            UserId = TestDataIds.MemberUserId,
            Email = "member@test.local",
            PasswordHash = "hash",
            FullName = "Sprint Two Member",
            Role = Roles.Standard,
            Status = UserStatus.Online,
            CreatedAt = DateTime.UtcNow
        };

        var systemUser = new User
        {
            Email = "system@tct.local",
            PasswordHash = "hash",
            FullName = "System User",
            Role = "System",
            Status = UserStatus.Offline,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(standardUser, adminUser, outsiderUser, memberUser, systemUser);
        await context.SaveChangesAsync();

        var userFolder = new Folder
        {
            FolderId = TestDataIds.UserFolderId,
            UserId = standardUser.UserId,
            FolderName = "Sprint One Folder"
        };

        var systemFolder = new Folder
        {
            FolderId = TestDataIds.SystemFolderId,
            UserId = systemUser.UserId,
            FolderName = "System Sprint Folder"
        };

        var deletableUserFolder = new Folder
        {
            FolderId = TestDataIds.DeletableUserFolderId,
            UserId = standardUser.UserId,
            FolderName = "Sprint One Delete Folder"
        };

        context.Folders.AddRange(userFolder, systemFolder, deletableUserFolder);
        await context.SaveChangesAsync();

        var userSet = new Set
        {
            SetId = TestDataIds.UserSetId,
            OwnerId = standardUser.UserId,
            FolderId = userFolder.FolderId,
            SetName = "Sprint One User Set",
            Description = "User-owned set",
            CreatedAt = DateTime.UtcNow
        };

        var systemSet = new Set
        {
            SetId = TestDataIds.SystemSetId,
            OwnerId = systemUser.UserId,
            FolderId = systemFolder.FolderId,
            SetName = "Sprint One System Set",
            Description = "System-owned set",
            CreatedAt = DateTime.UtcNow
        };

        context.Sets.AddRange(userSet, systemSet);
        await context.SaveChangesAsync();

        context.Cards.AddRange(
            new Card
            {
                CardId = TestDataIds.UserCardId,
                SetId = userSet.SetId,
                Term = "forecast",
                Definition = "predict"
            },
            new Card
            {
                CardId = TestDataIds.SystemCardId,
                SetId = systemSet.SetId,
                Term = "invoice",
                Definition = "hoa don"
            });

        context.Classes.Add(new Class
        {
            ClassId = TestDataIds.ClassId,
            ClassName = "Sprint One Class",
            OwnerId = standardUser.UserId,
            HasPassword = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
            Description = "Protected class",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        context.ClassMembers.Add(new ClassMember
        {
            ClassId = TestDataIds.ClassId,
            UserId = memberUser.UserId
        });

        context.ClassFolders.Add(new ClassFolder
        {
            ClassId = TestDataIds.ClassId,
            FolderId = userFolder.FolderId,
            AddedByUserId = standardUser.UserId,
            AddedAt = DateTime.UtcNow
        });

        context.ClassMessages.Add(new ClassMessage
        {
            ClassId = TestDataIds.ClassId,
            UserId = adminUser.UserId,
            Content = "PRIVATE-CHAT-501",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}
