using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TCTVocabulary.Services
{
    public interface IAvatarUploadService
    {
        Task<string?> UploadAvatarAsync(IFormFile avatarFile, string? oldAvatarUrl);
    }
}
