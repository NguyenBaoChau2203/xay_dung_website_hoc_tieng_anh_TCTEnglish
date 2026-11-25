// Local-only sample dashboard progress for learner "Anh Hoàng".
// Run: pwsh -NoProfile -File scripts/seed-anh-hoang-dashboard.ps1 -Action apply|remove|status
// Standalone file-based utility (no TCTEnglish project build / apphost lock).
#:package MySqlConnector@2.5.0
#:property PublishAot=false
#:property PublishTrimmed=false

using System.Globalization;
using System.Text.Json;
using MySqlConnector;

const string SeedMarker = "local-sample:anh-hoang-dashboard-v1";
const string LearnerFullName = "Anh Hoàng";
const int HistoryDays = 45;
const int StreakDays = 14;

if (args.Length != 2 || !new[] { "apply", "remove", "status" }.Contains(args[0], StringComparer.Ordinal))
    throw new ArgumentException("Use <apply|remove|status> <repoRoot>.");

var action = args[0];
var repo = Path.GetFullPath(args[1]);
var receiptDir = Path.Combine(repo, ".appdata", "local", "anh-hoang-dashboard-seed");
var receiptPath = Path.Combine(receiptDir, "receipt.json");
Directory.CreateDirectory(receiptDir);

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Local child-process connection required.");
var parsed = new MySqlConnectionStringBuilder(connection);
if (parsed.Server is not ("127.0.0.1" or "localhost" or "::1")
    || parsed.Port != 3307
    || !string.Equals(parsed.Database, "tctenglish_dev", StringComparison.Ordinal))
    throw new InvalidOperationException("Only Docker MariaDB tctenglish_dev on 127.0.0.1:3307 is allowed.");

await using var db = new MySqlConnection(connection);
await db.OpenAsync();

await using (var versionCmd = db.CreateCommand())
{
    versionCmd.CommandText = "SELECT SUBSTRING_INDEX(VERSION(),'-',1)";
    if ((string)(await versionCmd.ExecuteScalarAsync())! != "10.5.23")
        throw new InvalidOperationException("MariaDB 10.5.23 required.");
}

var today = BusinessDateUtcPlus7(DateTime.UtcNow);
var user = await FindLearnerAsync(db, LearnerFullName)
    ?? throw new InvalidOperationException($"Learner '{LearnerFullName}' not found on local DB.");
var cardIds = await LoadOwnedCardIdsAsync(db, user.UserId);

if (action == "status")
{
    await PrintStatusAsync(db, user, cardIds.Count, today);
    return;
}

if (action == "remove")
{
    await RemoveAsync(db, user, receiptPath, today);
    return;
}

await ApplyAsync(db, user, cardIds, today, receiptPath);

static DateTime BusinessDateUtcPlus7(DateTime utcNow)
    => DateTime.SpecifyKind(utcNow.AddHours(7).Date, DateTimeKind.Unspecified);

static async Task<LearnerRow?> FindLearnerAsync(MySqlConnection db, string fullName)
{
    await using var cmd = db.CreateCommand();
    cmd.CommandText = """
        SELECT UserId, FullName, Streak, LongestStreak, LastStudyDate, Goal
        FROM Users
        WHERE FullName = @name AND AnonymizedAt IS NULL
        LIMIT 2
        """;
    cmd.Parameters.AddWithValue("@name", fullName);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return null;
    var row = ReadLearner(reader);
    if (await reader.ReadAsync())
        throw new InvalidOperationException($"Multiple users named '{fullName}'.");
    return row;
}

static LearnerRow ReadLearner(MySqlDataReader reader) => new(
    reader.GetInt32("UserId"),
    reader.GetString("FullName"),
    reader.IsDBNull(reader.GetOrdinal("Streak")) ? null : reader.GetInt32("Streak"),
    reader.IsDBNull(reader.GetOrdinal("LongestStreak")) ? null : reader.GetInt32("LongestStreak"),
    reader.IsDBNull(reader.GetOrdinal("LastStudyDate")) ? null : reader.GetDateTime("LastStudyDate"),
    reader.IsDBNull(reader.GetOrdinal("Goal")) ? null : reader.GetInt32("Goal"));

