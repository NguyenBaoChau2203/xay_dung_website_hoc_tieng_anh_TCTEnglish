using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TCTVocabulary.Models
{
    public static class SpeakingDataSeeder
    {
        private const string FirstVideoId = "erjMgola4fQ";

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

            try
            {
                // Skip if data already seeded (check for 12 videos)
                bool hasFullData = context.SpeakingPlaylists.Any(p => p.Name == "A1 English Listening Practice")
                                && context.SpeakingVideos.Count() >= 12;
                if (hasFullData) return;

                // Clear any incomplete/old data
                var oldSentences = context.SpeakingSentences.ToList();
                var oldVideos    = context.SpeakingVideos.ToList();
                var oldPlaylists = context.SpeakingPlaylists.ToList();
                if (oldSentences.Any()) context.SpeakingSentences.RemoveRange(oldSentences);
                if (oldVideos.Any())    context.SpeakingVideos.RemoveRange(oldVideos);
                if (oldPlaylists.Any()) context.SpeakingPlaylists.RemoveRange(oldPlaylists);
                await context.SaveChangesAsync();
            }
            catch (Exception)
            {
                return;
            }

            // ── 1. Seed the Playlist ─────────────────────────────────────────
            var playlist = new SpeakingPlaylist
            {
                Name        = "A1 English Listening Practice",
                Description = "Simple, slow English conversations for absolute beginners (A1 level). Perfect for shadowing practice.",
                ThumbnailUrl = $"https://img.youtube.com/vi/{FirstVideoId}/hqdefault.jpg"
            };
            await context.SpeakingPlaylists.AddAsync(playlist);
            await context.SaveChangesAsync();

            // ── 2. Seed 12 Videos from the real YouTube playlist ─────────────
            // Source: https://www.youtube.com/playlist?list=PLhNRdHEdUQewzYZ0X6gt3x9_CVen_HldI
            // NEW: Added Topic property and changed Level from "Easy" to "A1" for all videos
            var videos = new List<SpeakingVideo>
            {
                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Conversation", Duration = "3:43", // NEW: Level="A1", Topic="Daily Conversation"
                    Title = "A1 English Listening Practice - Language Learning",
                    YoutubeId = "erjMgola4fQ",
                    ThumbnailUrl = "https://img.youtube.com/vi/erjMgola4fQ/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Life", Duration = "5:59", // NEW: Level="A1", Topic="Daily Life"
                    Title = "A1 English Listening Practice - Cooking",
                    YoutubeId = "uVGV8LG3HHM",
                    ThumbnailUrl = "https://img.youtube.com/vi/uVGV8LG3HHM/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Life", Duration = "5:04", // NEW: Level="A1", Topic="Daily Life"
                    Title = "A1 English Listening Practice - Weather",
                    YoutubeId = "eYAaLWdx_h0",
                    ThumbnailUrl = "https://img.youtube.com/vi/eYAaLWdx_h0/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Life", Duration = "4:28", // NEW: Level="A1", Topic="Daily Life"
                    Title = "A1 English Listening Practice - Pets",
                    YoutubeId = "2XRnB4wy4yA",
                    ThumbnailUrl = "https://img.youtube.com/vi/2XRnB4wy4yA/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Lifestyle & Culture", Duration = "4:11", // NEW: Level="A1", Topic="Lifestyle & Culture"
                    Title = "A1 English Listening Practice - New Year's Resolutions",
                    YoutubeId = "98pYyFdHw38",
                    ThumbnailUrl = "https://img.youtube.com/vi/98pYyFdHw38/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Life", Duration = "4:46", // NEW: Level="A1", Topic="Daily Life"
                    Title = "A1 English Listening Practice - Daily Routine",
                    YoutubeId = "aQ0w2I0Eb9I",
                    ThumbnailUrl = "https://img.youtube.com/vi/aQ0w2I0Eb9I/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Conversation", Duration = "4:31", // NEW: Level="A1", Topic="Daily Conversation"
                    Title = "A1 English Listening Practice - Social Media Apps",
                    YoutubeId = "Y6CERK3AXCw",
                    ThumbnailUrl = "https://img.youtube.com/vi/Y6CERK3AXCw/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Lifestyle & Culture", Duration = "4:47", // NEW: Level="A1", Topic="Lifestyle & Culture"
                    Title = "A1 English Listening Practice - Exercise",
                    YoutubeId = "uxbG_tFS0Jw",
                    ThumbnailUrl = "https://img.youtube.com/vi/uxbG_tFS0Jw/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Life", Duration = "4:30", // NEW: Level="A1", Topic="Daily Life"
                    Title = "A1 English Listening Practice - Homes",
                    YoutubeId = "ApzkloKc3Lc",
                    ThumbnailUrl = "https://img.youtube.com/vi/ApzkloKc3Lc/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Lifestyle & Culture", Duration = "4:42", // NEW: Level="A1", Topic="Lifestyle & Culture"
                    Title = "A1 English Listening Practice - Soccer",
                    YoutubeId = "jbdoyphEcsc",
                    ThumbnailUrl = "https://img.youtube.com/vi/jbdoyphEcsc/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Daily Life", Duration = "4:37", // NEW: Level="A1", Topic="Daily Life"
                    Title = "A1 English Listening Practice - Jobs",
                    YoutubeId = "v95eemWZ-4s",
                    ThumbnailUrl = "https://img.youtube.com/vi/v95eemWZ-4s/hqdefault.jpg" },

                new() { PlaylistId = playlist.Id, Level = "A1", Topic = "Lifestyle & Culture", Duration = "4:25", // NEW: Level="A1", Topic="Lifestyle & Culture"
                    Title = "A1 English Listening Practice - Transportation",
                    YoutubeId = "Wlehc1l50bk",
                    ThumbnailUrl = "https://img.youtube.com/vi/Wlehc1l50bk/hqdefault.jpg" },
            };

            await context.SpeakingVideos.AddRangeAsync(videos);
            await context.SaveChangesAsync();

            // ── 3. Seed 10 sentences for Video 1: Language Learning ──────────
            var video1 = context.SpeakingVideos.FirstOrDefault(v => v.YoutubeId == "erjMgola4fQ");
            if (video1 != null)
            {
                var sentences1 = new List<SpeakingSentence>
                {
                    new() { VideoId = video1.Id, StartTime =  4.0, EndTime =  8.5,  Text = "I want to learn English.",                  VietnameseMeaning = "Tôi muốn học tiếng Anh." },
                    new() { VideoId = video1.Id, StartTime =  9.0, EndTime = 14.0,  Text = "I study English every day.",               VietnameseMeaning = "Tôi học tiếng Anh mỗi ngày." },
                    new() { VideoId = video1.Id, StartTime = 15.0, EndTime = 20.5,  Text = "English is a very useful language.",       VietnameseMeaning = "Tiếng Anh là một ngôn ngữ rất hữu ích." },
                    new() { VideoId = video1.Id, StartTime = 21.0, EndTime = 27.0,  Text = "I practice speaking with my friends.",     VietnameseMeaning = "Tôi luyện nói với bạn bè của mình." },
                    new() { VideoId = video1.Id, StartTime = 28.0, EndTime = 34.0,  Text = "My English is getting better every day.",  VietnameseMeaning = "Tiếng Anh của tôi ngày càng tốt hơn." },
                    new() { VideoId = video1.Id, StartTime = 35.0, EndTime = 42.0,  Text = "I watch English videos online.",           VietnameseMeaning = "Tôi xem video tiếng Anh trực tuyến." },
                    new() { VideoId = video1.Id, StartTime = 43.0, EndTime = 50.0,  Text = "I listen to English podcasts every morning.", VietnameseMeaning = "Tôi nghe podcast tiếng Anh mỗi sáng." },
                    new() { VideoId = video1.Id, StartTime = 51.0, EndTime = 57.5,  Text = "Speaking English makes me confident.",     VietnameseMeaning = "Nói tiếng Anh giúp tôi tự tin hơn." },
                    new() { VideoId = video1.Id, StartTime = 58.5, EndTime = 65.0,  Text = "I read English books to improve my vocabulary.", VietnameseMeaning = "Tôi đọc sách tiếng Anh để nâng cao vốn từ vựng." },
                    new() { VideoId = video1.Id, StartTime = 66.0, EndTime = 73.0,  Text = "Learning a new language is a great experience.", VietnameseMeaning = "Học một ngôn ngữ mới là một trải nghiệm tuyệt vời." },
                };
                await context.SpeakingSentences.AddRangeAsync(sentences1);
                await context.SaveChangesAsync();
            }

            // ── 4. Seed 10 sentences for Video 2: Cooking ───────────────────
            var video2 = context.SpeakingVideos.FirstOrDefault(v => v.YoutubeId == "uVGV8LG3HHM");
            if (video2 != null)
            {
                var sentences2 = new List<SpeakingSentence>
                {
                    new() { VideoId = video2.Id, StartTime =  4.0, EndTime =  9.5,  Text = "I love cooking at home.",                  VietnameseMeaning = "Tôi thích nấu ăn ở nhà." },
                    new() { VideoId = video2.Id, StartTime = 10.5, EndTime = 16.0,  Text = "I make breakfast every morning.",           VietnameseMeaning = "Tôi làm bữa sáng mỗi buổi sáng." },
                    new() { VideoId = video2.Id, StartTime = 17.0, EndTime = 23.0,  Text = "My favorite food is pasta.",               VietnameseMeaning = "Món ăn yêu thích của tôi là mì ống." },
                    new() { VideoId = video2.Id, StartTime = 24.0, EndTime = 30.0,  Text = "I need onions, garlic, and tomatoes.",     VietnameseMeaning = "Tôi cần hành tây, tỏi và cà chua." },
                    new() { VideoId = video2.Id, StartTime = 31.0, EndTime = 37.5,  Text = "First, I chop the vegetables.",            VietnameseMeaning = "Đầu tiên, tôi thái rau củ." },
                    new() { VideoId = video2.Id, StartTime = 38.5, EndTime = 45.0,  Text = "Then I heat the oil in a pan.",            VietnameseMeaning = "Sau đó tôi đun nóng dầu trong chảo." },
                    new() { VideoId = video2.Id, StartTime = 46.0, EndTime = 52.5,  Text = "I add the garlic and cook for one minute.", VietnameseMeaning = "Tôi cho tỏi vào và nấu trong một phút." },
                    new() { VideoId = video2.Id, StartTime = 53.5, EndTime = 60.0,  Text = "The sauce smells delicious.",              VietnameseMeaning = "Nước sốt thơm ngon quá." },
                    new() { VideoId = video2.Id, StartTime = 61.0, EndTime = 67.5,  Text = "I boil the pasta for ten minutes.",        VietnameseMeaning = "Tôi luộc mì ống trong mười phút." },
                    new() { VideoId = video2.Id, StartTime = 68.5, EndTime = 75.0,  Text = "Dinner is ready, let's eat!",             VietnameseMeaning = "Bữa tối đã sẵn sàng, cùng ăn thôi!" },
                };
                await context.SpeakingSentences.AddRangeAsync(sentences2);
                await context.SaveChangesAsync();
            }
        }
    }
}
