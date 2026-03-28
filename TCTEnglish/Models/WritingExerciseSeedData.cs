using System;
using System.Collections.Generic;
using System.Linq;

namespace TCTVocabulary.Models;

public static class WritingExerciseSeedData
{
    private const string BeginnerLevel = "beginner";
    private const string EmailContentType = "emails";
    private static readonly DateTime SeedCreatedAt = new(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<WritingExerciseSeedDefinition> SeedExercises { get; } = BuildExercises();
    private static IReadOnlyList<WritingExerciseSentenceSeedRow> SeedSentences { get; } = BuildSentences();

    public static object[,] GetExerciseRows()
    {
        var rows = new object[SeedExercises.Count, 8];

        for (var index = 0; index < SeedExercises.Count; index++)
        {
            var exercise = SeedExercises[index];
            rows[index, 0] = exercise.Id;
            rows[index, 1] = exercise.Title;
            rows[index, 2] = BeginnerLevel;
            rows[index, 3] = EmailContentType;
            rows[index, 4] = exercise.Topic;
            rows[index, 5] = exercise.PreviewText;
            rows[index, 6] = true;
            rows[index, 7] = SeedCreatedAt;
        }

        return rows;
    }

    public static object[,] GetSentenceRows()
    {
        var rows = new object[SeedSentences.Count, 6];

        for (var index = 0; index < SeedSentences.Count; index++)
        {
            var sentence = SeedSentences[index];
            rows[index, 0] = sentence.Id;
            rows[index, 1] = sentence.WritingExerciseId;
            rows[index, 2] = sentence.SortOrder;
            rows[index, 3] = sentence.VietnameseText;
            rows[index, 4] = sentence.EnglishMeaning;
            rows[index, 5] = sentence.BreakAfter;
        }

        return rows;
    }

    public static List<WritingExercise> CreateExercises()
    {
        return SeedExercises
            .Select(exercise => new WritingExercise
            {
                Id = exercise.Id,
                Title = exercise.Title,
                Level = BeginnerLevel,
                ContentType = EmailContentType,
                Topic = exercise.Topic,
                PreviewText = exercise.PreviewText,
                IsPublished = true,
                CreatedAt = SeedCreatedAt
            })
            .ToList();
    }

    public static List<WritingExerciseSentence> CreateSentences()
    {
        return SeedSentences
            .Select(sentence => new WritingExerciseSentence
            {
                Id = sentence.Id,
                WritingExerciseId = sentence.WritingExerciseId,
                SortOrder = sentence.SortOrder,
                VietnameseText = sentence.VietnameseText,
                EnglishMeaning = sentence.EnglishMeaning,
                BreakAfter = sentence.BreakAfter
            })
            .ToList();
    }

    private static IReadOnlyList<WritingExerciseSentenceSeedRow> BuildSentences()
    {
        var rows = new List<WritingExerciseSentenceSeedRow>();
        var nextId = 1;

        foreach (var exercise in SeedExercises)
        {
            for (var index = 0; index < exercise.Sentences.Count; index++)
            {
                var sentence = exercise.Sentences[index];
                rows.Add(new WritingExerciseSentenceSeedRow(
                    nextId++,
                    exercise.Id,
                    index + 1,
                    sentence.VietnameseText,
                    sentence.EnglishMeaning,
                    sentence.BreakAfter));
            }
        }

        return rows;
    }

    private static IReadOnlyList<WritingExerciseSeedDefinition> BuildExercises()
    {
        return new List<WritingExerciseSeedDefinition>
        {
            new(
                1,
                "Just Checking In!",
                "Personal Check-In",
                "Translate a friendly personal email that asks about the weekend, checks on a project, and offers support.",
                new List<WritingSentenceSeedDefinition>
                {
                    new("Xin chào!", "Hello!"),
                    new("Tôi hy vọng bạn khỏe.", "I hope you are well.", true),
                    new("Tôi muốn biết bạn thế nào.", "I want to know how you are doing."),
                    new("Cuối tuần của bạn thế nào?", "How was your weekend?"),
                    new("Bạn có làm gì đặc biệt không?", "Did you do anything special?"),
                    new("Tôi đã có một chuyến đi bộ đường dài thú vị vào thứ Bảy.", "I went on an exciting hike on Saturday."),
                    new("Thật sảng khoái!", "It was so refreshing!", true),
                    new("Ngoài ra, tôi tò mò về dự án bạn đã đề cập tuần trước.", "Also, I am curious about the project you mentioned last week."),
                    new("Mọi việc thế nào rồi?", "How is everything going?"),
                    new("Nếu bạn cần bất kỳ sự trợ giúp nào, vui lòng cho tôi biết.", "If you need any help, please let me know."),
                    new("Tôi rất muốn hỗ trợ.", "I would be happy to help.", true),
                    new("Hãy cẩn thận và tôi mong nhận được hồi âm từ bạn!", "Take care, and I look forward to hearing from you!", true),
                    new("Trân trọng.", "Best regards.", true),
                    new("Emily", "Emily")
                }),
            new(
                2,
                "Catching Up and Checking In",
                "Project Follow-Up",
                "Practice a warm follow-up email about a marketing project, deadline, and next steps.",
                new List<WritingSentenceSeedDefinition>
                {
                    new("Xin chào Sarah,", "Hello Sarah,", true),
                    new("Tôi hy vọng bạn vẫn ổn.", "I hope you are still doing well."),
                    new("Tôi muốn kiểm tra dự án tiếp thị.", "I wanted to check in on the marketing project."),
                    new("Mọi việc diễn ra thế nào ở phía bạn?", "How are things going on your side?"),
                    new("Tôi nhận thấy chúng ta sắp có hạn chót.", "I noticed that our deadline is coming up soon."),
                    new("Bạn có cần hỗ trợ gì cho các nhiệm vụ của mình không?", "Do you need any help with your tasks?", true),
                    new("Sẽ rất tuyệt nếu chúng ta trò chuyện về các bước tiếp theo.", "It would be great if we could talk about the next steps."),
                    new("Tôi nghĩ chúng ta nên thống nhất mục tiêu.", "I think we should align on our goals."),
                    new("Hãy cho tôi biết thời gian rảnh của bạn trong tuần này.", "Please let me know when you are free this week."),
                    new("Tôi rất mong được nghe suy nghĩ của bạn.", "I look forward to hearing your thoughts.", true),
                    new("Mong nhận được phản hồi của bạn!", "I look forward to your reply!", true),
                    new("Trân trọng,", "Best regards,", true),
                    new("Michael", "Michael")
                }),
            new(
                3,
                "Holiday Plans: What's on Your Agenda?",
                "Holiday Plans",
                "Write a light email about holiday plans, family travel, and meeting before the break.",
                new List<WritingSentenceSeedDefinition>
                {
                    new("Xin chào Sarah,", "Hello Sarah,", true),
                    new("Tôi hy vọng bạn khỏe!", "I hope you are well!"),
                    new("Kỳ nghỉ sắp đến rồi.", "The holiday is coming soon."),
                    new("Bạn có kế hoạch gì cho thời gian này không?", "Do you have any plans for this time?"),
                    new("Tôi rất háo hức về kỳ nghỉ.", "I am very excited about the holiday."),
                    new("Tôi muốn đi thăm gia đình.", "I want to visit my family."),
                    new("Họ sống ở một thành phố khác.", "They live in another city.", true),
                    new("Còn bạn thì sao?", "How about you?"),
                    new("Bạn sẽ ở lại đây hay đi du lịch?", "Will you stay here or travel?"),
                    new("Tôi rất muốn nghe kế hoạch của bạn.", "I would love to hear about your plans."),
                    new("Có lẽ chúng ta có thể gặp nhau trước kỳ nghỉ?", "Maybe we can meet before the holiday?"),
                    new("Sẽ thật tuyệt nếu chúng ta gặp nhau và chia sẻ một chút niềm vui trong kỳ nghỉ.", "It would be great if we could meet and share some holiday joy.", true),
                    new("Giữ gìn sức khỏe nhé!", "Take care!", true),
                    new("Trân trọng,", "Best regards,", true),
                    new("Michael", "Michael")
                }),
            new(
                4,
                "Checking In: How Are You Today?",
                "Supportive Check-In",
                "Translate a supportive email that asks about progress, challenges, and a quick chat.",
                new List<WritingSentenceSeedDefinition>
                {
                    new("Xin chào Sarah,", "Hello Sarah,", true),
                    new("Tôi hy vọng hôm nay bạn khỏe!", "I hope you are well today!"),
                    new("Tôi muốn hỏi thăm bạn.", "I wanted to check in on you."),
                    new("Dự án của bạn thế nào rồi?", "How is your project going?"),
                    new("Tôi rất mong được nghe tin tức cập nhật của bạn.", "I would really like to hear your latest update."),
                    new("Bạn có gặp phải thách thức nào trong tuần này không?", "Have you faced any challenges this week?"),
                    new("Tôi biết giai đoạn cuối có thể rất khó khăn.", "I know the final stage can be very difficult.", true),
                    new("Ngoài ra, bạn có rảnh để trò chuyện nhanh trong tuần này không?", "Also, are you free for a quick chat this week?"),
                    new("Tôi rất muốn thảo luận về các bước tiếp theo của chúng ta.", "I would love to discuss our next steps."),
                    new("Hãy cho tôi biết thời gian nào phù hợp với bạn.", "Please let me know what time works for you.", true),
                    new("Tôi mong nhận được phản hồi của bạn!", "I look forward to your reply!", true),
                    new("Trân trọng,", "Best regards,", true),
                    new("Michael", "Michael")
                }),
            new(
                5,
                "Time for a Fun Weekend Adventure!",
                "Weekend Adventure",
                "Practice an informal email that suggests a trip to the lake and plans a picnic with friends.",
                new List<WritingSentenceSeedDefinition>
                {
                    new("Chào Emma,", "Hi Emma,", true),
                    new("Tôi hy vọng bạn khỏe!", "I hope you are well!"),
                    new("Tôi đang nghĩ rằng chúng ta nên lên kế hoạch cho một chuyến đi chơi cuối tuần sớm thôi.", "I am thinking that we should plan a weekend outing soon."),
                    new("Đã lâu rồi chúng ta không đi chơi cùng nhau.", "It has been a long time since we last hung out together."),
                    new("Bạn nghĩ sao về việc đi đến hồ?", "What do you think about going to the lake?"),
                    new("Chúng ta có thể tận hưởng chút nắng và đi dã ngoại.", "We could enjoy some sunshine and have a picnic."),
                    new("Bạn có muốn mời Jake và Mia đi cùng không?", "Do you want to invite Jake and Mia to come along?", true),
                    new("Chúng ta có thể đi vào thứ Bảy.", "We could go on Saturday."),
                    new("Chúng ta có thể bắt đầu sớm để có một vị trí tốt.", "We could start early to get a good spot."),
                    new("Bạn có ý tưởng nào thú vị cho bữa trưa không?", "Do you have any fun ideas for lunch?"),
                    new("Tôi có thể mang theo một ít đồ ăn nhẹ và đồ uống.", "I can bring some snacks and drinks."),
                    new("Hãy cho tôi biết bạn nghĩ gì nhé!", "Let me know what you think!", true),
                    new("Tôi rất vui khi được dành thời gian bên nhau.", "I am really happy to spend time together.", true),
                    new("Trân trọng,", "Best regards,", true),
                    new("John", "John")
                }),
            new(
                6,
                "A Heartfelt Thank You for Your Thoughtful Gift!",
                "Thank-You Note",
                "Translate a thank-you email about a thoughtful gift, a new office, and an invitation to visit.",
                new List<WritingSentenceSeedDefinition>
                {
                    new("Emily thân mến,", "Dear Emily,", true),
                    new("Tôi hy vọng tin nhắn này sẽ đến được với bạn.", "I hope this message reaches you."),
                    new("Tôi muốn cảm ơn bạn vì món quà đáng yêu này.", "I want to thank you for this lovely gift."),
                    new("Thật là một bất ngờ tuyệt vời!", "It was such a wonderful surprise!"),
                    new("Sự chu đáo của bạn có ý nghĩa rất lớn đối với tôi.", "Your thoughtfulness means a lot to me."),
                    new("Làm sao bạn biết tôi muốn món quà này?", "How did you know I wanted this gift?"),
                    new("Tôi thực sự thích nó!", "I really love it!", true),
                    new("Nó hoàn hảo cho văn phòng mới của tôi.", "It is perfect for my new office."),
                    new("Tôi đánh giá cao sự hào phóng và hỗ trợ của bạn.", "I appreciate your generosity and support."),
                    new("Bạn có muốn đến thăm không gian mới của tôi sớm không?", "Would you like to visit my new space soon?"),
                    new("Tôi rất muốn gặp lại và đưa bạn đi tham quan.", "I would love to see you again and show you around.", true),
                    new("Một lần nữa, cảm ơn bạn vì món quà tuyệt vời của bạn.", "Once again, thank you for your wonderful gift."),
                    new("Nó thực sự làm cho ngày của tôi tươi sáng hơn!", "It really made my day brighter!", true),
                    new("Chúc may mắn,", "Best wishes,", true),
                    new("Michael", "Michael")
                })
        };
    }

    private sealed record WritingExerciseSeedDefinition(
        int Id,
        string Title,
        string Topic,
        string PreviewText,
        IReadOnlyList<WritingSentenceSeedDefinition> Sentences);

    private sealed record WritingSentenceSeedDefinition(
        string VietnameseText,
        string EnglishMeaning,
        bool BreakAfter = false);

    private sealed record WritingExerciseSentenceSeedRow(
        int Id,
        int WritingExerciseId,
        int SortOrder,
        string VietnameseText,
        string EnglishMeaning,
        bool BreakAfter);
}
