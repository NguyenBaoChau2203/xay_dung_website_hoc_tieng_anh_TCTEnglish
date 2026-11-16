using System.Globalization;

namespace TCTEnglish.Services.Writing.Generation;

public enum WritingTemplateCoverage { Supported, NeedsTemplateConfirmation, Unsupported }
public sealed record BilingualWritingLine(string VietnameseText, string EnglishMeaning, bool BreakAfter);
public sealed record WritingTemplateCapability(string Key, int Version, string Topic, string ContentType,
    string Level, string Title, string[] Tones, string[] Lengths, string[] Slots, string[] Limitations)
{
    public IReadOnlyList<BilingualWritingLine> UnfilledPreview => WritingTemplateCatalog.Preview(Key, Level);
    public IReadOnlyList<WritingTemplateSlot> SlotDefinitions => Key == "leave-request" ?
    [new("recipient", "proper_name", 60, []), new("startDate", "date_yyyy_MM_dd", 10, []),
        new("endDate", "date_yyyy_MM_dd", 10, []), new("reason", "choice", 10, ["family", "health", "personal"])] : [];
}
public sealed record WritingTemplateSlot(string Key, string Kind, int MaxLength, string[] AllowedValues);
public sealed record InternalWritingComposition(WritingTemplateCapability Profile, IReadOnlyList<BilingualWritingLine> Sentences);

/// <summary>Original, source-managed bilingual frames. A topic hint is never semantic coverage proof.</summary>
public static class WritingTemplateCatalog
{
    public const int Version = 1;
    public static readonly string[] Levels = ["beginner", "intermediate", "advanced"];
    private sealed record Frame(string Key, string Topic, string Type, string Title, string Tone, string[] Hints);
    private static readonly Frame[] Frames =
    [
        new("leave-request", "work", "emails", "A leave request", "formal", ["leave", "nghỉ phép"]),
        new("meeting-follow-up", "work", "emails", "Following up on our meeting", "formal", ["meeting", "cuộc họp"]),
        new("study-reflection", "study", "diaries", "Looking back on a study session", "friendly", ["study", "học", "reflection"]),
        new("transport-opinion", "travel", "essays", "A case for public transport", "neutral", ["transport", "bus", "giao thông", "xe buýt"]),
        new("study-habit", "study", "articles", "A small daily study habit", "neutral", ["study", "habit", "học", "thói quen"]),
        new("weekend-story", "daily-life", "stories", "A weekend at the community garden", "friendly", ["weekend", "cuối tuần"]),
        new("community-cleanup", "environment", "reports", "A fictional community cleanup", "formal", ["cleanup", "clean-up", "dọn", "rác"])
    ];

    public static IReadOnlyList<WritingTemplateCapability> Capabilities => Frames.SelectMany(f => Levels.Select(l => Capability(f, l))).ToArray();
    public static IReadOnlyList<BilingualWritingLine> Preview(string key, string level) => LanguageBlocks(key, level);

    private static WritingTemplateCapability Capability(Frame f, string level) => new(f.Key, Version, f.Topic, f.Type, level,
        f.Title, [f.Tone], ["short"], f.Key == "leave-request" ? ["recipient", "startDate", "endDate", "reason"] : [],
        ["A bounded bilingual frame, not free-form AI composition.",
         "Only the listed slots and register are supported; other details in the brief are not carried over.",
         f.Key == "community-cleanup" ? "All findings and numbers are fictional practice data." : "The scenario is a fictional practice example."]);

    public static IReadOnlyList<WritingTemplateCapability> Suggest(string brief, string level, string type, string length, string tone) =>
        Frames.Where(f => f.Type == type && f.Tone == tone && length == "short"
                && f.Hints.Any(h => brief.Contains(h, StringComparison.OrdinalIgnoreCase)))
            .Select(f => Capability(f, level)).ToArray();

