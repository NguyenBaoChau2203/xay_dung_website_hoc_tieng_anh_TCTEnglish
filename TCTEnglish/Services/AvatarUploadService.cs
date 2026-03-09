using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TCTVocabulary.Services
{
    public class AvatarUploadService : IAvatarUploadService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AvatarUploadService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string?> UploadAvatarAsync(IFormFile avatarFile, string? oldAvatarUrl)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return oldAvatarUrl;

            // Validate file size (2MB max)
            if (avatarFile.Length > 2 * 1024 * 1024)
            {
                throw new InvalidOperationException("File ảnh không được vượt quá 2MB.");
            }

            // Validate extension
            var ext = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            {
                throw new InvalidOperationException("Chỉ chấp nhận định dạng ảnh .jpg, .jpeg, .png");
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + avatarFile.FileName;
            string filePath = "";
            
            using (var ms = new MemoryStream())
            {
                await avatarFile.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                bool isImage = false;
                
                if (fileBytes.Length >= 3 && fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
                    isImage = true;
                else if (fileBytes.Length >= 8 && fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47 &&
                         fileBytes[4] == 0x0D && fileBytes[5] == 0x0A && fileBytes[6] == 0x1A && fileBytes[7] == 0x0A)
                    isImage = true;

                if (!isImage)
                {
                    throw new InvalidOperationException("Tệp tải lên không phải là định dạng hình ảnh hợp lệ (Nguy cơ bảo mật).");
                }

                // Ensure directory exists
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(uploadsFolder);
                filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);
            }

            // Delete old avatar if it's a local file
            if (!string.IsNullOrEmpty(oldAvatarUrl) && oldAvatarUrl.StartsWith("/images/avatars/"))
            {
                string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, oldAvatarUrl.TrimStart('/'));
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { }
                }
            }

            return "/images/avatars/" + uniqueFileName;
        }
    }
}
