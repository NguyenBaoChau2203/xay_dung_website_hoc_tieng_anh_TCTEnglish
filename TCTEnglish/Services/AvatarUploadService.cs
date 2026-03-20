using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TCTVocabulary.Services
{
    public class AvatarUploadService : IAvatarUploadService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<AvatarUploadService> _logger;

        public AvatarUploadService(
            IFileStorageService fileStorageService,
            ILogger<AvatarUploadService> logger)
        {
            _logger = logger;
            _fileStorageService = fileStorageService;
        }

        public async Task<string?> UploadAvatarAsync(IFormFile avatarFile, string? oldAvatarUrl)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return oldAvatarUrl;

            var avatarUrl = await _fileStorageService.SaveImageAsync(avatarFile, ImageUploadPolicies.Avatar);

            _logger.LogInformation(
                "Avatar uploaded to {publicUrlPrefix}, sizeBytes {fileSizeBytes}",
                ImageUploadPolicies.Avatar.PublicUrlPrefix,
                avatarFile.Length);

            // Delete old avatar if it's a local file
            _fileStorageService.TryDeleteLocalFile(oldAvatarUrl, ImageUploadPolicies.Avatar.PublicUrlPrefix);

            return avatarUrl;
        }
    }
}