    public static InternalWritingComposition Compose(string key, int version, string level, string tone, string length,
        IReadOnlyDictionary<string, string> slots)
    {
        var profile = Capabilities.SingleOrDefault(p => p.Key == key && p.Version == version && p.Level == level
            && p.Tones.Contains(tone) && p.Lengths.Contains(length)) ?? throw new ArgumentException("Unsupported template profile.");
        if (slots.Count != profile.Slots.Length || slots.Keys.Any(k => !profile.Slots.Contains(k)))
            throw new ArgumentException("Supply exactly the advertised slots.");
        var lines = LanguageBlocks(key, level);
        if (key == "leave-request")
        {
            var name = slots["recipient"].Trim().Normalize();
            if (name.Length is < 1 or > 60 || name.Any(c => !char.IsLetter(c) && c is not ' ' and not '-' and not '\''))
                throw new ArgumentException("Recipient must be a proper name, without markup or instructions.");
            if (!DateOnly.TryParseExact(slots["startDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                || !DateOnly.TryParseExact(slots["endDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
                || end < start || end.DayNumber - start.DayNumber > 31)
                throw new ArgumentException("Leave dates must span 0–31 days, in yyyy-MM-dd format.");
            var reason = slots["reason"] switch
            {
                "family" => (Vi: "Tôi cần thời gian để thăm gia đình.", En: "I need time to visit my family."),
                "health" => (Vi: "Tôi cần thời gian để chăm sóc sức khỏe.", En: "I need time to look after my health."),
                "personal" => (Vi: "Tôi cần thời gian để giải quyết việc cá nhân.", En: "I need time to attend to a personal matter."),
                _ => throw new ArgumentException("Reason must be family, health or personal; free text is not translated.")
            };
            lines = lines.Select(line => line with
            {
                VietnameseText = line.VietnameseText.Replace("{name}", name).Replace("{start}", start.ToString("dd/MM/yyyy"))
                    .Replace("{end}", end.ToString("dd/MM/yyyy")).Replace("{reason}", reason.Vi),
                EnglishMeaning = line.EnglishMeaning.Replace("{name}", name).Replace("{start}", start.ToString("d MMMM yyyy", CultureInfo.InvariantCulture))
                    .Replace("{end}", end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)).Replace("{reason}", reason.En)
            }).ToArray();
        }
        return new(profile, lines);
    }

    private static IReadOnlyList<BilingualWritingLine> Parse(string text) => text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select((line, i) => { var pair = line.Trim().Split('|'); return new BilingualWritingLine(pair[0], pair[1], i is 0 or 3 or 6); }).ToArray();

    private static IReadOnlyList<BilingualWritingLine> LanguageBlocks(string key, string level) => Parse((key, level) switch
    {
        ("leave-request", "beginner") => """
            Kính gửi {name},|Dear {name},
            Tôi xin phép nghỉ từ ngày {start} đến ngày {end}.|I would like to take leave from {start} to {end}.
            {reason}|{reason}
            Tôi sẽ hoàn thành công việc trước khi nghỉ.|I will finish my work before I leave.
            Đồng nghiệp của tôi có thể hỗ trợ khi tôi vắng mặt.|My colleague can help while I am away.
            Xin vui lòng cho tôi biết liệu những ngày này có phù hợp không.|Please let me know if these dates are suitable.
            Cảm ơn anh/chị đã xem xét đề nghị của tôi.|Thank you for considering my request.
            """,
        ("leave-request", "intermediate") => """
            Kính gửi {name},|Dear {name},
            Tôi viết thư này để xin nghỉ phép từ ngày {start} đến ngày {end}.|I am writing to request leave from {start} to {end}.
            {reason}|{reason}
            Trước khi nghỉ, tôi sẽ hoàn thành các công việc đang phụ trách.|Before my leave begins, I will complete the tasks I am responsible for.
            Tôi cũng sẽ chuẩn bị ghi chú bàn giao để đồng nghiệp có thể hỗ trợ.|I will also prepare handover notes so that my colleagues can provide support.
            Xin vui lòng cho tôi biết nếu cần điều chỉnh lịch nghỉ.|Please let me know if any changes to these dates are needed.
            Tôi rất cảm ơn anh/chị đã dành thời gian xem xét đề nghị này.|I appreciate your time in considering this request.
            """,
        ("leave-request", "advanced") => """
            Kính gửi {name},|Dear {name},
            Tôi kính đề nghị được nghỉ phép trong khoảng từ ngày {start} đến ngày {end}.|I would like to request approval for leave from {start} to {end}.
            {reason}|{reason}
            Để hạn chế gián đoạn, tôi sẽ hoàn tất các đầu việc ưu tiên trước khi nghỉ.|To minimise disruption, I will complete my priority tasks before my leave begins.
            Tôi sẽ phối hợp với đồng nghiệp để bảo đảm việc bàn giao diễn ra suôn sẻ.|I will coordinate with my colleagues to ensure a smooth handover.
            Nếu khoảng thời gian này gây khó khăn cho nhóm, tôi sẵn sàng trao đổi phương án điều chỉnh.|If these dates present difficulties for the team, I would be happy to discuss adjustments.
            Tôi trân trọng sự xem xét của anh/chị và mong nhận được phản hồi.|Thank you for your consideration; I look forward to your response.
            """,
        ("meeting-follow-up", "beginner") => """
            Kính gửi cả nhóm,|Dear team,
            Cảm ơn mọi người đã tham gia cuộc họp hôm nay.|Thank you for joining today's meeting.
            Chúng ta đã xem lại kế hoạch của nhóm.|We reviewed our team's plan.
            Chúng ta đồng ý bắt đầu bằng một thử nghiệm nhỏ.|We agreed to start with a small trial.
            Mỗi người sẽ kiểm tra phần việc của mình trước thứ Sáu.|Everyone will check their own tasks before Friday.
            Tôi sẽ tổng hợp các góp ý và gửi bản cập nhật.|I will collect the comments and send an update.
            Xin hãy cho tôi biết nếu tôi bỏ sót điều gì.|Please let me know if I missed anything.
            """,
        ("meeting-follow-up", "intermediate") => """
            Kính gửi cả nhóm,|Dear team,
            Cảm ơn mọi người đã đóng góp ý kiến trong cuộc họp hôm nay.|Thank you for sharing your ideas during today's meeting.
            Chúng ta đã xem xét kế hoạch và xác định những việc cần ưu tiên.|We reviewed the plan and identified our priorities.
            Nhóm thống nhất thực hiện một thử nghiệm nhỏ trước khi mở rộng.|The team agreed to run a small trial before expanding the work.
            Mỗi thành viên sẽ kiểm tra nhiệm vụ được giao trước thứ Sáu.|Each member will review their assigned tasks before Friday.
            Sau khi nhận được góp ý, tôi sẽ gửi một bản cập nhật chung.|Once I receive the feedback, I will send a combined update.
            Xin vui lòng phản hồi nếu bản tóm tắt này cần được chỉnh sửa.|Please reply if this summary needs any corrections.
            """,
        ("meeting-follow-up", "advanced") => """
            Kính gửi cả nhóm,|Dear team,
            Cảm ơn mọi người đã tham gia thảo luận một cách xây dựng trong cuộc họp hôm nay.|Thank you for contributing to a constructive discussion in today's meeting.
            Sau khi xem xét kế hoạch, chúng ta đã thống nhất các ưu tiên trước mắt.|Having reviewed the plan, we agreed on our immediate priorities.
            Để đánh giá tính khả thi, nhóm sẽ tiến hành một thử nghiệm nhỏ trước khi mở rộng quy mô.|To assess feasibility, the team will conduct a small trial before scaling up.
            Các thành viên được đề nghị rà soát phần việc phụ trách và nêu khó khăn trước thứ Sáu.|Members are asked to review their responsibilities and raise any concerns before Friday.
            Tôi sẽ tổng hợp phản hồi thành một bản cập nhật để cả nhóm cùng theo dõi.|I will consolidate the feedback into an update that the whole team can follow.
            Xin vui lòng thông báo nếu bản tóm tắt chưa phản ánh chính xác nội dung đã thống nhất.|Please let me know if this summary does not accurately reflect what we agreed.
            """,
        ("study-reflection", "beginner") => """
            Hôm nay tôi học tiếng Anh ở nhà.|Today I studied English at home.
            Tôi đọc một đoạn văn ngắn về thức ăn.|I read a short passage about food.
            Một vài từ mới làm tôi thấy khó.|A few new words were difficult for me.
            Tôi viết những từ đó vào vở.|I wrote those words in my notebook.
            Sau đó tôi đọc lại đoạn văn thật chậm.|Then I read the passage again slowly.
            Tôi hiểu bài hơn và cảm thấy vui.|I understood it better and felt happy.
            Ngày mai tôi sẽ ôn lại những từ này.|Tomorrow I will review these words.
            """,
        ("study-reflection", "intermediate") => """
            Buổi học tiếng Anh hôm nay khiến tôi nhận ra mình cần kiên nhẫn hơn.|Today's English study session made me realise that I need more patience.
            Tôi chọn một đoạn văn ngắn về thức ăn để luyện đọc.|I chose a short passage about food for reading practice.
            Ban đầu, những từ chưa biết khiến tôi liên tục dừng lại.|At first, unfamiliar words kept making me stop.
            Thay vì bỏ cuộc, tôi ghi chúng vào vở và đoán nghĩa từ ngữ cảnh.|Instead of giving up, I noted them down and guessed their meanings from context.
            Khi đọc lần thứ hai, tôi theo dõi ý chính dễ dàng hơn.|On my second reading, I could follow the main ideas more easily.
            Sự tiến bộ nhỏ ấy khiến tôi tự tin hơn.|That small improvement made me feel more confident.
            Ngày mai tôi định ôn từ mới trước khi chọn một bài đọc khác.|Tomorrow I plan to review the new words before choosing another passage.
            """,
        ("study-reflection", "advanced") => """
            Buổi tự học hôm nay nhắc tôi rằng tiến bộ thường đến từ những điều chỉnh nhỏ.|Today's independent study reminded me that progress often comes from small adjustments.
            Tôi chọn một bài đọc về thức ăn, tưởng rằng chủ đề quen thuộc sẽ giúp việc đọc dễ dàng.|I chose a passage about food, assuming that a familiar topic would make reading straightforward.
            Tuy nhiên, những từ ít gặp liên tục làm gián đoạn sự tập trung của tôi.|Unfamiliar vocabulary, however, repeatedly interrupted my concentration.
            Tôi chuyển sang ghi chú và suy luận nghĩa từ ngữ cảnh thay vì tra từng từ ngay lập tức.|I switched to taking notes and inferring meanings from context rather than looking up every word immediately.
            Cách tiếp cận này giúp tôi giữ được mạch ý khi đọc lại.|This approach helped me maintain the flow of ideas when I read the passage again.
            Dù chưa hiểu mọi chi tiết, tôi thấy mình đã học chủ động hơn.|Although I did not understand every detail, I felt that I had taken a more active approach to learning.
            Lần học tới, tôi sẽ ôn lại những ghi chú này để xem mình còn nhớ được bao nhiêu.|In my next session, I will revisit these notes to see how much I have retained.
            """,
        ("transport-opinion", "beginner") => """
            Tôi nghĩ xe buýt là một cách đi lại hữu ích.|I think buses are a useful way to travel.
            Nhiều người có thể đi chung một chiếc xe.|Many people can share one vehicle.
            Điều này có thể giúp giảm số xe trên đường.|This can help reduce the number of vehicles on the road.
            Tuy nhiên, đôi khi hành khách phải chờ lâu.|However, passengers sometimes have to wait a long time.
            Các điểm dừng cũng cần sạch sẽ và an toàn.|Bus stops also need to be clean and safe.
            Lịch chạy rõ ràng có thể giúp mọi người lên kế hoạch.|Clear timetables can help people plan their trips.
            Vì vậy, cải thiện dịch vụ xe buýt là điều đáng làm.|Therefore, improving bus services is worthwhile.
            """,
        ("transport-opinion", "intermediate") => """
            Giao thông công cộng có thể giúp việc đi lại trong thành phố thuận tiện hơn.|Public transport can make travelling around a city more convenient.
            Khi nhiều người cùng đi xe buýt, họ cần ít phương tiện cá nhân hơn.|When more people share a bus, they need fewer private vehicles.
            Điều này có thể giảm áp lực lên những tuyến đường đông đúc.|This can reduce pressure on busy roads.
            Tuy vậy, thời gian chờ đợi dài có thể khiến hành khách nản lòng.|Nevertheless, long waiting times can discourage passengers.
            Một dịch vụ đáng tin cậy cần có lịch chạy rõ ràng và điểm dừng an toàn.|A reliable service needs clear timetables and safe stops.
            Chính quyền nên lắng nghe hành khách khi điều chỉnh các tuyến xe.|Local authorities should listen to passengers when adjusting routes.
            Theo tôi, dịch vụ tốt hơn sẽ khiến giao thông công cộng trở thành lựa chọn thực tế hơn.|In my view, better services would make public transport a more practical choice.
            """,
        ("transport-opinion", "advanced") => """
            Đầu tư vào giao thông công cộng là một cách thiết thực để cải thiện khả năng di chuyển trong đô thị.|Investing in public transport is a practical way to improve urban mobility.
            Việc chở nhiều hành khách trên cùng một phương tiện có thể giảm nhu cầu sử dụng xe cá nhân.|Carrying many passengers in one vehicle can reduce demand for private car use.
            Tuy nhiên, lợi ích đó phụ thuộc vào chất lượng và độ tin cậy của dịch vụ.|That benefit, however, depends on the quality and reliability of the service.
            Nếu lịch chạy thất thường, người đi làm khó có thể coi xe buýt là lựa chọn phù hợp.|If timetables are unpredictable, commuters can hardly regard buses as a suitable option.
            Cải thiện điểm dừng và thông tin tuyến xe vì thế cần được ưu tiên cùng với việc tăng số chuyến.|Improving stops and route information should therefore be prioritised alongside increasing service frequency.
            Ý kiến của hành khách có thể giúp xác định những nơi mà các thay đổi nhỏ mang lại lợi ích lớn nhất.|Passenger feedback can help identify where small changes would deliver the greatest benefit.
            Vì những lý do này, tôi ủng hộ việc cải thiện dịch vụ dựa trên nhu cầu thực tế thay vì chỉ mở rộng mạng lưới.|For these reasons, I support improving services around actual needs rather than merely expanding the network.
            """,
        ("study-habit", "beginner") => """
            Một thói quen nhỏ có thể giúp bạn học đều hơn.|A small habit can help you study more regularly.
            Hãy chọn một giờ học phù hợp mỗi ngày.|Choose a suitable time to study each day.
            Bạn có thể bắt đầu bằng mười phút đọc sách.|You can start with ten minutes of reading.
            Hãy để vở và bút ở nơi dễ lấy.|Keep your notebook and pen within easy reach.
            Sau khi đọc, hãy viết một điều bạn nhớ.|After reading, write down one thing you remember.
            Nếu bỏ lỡ một ngày, hãy tiếp tục vào ngày hôm sau.|If you miss a day, continue the next day.
            Một kế hoạch đơn giản thường dễ duy trì hơn.|A simple plan is often easier to maintain.
            """,
        ("study-habit", "intermediate") => """
            Học đều đặn thường bắt đầu từ một thói quen vừa sức.|Regular study often begins with a manageable habit.
            Thay vì lên lịch quá nhiều, hãy chọn một khoảng thời gian ngắn mỗi ngày.|Instead of scheduling too much, choose a short period each day.
            Mười phút đọc sách là một điểm khởi đầu thực tế.|Ten minutes of reading is a realistic starting point.
            Chuẩn bị sẵn vở và bút sẽ giúp bạn bắt đầu dễ dàng hơn.|Having a notebook and pen ready will make it easier to begin.
            Sau mỗi buổi, hãy ghi lại một ý chính bằng lời của mình.|After each session, record one main idea in your own words.
            Khi bỏ lỡ một ngày, bạn có thể tiếp tục mà không cần học bù quá nhiều.|When you miss a day, you can continue without trying to catch up on too much at once.
            Theo thời gian, cách làm đơn giản này có thể giúp việc học trở nên tự nhiên hơn.|Over time, this simple approach can make studying feel more natural.
            """,
        ("study-habit", "advanced") => """
            Một thói quen học tập bền vững cần phù hợp với nhịp sống thực tế.|A sustainable study habit needs to fit the realities of daily life.
            Đặt mục tiêu quá lớn ngay từ đầu có thể khiến người học khó duy trì sự đều đặn.|Setting an overly ambitious goal at the outset can make consistency difficult to maintain.
            Một khoảng đọc sách mười phút mỗi ngày thường là điểm khởi đầu dễ thực hiện hơn.|A daily ten-minute reading session is often a more achievable starting point.
            Việc chuẩn bị sẵn tài liệu giúp giảm những trở ngại nhỏ trước mỗi buổi học.|Preparing materials in advance reduces the small obstacles that precede each session.
            Sau đó, tóm tắt một ý chính bằng lời của mình có thể biến việc đọc thành hoạt động chủ động.|Summarising one main idea in your own words can then turn reading into an active task.
            Nếu lịch học bị gián đoạn, điều hữu ích là quay lại thói quen thay vì tự đặt ra khối lượng học bù quá lớn.|If the routine is interrupted, returning to it is more useful than imposing an excessive catch-up workload.
            Sự linh hoạt như vậy có thể giúp người học duy trì nỗ lực trong thời gian dài.|Such flexibility can help learners sustain their efforts over the long term.
            """,
        ("weekend-story", "beginner") => """
            Sáng thứ Bảy, Linh đi đến khu vườn cộng đồng.|On Saturday morning, Linh went to the community garden.
            Cô mang theo một bình nước nhỏ.|She brought a small watering can.
            Gần cổng, cô thấy một cây non bị đổ.|Near the gate, she saw a young plant that had fallen over.
            Linh gọi người làm vườn đến giúp.|Linh called the gardener for help.
            Họ cùng dựng cây lên và thêm đất.|Together they stood the plant up and added soil.
            Sau đó, Linh tưới nước quanh gốc cây.|Then Linh watered the soil around the plant.
            Cô về nhà với đôi giày bẩn và một nụ cười.|She went home with muddy shoes and a smile.
            """,
        ("weekend-story", "intermediate") => """
            Linh dành buổi sáng thứ Bảy để giúp đỡ ở khu vườn cộng đồng.|Linh spent Saturday morning helping at the community garden.
            Cô mang theo một bình tưới nhỏ vì muốn chăm sóc những luống hoa.|She brought a small watering can because she wanted to care for the flower beds.
            Vừa đến cổng, cô nhận ra một cây non đã bị đổ.|As she reached the gate, she noticed that a young plant had fallen over.
            Không biết nên làm gì, cô nhờ người làm vườn giúp đỡ.|Unsure what to do, she asked the gardener for help.
            Họ nhẹ nhàng dựng cây lên rồi đắp thêm đất quanh rễ.|They gently stood the plant up and added more soil around its roots.
            Khi tưới nước xong, Linh thấy cây đã đứng vững hơn.|After watering it, Linh could see that the plant was standing more firmly.
            Trên đường về, cô vui vì việc làm nhỏ của mình đã có ích.|On the way home, she was glad that her small effort had been useful.
            """,
        ("weekend-story", "advanced") => """
            Linh đã dự định dành một buổi sáng thứ Bảy yên tĩnh chăm sóc hoa ở khu vườn cộng đồng.|Linh had planned to spend a quiet Saturday morning tending flowers at the community garden.
            Với chiếc bình tưới nhỏ trong tay, cô nghĩ công việc hôm ấy sẽ khá nhẹ nhàng.|With a small watering can in hand, she expected the morning's work to be fairly easy.
            Tuy nhiên, ngay bên cổng là một cây non nằm nghiêng, để lộ một phần bộ rễ.|Beside the gate, however, a young plant lay on its side with part of its roots exposed.
            Thay vì tự kéo cây lên, cô tìm người làm vườn để hỏi cách xử lý.|Rather than pulling it upright herself, she sought the gardener's advice.
            Cùng nhau, họ đỡ thân cây và nén đất quanh rễ để giữ cây đứng vững.|Together, they supported the stem and pressed soil around the roots to hold the plant upright.
            Linh tưới nước thật chậm, cẩn thận để không làm trôi lớp đất mới.|Linh watered it slowly, taking care not to wash away the fresh soil.
            Khi rời khu vườn, cô nhận ra buổi sáng bất ngờ ấy đã cho mình một điều đáng nhớ.|As she left the garden, she realised that the unexpected morning had given her something to remember.
            """,
        ("community-cleanup", "beginner") => """
            Báo cáo này mô tả một buổi dọn vệ sinh giả định ở khu dân cư.|This report describes a fictional neighbourhood cleanup.
            Mười tình nguyện viên tham gia vào sáng Chủ nhật.|Ten volunteers took part on Sunday morning.
            Họ thu gom rác dọc một con đường nhỏ.|They collected litter along a small road.
            Nhóm phân loại chai nhựa và giấy vào các túi riêng.|The group put plastic bottles and paper into separate bags.
            Sau buổi dọn, lối đi sạch hơn.|After the cleanup, the path was cleaner.
            Tuy nhiên, khu vực gần cổng vẫn thiếu thùng rác.|However, the area near the gate still lacked a bin.
            Nhóm đề nghị đặt thêm một thùng rác tại đó.|The group suggested placing another bin there.
            """,
        ("community-cleanup", "intermediate") => """
            Báo cáo này trình bày kết quả của một hoạt động dọn vệ sinh giả định.|This report presents the results of a fictional cleanup activity.
            Mười tình nguyện viên tập trung vào sáng Chủ nhật để làm sạch một con đường trong khu dân cư.|Ten volunteers met on Sunday morning to clean a neighbourhood road.
            Họ chia thành các nhóm nhỏ để thu gom rác dọc lối đi.|They divided into small groups to collect litter along the path.
            Chai nhựa và giấy được phân loại riêng để thuận tiện cho việc tái chế.|Plastic bottles and paper were separated to make recycling easier.
            Khi hoạt động kết thúc, lối đi đã gọn gàng hơn rõ rệt.|By the end of the activity, the path was noticeably tidier.
            Nhóm nhận thấy khu vực gần cổng chưa có đủ thùng rác.|The group observed that there were not enough bins near the gate.
            Báo cáo đề xuất bổ sung một thùng rác và kiểm tra khu vực này thường xuyên.|The report recommends adding a bin and checking this area regularly.
            """,
        ("community-cleanup", "advanced") => """
            Báo cáo này đánh giá một hoạt động dọn vệ sinh giả định và đưa ra đề xuất cải thiện.|This report evaluates a fictional cleanup activity and proposes improvements.
            Vào sáng Chủ nhật, mười tình nguyện viên cùng xử lý rác trên một con đường trong khu dân cư.|On Sunday morning, ten volunteers addressed litter on a neighbourhood road.
            Việc chia khu vực thành các đoạn nhỏ giúp nhóm phân công công việc rõ ràng.|Dividing the area into smaller sections helped the group allocate tasks clearly.
            Các vật liệu có thể tái chế, gồm chai nhựa và giấy, được tách khỏi phần rác còn lại.|Recyclable materials, including plastic bottles and paper, were separated from the remaining waste.
            Mặc dù lối đi sạch hơn sau hoạt động, kết quả này chưa chứng minh được hiệu quả lâu dài.|Although the path was cleaner afterwards, this result does not establish long-term effectiveness.
            Tình trạng thiếu thùng rác gần cổng được ghi nhận là một vấn đề cần xử lý.|The shortage of bins near the gate was identified as an issue requiring attention.
            Vì vậy, báo cáo đề xuất bổ sung một thùng rác và theo dõi định kỳ để đánh giá tình trạng tái xuất hiện của rác.|The report therefore recommends adding a bin and monitoring the area regularly to assess whether litter returns.
            """,
        _ => throw new ArgumentException("Unsupported language profile.")
    });
}
