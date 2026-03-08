using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TCTVocabulary.Models
{
    public static class JsonVocabularySeeder
    {
        private const string SystemEmail = "system@tct.local";
        private const string JsonPath = "wwwroot/data/system-vocabulary.json";

        public static async Task SeedFromJsonAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            List<JsonFolderDto>? folders = null;

            // ── BƯỚC 1: ĐỌC VÀ PARSE FILE JSON (Bắt lỗi Serialization) ────────
            try
            {
                var filePath = Path.Combine(env.ContentRootPath, JsonPath);
                if (!File.Exists(filePath))
                {
                    logger.LogWarning("JsonVocabularySeeder [CẢNH BÁO]: Không tìm thấy file JSON tại {Path}", filePath);
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);
                folders = JsonSerializer.Deserialize<List<JsonFolderDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (folders == null || !folders.Any())
                {
                    logger.LogWarning("JsonVocabularySeeder [CẢNH BÁO]: File JSON trống hoặc không đúng định dạng Array.");
                    return;
                }
                
                logger.LogInformation("JsonVocabularySeeder: Đọc thành công file JSON. Bắt đầu Seed vào Database...");
            }
            catch (JsonException jsonEx)
            {
                logger.LogError("JsonVocabularySeeder [LỖI JSON]: Cú pháp file JSON bị sai. Vui lòng kiểm tra lại dấu phẩy, ngoặc kép trong file JSON.\nChi tiết: {Message}\nInner: {Inner}",
                    jsonEx.Message, jsonEx.InnerException?.Message);
                return; // Dừng tiến trình seed nếu file JSON lỗi
            }
            catch (Exception ex)
            {
                logger.LogError("JsonVocabularySeeder [LỖI HỆ THỐNG]: Lỗi khi mở file JSON.\nChi tiết: {Message}\nInner: {Inner}",
                    ex.Message, ex.InnerException?.Message);
                return;
            }

            // ── BƯỚC 2: UPSERT VÀO DATABASE (Bắt lỗi Database) ───────────────
            try
            {
                // Lấy hoặc tạo User hệ thống
                var sysUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Email == SystemEmail);

                if (sysUser == null)
                {
                    sysUser = new User
                    {
                        Email = SystemEmail,
                        PasswordHash = "$2a$11$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.", // Dummy hash
                        FullName = "TCT System",
                        Role = "System",
                        Streak = 0,
                        Goal = 0,
                        CreatedAt = new DateTime(2026, 1, 1)
                    };
                    context.Users.Add(sysUser);
                    await context.SaveChangesAsync();
                }

                int sysId = sysUser.UserId;

                // Lặp qua từng Folder
                foreach (var folderDto in folders)
                {
                    var folder = await context.Folders
                        .FirstOrDefaultAsync(f => f.UserId == sysId && f.FolderName == folderDto.FolderName);

                    if (folder == null)
                    {
                        folder = new Folder { UserId = sysId, FolderName = folderDto.FolderName };
                        context.Folders.Add(folder);
                        await context.SaveChangesAsync();
                    }

                    // Lặp qua Sets
                    foreach (var setDto in folderDto.Sets)
                    {
                        var set = await context.Sets
                            .FirstOrDefaultAsync(s => s.OwnerId == sysId && s.SetName == setDto.SetName && s.FolderId == folder.FolderId);

                        if (set == null)
                        {
                            set = new Set
                            {
                                SetName = setDto.SetName,
                                Description = setDto.Description,
                                OwnerId = sysId,
                                FolderId = folder.FolderId,
                                CreatedAt = DateTime.Now
                            };
                            context.Sets.Add(set);
                            await context.SaveChangesAsync();
                        }
                        else
                        {
                            // Upsert Set (Nếu description trong JSON thay đổi, ta cập nhật lại DB)
                            if (set.Description != setDto.Description)
                            {
                                set.Description = setDto.Description;
                            }
                        }

                        // Lặp qua Cards
                        foreach (var cardDto in setDto.Cards)
                        {
                            var termToMatch = cardDto.Term.Trim().ToLower();

                            var existingCard = await context.Cards
                                .FirstOrDefaultAsync(c => c.SetId == set.SetId && c.Term.ToLower().Trim() == termToMatch);

                            if (existingCard == null)
                            {
                                // CHƯA CÓ -> THÊM MỚI (INSERT)
                                context.Cards.Add(new Card
                                {
                                    SetId = set.SetId,
                                    Term = cardDto.Term.Trim(),
                                    Definition = cardDto.Definition.Trim(),
                                    ImageUrl = cardDto.ImageUrl,
                                    Phonetic = cardDto.Phonetic,
                                    Example = cardDto.Example,
                                    ExampleTranslation = cardDto.ExampleTranslation,
                                    Topic = cardDto.Topic
                                });
                            }
                            else
                            {
                                // ĐÃ CÓ -> KIỂM TRA ĐỂ CẬP NHẬT (UPDATE)
                                if (existingCard.Definition != cardDto.Definition.Trim())
                                {
                                    existingCard.Definition = cardDto.Definition.Trim();
                                }

                                if (existingCard.ImageUrl != cardDto.ImageUrl)
                                {
                                    existingCard.ImageUrl = cardDto.ImageUrl;
                                }

                                if (existingCard.Phonetic != cardDto.Phonetic)
                                {
                                    existingCard.Phonetic = cardDto.Phonetic;
                                }

                                if (existingCard.Example != cardDto.Example)
                                {
                                    existingCard.Example = cardDto.Example;
                                }

                                if (existingCard.ExampleTranslation != cardDto.ExampleTranslation)
                                {
                                    existingCard.ExampleTranslation = cardDto.ExampleTranslation;
                                }

                                if (existingCard.Topic != cardDto.Topic)
                                {
                                    existingCard.Topic = cardDto.Topic;
                                }
                                
                                // Ghi chú: Vì objects (existingCard) đang được EF Core Track (theo dõi).
                                // Việc gán giá trị ở trên đã tự đánh dấu entity thành Modified.
                                // Không cần phải gọi context.Cards.Update(existingCard) trừ khi Track changes bị tắt.
                            }
                        }

                        // ── BƯỚC 3: DELETE NHỮNG CARD KHÔNG CÒN TRONG JSON ────────
                        var jsonTerms = setDto.Cards.Select(c => c.Term.Trim().ToLower()).ToList();
                        var cardsInDb = await context.Cards.Where(c => c.SetId == set.SetId).ToListAsync();
                        var cardsToDelete = cardsInDb.Where(c => !jsonTerms.Contains(c.Term.Trim().ToLower())).ToList();
                        
                        if (cardsToDelete.Any())
                        {
                            context.Cards.RemoveRange(cardsToDelete);
                        }

                        // Save từng Set để DB an toàn
                        await context.SaveChangesAsync();
                    }
                }

                logger.LogInformation("JsonVocabularySeeder [THÀNH CÔNG]: Hoàn tất cập nhật (Upsert) dữ liệu Vocabulary từ JSON.");
            }
            catch (DbUpdateException dbEx)
            {
                logger.LogError("JsonVocabularySeeder [LỖI DATABASE]: Không thể lưu vào CSDL do lỗi Schema/Ràng buộc.\nChi tiết: {Message}\nInner: {Inner}",
                    dbEx.Message, dbEx.InnerException?.Message);
            }
            catch (Exception ex)
            {
                logger.LogError("JsonVocabularySeeder [LỖI LƯU TRỮ]: Lỗi không xác định khi lưu Database.\nChi tiết: {Message}\nInner: {Inner}",
                    ex.Message, ex.InnerException?.Message);
            }
        }
    }
}
