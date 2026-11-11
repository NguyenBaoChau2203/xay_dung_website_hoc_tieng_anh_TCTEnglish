using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TCTVocabulary.Services
{
    public interface IAvatarUploadService
    {
        Task<string?> UploadAvatarAsync(IFormFile avatarFile, string? oldAvatarUrl);

        /// <summary>Stores a new avatar and leaves the previous file in place.</summary>
        Task<string> StageAvatarAsync(IFormFile avatarFile, CancellationToken cancellationToken = default);

        /// <summary>Deletes a previously staged or replaced avatar when it is an owned local file.</summary>
        bool DeleteOwnedAvatar(string? publicUrl);
    }
}
