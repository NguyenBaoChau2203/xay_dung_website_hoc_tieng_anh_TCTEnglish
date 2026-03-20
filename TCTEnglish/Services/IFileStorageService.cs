using Microsoft.AspNetCore.Http;

namespace TCTVocabulary.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveImageAsync(IFormFile file, FileUploadOptions options, CancellationToken cancellationToken = default);
        void TryDeleteLocalFile(string? publicUrl, string expectedPublicPrefix);
    }
}
