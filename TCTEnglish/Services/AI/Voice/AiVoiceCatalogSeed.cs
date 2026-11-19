using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Voice;

public static class AiVoiceCatalogSeed
{
    public static async Task SeedAsync(DbflashcardContext db, CancellationToken ct = default)
    {
        var topics = new[]
        {
            Topic("introductions", "Làm quen", "Meet a new colleague. Introduce yourself and learn their name, job and interests.", "New colleague", "New team member", "A1", 1),
            Topic("cafe", "Gọi món ở quán cà phê", "Order a drink, ask about sizes and prices, and politely confirm your order.", "Barista", "Customer", "A1", 2),
            Topic("travel", "Hỏi đường khi du lịch", "Find a local attraction. Ask for directions and confirm the route and travel time.", "Local resident", "Visitor", "A2", 3),
            Topic("daily-routine", "Một ngày của bạn", "Compare your daily routines and explain what you enjoy about your day.", "Friendly classmate", "English learner", "A2", 4),
            Topic("hobbies", "Sở thích cuối tuần", "Discuss hobbies and suggest a shared activity for next weekend.", "Friend", "Friend", "B1", 5),
            Topic("interview", "Phỏng vấn công việc", "Practise an interview. Discuss strengths, experience and one challenging work situation.", "Interviewer", "Job applicant", "B2", 6)
        };
        foreach (var topic in topics)
            if (!await db.AiVoiceCatalogTopics.AnyAsync(x => x.StableKey == topic.StableKey, ct)) db.AiVoiceCatalogTopics.Add(topic);
        await db.SaveChangesAsync(ct);
    }
    private static AiVoiceCatalogTopic Topic(string key, string title, string scenario, string ai, string learner, string level, int order)
        => new() { StableKey = "voice." + key, Title = title, Scenario = scenario, AiRole = ai, LearnerRole = learner, Level = level, IsPublished = true, SortOrder = order };
}
