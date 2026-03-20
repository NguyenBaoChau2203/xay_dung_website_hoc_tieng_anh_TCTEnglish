using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TCTVocabulary.Areas.Admin.Controllers;
using TCTVocabulary.Controllers;
using TCTVocabulary.Hubs;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class Sprint4SmokeTests
{
    [Fact]
    public void ProjectStructure_NormalizesFeatureViewModelsIntoSingleFolder()
    {
        var workspaceRoot = GetWorkspaceRoot();
        var legacyFolder = Path.Combine(workspaceRoot, "TCTEnglish", "ViewModel");
        var normalizedFolder = Path.Combine(workspaceRoot, "TCTEnglish", "ViewModels");

        var legacyFeatureViewModels = Directory.Exists(legacyFolder)
            ? Directory.GetFiles(legacyFolder, "*.cs", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        Assert.Empty(legacyFeatureViewModels);
        Assert.True(Directory.Exists(normalizedFolder));
        Assert.True(File.Exists(Path.Combine(normalizedFolder, "ClassDetailViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(normalizedFolder, "StudyViewModel.cs")));
        Assert.True(File.Exists(Path.Combine(normalizedFolder, "VocabularyPageViewModels.cs")));
        Assert.False(File.Exists(Path.Combine(workspaceRoot, "TCTEnglish", "Models", "UpdateProfileViewModel.cs")));
    }

    [Fact]
    public async Task LocalFileStorageService_SavesAndDeletesFilesUnderConfiguredPrefix()
    {
        var webRootPath = Path.Combine(Path.GetTempPath(), "tctenglish-sprint4", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRootPath);

        try
        {
            var service = new LocalFileStorageService(webRootPath);
            await using var stream = new MemoryStream(GetPngBytes());
            IFormFile formFile = new FormFile(stream, 0, stream.Length, "image", "chat.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            var publicUrl = await service.SaveImageAsync(formFile, ImageUploadPolicies.ChatImage);
            var relativePath = publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(webRootPath, relativePath);

            Assert.StartsWith("/uploads/chat/", publicUrl, StringComparison.Ordinal);
            Assert.True(File.Exists(absolutePath));

            service.TryDeleteLocalFile(publicUrl, ImageUploadPolicies.ChatImage.PublicUrlPrefix);

            Assert.False(File.Exists(absolutePath));
        }
        finally
        {
            if (Directory.Exists(webRootPath))
            {
                Directory.Delete(webRootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalFileStorageService_RejectsFilesWithInvalidImageSignature()
    {
        var webRootPath = Path.Combine(Path.GetTempPath(), "tctenglish-sprint4", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRootPath);

        try
        {
            var service = new LocalFileStorageService(webRootPath);
            await using var stream = new MemoryStream("not-an-image"u8.ToArray());
            IFormFile formFile = new FormFile(stream, 0, stream.Length, "image", "chat.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveImageAsync(formFile, ImageUploadPolicies.ChatImage));
        }
        finally
        {
            if (Directory.Exists(webRootPath))
            {
                Directory.Delete(webRootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void CriticalFlows_RequireConstructorInjectedLogger()
    {
        AssertRequiresLogger(typeof(AccountController));
        AssertRequiresLogger(typeof(ChatController));
        AssertRequiresLogger(typeof(ClassController));
        AssertRequiresLogger(typeof(LearningApiController));
        AssertRequiresLogger(typeof(HomeController));
        AssertRequiresLogger(typeof(ClassChatHub));
        AssertRequiresLogger(typeof(SpeakingVideoManagementController));
        AssertRequiresLogger(typeof(AvatarUploadService));
        AssertRequiresLogger(typeof(ClassService));
        AssertRequiresLogger(typeof(StreakService));
    }

    [Fact]
    public void ClassChatHub_RequiresConstructorInjectedClassService()
    {
        AssertRequiresNonOptionalService(typeof(ClassChatHub), typeof(IClassService));
    }

    [Fact]
    public void ApplicationCode_DoesNotUseConsoleWriteLine()
    {
        var projectRoot = Path.Combine(GetWorkspaceRoot(), "TCTEnglish");
        var sourceFiles = Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var offenders = sourceFiles
            .Where(path => File.ReadAllText(path).Contains("Console.WriteLine", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    private static void AssertRequiresLogger(Type targetType)
    {
        var loggerParameter = targetType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .SingleOrDefault(parameter =>
                parameter.ParameterType.IsGenericType
                && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));

        Assert.NotNull(loggerParameter);
        Assert.False(loggerParameter!.IsOptional);
        Assert.False(loggerParameter.HasDefaultValue);
    }

    private static void AssertRequiresNonOptionalService(Type targetType, Type serviceType)
    {
        var serviceParameter = targetType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .SingleOrDefault(parameter => parameter.ParameterType == serviceType);

        Assert.NotNull(serviceParameter);
        Assert.False(serviceParameter!.IsOptional);
        Assert.False(serviceParameter.HasDefaultValue);
    }

    private static string GetWorkspaceRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static byte[] GetPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D,
            0x18, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        ];
    }
}
