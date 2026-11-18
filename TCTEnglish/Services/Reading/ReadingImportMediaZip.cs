using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Reading;

/// <summary>
/// Safely maps ZIP root images named {ReadingCode}.ext onto import drafts.
/// ZIP matches win over Excel ImageUrl values.
/// </summary>
public static class ReadingImportMediaZip
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private static readonly Dictionary<string, string> MimeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    public static async Task ApplyAsync(
        IFormFile? mediaZip,
        IReadOnlyList<ReadingEditorViewModel> readings,
        IReadingAdminService admin,
        List<ReadingExcelWarning> warnings,
        CancellationToken ct = default)
    {
        if (mediaZip == null || mediaZip.Length == 0 || readings.Count == 0)
            return;

        if (!string.Equals(Path.GetExtension(mediaZip.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(new ReadingExcelWarning
            {
                Sheet = "MediaZip",
                Message = "Gói ảnh phải là file .zip. Đã bỏ qua media ZIP."
            });
            return;
        }

        const long maxZipBytes = 25 * 1024 * 1024;
        if (mediaZip.Length > maxZipBytes)
        {
            warnings.Add(new ReadingExcelWarning
            {
                Sheet = "MediaZip",
                Message = "ZIP vượt quá 25MB. Đã bỏ qua media ZIP."
            });
            return;
        }

        var byCode = readings
            .Where(r => !string.IsNullOrWhiteSpace(r.ExternalReadingKey))
            .GroupBy(r => r.ExternalReadingKey!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var zipStream = mediaZip.OpenReadStream();
        ZipArchive archive;
        try { archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException)
        {
            warnings.Add(new ReadingExcelWarning { Sheet = "MediaZip", Message = "ZIP không hợp lệ." });
            return;
        }
        using var archiveLifetime = archive;
        if (archive.Entries.Count > 200 || archive.Entries.Sum(e => (double)e.Length) > 50 * 1024 * 1024)
        {
            warnings.Add(new ReadingExcelWarning { Sheet = "MediaZip", Message = "ZIP có quá nhiều ảnh hoặc vượt 50MB sau giải nén." });
            return;
        }

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Length > ImageUploadPolicies.ReadingPassage.MaxFileSizeInBytes)
            {
                warnings.Add(new ReadingExcelWarning { Sheet = "MediaZip", Message = $"Bỏ qua '{entry.Name}' — ảnh vượt quá 5MB sau giải nén." });
                continue;
            }
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.FullName.Contains('/') || entry.FullName.Contains('\\'))
            {
                if (!string.IsNullOrWhiteSpace(entry.Name) && entry.Length > 0)
                {
                    warnings.Add(new ReadingExcelWarning
                    {
                        Sheet = "MediaZip",
                        Message = $"Bỏ qua '{entry.FullName}' — chỉ chấp nhận ảnh ở thư mục gốc ZIP."
                    });
                }

                continue;
            }

            if (entry.Length <= 0) continue;

            var ext = Path.GetExtension(entry.Name);
            if (!AllowedExtensions.Contains(ext))
            {
                warnings.Add(new ReadingExcelWarning
                {
                    Sheet = "MediaZip",
                    Message = $"Bỏ qua '{entry.Name}' — định dạng không hỗ trợ."
                });
                continue;
            }

            var code = Path.GetFileNameWithoutExtension(entry.Name).Trim();
            if (string.IsNullOrWhiteSpace(code) || !byCode.TryGetValue(code, out var reading))
            {
                warnings.Add(new ReadingExcelWarning
                {
                    ReadingCode = code,
                    Sheet = "MediaZip",
                    Message = $"Không có ReadingCode khớp với '{entry.Name}' trong file Excel hợp lệ."
                });
                continue;
            }

            if (matched.Contains(code))
            {
                warnings.Add(new ReadingExcelWarning
                {
                    ReadingCode = code,
                    Sheet = "MediaZip",
                    Message = $"Trùng ảnh cho {code}; giữ file đã áp dụng trước."
                });
                continue;
            }

            try
            {
                await using var entryStream = entry.Open();
                await using var copy = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = await entryStream.ReadAsync(buffer, ct)) > 0)
                {
                    if (copy.Length + read > ImageUploadPolicies.ReadingPassage.MaxFileSizeInBytes)
                        throw new InvalidDataException("Expanded image exceeds the limit.");
                    await copy.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                copy.Position = 0;

                var formFile = new FormFile(copy, 0, copy.Length, "ImageFile", entry.Name)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = MimeByExtension[ext]
                };

                reading.ImageUrl = await admin.SavePassageImageAsync(formFile, ct);
                matched.Add(code);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add(new ReadingExcelWarning
                {
                    ReadingCode = code,
                    Sheet = "MediaZip",
                    Message = $"Không lưu được '{entry.Name}'. Kiểm tra định dạng và dung lượng ảnh."
                });
            }
        }
    }
}