static async Task<List<int>> LoadOwnedCardIdsAsync(MySqlConnection db, int userId)
{
    await using var cmd = db.CreateCommand();
    cmd.CommandText = """
        SELECT c.CardID
        FROM Cards c
        INNER JOIN Sets s ON c.SetID = s.SetID
        WHERE s.OwnerID = @userId
        ORDER BY c.CardID
        """;
    cmd.Parameters.AddWithValue("@userId", userId);
    var ids = new List<int>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        ids.Add(reader.GetInt32(0));
    return ids;
}

static async Task ApplyAsync(
    MySqlConnection db,
    LearnerRow user,
    IReadOnlyList<int> cardIds,
    DateTime today,
    string receiptPath)
{
    if (cardIds.Count == 0)
        throw new InvalidOperationException("Learner has no owned cards; create sets/cards before seeding progress.");

    int publishedPassageId;
    await using (var cmd = db.CreateCommand())
    {
        cmd.CommandText = """
            SELECT Id FROM ReadingPassages
            WHERE IsPublished = 1 AND Status = 'Published'
            ORDER BY Id
            LIMIT 1
            """;
        var scalar = await cmd.ExecuteScalarAsync();
        if (scalar is null or DBNull)
            throw new InvalidOperationException("No published ReadingPassage available for 'Đang đọc dở'.");
        publishedPassageId = Convert.ToInt32(scalar);
    }

    int? existingReadingId = null;
    await using (var cmd = db.CreateCommand())
    {
        cmd.CommandText = """
            SELECT Id FROM UserReadingHistories
            WHERE UserId = @userId AND IsCompleted = 0
            ORDER BY ViewedAt DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@userId", user.UserId);
        var scalar = await cmd.ExecuteScalarAsync();
        if (scalar is not null and not DBNull)
            existingReadingId = Convert.ToInt32(scalar);
    }

    Receipt? prior = File.Exists(receiptPath)
        ? JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath))
        : null;

    var userBackup = prior?.UserBackup ?? new UserBackup(user.Streak, user.LongestStreak, user.LastStudyDate, user.Goal);
    var restOffsets = new HashSet<int> { 3, 9, 16, 22, 28, 35, 41 };
    var windowStart = today.AddDays(-(HistoryDays - 1));
    var activityDates = new List<DateTime>();
    for (var i = 0; i < HistoryDays; i++)
    {
        var day = windowStart.AddDays(i);
        var offsetFromToday = (today - day).Days;
        if (offsetFromToday < StreakDays || !restOffsets.Contains(offsetFromToday))
            activityDates.Add(day);
    }

    await using var tx = await db.BeginTransactionAsync();
    var rng = new Random(7007);
    var seededDailyIds = new List<int>();

    foreach (var day in activityDates)
    {
        var dayIndex = (today - day).Days;
        var intensity = dayIndex < StreakDays ? 1.0 : 0.75 + (rng.NextDouble() * 0.35);
        var cards = Clamp((int)Math.Round((8 + rng.Next(0, 25)) * intensity), 4, 42);
        var xp = Clamp((int)Math.Round((18 + cards * 1.6 + rng.Next(0, 20)) * intensity), 12, 95);
        var newCards = dayIndex % 5 == 0 ? rng.Next(1, 4) : (dayIndex % 3 == 0 ? 1 : 0);
        var quizzes = dayIndex % 4 == 0 ? rng.Next(1, 3) : 0;
        var speaking = dayIndex % 6 == 0 ? 1 : 0;
        var writing = dayIndex % 7 == 0 ? 1 : 0;
        var reading = dayIndex % 5 == 1 ? 1 : 0;
        var listening = dayIndex % 8 == 0 ? 1 : 0;
        var streakXp = dayIndex < StreakDays ? 5 : (rng.NextDouble() < 0.35 ? 5 : 0);
        var vocabDone = Math.Max(0, Math.Min(cards / 5, 6));

        if (day == today)
        {
            cards = Math.Max(cards, 12);
            xp = Math.Max(xp, 40);
            speaking = Math.Max(speaking, 1);
            streakXp = Math.Max(streakXp, 5);
            reading = 0; // leave Reading goal unfinished on dashboard
        }

        await using var upsert = db.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = """
            INSERT INTO UserDailyActivities
                (UserID, ActivityDate, XpEarned, StreakXpAwarded, CardsReviewed, NewCardsLearned,
                 VocabularyCompletedCount, QuizzesCompleted, SpeakingCompletedCount, WritingCompletedCount,
                 ReadingCompletedCount, ListeningCompletedCount, DailyChallengeCompleted)
            VALUES
                (@userId, @day, @xp, @streakXp, @cards, @newCards,
                 @vocab, @quizzes, @speaking, @writing, @reading, @listening, @challenge)
            ON DUPLICATE KEY UPDATE
                XpEarned = VALUES(XpEarned),
                StreakXpAwarded = VALUES(StreakXpAwarded),
                CardsReviewed = VALUES(CardsReviewed),
                NewCardsLearned = VALUES(NewCardsLearned),
                VocabularyCompletedCount = VALUES(VocabularyCompletedCount),
                QuizzesCompleted = VALUES(QuizzesCompleted),
                SpeakingCompletedCount = VALUES(SpeakingCompletedCount),
                WritingCompletedCount = VALUES(WritingCompletedCount),
                ReadingCompletedCount = VALUES(ReadingCompletedCount),
                ListeningCompletedCount = VALUES(ListeningCompletedCount),
                DailyChallengeCompleted = VALUES(DailyChallengeCompleted)
            """;
        upsert.Parameters.AddWithValue("@userId", user.UserId);
        upsert.Parameters.AddWithValue("@day", day.Date);
        upsert.Parameters.AddWithValue("@xp", xp);
        upsert.Parameters.AddWithValue("@streakXp", streakXp);
        upsert.Parameters.AddWithValue("@cards", cards);
        upsert.Parameters.AddWithValue("@newCards", newCards);
        upsert.Parameters.AddWithValue("@vocab", vocabDone);
        upsert.Parameters.AddWithValue("@quizzes", quizzes);
        upsert.Parameters.AddWithValue("@speaking", speaking);
        upsert.Parameters.AddWithValue("@writing", writing);
        upsert.Parameters.AddWithValue("@reading", reading);
        upsert.Parameters.AddWithValue("@listening", listening);
        upsert.Parameters.AddWithValue("@challenge", dayIndex % 3 == 0);
        await upsert.ExecuteNonQueryAsync();

        await using var idCmd = db.CreateCommand();
        idCmd.Transaction = tx;
        idCmd.CommandText = "SELECT Id FROM UserDailyActivities WHERE UserID = @userId AND ActivityDate = @day";
        idCmd.Parameters.AddWithValue("@userId", user.UserId);
        idCmd.Parameters.AddWithValue("@day", day.Date);
        seededDailyIds.Add(Convert.ToInt32(await idCmd.ExecuteScalarAsync()));
    }

    var weekMondayOffset = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
    var weekStart = today.AddDays(-weekMondayOffset);

    if (prior?.FeatureTimeLogIds is { Count: > 0 } priorFeatureIds)
    {
        await using var del = db.CreateCommand();
        del.Transaction = tx;
        del.CommandText = $"DELETE FROM UserFeatureTimeLogs WHERE UserId = @userId AND Id IN ({string.Join(",", priorFeatureIds.Select((_, i) => "@f" + i))})";
        del.Parameters.AddWithValue("@userId", user.UserId);
        for (var i = 0; i < priorFeatureIds.Count; i++)
            del.Parameters.AddWithValue("@f" + i, priorFeatureIds[i]);
        await del.ExecuteNonQueryAsync();
    }
    else
    {
        await using var del = db.CreateCommand();
        del.Transaction = tx;
        del.CommandText = """
            DELETE FROM UserFeatureTimeLogs
            WHERE UserId = @userId AND LoggedDate >= @weekStart AND LoggedDate <= @today
            """;
        del.Parameters.AddWithValue("@userId", user.UserId);
        del.Parameters.AddWithValue("@weekStart", weekStart.Date);
        del.Parameters.AddWithValue("@today", today.Date);
        await del.ExecuteNonQueryAsync();
    }

    var features = new[] { "Flashcard", "Quiz", "Speaking", "Reading", "Listening", "Writing", "Grammar" };
    var featureIds = new List<int>();
    for (var d = 0; d <= weekMondayOffset; d++)
    {
        var day = weekStart.AddDays(d);
        var activeFeatures = features.Where((_, idx) => (d + idx) % 3 != 2).Take(4).ToArray();
        if (activeFeatures.Length == 0)
            activeFeatures = ["Flashcard"];

        foreach (var feature in activeFeatures)
        {
            var minutes = 8 + ((d * 7 + feature.Length * 3) % 28);
            if (day == today && feature == "Flashcard")
                minutes = Math.Max(minutes, 18);

            await using var insert = db.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO UserFeatureTimeLogs (UserId, Feature, DurationSeconds, LoggedDate)
                VALUES (@userId, @feature, @seconds, @day)
                """;
            insert.Parameters.AddWithValue("@userId", user.UserId);
            insert.Parameters.AddWithValue("@feature", feature);
            insert.Parameters.AddWithValue("@seconds", minutes * 60);
            insert.Parameters.AddWithValue("@day", day.Date);
            await insert.ExecuteNonQueryAsync();
            featureIds.Add((int)insert.LastInsertedId);
        }
    }

    var masteredTake = Math.Min(18, cardIds.Count);
    var learningTake = Math.Min(14, Math.Max(0, cardIds.Count - masteredTake));
    var reviewingTake = Math.Min(6, Math.Max(0, cardIds.Count - masteredTake - learningTake));
    var progressPlan = new List<(int CardId, string Status, int Reps)>();
    for (var i = 0; i < masteredTake; i++)
        progressPlan.Add((cardIds[i], "Mastered", 4 + (i % 3)));
    for (var i = 0; i < learningTake; i++)
        progressPlan.Add((cardIds[masteredTake + i], "Learning", 1 + (i % 2)));
    for (var i = 0; i < reviewingTake; i++)
        progressPlan.Add((cardIds[masteredTake + learningTake + i], "Reviewing", 2 + (i % 2)));

    var progressIds = new List<int>();
    foreach (var (cardId, status, reps) in progressPlan)
    {
        var lastReviewed = today.AddDays(-(reps % 5)).AddHours(10);
        var nextReview = status == "Mastered" ? today.AddDays(7) : today.AddDays(1);
        var wrong = status == "Mastered" ? 0 : 1 + (reps % 2);

        await using var upsert = db.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = """
            INSERT INTO LearningProgress
                (UserID, CardID, Status, WrongCount, RepetitionCount, LastReviewedDate, NextReviewDate, IsSuspended)
            VALUES
                (@userId, @cardId, @status, @wrong, @reps, @last, @next, 0)
            ON DUPLICATE KEY UPDATE
                Status = VALUES(Status),
                WrongCount = VALUES(WrongCount),
                RepetitionCount = VALUES(RepetitionCount),
                LastReviewedDate = VALUES(LastReviewedDate),
                NextReviewDate = VALUES(NextReviewDate),
                IsSuspended = 0
            """;
        upsert.Parameters.AddWithValue("@userId", user.UserId);
        upsert.Parameters.AddWithValue("@cardId", cardId);
        upsert.Parameters.AddWithValue("@status", status);
        upsert.Parameters.AddWithValue("@wrong", wrong);
        upsert.Parameters.AddWithValue("@reps", reps);
        upsert.Parameters.AddWithValue("@last", lastReviewed);
        upsert.Parameters.AddWithValue("@next", nextReview);
        await upsert.ExecuteNonQueryAsync();

        await using var idCmd = db.CreateCommand();
        idCmd.Transaction = tx;
        idCmd.CommandText = "SELECT ProgressID FROM LearningProgress WHERE UserID = @userId AND CardID = @cardId";
        idCmd.Parameters.AddWithValue("@userId", user.UserId);
        idCmd.Parameters.AddWithValue("@cardId", cardId);
        progressIds.Add(Convert.ToInt32(await idCmd.ExecuteScalarAsync()));
    }

    var goalSpecs = new (int Area, int Target)[] { (1, 20), (2, 1), (4, 1) }; // Vocabulary, Speaking, Reading
    var goalIds = new List<int>();
    var createdAt = DateTime.UtcNow.AddDays(-HistoryDays);
    var updatedAt = DateTime.UtcNow;
    foreach (var (area, target) in goalSpecs)
    {
        await using var upsert = db.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = """
            INSERT INTO UserGoals (UserID, GoalArea, TargetValue, IsActive, CreatedAt, UpdatedAt)
            VALUES (@userId, @area, @target, 1, @created, @updated)
            ON DUPLICATE KEY UPDATE
                TargetValue = VALUES(TargetValue),
                IsActive = 1,
                UpdatedAt = VALUES(UpdatedAt)
            """;
        upsert.Parameters.AddWithValue("@userId", user.UserId);
        upsert.Parameters.AddWithValue("@area", area);
        upsert.Parameters.AddWithValue("@target", target);
        upsert.Parameters.AddWithValue("@created", createdAt);
        upsert.Parameters.AddWithValue("@updated", updatedAt);
        await upsert.ExecuteNonQueryAsync();

        await using var idCmd = db.CreateCommand();
        idCmd.Transaction = tx;
        idCmd.CommandText = "SELECT Id FROM UserGoals WHERE UserID = @userId AND GoalArea = @area";
        idCmd.Parameters.AddWithValue("@userId", user.UserId);
        idCmd.Parameters.AddWithValue("@area", area);
        goalIds.Add(Convert.ToInt32(await idCmd.ExecuteScalarAsync()));
    }

    int readingHistoryId;
    bool readingWasCreated;
    var viewedAt = DateTime.UtcNow.AddHours(-2);
    if (existingReadingId is int existingId)
    {
        await using var update = db.CreateCommand();
        update.Transaction = tx;
        update.CommandText = """
            UPDATE UserReadingHistories
            SET ViewedAt = @viewedAt, IsCompleted = 0, Score = NULL
            WHERE Id = @id AND UserId = @userId
            """;
        update.Parameters.AddWithValue("@viewedAt", viewedAt);
        update.Parameters.AddWithValue("@id", existingId);
        update.Parameters.AddWithValue("@userId", user.UserId);
        await update.ExecuteNonQueryAsync();
        readingHistoryId = existingId;
        readingWasCreated = false;
    }
    else
    {
        await using var insert = db.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO UserReadingHistories (UserId, ReadingPassageId, ViewedAt, IsCompleted, Score)
            VALUES (@userId, @passageId, @viewedAt, 0, NULL)
            """;
        insert.Parameters.AddWithValue("@userId", user.UserId);
        insert.Parameters.AddWithValue("@passageId", publishedPassageId);
        insert.Parameters.AddWithValue("@viewedAt", viewedAt);
        await insert.ExecuteNonQueryAsync();
        readingHistoryId = (int)insert.LastInsertedId;
        readingWasCreated = true;
    }

    var longest = Math.Max(userBackup.LongestStreak ?? 0, 21);
    await using (var updateUser = db.CreateCommand())
    {
        updateUser.Transaction = tx;
        updateUser.CommandText = """
            UPDATE Users
            SET Streak = @streak,
                LongestStreak = @longest,
                LastStudyDate = @lastStudy,
                Goal = @goal
            WHERE UserId = @userId
            """;
        updateUser.Parameters.AddWithValue("@streak", StreakDays);
        updateUser.Parameters.AddWithValue("@longest", longest);
        updateUser.Parameters.AddWithValue("@lastStudy", today);
        updateUser.Parameters.AddWithValue("@goal", 20);
        updateUser.Parameters.AddWithValue("@userId", user.UserId);
        await updateUser.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();

    var receipt = new Receipt(
        SeedMarker,
        DateTime.UtcNow,
        user.UserId,
        LearnerFullName,
        today,
        windowStart,
        activityDates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList(),
        seededDailyIds.Distinct().OrderBy(x => x).ToList(),
        featureIds.Distinct().OrderBy(x => x).ToList(),
        progressIds.Distinct().OrderBy(x => x).ToList(),
        goalIds.Distinct().OrderBy(x => x).ToList(),
        readingHistoryId,
        readingWasCreated,
        userBackup);

    await File.WriteAllTextAsync(receiptPath, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        action = "apply",
        marker = SeedMarker,
        userId = user.UserId,
        fullName = user.FullName,
        businessDate = today.ToString("yyyy-MM-dd"),
        activityDays = activityDates.Count,
        streak = StreakDays,
        dailyRows = seededDailyIds.Count,
        featureLogs = featureIds.Count,
        learningProgress = progressIds.Count,
        goals = goalIds.Count,
        readingHistoryId,
        receipt = receiptPath
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task RemoveAsync(MySqlConnection db, LearnerRow user, string receiptPath, DateTime today)
{
    if (!File.Exists(receiptPath))
        throw new InvalidOperationException($"No receipt at {receiptPath}. Nothing safe to remove.");

    var receipt = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath))
        ?? throw new InvalidOperationException("Receipt is empty or invalid.");
    if (!string.Equals(receipt.Marker, SeedMarker, StringComparison.Ordinal) || receipt.UserId != user.UserId)
        throw new InvalidOperationException("Receipt does not match Anh Hoàng local sample marker/user.");

    await using var tx = await db.BeginTransactionAsync();

    async Task DeleteByIdsAsync(string table, string idColumn, string userColumn, IReadOnlyList<int> ids)
    {
        if (ids.Count == 0) return;
        await using var del = db.CreateCommand();
        del.Transaction = tx;
        var paramNames = ids.Select((_, i) => "@id" + i).ToArray();
        del.CommandText = $"DELETE FROM {table} WHERE {userColumn} = @userId AND {idColumn} IN ({string.Join(",", paramNames)})";
        del.Parameters.AddWithValue("@userId", user.UserId);
        for (var i = 0; i < ids.Count; i++)
            del.Parameters.AddWithValue(paramNames[i], ids[i]);
        await del.ExecuteNonQueryAsync();
    }

    await DeleteByIdsAsync("UserDailyActivities", "Id", "UserID", receipt.DailyActivityIds);
    await DeleteByIdsAsync("UserFeatureTimeLogs", "Id", "UserId", receipt.FeatureTimeLogIds);
    await DeleteByIdsAsync("LearningProgress", "ProgressID", "UserID", receipt.LearningProgressIds);
    await DeleteByIdsAsync("UserGoals", "Id", "UserID", receipt.GoalIds);

    if (receipt.ReadingHistoryCreated)
    {
        await using var del = db.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM UserReadingHistories WHERE Id = @id AND UserId = @userId";
        del.Parameters.AddWithValue("@id", receipt.ReadingHistoryId);
        del.Parameters.AddWithValue("@userId", user.UserId);
        await del.ExecuteNonQueryAsync();
    }

    await using (var updateUser = db.CreateCommand())
    {
        updateUser.Transaction = tx;
        updateUser.CommandText = """
            UPDATE Users
            SET Streak = @streak,
                LongestStreak = @longest,
                LastStudyDate = @lastStudy,
                Goal = @goal
            WHERE UserId = @userId
            """;
        updateUser.Parameters.AddWithValue("@streak", (object?)receipt.UserBackup.Streak ?? DBNull.Value);
        updateUser.Parameters.AddWithValue("@longest", (object?)receipt.UserBackup.LongestStreak ?? DBNull.Value);
        updateUser.Parameters.AddWithValue("@lastStudy", (object?)receipt.UserBackup.LastStudyDate ?? DBNull.Value);
        updateUser.Parameters.AddWithValue("@goal", (object?)receipt.UserBackup.Goal ?? DBNull.Value);
        updateUser.Parameters.AddWithValue("@userId", user.UserId);
        await updateUser.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();
    File.Delete(receiptPath);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        action = "remove",
        userId = user.UserId,
        removedDaily = receipt.DailyActivityIds.Count,
        removedFeatureLogs = receipt.FeatureTimeLogIds.Count,
        removedProgress = receipt.LearningProgressIds.Count,
        removedGoals = receipt.GoalIds.Count,
        readingHistoryDeleted = receipt.ReadingHistoryCreated,
        businessDate = today.ToString("yyyy-MM-dd")
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static async Task PrintStatusAsync(MySqlConnection db, LearnerRow user, int ownedCards, DateTime today)
{
    await using var cmd = db.CreateCommand();
    cmd.CommandText = """
        SELECT
            (SELECT COUNT(*) FROM UserDailyActivities WHERE UserID = @userId) AS DailyRows,
            (SELECT COALESCE(SUM(XpEarned), 0) FROM UserDailyActivities WHERE UserID = @userId) AS TotalXp,
            (SELECT COALESCE(SUM(XpEarned), 0) FROM UserDailyActivities WHERE UserID = @userId AND ActivityDate = @today) AS TodayXp,
            (SELECT COUNT(*) FROM UserFeatureTimeLogs WHERE UserId = @userId) AS FeatureLogs,
            (SELECT COUNT(*) FROM LearningProgress WHERE UserID = @userId AND Status = 'Mastered') AS Mastered,
            (SELECT COUNT(*) FROM LearningProgress WHERE UserID = @userId AND Status IN ('Learning','Reviewing')) AS Learning,
            (SELECT COUNT(*) FROM UserGoals WHERE UserID = @userId AND IsActive = 1) AS Goals,
            (SELECT Id FROM UserReadingHistories WHERE UserId = @userId AND IsCompleted = 0 ORDER BY ViewedAt DESC LIMIT 1) AS ReadingId,
            (SELECT ReadingPassageId FROM UserReadingHistories WHERE UserId = @userId AND IsCompleted = 0 ORDER BY ViewedAt DESC LIMIT 1) AS PassageId,
            (SELECT Streak FROM Users WHERE UserId = @userId) AS Streak,
            (SELECT LongestStreak FROM Users WHERE UserId = @userId) AS LongestStreak,
            (SELECT LastStudyDate FROM Users WHERE UserId = @userId) AS LastStudyDate,
            (SELECT Goal FROM Users WHERE UserId = @userId) AS Goal
        """;
    cmd.Parameters.AddWithValue("@userId", user.UserId);
    cmd.Parameters.AddWithValue("@today", today.Date);
    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        action = "status",
        userId = user.UserId,
        fullName = user.FullName,
        streak = reader["Streak"],
        longestStreak = reader["LongestStreak"],
        lastStudyDate = reader["LastStudyDate"] is DBNull ? null : reader["LastStudyDate"],
        goal = reader["Goal"],
        ownedCards,
        dailyActivityRows = reader.GetInt32("DailyRows"),
        totalXp = Convert.ToInt32(reader["TotalXp"]),
        todayXp = Convert.ToInt32(reader["TodayXp"]),
        featureTimeLogs = reader.GetInt32("FeatureLogs"),
        mastered = reader.GetInt32("Mastered"),
        learningOrReviewing = reader.GetInt32("Learning"),
        activeGoals = reader.GetInt32("Goals"),
        inProgressReadingId = reader["ReadingId"] is DBNull ? null : reader["ReadingId"],
        inProgressPassageId = reader["PassageId"] is DBNull ? null : reader["PassageId"],
        businessDate = today.ToString("yyyy-MM-dd")
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

sealed record LearnerRow(int UserId, string FullName, int? Streak, int? LongestStreak, DateTime? LastStudyDate, int? Goal);
sealed record UserBackup(int? Streak, int? LongestStreak, DateTime? LastStudyDate, int? Goal);
sealed record Receipt(
    string Marker,
    DateTime AppliedAtUtc,
    int UserId,
    string FullName,
    DateTime BusinessDate,
    DateTime WindowStart,
    List<string> ActivityDates,
    List<int> DailyActivityIds,
    List<int> FeatureTimeLogIds,
    List<int> LearningProgressIds,
    List<int> GoalIds,
    int ReadingHistoryId,
    bool ReadingHistoryCreated,
    UserBackup UserBackup);
