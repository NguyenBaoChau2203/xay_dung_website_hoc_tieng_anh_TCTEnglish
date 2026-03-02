using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TCTVocabulary.Models
{
    public static class SpeakingDataSeeder
    {
        private const string PlaylistYoutubeId = "PLhNRdHEdUQewzYZ0X6gt3x9_CVen_HldI";
        private const string FirstVideoId = "erjMgola4fQ"; // Used to check if valid data exists

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

            try
            {
                // Check if we already have valid data
                bool hasValidData = context.SpeakingPlaylists.Any(p => p.Name == "A1 English Listening Practice");
                if (hasValidData) return;

                // Clear any old/invalid seed data
                var oldSentences = context.SpeakingSentences.ToList();
                var oldVideos = context.SpeakingVideos.ToList();
                var oldPlaylists = context.SpeakingPlaylists.ToList();
                if (oldSentences.Any()) context.SpeakingSentences.RemoveRange(oldSentences);
                if (oldVideos.Any()) context.SpeakingVideos.RemoveRange(oldVideos);
                if (oldPlaylists.Any()) context.SpeakingPlaylists.RemoveRange(oldPlaylists);
                await context.SaveChangesAsync();
            }
            catch (Exception)
            {
                return;
            }

            // 1. Seed the Playlist
            var playlist = new SpeakingPlaylist
            {
                Name = "A1 English Listening Practice",
                Description = "Simple, slow English conversations for absolute beginners (A1 level). Perfect for shadowing practice.",
                ThumbnailUrl = $"https://img.youtube.com/vi/{FirstVideoId}/hqdefault.jpg"
            };
            await context.SpeakingPlaylists.AddAsync(playlist);
            await context.SaveChangesAsync();

            // 2. Seed the 10 Videos from the playlist
            var videos = new List<SpeakingVideo>
            {
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Language Learning", YoutubeId = "erjMgola4fQ", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/erjMgola4fQ/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Cooking",           YoutubeId = "uVGV8LG3HHM", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/uVGV8LG3HHM/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Weather",           YoutubeId = "eYAaLWdx_h0", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/eYAaLWdx_h0/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Pets",              YoutubeId = "2XRnB4wy4yA", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/2XRnB4wy4yA/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - New Year's Resolutions", YoutubeId = "98pYyFdHw38", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/98pYyFdHw38/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Daily Routine",    YoutubeId = "aQ0w2I0Eb9I", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/aQ0w2I0Eb9I/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Social Media Apps", YoutubeId = "Y6CERK3AXCw", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/Y6CERK3AXCw/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Exercise",         YoutubeId = "uxbG_tFS0Jw", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/uxbG_tFS0Jw/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Homes",            YoutubeId = "ApzkloKc3Lc", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/ApzkloKc3Lc/hqdefault.jpg" },
                new SpeakingVideo { PlaylistId = playlist.Id, Title = "A1 English Listening Practice - Soccer",           YoutubeId = "jbdoyphEcsc", Level = "Easy", Duration = "~5 min", ThumbnailUrl = "https://img.youtube.com/vi/jbdoyphEcsc/hqdefault.jpg" },
            };

            await context.SpeakingVideos.AddRangeAsync(videos);
            await context.SaveChangesAsync();

            // 3. Seed 5 sample sentences for Video 1
            var firstVideo = context.SpeakingVideos.FirstOrDefault(v => v.YoutubeId == "erjMgola4fQ");
            if (firstVideo != null)
            {
                var sentences = new List<SpeakingSentence>
                {
                    new SpeakingSentence { VideoId = firstVideo.Id, StartTime = 5.0,  EndTime = 10.5, Text = "I want to learn English.", VietnameseMeaning = "Tôi muốn học tiếng Anh." },
                    new SpeakingSentence { VideoId = firstVideo.Id, StartTime = 11.0, EndTime = 16.5, Text = "I study English every day.", VietnameseMeaning = "Tôi học tiếng Anh mỗi ngày." },
                    new SpeakingSentence { VideoId = firstVideo.Id, StartTime = 17.0, EndTime = 22.5, Text = "English is a very useful language.", VietnameseMeaning = "Tiếng Anh là một ngôn ngữ rất hữu ích." },
                    new SpeakingSentence { VideoId = firstVideo.Id, StartTime = 23.0, EndTime = 28.5, Text = "I practice speaking with my friends.", VietnameseMeaning = "Tôi luyện nói với bạn bè của mình." },
                    new SpeakingSentence { VideoId = firstVideo.Id, StartTime = 29.0, EndTime = 35.0, Text = "My English is getting better every day.", VietnameseMeaning = "Tiếng Anh của tôi ngày càng tiến bộ hơn." },
                };
                await context.SpeakingSentences.AddRangeAsync(sentences);
                await context.SaveChangesAsync();
            }
        }
    }
}
