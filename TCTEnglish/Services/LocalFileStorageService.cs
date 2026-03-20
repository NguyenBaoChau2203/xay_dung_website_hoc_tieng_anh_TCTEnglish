using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace TCTVocabulary.Services
{
    public sealed class LocalFileStorageService : IFileStorageService
    {
        private static readonly Dictionary<string, byte[][]> SignatureMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = [
                [0xFF, 0xD8, 0xFF]
            ],
            [".jpeg"] = [
                [0xFF, 0xD8, 0xFF]
            ],
            [".png"] = [
                [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
            ],
            [".gif"] = [
                [0x47, 0x49, 0x46, 0x38, 0x37, 0x61],
                [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]
            ],
            [".webp"] = [
                [0x52, 0x49, 0x46, 0x46]
            ]
        };

        private readonly string _webRootPath;
        private readonly ILogger<LocalFileStorageService>? _logger;

        public LocalFileStorageService(IWebHostEnvironment environment)
            : this(environment.WebRootPath)
        {
        }

        public LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger)
            : this(environment.WebRootPath, logger)
        {
        }

        public LocalFileStorageService(string webRootPath)
            : this(webRootPath, null)
        {
        }

        public LocalFileStorageService(string webRootPath, ILogger<LocalFileStorageService>? logger)
        {
            _webRootPath = webRootPath;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile file, FileUploadOptions options, CancellationToken cancellationToken = default)
        {
            Validate(file, options);

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            var fileBytes = memoryStream.ToArray();

            if (options.ValidateFileSignature)
            {
                ValidateSignature(fileBytes, Path.GetExtension(file.FileName));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadsFolder = Path.Combine(_webRootPath, options.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);

            _logger?.LogInformation(
                "File uploaded to {publicUrlPrefix} with extension {extension}, sizeBytes {fileSizeBytes}",
                options.PublicUrlPrefix,
                extension,
                file.Length);

            return options.PublicUrlPrefix.TrimEnd('/') + "/" + fileName;
        }

        public void TryDeleteLocalFile(string? publicUrl, string expectedPublicPrefix)
        {
            if (string.IsNullOrWhiteSpace(publicUrl)
                || !publicUrl.StartsWith(expectedPublicPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativePath = publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_webRootPath, relativePath);

            if (!File.Exists(fullPath))
            {
                _logger?.LogDebug("Skip delete because local file does not exist for prefix {publicUrlPrefix}", expectedPublicPrefix);
                return;
            }

            try
            {
                File.Delete(fullPath);
                _logger?.LogInformation("Deleted local file for prefix {publicUrlPrefix}", expectedPublicPrefix);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete local file for prefix {publicUrlPrefix}", expectedPublicPrefix);
            }
        }

        private static void Validate(IFormFile file, FileUploadOptions options)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("Tệp tải lên không hợp lệ.");
            }

            if (file.Length > options.MaxFileSizeInBytes)
            {
                throw new InvalidOperationException("Tệp tải lên vượt quá kích thước cho phép.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension)
                || !options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Định dạng tệp không được hỗ trợ.");
            }

            if (string.IsNullOrWhiteSpace(file.ContentType)
                || !options.AllowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Loại MIME của tệp không hợp lệ.");
            }
        }

        private static void ValidateSignature(IReadOnlyList<byte> fileBytes, string extension)
        {
            if (!SignatureMap.TryGetValue(extension, out var knownSignatures))
            {
                return;
            }

            if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                if (fileBytes.Count < 12)
                {
                    throw new InvalidOperationException("Chữ ký tệp không hợp lệ.");
                }

                var isWebP = fileBytes[0] == 0x52
                    && fileBytes[1] == 0x49
                    && fileBytes[2] == 0x46
                    && fileBytes[3] == 0x46
                    && fileBytes[8] == 0x57
                    && fileBytes[9] == 0x45
                    && fileBytes[10] == 0x42
                    && fileBytes[11] == 0x50;

                if (!isWebP)
                {
                    throw new InvalidOperationException("Chữ ký tệp không hợp lệ.");
                }

                return;
            }

            foreach (var signature in knownSignatures)
            {
                if (fileBytes.Count < signature.Length)
                {
                    continue;
                }

                var matched = true;
                for (var i = 0; i < signature.Length; i++)
                {
                    if (fileBytes[i] == signature[i])
                    {
                        continue;
                    }

                    matched = false;
                    break;
                }

                if (matched)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Chữ ký tệp không hợp lệ.");
        }
    }
}
