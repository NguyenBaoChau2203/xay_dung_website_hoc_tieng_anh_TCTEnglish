namespace TCTVocabulary.Services
{
    public sealed class FileUploadOptions
    {
        public required string RelativeDirectory { get; init; }
        public required string PublicUrlPrefix { get; init; }
        public required long MaxFileSizeInBytes { get; init; }
        public required IReadOnlyCollection<string> AllowedExtensions { get; init; }
        public required IReadOnlyCollection<string> AllowedMimeTypes { get; init; }
        public bool ValidateFileSignature { get; init; } = true;
    }
}
