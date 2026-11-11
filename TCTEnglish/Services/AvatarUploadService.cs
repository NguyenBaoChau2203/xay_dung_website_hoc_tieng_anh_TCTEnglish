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

            return await StageAvatarAsync(avatarFile);
        }

        public async Task<string> StageAvatarAsync(IFormFile avatarFile, CancellationToken cancellationToken = default)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                throw new InvalidOperationException("Tệp tải lên không hợp lệ.");
            }

            var avatarUrl = await _fileStorageService.SaveImageAsync(avatarFile, ImageUploadPolicies.Avatar, cancellationToken);
            _logger.LogInformation(
                "Avatar staged under {publicUrlPrefix}, sizeBytes {fileSizeBytes}",
                ImageUploadPolicies.Avatar.PublicUrlPrefix,
                avatarFile.Length);
            return avatarUrl;
        }

        public bool DeleteOwnedAvatar(string? publicUrl)
        {
            return _fileStorageService.TryDeleteOwnedLocalFile(publicUrl, ImageUploadPolicies.Avatar.PublicUrlPrefix);
        }
    }
}
