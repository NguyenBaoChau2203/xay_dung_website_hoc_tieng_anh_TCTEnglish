using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TCTVocabulary.Models
{
    public static class SystemVocabularySeeder
    {
        // Email cố định để nhận diện user hệ thống
        private const string SystemEmail = "system@tct.local";

        // Property để các nơi khác (Controller) lấy UserId thực tế
        public static int GetSystemUserId(DbflashcardContext context)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == SystemEmail);
            return user?.UserId ?? 0;
        }

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

            try
            {
                // Guard: nếu user hệ thống đã tồn tại thì bỏ qua toàn bộ
                if (context.Users.Any(u => u.Email == SystemEmail))
                    return;

                // ── 1. Tạo User hệ thống — để DB tự sinh UserId (IDENTITY) ──
                var systemUser = new User
                {
                    Email        = SystemEmail,
                    PasswordHash = "$2a$11$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.",
                    FullName     = "TCT System",
                    Role         = "System",
                    Streak       = 0,
                    Goal         = 0,
                    CreatedAt    = new DateTime(2026, 1, 1)
                };
                context.Users.Add(systemUser);
                await context.SaveChangesAsync();

                // Lấy UserId mà DB vừa tự sinh ra
                int sysId = systemUser.UserId;

                // ── 2. Folder 1: Từ Vựng Tiếng Anh Thông Dụng ───────────────
                var folder1 = new Folder { UserId = sysId, FolderName = "Từ Vựng Tiếng Anh Thông Dụng" };
                context.Folders.Add(folder1);
                await context.SaveChangesAsync();

                // Set 1.1
                var set1 = new Set { SetName = "1000 từ tiếng Anh thông dụng", OwnerId = sysId, FolderId = folder1.FolderId, Description = "#common #basic #a1", CreatedAt = DateTime.Now };
                context.Sets.Add(set1);
                await context.SaveChangesAsync();

                context.Cards.AddRange(new List<Card>
                {
                    new() { SetId = set1.SetId, Term = "Hello",     Definition = "Xin chào", Topic = "Greetings" },
                    new() { SetId = set1.SetId, Term = "Goodbye",   Definition = "Tạm biệt", Topic = "Greetings" },
                    new() { SetId = set1.SetId, Term = "Thank you", Definition = "Cảm ơn", Topic = "Politeness" },
                    new() { SetId = set1.SetId, Term = "Please",    Definition = "Làm ơn", Topic = "Politeness" },
                    new() { SetId = set1.SetId, Term = "Sorry",     Definition = "Xin lỗi", Topic = "Politeness" },
                    new() { SetId = set1.SetId, Term = "Yes",       Definition = "Có / Vâng", Topic = "Basic" },
                    new() { SetId = set1.SetId, Term = "No",        Definition = "Không", Topic = "Basic" },
                    new() { SetId = set1.SetId, Term = "Help",      Definition = "Giúp đỡ", Topic = "Actions" },
                    new() { SetId = set1.SetId, Term = "Water",     Definition = "Nước", Topic = "Items" },
                    new() { SetId = set1.SetId, Term = "Food",      Definition = "Đồ ăn", Topic = "Items" }
                });

                // Set 1.2
                var set2 = new Set { SetName = "Từ vựng tiếng Anh giao tiếp", OwnerId = sysId, FolderId = folder1.FolderId, Description = "#communication #conversation #basic", CreatedAt = DateTime.Now };
                context.Sets.Add(set2);
                await context.SaveChangesAsync();

                context.Cards.AddRange(new List<Card>
                {
                    new() { SetId = set2.SetId, Term = "How are you?",        Definition = "Bạn có khỏe không?" },
                    new() { SetId = set2.SetId, Term = "Nice to meet you",    Definition = "Rất vui được gặp bạn" },
                    new() { SetId = set2.SetId, Term = "What is your name?",  Definition = "Tên bạn là gì?" },
                    new() { SetId = set2.SetId, Term = "Where are you from?", Definition = "Bạn đến từ đâu?" },
                    new() { SetId = set2.SetId, Term = "I don't understand",  Definition = "Tôi không hiểu" },
                    new() { SetId = set2.SetId, Term = "Can you repeat?",     Definition = "Bạn có thể nhắc lại không?" },
                    new() { SetId = set2.SetId, Term = "Speak slowly please", Definition = "Làm ơn nói chậm thôi" },
                    new() { SetId = set2.SetId, Term = "I agree",             Definition = "Tôi đồng ý" },
                    new() { SetId = set2.SetId, Term = "Let's go",            Definition = "Đi thôi" },
                    new() { SetId = set2.SetId, Term = "See you later",       Definition = "Hẹn gặp lại" }
                });
                await context.SaveChangesAsync();

                // ── 3. Folder 2: Từ Vựng Oxford ──────────────────────────────
                var folder2 = new Folder { UserId = sysId, FolderName = "Từ Vựng Oxford" };
                context.Folders.Add(folder2);
                await context.SaveChangesAsync();

                // Set 2.1: Oxford A1
                var set3 = new Set { SetName = "3000 từ vựng Oxford A1", OwnerId = sysId, FolderId = folder2.FolderId, Description = "#oxford #a1 #essential", CreatedAt = DateTime.Now };
                context.Sets.Add(set3);
                await context.SaveChangesAsync();

                context.Cards.AddRange(new List<Card>
                {
                    new() { SetId = set3.SetId, Term = "Apple",     Definition = "Quả táo" },
                    new() { SetId = set3.SetId, Term = "Book",      Definition = "Quyển sách" },
                    new() { SetId = set3.SetId, Term = "Cat",       Definition = "Con mèo" },
                    new() { SetId = set3.SetId, Term = "Dog",       Definition = "Con chó" },
                    new() { SetId = set3.SetId, Term = "Egg",       Definition = "Quả trứng" },
                    new() { SetId = set3.SetId, Term = "Fish",      Definition = "Con cá" },
                    new() { SetId = set3.SetId, Term = "Girl",      Definition = "Cô gái" },
                    new() { SetId = set3.SetId, Term = "House",     Definition = "Căn nhà" },
                    new() { SetId = set3.SetId, Term = "Ice",       Definition = "Nước đá / Băng" },
                    new() { SetId = set3.SetId, Term = "Jump",      Definition = "Nhảy" }
                });

                // Set 2.2: Oxford A2
                var set4 = new Set { SetName = "3000 từ vựng Oxford A2", OwnerId = sysId, FolderId = folder2.FolderId, Description = "#oxford #a2 #essential", CreatedAt = DateTime.Now };
                context.Sets.Add(set4);
                await context.SaveChangesAsync();

                context.Cards.AddRange(new List<Card>
                {
                    new() { SetId = set4.SetId, Term = "Achieve",     Definition = "Đạt được" },
                    new() { SetId = set4.SetId, Term = "Benefit",     Definition = "Lợi ích" },
                    new() { SetId = set4.SetId, Term = "Challenge",   Definition = "Thử thách" },
                    new() { SetId = set4.SetId, Term = "Decision",    Definition = "Quyết định" },
                    new() { SetId = set4.SetId, Term = "Environment", Definition = "Môi trường" },
                    new() { SetId = set4.SetId, Term = "Flexible",    Definition = "Linh hoạt" },
                    new() { SetId = set4.SetId, Term = "Growth",      Definition = "Sự tăng trưởng" },
                    new() { SetId = set4.SetId, Term = "Honest",      Definition = "Trung thực" },
                    new() { SetId = set4.SetId, Term = "Improve",     Definition = "Cải thiện" },
                    new() { SetId = set4.SetId, Term = "Journey",     Definition = "Hành trình" }
                });
                await context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Không làm gì để không ảnh hưởng app start
            }
        }
    }
}
