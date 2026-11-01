using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure;

public static class LocalDevelopmentBootstrap
{
    public static void ValidateTarget(IConfiguration configuration, bool isDevelopment)
    {
        if (!isDevelopment) throw new InvalidOperationException("Local bootstrap is allowed only in Development.");
        var target = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"));
        if (target.Server is not ("127.0.0.1" or "localhost" or "::1")
            || target.Database != "tctenglish_dev" || target.UserID != "tct_dev")
            throw new InvalidOperationException("Local bootstrap requires loopback / tctenglish_dev / tct_dev. No database was changed.");
    }

    public static async Task<bool> RunAsync(DbflashcardContext db, IConfiguration configuration,
        bool isDevelopment, string accessPath, CancellationToken ct = default)
    {
        ValidateTarget(configuration, isDevelopment);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // All-or-nothing first-machine fixture. Never alter a restored or already-used database.
        if (await db.Users.AsNoTracking().AnyAsync(ct)) return false;
        accessPath = Path.GetFullPath(accessPath);
        Directory.CreateDirectory(Path.GetDirectoryName(accessPath)!);
        var ownsFile = false;
        var committed = false;
        try
        {
            await using var access = new FileStream(accessPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
            ownsFile = true;
            var accounts = new List<LocalAccount>();
            foreach (var (email, role) in new[]
            {
                ("admin@tct.local", Roles.Admin), ("student@tct.local", Roles.Standard), ("premium@tct.local", Roles.Premium)
            })
            {
                var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
                accounts.Add(new(email, role, password));
                db.Users.Add(new User
                {
                    Email = email, FullName = "Local " + role, Role = role,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                    Status = UserStatus.Offline, CreatedAt = DateTime.UtcNow, Goal = 5
                });
            }
            await db.SaveChangesAsync(ct);
            var student = await db.Users.SingleAsync(u => u.Email == "student@tct.local", ct);
            var folder = new Folder { FolderName = "Local practice", UserId = student.UserId };
            var set = new Set { SetName = "Everyday English (local sample)", OwnerId = student.UserId, Folder = folder, CreatedAt = DateTime.UtcNow };
            foreach (var (term, meaning) in new[] { ("hello", "xin chào"), ("morning", "buổi sáng"), ("learn", "học"), ("friend", "bạn bè"), ("book", "quyển sách"), ("practice", "luyện tập") })
                set.Cards.Add(new Card { Term = term, Definition = meaning });
            db.Sets.Add(set);
            if (!await db.WritingExercises.AnyAsync(ct))
            {
                var writing = WritingExerciseSeedData.CreateExercises();
                foreach (var exercise in writing) exercise.SourceType = "system";
                db.WritingExercises.AddRange(writing);
                db.WritingExerciseSentences.AddRange(WritingExerciseSeedData.CreateSentences());
            }
            if (!await db.SpeakingVideos.AnyAsync(ct))
            {
                // Explicit synthetic fixture for mic/assessment development, not claimed YouTube captions.
                var video = new SpeakingVideo
                {
                    Title = "Local ASR development fixture", YoutubeId = "9F8o8eH_Hms", Level = "A1",
                    Topic = "Local sample", Duration = "00:15", TranscriptSource = "local-fixture",
                    CreatedAt = DateTime.UtcNow,
                    SpeakingPlaylist = new SpeakingPlaylist { Name = "Local development samples" }
                };
                var lines = new[] { ("Good morning everyone", "Chào buổi sáng mọi người"), ("I am learning English", "Tôi đang học tiếng Anh"), ("Practice makes progress", "Luyện tập giúp tiến bộ") };
                for (var i = 0; i < lines.Length; i++)
                    video.SpeakingSentences.Add(new SpeakingSentence { Text = lines[i].Item1, VietnameseMeaning = lines[i].Item2, StartTime = i * 5, EndTime = i * 5 + 4 });
                db.SpeakingVideos.Add(video);
            }
            if (!await db.ReadingPassages.AnyAsync(ct))
                db.ReadingPassages.Add(new ReadingPassage { Title = "Learning together", Level = "A1", Topic = "Local sample", Content = "We learn English together. We read books and practise every day.", IsPublished = true, CreatedAt = DateTime.UtcNow });
            if (!await db.ListeningLessons.AnyAsync(ct))
                db.ListeningLessons.Add(new ListeningLesson
                {
                    Title = "Local listening development fixture", Level = "A1", Topic = "Local sample",
                    YoutubeId = "9F8o8eH_Hms", TranscriptSource = "local-fixture", IsPublished = true,
                    TranscriptLines = new List<ListeningTranscriptLine>
                    {
                        new() { OrderIndex = 1, Speaker = "Student", Text = "I practise English every day.", VietnameseMeaning = "Tôi luyện tiếng Anh mỗi ngày." },
                        new() { OrderIndex = 2, Speaker = "Friend", Text = "We can learn together.", VietnameseMeaning = "Chúng ta có thể học cùng nhau." }
                    },
                    QuizQuestions = new List<ListeningQuizQuestion>
                    {
                        new() { OrderIndex = 1, QuestionText = "What does the student practise?", OptionA = "English", OptionB = "Math", OptionC = "Music", OptionD = "Art", CorrectAnswer = "A" }
                    }
                });
            await db.SaveChangesAsync(ct);
            await JsonSerializer.SerializeAsync(access, new { createdAtUtc = DateTime.UtcNow, accounts }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }, ct);
            await access.FlushAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return true;
        }
        finally
        {
            if (ownsFile && !committed) File.Delete(accessPath);
        }
    }

    private sealed record LocalAccount(string Email, string Role, string Password);
}
