using Microsoft.AspNetCore.Http;

namespace TCTVocabulary.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveImageAsync(IFormFile file, FileUploadOptions options, CancellationToken cancellationToken = default);
        void TryDeleteLocalFile(string? publicUrl, string expectedPublicPrefix);

        /// <summary>
        /// Deletes a file only when the public URL is a relative path inside the expected prefix
        /// and the resolved path stays inside that directory.
        /// </summary>
        bool TryDeleteOwnedLocalFile(string? publicUrl, string expectedPublicPrefix);
    }
}
