namespace TCTVocabulary.Services
{
    public static class ImageUploadPolicies
    {
        public static readonly FileUploadOptions Avatar = new()
        {
            RelativeDirectory = "images/avatars",
            PublicUrlPrefix = "/images/avatars/",
            MaxFileSizeInBytes = 2 * 1024 * 1024,
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png" },
            AllowedMimeTypes = new[] { "image/jpeg", "image/png" }
        };

        public static readonly FileUploadOptions ChatImage = new()
        {
            RelativeDirectory = "uploads/chat",
            PublicUrlPrefix = "/uploads/chat/",
            MaxFileSizeInBytes = 5 * 1024 * 1024,
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
            AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" }
        };

        public static readonly FileUploadOptions ClassImage = new()
        {
            RelativeDirectory = "images/classes",
            PublicUrlPrefix = "/images/classes/",
            MaxFileSizeInBytes = 5 * 1024 * 1024,
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
            AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" }
        };

        public static readonly FileUploadOptions CardImage = new()
        {
            RelativeDirectory = "images/cards",
            PublicUrlPrefix = "/images/cards/",
            MaxFileSizeInBytes = 5 * 1024 * 1024,
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
            AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" }
        };
    }
}
