using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTVocabulary.Models;

public static class ListeningLessonSeedData
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        if (await context.ListeningLessons.AnyAsync())
        {
            return;
        }

        var lessons = GetSeedData();
        context.ListeningLessons.AddRange(lessons);
        await context.SaveChangesAsync();
    }

    private static List<ListeningLesson> GetSeedData()
    {
        var now = DateTime.UtcNow;
        return new List<ListeningLesson>
        {
            // A1 - 1
            new ListeningLesson
            {
                Title = "Ordering Food at a Restaurant",
                Level = "A1",
                Topic = "Food",
                YoutubeId = "9F8o8eH_Hms", // BBC Learning English: Ordering Food
                Duration = "4:15",
                Speaker1Name = "Waiter", Speaker1Country = "UK",
                Speaker2Name = "Customer", Speaker2Country = "US",
                IsPublished = true,
                CreatedAt = now,
                TranscriptLines = new List<ListeningTranscriptLine>
                {
                    new() { OrderIndex = 1, Speaker = "Waiter", Text = "Good evening! Welcome to our restaurant.", VietnameseMeaning = "Chào buổi tối! Chào mừng bạn đến nhà hàng của chúng tôi." },
                    new() { OrderIndex = 2, Speaker = "Customer", Text = "Hello! Can I see the menu, please?", VietnameseMeaning = "Xin chào! Cho tôi xem thực đơn được không?" },
                    new() { OrderIndex = 3, Speaker = "Waiter", Text = "Certainly. Here you are.", VietnameseMeaning = "Chắc chắn rồi. Của bạn đây." },
                    new() { OrderIndex = 4, Speaker = "Customer", Text = "Thank you. What is the soup of the day?", VietnameseMeaning = "Cảm ơn. Súp của ngày hôm nay là gì?" },
                    new() { OrderIndex = 5, Speaker = "Waiter", Text = "It's tomato soup. It's very fresh.", VietnameseMeaning = "Đó là súp cà chua. Nó rất tươi." },
                    new() { OrderIndex = 6, Speaker = "Customer", Text = "Great. I'll have the soup and the grilled chicken.", VietnameseMeaning = "Tuyệt. Tôi sẽ lấy súp và gà nướng." },
                    new() { OrderIndex = 7, Speaker = "Waiter", Text = "Excellent choice. Would you like anything to drink?", VietnameseMeaning = "Lựa chọn tuyệt vời. Bạn có muốn dùng đồ uống gì không?" },
                    new() { OrderIndex = 8, Speaker = "Customer", Text = "Just water for now, thanks.", VietnameseMeaning = "Hiện tại chỉ nước lọc thôi, cảm ơn." }
                },
                QuizQuestions = new List<ListeningQuizQuestion>
                {
                    new() { OrderIndex = 1, QuestionText = "Where is the customer?", OptionA = "At a park", OptionB = "At a restaurant", OptionC = "At a library", OptionD = "At home", CorrectAnswer = "B" },
                    new() { OrderIndex = 2, QuestionText = "What is the soup of the day?", OptionA = "Onion", OptionB = "Mushroom", OptionC = "Tomato", OptionD = "Potato", CorrectAnswer = "C" },
                    new() { OrderIndex = 3, QuestionText = "What main dish does the customer order?", OptionA = "Fish", OptionB = "Grilled chicken", OptionC = "Pizza", OptionD = "Pasta", CorrectAnswer = "B" }
                },
                VocabItems = new List<ListeningVocabItem>
                {
                    new() { OrderIndex = 1, Word = "Menu", Definition = "A list of food and drinks in a restaurant", ExampleSentence = "He looked at the menu for a long time." },
                    new() { OrderIndex = 2, Word = "Certainly", Definition = "Without doubt; used to agree to something", ExampleSentence = "Can you help me? Certainly." }
                }
            },

            // A2 - 1
            new ListeningLesson
            {
                Title = "Talking About Summer Hobbies",
                Level = "A2",
                Topic = "Daily Life",
                YoutubeId = "oZ_M8u_v-3Y", // Simple English: Hobbies
                Duration = "3:45",
                Speaker1Name = "John", Speaker1Country = "AU",
                Speaker2Name = "Emma", Speaker2Country = "GB",
                IsPublished = true,
                CreatedAt = now,
                TranscriptLines = new List<ListeningTranscriptLine>
                {
                    new() { OrderIndex = 1, Speaker = "John", Text = "Hey Emma, what do you usually do in the summer?", VietnameseMeaning = "Chào Emma, bạn thường làm gì vào mùa hè?" },
                    new() { OrderIndex = 2, Speaker = "Emma", Text = "I love outdoor activities. I often go swimming or hiking.", VietnameseMeaning = "Tôi thích các hoạt động ngoài trời. Tôi thường đi bơi hoặc leo núi." },
                    new() { OrderIndex = 3, Speaker = "John", Text = "That sounds fun! I prefer reading books in the garden.", VietnameseMeaning = "Nghe có vẻ vui! Tôi thích đọc sách trong vườn hơn." },
                    new() { OrderIndex = 4, Speaker = "Emma", Text = "Do you ever go camping?", VietnameseMeaning = "Bạn có bao giờ đi cắm trại không?" },
                    new() { OrderIndex = 5, Speaker = "John", Text = "Sometimes, but only if the weather is nice.", VietnameseMeaning = "Thỉnh thoảng, nhưng chỉ khi thời tiết đẹp." }
                },
                QuizQuestions = new List<ListeningQuizQuestion>
                {
                    new() { OrderIndex = 1, QuestionText = "What does Emma like doing in summer?", OptionA = "Sleeping", OptionB = "Outdoor activities", OptionC = "Cooking", OptionD = "Shopping", CorrectAnswer = "B" },
                    new() { OrderIndex = 2, QuestionText = "Where does John like to read books?", OptionA = "In his bedroom", OptionB = "In the library", OptionC = "In the garden", OptionD = "At the beach", CorrectAnswer = "C" }
                },
                VocabItems = new List<ListeningVocabItem>
                {
                    new() { OrderIndex = 1, Word = "Prefer", Definition = "To like one thing more than another", ExampleSentence = "I prefer tea to coffee." },
                    new() { OrderIndex = 2, Word = "Hiking", Definition = "The activity of going for long walks in the country", ExampleSentence = "We went hiking in the mountains." }
                }
            },

            // B1 - 1
            new ListeningLesson
            {
                Title = "The Importance of Recycling",
                Level = "B1",
                Topic = "Environment",
                YoutubeId = "VmlcKyXBR_Q", // Environment / Recycling
                Duration = "5:20",
                Speaker1Name = "Dr. Green", Speaker1Country = "CA",
                Speaker2Name = "Student", Speaker2Country = "US",
                IsPublished = true,
                CreatedAt = now,
                TranscriptLines = new List<ListeningTranscriptLine>
                {
                    new() { OrderIndex = 1, Speaker = "Student", Text = "Dr. Green, why is recycling so important for our planet?", VietnameseMeaning = "Thưa Tiến sĩ Green, tại sao tái chế lại quan trọng đối với hành tinh của chúng ta?" },
                    new() { OrderIndex = 2, Speaker = "Dr. Green", Text = "Well, recycling reduces the need for raw materials and saves energy.", VietnameseMeaning = "À, tái chế giúp giảm nhu cầu về nguyên liệu thô và tiết kiệm năng lượng." },
                    new() { OrderIndex = 3, Speaker = "Student", Text = "Which materials are the easiest to recycle?", VietnameseMeaning = "Những loại vật liệu nào dễ tái chế nhất?" },
                    new() { OrderIndex = 4, Speaker = "Dr. Green", Text = "Paper, glass, and aluminum are excellent examples.", VietnameseMeaning = "Giấy, thủy tinh và nhôm là những ví dụ điển hình." }
                },
                QuizQuestions = new List<ListeningQuizQuestion>
                {
                    new() { OrderIndex = 1, QuestionText = "What is one benefit of recycling?", OptionA = "It costs more money", OptionB = "It saves energy", OptionC = "It uses more raw materials", OptionD = "It makes more trash", CorrectAnswer = "B" }
                },
                VocabItems = new List<ListeningVocabItem>
                {
                    new() { OrderIndex = 1, Word = "Environment", Definition = "The natural world, as a whole or in a particular area", ExampleSentence = "We must protect the environment." },
                    new() { OrderIndex = 2, Word = "Recycling", Definition = "The process of converting waste materials into new materials", ExampleSentence = "Recycling paper saves trees." }
                }
            },

            // B2 - 1
            new ListeningLesson
            {
                Title = "Work-Life Balance in the Digital Age",
                Level = "B2",
                Topic = "Careers",
                YoutubeId = "I-A5p7YQ8Qk", // Work life balance
                Duration = "6:10",
                Speaker1Name = "HR Manager", Speaker1Country = "IE",
                Speaker2Name = "Employee", Speaker2Country = "US",
                IsPublished = true,
                CreatedAt = now,
                TranscriptLines = new List<ListeningTranscriptLine>
                {
                    new() { OrderIndex = 1, Speaker = "HR Manager", Text = "In today's connected world, many find it hard to disconnect from work.", VietnameseMeaning = "Trong thế giới kết nối ngày nay, nhiều người thấy khó có thể ngắt kết nối với công việc." }
                },
                QuizQuestions = new List<ListeningQuizQuestion>
                {
                    new() { OrderIndex = 1, QuestionText = "Why do people find it hard to disconnect?", OptionA = "They have no phones", OptionB = "The world is very connected", OptionC = "They don't like weekends", OptionD = "Work is boring", CorrectAnswer = "B" }
                },
                VocabItems = new List<ListeningVocabItem>
                {
                    new() { OrderIndex = 1, Word = "Productivity", Definition = "The effectiveness of productive effort", ExampleSentence = "Higher productivity leads to better results." }
                }
            },

            // C1 - 1
            new ListeningLesson
            {
                Title = "The Ethics of Artificial Intelligence",
                Level = "C1",
                Topic = "Technology",
                YoutubeId = "reUZRyXxUs4", // AI ethics
                Duration = "8:30",
                Speaker1Name = "Professor", Speaker1Country = "US",
                Speaker2Name = "Interviewer", Speaker2Country = "GB",
                IsPublished = true,
                CreatedAt = now,
                TranscriptLines = new List<ListeningTranscriptLine>
                {
                    new() { OrderIndex = 1, Speaker = "Interviewer", Text = "Professor, how do we ensure AI remains beneficial to humanity?", VietnameseMeaning = "Thưa Giáo sư, làm thế nào để chúng ta đảm bảo AI vẫn có lợi cho nhân loại?" }
                },
                QuizQuestions = new List<ListeningQuizQuestion>
                {
                    new() { OrderIndex = 1, QuestionText = "What is a main concern with AI development?", OptionA = "It's too cheap", OptionB = "It might be unethical", OptionC = "Nobody uses it", OptionD = "It's too slow", CorrectAnswer = "B" }
                },
                VocabItems = new List<ListeningVocabItem>
                {
                    new() { OrderIndex = 1, Word = "Unprecedented", Definition = "Never done or known before", ExampleSentence = "This discovery is unprecedented in modern history." }
                }
            }
        }.Concat(GenerateMoreLessons(now)).ToList();
    }

    private static List<ListeningLesson> GenerateMoreLessons(DateTime now)
    {
        var extra = new List<ListeningLesson>();
        string[] levels = { "A1", "A2", "B1", "B2", "C1" };
        string[] topics = { "Travel", "Media", "Science", "Culture", "Health" };
        string[] ytIds = { "dQw4w9WgXcQ", "jNQXAC9IVRw", "9bZkp7q19f0", "2Vv-BfVoq4g", "OPf0YbXqDm0", "rYEDA3JcQqw", "7ghp4ZkDs6Y", "JGwWNGJdvx8", "H6u0VBqNBQ8", "YQHsXMglC9A" };

        for (int i = 0; i < 10; i++)
        {
            var level = levels[i % 5];
            extra.Add(new ListeningLesson
            {
                Title = $"{topics[i % 5]} Discussion - Part {i / 5 + 1}",
                Level = level,
                Topic = topics[i % 5],
                YoutubeId = ytIds[i],
                Duration = "5:00",
                Speaker1Name = "Host", Speaker1Country = "US",
                Speaker2Name = "Guest", Speaker2Country = "CA",
                IsPublished = true,
                CreatedAt = now.AddMinutes(-i),
                TranscriptLines = new List<ListeningTranscriptLine> {
                    new() { OrderIndex = 1, Speaker = "Host", Text = "Welcome back to our weekly discussion.", VietnameseMeaning = "Chào mừng bạn quay lại với buổi thảo luận hàng tuần của chúng tôi." },
                    new() { OrderIndex = 2, Speaker = "Guest", Text = "It's a pleasure to be here today.", VietnameseMeaning = "Thật vinh dự khi được ở đây hôm nay." }
                },
                QuizQuestions = new List<ListeningQuizQuestion> {
                    new() { OrderIndex = 1, QuestionText = "Is the guest happy to be there?", OptionA = "Yes", OptionB = "No", OptionC = "Maybe", OptionD = "Not mentioned", CorrectAnswer = "A" }
                },
                VocabItems = new List<ListeningVocabItem> {
                    new() { OrderIndex = 1, Word = "Discussion", Definition = "A conversation about a specific topic", ExampleSentence = "We had a long discussion about the future." }
                }
            });
        }
        return extra;
    }
}
