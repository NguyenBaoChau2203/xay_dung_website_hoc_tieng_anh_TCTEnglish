namespace TCTEnglish.Tests.ReadingContent;

/// <summary>
/// Twelve CEFR-aligned reading packs (2 per level) following
/// docs/content/reading-content-authoring-guidelines.md word/duration bands.
/// </summary>
internal static class ReadingLevelPacks
{
    public static IReadOnlyDictionary<string, IReadOnlyList<ReadingPack>> AllByCefr { get; } =
        new Dictionary<string, IReadOnlyList<ReadingPack>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1"] = A1(),
            ["A2"] = A2(),
            ["B1"] = B1(),
            ["B2"] = B2(),
            ["C1"] = C1(),
            ["C2"] = C2()
        };

    private static IReadOnlyList<ReadingPack> A1() =>
    [
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L1", "My Morning Routine", "A1", "Daily life", 6,
            "A simple morning routine at home.",
            [
                "I wake up at seven o'clock every day. First I wash my face and brush my teeth. Then I put on clean clothes and drink a glass of water. My mother is already in the kitchen, and she smiles when she sees me.",
                "We eat breakfast together at the table. I usually have bread, eggs, and milk. Sometimes I also eat fruit. After breakfast I pack my school bag and say goodbye. I walk to school with my sister. The morning is quiet and I feel happy."
            ],
            "What does the writer do after waking up?",
            "Wash their face and brush their teeth", "Go to bed again", "Drive a car",
            "The writer walks to school after breakfast.", true,
            "I wake up at _____ o'clock every day.", "seven",
            "Match each English word with its Vietnamese meaning.",
            [("breakfast", "bữa sáng", "p1"), ("school", "trường học", "p2")],
            "How does the writer feel about the morning?", "quiet and happy",
            "2", "The morning is quiet and I feel happy.",
            [
                new("wake up", "/weɪk ʌp/", "Verb", "thức dậy", "I wake up early.", 1),
                new("breakfast", "/ˈbrekfəst/", "Noun", "bữa sáng", "Eat breakfast.", 1),
                new("brush", "/brʌʃ/", "Verb", "đánh (răng)", "Brush your teeth.", 1),
                new("school", "/skuːl/", "Noun", "trường học", "Walk to school.", 2),
                new("quiet", "/ˈkwaɪət/", "Adjective", "yên tĩnh", "A quiet morning.", 2)
            ]),
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L2", "At School", "A1", "School", 6,
            "A short day at school for a student.",
            [
                "My school starts at eight in the morning. We study English and maths in the first lessons. My teacher is kind and helpful. She explains new words slowly so we can understand them well. I write notes in my notebook every day.",
                "At lunch I sit with my friends in the yard. We talk about our hobbies and share fruit from home. In the afternoon we have art class. I draw pictures of my family and feel happy when the teacher smiles at my work. School ends at four."
            ],
            "When does school start?",
            "At eight", "At noon", "At five",
            "The teacher is kind and helpful.", true,
            "In the afternoon we have _____ class.", "art",
            "Match each English word with its Vietnamese meaning.",
            [("teacher", "giáo viên", "p1"), ("friends", "bạn bè", "p2")],
            "What does the student do in art class?", "draw pictures",
            "2", "I draw pictures of my family and feel happy when the teacher smiles.",
            [
                new("study", "/ˈstʌdi/", "Verb", "học", "We study English.", 1),
                new("teacher", "/ˈtiːtʃə/", "Noun", "giáo viên", "My teacher is kind.", 1),
                new("lunch", "/lʌntʃ/", "Noun", "bữa trưa", "At lunch I sit with friends.", 2),
                new("friends", "/frendz/", "Noun", "bạn bè", "Sit with friends.", 2),
                new("draw", "/drɔː/", "Verb", "vẽ", "I draw pictures.", 2)
            ])
    ];

    private static IReadOnlyList<ReadingPack> A2() =>
    [
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L1", "A Weekend Trip", "A2", "Travel", 8,
            "A short weekend trip to the beach.",
            [
                "Last Saturday my family took a bus to the beach. The weather was warm and sunny, so we did not need jackets. We walked on the soft sand and collected small shells near the water. My little brother built a small castle with a stick and laughed when the waves touched it.",
                "In the afternoon we ate fresh fish at a small restaurant by the road. Then we swam in the sea for almost an hour. My father took many photos of the blue water. Children nearby played football, and music came from a radio on the sand.",
                "On the way home I was tired, but I felt excited about the trip. I want to go again next month if the weather is good and we have free time. My mother already put the shells in a small box on the shelf."
            ],
            "How did the family travel to the beach?",
            "By bus", "By plane", "By train",
            "They swam in the sea in the afternoon.", true,
            "We walked on the sand and collected small _____.", "shells",
            "Match each English word with its Vietnamese meaning.",
            [("beach", "bãi biển", "p1"), ("restaurant", "nhà hàng", "p2")],
            "How did the writer feel on the way home?", "tired but excited",
            "2", "On the way home I was tired, but I felt excited about the trip.",
            [
                new("weekend", "/ˌwiːkˈend/", "Noun", "cuối tuần", "A weekend trip.", 1),
                new("beach", "/biːtʃ/", "Noun", "bãi biển", "Go to the beach.", 1),
                new("shells", "/ʃelz/", "Noun", "vỏ sò", "Collect shells.", 1),
                new("restaurant", "/ˈrestrɒnt/", "Noun", "nhà hàng", "Eat at a restaurant.", 2),
                new("excited", "/ɪkˈsaɪtɪd/", "Adjective", "hào hứng", "I felt excited.", 2),
                new("swam", "/swæm/", "Verb", "bơi (quá khứ)", "We swam in the sea.", 2)
            ]),
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L2", "Cooking with Grandma", "A2", "Food", 8,
            "Learning to cook a simple meal at home.",
            [
                "On Sunday I cook with my grandma in our small kitchen. She teaches me how to wash vegetables and cut them carefully on a board. We also boil rice and prepare a light soup with carrots and onions. I try to remember each step so I can help again next time without asking too many questions.",
                "When the food is ready, the kitchen smells wonderful. We set the table and share the meal with my parents. Everyone says the soup tastes better because we made it together. My father asks for a second bowl and smiles.",
                "Cooking together makes me feel close to my family. Next week grandma says we will bake a simple cake for dessert. I already bought flour and sugar so we can start early in the morning."
            ],
            "What does grandma teach the writer first?",
            "How to wash and cut vegetables", "How to bake a cake", "How to buy fish",
            "They share the meal with the writer's parents.", true,
            "We also boil rice and prepare a light _____.", "soup",
            "Match each English word with its Vietnamese meaning.",
            [("vegetables", "rau củ", "p1"), ("kitchen", "nhà bếp", "p2")],
            "Why does cooking together matter to the writer?", "feel close to family",
            "2", "Cooking together makes me feel close to my family.",
            [
                new("cook", "/kʊk/", "Verb", "nấu ăn", "I cook with grandma.", 1),
                new("vegetables", "/ˈvedʒtəblz/", "Noun", "rau củ", "Wash vegetables.", 1),
                new("soup", "/suːp/", "Noun", "súp", "Prepare a light soup.", 1),
                new("kitchen", "/ˈkɪtʃɪn/", "Noun", "nhà bếp", "The kitchen smells wonderful.", 2),
                new("meal", "/miːl/", "Noun", "bữa ăn", "Share the meal.", 2),
                new("carefully", "/ˈkeəfəli/", "Adverb", "cẩn thận", "Cut them carefully.", 1)
            ])
    ];

    private static IReadOnlyList<ReadingPack> B1() =>
    [
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L1", "Protecting City Parks", "B1", "Environment", 12,
            "Why city parks matter and how people can protect them.",
            [
                "City parks give people fresh air and a quiet place to rest after work. Trees in parks also help reduce heat on hot days, which is useful in crowded neighbourhoods. Many families visit parks on weekends to walk, play, or simply sit under the shade. For children, open grass is often safer than busy streets.",
                "However, litter and damaged benches are common problems. When visitors leave bottles on the grass, the park quickly looks uncared for. Broken lights make evening walks feel unsafe. Local groups now organise clean-up days and plant new trees along the main paths. When citizens join these activities, parks stay safer and more beautiful for everyone who lives nearby.",
                "Schools can also teach students to respect shared green spaces. Teachers can include short park visits in science lessons about plants and animals. Small actions, such as using bins and staying on paths, protect parks for the next generation.",
                "If more people take responsibility, city parks will remain valuable public places rather than neglected corners of the city. Local governments can support these efforts with clear rules and regular maintenance budgets that keep paths safe all year for walkers, runners, and families with young children who visit after school."
            ],
            "What problem is mentioned in city parks?",
            "Litter and damaged benches", "Too many restaurants", "No visitors",
            "Local groups plant new trees during clean-up activities.", true,
            "Trees in parks also help reduce _____ on hot days.", "heat",
            "Match each English word with its Vietnamese meaning.",
            [("litter", "rác thải", "p1"), ("citizens", "công dân", "p2"), ("generation", "thế hệ", "p3")],
            "What small actions can protect parks?", "using bins and staying on paths",
            "3", "Small actions, such as using bins and staying on paths, protect parks for the next generation.",
            [
                new("parks", "/pɑːks/", "Noun", "công viên", "City parks give fresh air.", 1),
                new("litter", "/ˈlɪtə/", "Noun", "rác thải", "Litter is a common problem.", 2),
                new("organize", "/ˈɔːɡənaɪz/", "Verb", "tổ chức", "Organize clean-up days.", 2),
                new("citizens", "/ˈsɪtɪzənz/", "Noun", "công dân", "Citizens join activities.", 2),
                new("respect", "/rɪˈspekt/", "Verb", "tôn trọng", "Respect green spaces.", 3),
                new("generation", "/ˌdʒenəˈreɪʃn/", "Noun", "thế hệ", "The next generation.", 3),
                new("benches", "/ˈbentʃɪz/", "Noun", "ghế dài", "Damaged benches.", 2),
                new("protect", "/prəˈtekt/", "Verb", "bảo vệ", "Protect parks.", 3)
            ]),
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L2", "Smartphones in Daily Study", "B1", "Technology", 12,
            "How smartphones help and distract students.",
            [
                "Many students use smartphones to check dictionaries, watch short lessons, and join online study groups. These tools can save time and make learning more flexible after class, especially when teachers share notes digitally. For some learners, a phone has become as common as a notebook, and they feel lost without it during homework.",
                "At the same time, notifications often break concentration. Some learners spend long minutes on social media instead of finishing homework. Games and chat apps make it easy to forget the original plan. Setting app limits is one practical solution, and turning off non-study alerts during evening review can help. Clear goals make phone use more purposeful and less random.",
                "Teachers suggest using phones only for clear study goals, such as reviewing vocabulary lists or recording spoken practice. When students plan phone breaks, they keep better focus and still enjoy useful technology. Parents can agree on shared rules at home.",
                "Balance, not total ban, seems to work best for most classes that want both discipline and modern tools. Students who practise that balance often finish tasks faster and feel less stressed about missing messages during quiet study time at night before important exams. Many teachers now discuss these habits openly in class."
            ],
            "What is one benefit of smartphones for students?",
            "Flexible learning after class", "Free cinema tickets", "Less homework",
            "Notifications can break concentration.", true,
            "Setting app _____ is one practical solution.", "limits",
            "Match each English word with its Vietnamese meaning.",
            [("dictionary", "từ điển", "p1"), ("notification", "thông báo", "p2"), ("focus", "sự tập trung", "p3")],
            "What do teachers suggest phones should be used for?", "clear study goals",
            "3", "Teachers suggest using phones only for clear study goals, such as reviewing vocabulary lists or recording spoken practice.",
            [
                new("smartphone", "/ˈsmɑːtfəʊn/", "Noun", "điện thoại thông minh", "Use a smartphone to study.", 1),
                new("flexible", "/ˈfleksəbl/", "Adjective", "linh hoạt", "More flexible learning.", 1),
                new("notification", "/ˌnəʊtɪfɪˈkeɪʃn/", "Noun", "thông báo", "Notifications break focus.", 2),
                new("concentration", "/ˌkɒnsnˈtreɪʃn/", "Noun", "sự tập trung", "Break concentration.", 2),
                new("limit", "/ˈlɪmɪt/", "Noun", "giới hạn", "App limits help.", 2),
                new("review", "/rɪˈvjuː/", "Verb", "ôn tập", "Review vocabulary lists.", 3),
                new("focus", "/ˈfəʊkəs/", "Noun", "sự tập trung", "Keep better focus.", 3),
                new("practical", "/ˈpræktɪkl/", "Adjective", "thực tế", "A practical solution.", 2)
            ])
    ];

    private static IReadOnlyList<ReadingPack> B2() =>
    [
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L1", "Remote Work and Team Trust", "B2", "Work", 18,
            "How remote teams build trust without a shared office.",
            [
                "Remote work allows specialists to collaborate across cities, yet trust becomes harder to build without informal hallway conversations. Managers therefore need clearer written goals and regular check-ins that feel supportive rather than controlling. Without those habits, silence is easily misread as disengagement, and small misunderstandings grow into lasting doubt about commitment.",
                "Successful teams share progress openly and document decisions in shared folders. This transparency reduces repeated questions and shows that colleagues are reliable even when they are offline. When updates arrive on a predictable rhythm, people stop guessing what others are doing and can plan their own tasks with confidence. Meeting notes become a shared memory that new members can read later.",
                "Still, constant messaging can create pressure. Instant replies are often treated as proof of loyalty, even when deep work needs uninterrupted time. Setting response-time expectations and protecting deep-work hours helps people stay productive without feeling watched. Leaders who model those boundaries make it safer for others to do the same without fear of looking lazy.",
                "When companies balance flexibility with visible accountability, remote work can strengthen rather than weaken professional relationships. Trust then grows from consistent delivery and honest communication, not from physical proximity alone. Teams that practise this balance often report higher satisfaction and clearer ownership of outcomes across locations.",
                "Hiring and onboarding also change under remote conditions. New colleagues need structured introductions to tools, norms, and decision rights; otherwise they copy the loudest voices rather than the clearest process. Organisations that invest early in shared rituals reduce later conflict about who is responsible for which deliverable and how success will be recognised across time zones.",
                "Performance review systems must adapt as well. Metrics that only count message volume can punish deep work and reward performative availability. Fair evaluation looks at outcomes, collaboration quality, and the reliability of handoffs rather than how quickly someone answers a late-night ping."
            ],
            "Why is trust harder in remote teams?",
            "Fewer informal hallway conversations", "Higher salaries", "Shorter workdays",
            "Documenting decisions can reduce repeated questions.", true,
            "Setting response-time expectations and protecting _____ hours helps people stay productive.", "deep-work",
            "Match each English idea with its Vietnamese meaning.",
            [("transparency", "tính minh bạch", "p1"), ("accountability", "trách nhiệm giải trình", "p2"), ("flexibility", "sự linh hoạt", "p3")],
            "What balance helps remote work succeed?", "flexibility with visible accountability",
            "4", "When companies balance flexibility with visible accountability, remote work can strengthen rather than weaken professional relationships.",
            [
                new("remote", "/rɪˈməʊt/", "Adjective", "từ xa", "Remote work across cities.", 1),
                new("collaborate", "/kəˈlæbəreɪt/", "Verb", "cộng tác", "Collaborate across cities.", 1),
                new("transparency", "/trænsˈpærənsi/", "Noun", "tính minh bạch", "Transparency reduces questions.", 2),
                new("reliable", "/rɪˈlaɪəbl/", "Adjective", "đáng tin cậy", "Reliable colleagues.", 2),
                new("productive", "/prəˈdʌktɪv/", "Adjective", "hiệu quả", "Stay productive.", 3),
                new("accountability", "/əˌkaʊntəˈbɪləti/", "Noun", "trách nhiệm giải trình", "Visible accountability.", 4),
                new("flexibility", "/ˌfleksəˈbɪləti/", "Noun", "sự linh hoạt", "Balance flexibility.", 4),
                new("expectation", "/ˌekspekˈteɪʃn/", "Noun", "kỳ vọng", "Response-time expectations.", 3),
                new("document", "/ˈdɒkjument/", "Verb", "ghi chép", "Document decisions.", 2),
                new("pressure", "/ˈpreʃə/", "Noun", "áp lực", "Create pressure.", 3)
            ]),
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L2", "Reading News in the Digital Age", "B2", "Media literacy", 18,
            "How readers can evaluate online news carefully.",
            [
                "Digital platforms deliver headlines within seconds, but speed often comes before careful checking. Readers may share a dramatic claim before reading the full article, especially when the title matches their existing beliefs. That habit turns partial information into social currency and rewards urgency over accuracy in everyday conversation.",
                "A practical habit is to identify the publisher, the date, and whether the piece separates facts from opinion. Looking for original sources is more useful than trusting a viral summary that strips away context. Even a short pause can change what people decide to forward to friends or post in a group chat where corrections rarely travel as far.",
                "Visuals can also mislead when old photos are reused for new events. Reverse image search and careful caption reading reduce that risk, though they take more effort than a quick scroll. Media producers know that striking images travel faster than careful explanations, which is why dramatic pictures often appear beside thin reporting.",
                "Media literacy does not mean rejecting all news; it means slowing down enough to ask who benefits from a story and what evidence supports it. Readers who practise that discipline stay informed without becoming easy targets for exaggeration. Schools and workplaces can treat this skill as civic competence rather than optional hobby.",
                "In group chats, a useful norm is to separate sharing from endorsement. People can pass along a link while noting what still needs checking, which lowers the social cost of caution. Over time, communities that reward careful reading produce fewer cascading errors than communities that reward only speed and dramatic certainty in every forward.",
                "Public figures amplify these dynamics when they treat corrections as weakness. A healthier media culture treats revision as normal intellectual hygiene. Readers then learn that changing a conclusion after better evidence arrives is a strength of responsible reporting, not a personal failure."
            ],
            "What problem comes with fast digital headlines?",
            "People may share claims before reading fully", "Newspapers stop printing", "Readers never see images",
            "Old photos can be reused for new events.", true,
            "Looking for original _____ is more useful than trusting a viral summary.", "sources",
            "Match each English word with its Vietnamese meaning.",
            [("publisher", "nhà xuất bản", "p2"), ("evidence", "bằng chứng", "p4"), ("caption", "chú thích ảnh", "p3")],
            "What does media literacy mean according to the text?", "slowing down to check who benefits and what evidence supports a story",
            "4", "Media literacy does not mean rejecting all news; it means slowing down enough to ask who benefits from a story and what evidence supports it.",
            [
                new("headline", "/ˈhedlaɪn/", "Noun", "tiêu đề", "Digital headlines arrive fast.", 1),
                new("publisher", "/ˈpʌblɪʃə/", "Noun", "nhà xuất bản", "Identify the publisher.", 2),
                new("opinion", "/əˈpɪnjən/", "Noun", "ý kiến", "Separate facts from opinion.", 2),
                new("viral", "/ˈvaɪrəl/", "Adjective", "lan truyền mạnh", "A viral summary.", 2),
                new("mislead", "/ˌmɪsˈliːd/", "Verb", "gây hiểu nhầm", "Visuals can mislead.", 3),
                new("caption", "/ˈkæpʃn/", "Noun", "chú thích ảnh", "Read the caption.", 3),
                new("evidence", "/ˈevɪdəns/", "Noun", "bằng chứng", "What evidence supports it.", 4),
                new("literacy", "/ˈlɪtərəsi/", "Noun", "năng lực hiểu biết", "Media literacy.", 4),
                new("claim", "/kleɪm/", "Noun", "tuyên bố", "A dramatic claim.", 1),
                new("source", "/sɔːs/", "Noun", "nguồn", "Original sources.", 2)
            ])
    ];

    private static IReadOnlyList<ReadingPack> C1() =>
    [
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L1", "Public Trust in Scientific Advice", "C1", "Science & society", 22,
            "Why public trust in scientific guidance rises and falls.",
            [
                "Scientific advice shapes decisions about health, climate, and technology, yet public trust is uneven across communities. When experts communicate uncertainty poorly, audiences may treat every revision as a contradiction rather than an update based on better data. That reaction is understandable after repeated surprises, but it weakens collective problem-solving and makes cooperation harder during the next crisis.",
                "Transparent methods and accessible summaries help non-specialists follow how conclusions are reached. Institutions that publish both findings and limitations tend to retain credibility during later corrections, because readers can see the reasoning path rather than only the final claim. Clarity about what is known and unknown is itself a form of honesty that resists the appearance of hidden agendas or selective disclosure.",
                "Social media rewards confident claims, which can amplify fringe interpretations that travel faster than careful caveats. Scientists who only broadcast absolute certainty may later struggle when new evidence requires nuance. The incentive structure of platforms therefore collides with the provisional nature of research, and performers of certainty often gain attention that careful researchers do not.",
                "Building durable trust depends less on never being wrong and more on showing how evidence is weighed, revised, and openly debated across time. Public communication that models that process invites citizens into inquiry instead of demanding blind acceptance. In the long run, that stance is more resilient than polished certainty, even if it feels slower and less dramatic in the short term.",
                "Policy makers also shape trust when they translate science into rules. If regulations change without explaining the evidentiary threshold that triggered them, the public hears only inconsistency. Coordinated messaging that links each update to a named body of evidence reduces citizens track the logic of change rather than memorising shifting slogans.",
                "Journalists mediate that translation for wider audiences. When coverage frames every revision as scandal rather than as method, audiences learn to expect betrayal from experts. Reporting that distinguishes fraud, honest error, and normal updating preserves space for science to remain public without becoming pure theatre of blame.",
                "Community organisations and schools can reinforce the same habits locally. Town meetings, classroom debates, and library talks that invite questions about evidence—not only about conclusions—train citizens to participate without demanding impossible certainty. Trust then becomes a shared practice rather than a one-way performance of authority from distant institutions. When these local habits spread, national messages about evidence face a public that already knows how to ask better questions. In that climate, scientific advice can be debated without being dismissed as either absolute truth or empty theatre for every contested policy choice. Citizens who practise those habits still disagree, yet they disagree about evidence rather than about whether experts deserve a seat at the table at all."
            ],
            "What happens when uncertainty is communicated poorly?",
            "Revisions may look like contradictions", "Trust automatically increases", "Research funding stops",
            "Publishing limitations can help retain credibility.", true,
            "Social media rewards _____ claims, which can amplify fringe interpretations.", "confident",
            "Match each concept with its Vietnamese meaning.",
            [("credibility", "uy tín", "p2"), ("uncertainty", "sự không chắc chắn", "p1"), ("nuance", "sắc thái tinh tế", "p3")],
            "What does durable trust depend on most, according to the text?", "showing how evidence is weighed, revised, and debated",
            "4", "Building durable trust depends less on never being wrong and more on showing how evidence is weighed, revised, and openly debated across time.",
            [
                new("uncertainty", "/ʌnˈsɜːtnti/", "Noun", "sự không chắc chắn", "Communicate uncertainty clearly.", 1),
                new("revision", "/rɪˈvɪʒn/", "Noun", "sự sửa đổi", "Treat revision as an update.", 1),
                new("transparent", "/trænsˈpærənt/", "Adjective", "minh bạch", "Transparent methods.", 2),
                new("credibility", "/ˌkredəˈbɪləti/", "Noun", "uy tín", "Retain credibility.", 2),
                new("amplify", "/ˈæmplɪfaɪ/", "Verb", "khuếch đại", "Amplify fringe interpretations.", 3),
                new("nuance", "/ˈnjuːɑːns/", "Noun", "sắc thái tinh tế", "New data require nuance.", 3),
                new("durable", "/ˈdjʊərəbl/", "Adjective", "bền vững", "Durable trust.", 4),
                new("institution", "/ˌɪnstɪˈtjuːʃn/", "Noun", "tổ chức", "Institutions publish findings.", 2),
                new("fringe", "/frɪndʒ/", "Adjective", "ngoài lề / cực đoan", "Fringe interpretations.", 3),
                new("debate", "/dɪˈbeɪt/", "Verb", "tranh luận", "Openly debated.", 4),
                new("limitation", "/ˌlɪmɪˈteɪʃn/", "Noun", "hạn chế", "Publish limitations.", 2),
                new("audience", "/ˈɔːdiəns/", "Noun", "đối tượng tiếp nhận", "Audiences may misunderstand.", 1)
            ]),
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L2", "Automation and Meaningful Work", "C1", "Science & society", 22,
            "How automation changes the meaning of everyday work.",
            [
                "Automation removes repetitive tasks, but it also reshapes what workers experience as meaningful. When machines handle routine steps, remaining duties may become more complex and judgment-heavy, which some people welcome and others find exhausting. The same technology can therefore raise skill demands while shrinking the number of familiar roles that once offered a stable sense of contribution.",
                "Some employees welcome this shift because it elevates problem-solving and reduces dull repetition. Others feel deskilled if decisions are constrained by opaque algorithms they cannot question or correct. The difference often lies less in the tool itself than in whether workers retain agency over how it is used, trained, and overridden when unusual cases appear.",
                "Organisations that explain automation goals and invite staff into redesign discussions usually face less resistance. Participation turns technology from an imposed threat into a negotiated tool whose limits can be debated in open forums. Without that conversation, rumours fill the gap and distrust spreads quickly through informal channels that managers rarely see until morale collapses.",
                "Meaningful work, in this context, depends on autonomy, skill use, and a visible link between effort and outcomes. Automation succeeds socially when those conditions are protected rather than eroded by efficiency targets alone. Leaders who treat people as partners in redesign are more likely to keep both productivity and dignity intact across teams of different ages and contract types.",
                "Training budgets reveal whether organisations take that partnership seriously. When workers receive time to understand new systems and to propose safer overrides, automation becomes a scaffold for expertise. When training is treated as a one-off announcement, the same systems become opaque barriers that separate people from the results of their labour.",
                "Labour policy sits behind these workplace choices. Rules about notice, reskilling support, and shared gains from productivity can prevent automation from concentrating benefits among a few owners while dispersing risk among many workers who did not design the tools.",
                "Customer-facing roles illustrate the tension clearly. Chatbots may answer routine queries, yet unusual cases still require human judgment, empathy, and the authority to override a script. If those humans lose status because the easy cases vanished, the remaining work feels heavier even when productivity metrics look better on a dashboard. A durable settlement treats automation as a redesign of the whole task system, including who gets credit, who gets trained, and who can stop an unsafe process. Without that redesign, organisations buy efficiency while quietly selling off the conditions that make work feel worth doing across whole careers. Workers who keep a voice in redesign are more likely to treat new tools as partners rather than as silent replacements for their judgment, memory, and skill on difficult cases. That partnership does not slow every project; it prevents costly failures that appear only after managers celebrate an early efficiency win on paper."
            ],
            "What may remaining duties become after automation removes routine steps?",
            "More complex and judgment-heavy", "Always easier and shorter", "Completely unnecessary",
            "Opaque algorithms can make some workers feel deskilled.", true,
            "Participation turns technology from an imposed threat into a _____ tool.", "negotiated",
            "Match each concept with its Vietnamese meaning.",
            [("automation", "tự động hóa", "p1"), ("autonomy", "quyền tự chủ", "p4"), ("algorithm", "thuật toán", "p2")],
            "When does automation succeed socially?", "when autonomy, skill use, and visible effort-outcome links are protected",
            "4", "Automation succeeds socially when those conditions are protected rather than eroded by efficiency targets alone.",
            [
                new("automation", "/ˌɔːtəˈmeɪʃn/", "Noun", "tự động hóa", "Automation removes repetitive tasks.", 1),
                new("repetitive", "/rɪˈpetətɪv/", "Adjective", "lặp đi lặp lại", "Repetitive tasks.", 1),
                new("deskilled", "/ˌdiːˈskɪld/", "Adjective", "bị giảm kỹ năng", "Feel deskilled.", 2),
                new("opaque", "/əʊˈpeɪk/", "Adjective", "không rõ ràng", "Opaque algorithms.", 2),
                new("algorithm", "/ˈælɡərɪðəm/", "Noun", "thuật toán", "Question the algorithm.", 2),
                new("resistance", "/rɪˈzɪstəns/", "Noun", "sự kháng cự", "Less resistance.", 3),
                new("autonomy", "/ɔːˈtɒnəmi/", "Noun", "quyền tự chủ", "Depends on autonomy.", 4),
                new("erode", "/ɪˈrəʊd/", "Verb", "xói mòn", "Protected rather than eroded.", 4),
                new("participation", "/pɑːˌtɪsɪˈpeɪʃn/", "Noun", "sự tham gia", "Participation turns technology.", 3),
                new("impose", "/ɪmˈpəʊz/", "Verb", "áp đặt", "An imposed threat.", 3),
                new("outcome", "/ˈaʊtkʌm/", "Noun", "kết quả", "Effort and outcomes.", 4),
                new("complex", "/ˈkɒmpleks/", "Adjective", "phức tạp", "More complex duties.", 1)
            ])
    ];

    private static IReadOnlyList<ReadingPack> C2() =>
    [
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L1", "The Limits of Consensus in Academic Debate", "C2", "Academic debate", 28,
            "Why scholarly consensus is useful yet incomplete.",
            [
                "Academic consensus offers a provisional map of what a field currently finds persuasive, not a permanent verdict sealed against revision. Treating consensus as unfinished knowledge keeps inquiry open when anomalies appear and refuse to fit the prevailing model. That stance is intellectually demanding because it asks scholars to hold confidence and doubt at once, especially when careers reward certainty more than careful hesitation.",
                "Yet consensus can also harden into orthodoxy. Gatekeeping journals, funding priorities, and citation habits may quietly marginalize dissenting methods before their claims are tested on equal terms. When that happens, the appearance of agreement may reflect institutional filters more than settled truth, and outsiders mistake silence for refutation. The resulting map looks stable while quietly excluding whole families of questions.",
                "Productive debate therefore requires both evidentiary standards and institutional humility. Scholars must ask whether disagreement is ignored because it is weak or because it is inconvenient to careers and prestige. The distinction is rarely announced; it must be inferred from how forums allocate time, space, and legitimacy, and from which methods are treated as unserious before evidence is examined.",
                "Historical reversals in science and the humanities show that minority positions sometimes become tomorrow's frameworks. The ethical task is to protect rigorous dissent without abandoning shared criteria of proof that make collective evaluation possible. Without those criteria, openness collapses into mere noise, and every claim demands equal standing regardless of method.",
                "In classrooms, presenting consensus alongside its contested edges trains students to evaluate arguments rather than memorize authorized conclusions. That pedagogical choice mirrors the deeper claim of the text: consensus is a tool for orientation, not a substitute for judgment. Students who practise that dual reading are better prepared for research cultures that revise themselves.",
                "Editors and conference organisers share related responsibilities. They can design review processes that reward clarity and reproducibility without treating novelty as the only currency of value. When venues diversify the kinds of contribution they recognise, dissent that meets evidentiary standards has somewhere to appear before it is dismissed as noise.",
                "Readers outside academia also inherit these maps. Popular summaries that flatten consensus into slogan form make later revision look like betrayal. Better public communication would present living agreement as provisional, evidence-based, and open to disciplined challenge rather than as a finished creed.",
                "Graduate training is another pressure point. Students learn quickly which questions are fundable and which methods look risky on a CV. If advisors only reward safe alignment, the next generation inherits orthodoxy as survival strategy. Mentorship that models rigorous dissent—with standards, not mere contrarianism—keeps fields capable of self-correction.",
                "None of this requires romanticising every outsider claim. Weak arguments still fail under scrutiny. The point is institutional design: give strong minority methods a fair hearing before declaring the map complete, and teach the public that completeness was never the real promise of scholarly consensus. When universities, journals, and funders treat provisional agreement as a living instrument rather than a finished creed, debate remains sharp without becoming theatre. Scholars can then defend standards of proof while still admitting that tomorrow’s evidence may redraw today’s map, and citizens can trust that revision is method rather than betrayal. In that climate, consensus retains its usefulness as orientation without pretending to replace judgment, and dissent retains its usefulness as correction without dissolving into noise. Those twin habits keep academic maps usable for teaching, policy advice, and public understanding while leaving space for the next careful revision."
            ],
            "What is academic consensus described as?",
            "A provisional map of persuasive findings", "A permanent verdict", "A list of banned topics",
            "Citation habits can marginalize dissenting methods.", true,
            "The ethical task is to protect rigorous _____ without abandoning shared criteria of proof.", "dissent",
            "Match each concept with its Vietnamese meaning.",
            [("consensus", "đồng thuận", "p1"), ("orthodoxy", "chính thống cứng nhắc", "p2"), ("dissent", "bất đồng / phản biện", "p4"), ("humility", "sự khiêm tốn", "p3")],
            "What should classrooms present alongside consensus?", "its contested edges",
            "5", "In classrooms, presenting consensus alongside its contested edges trains students to evaluate arguments rather than memorize authorized conclusions.",
            [
                new("consensus", "/kənˈsensəs/", "Noun", "đồng thuận", "Academic consensus.", 1),
                new("provisional", "/prəˈvɪʒənl/", "Adjective", "tạm thời", "A provisional map.", 1),
                new("orthodoxy", "/ˈɔːθədɒksi/", "Noun", "chính thống cứng nhắc", "Harden into orthodoxy.", 2),
                new("marginalize", "/ˈmɑːdʒɪnəlaɪz/", "Verb", "đẩy ra ngoài lề", "Marginalize dissenting methods.", 2),
                new("evidentiary", "/ˌevɪˈdenʃəri/", "Adjective", "thuộc về bằng chứng", "Evidentiary standards.", 3),
                new("humility", "/hjuːˈmɪləti/", "Noun", "sự khiêm tốn", "Institutional humility.", 3),
                new("dissent", "/dɪˈsent/", "Noun", "bất đồng / phản biện", "Rigorous dissent.", 4),
                new("reversal", "/rɪˈvɜːsl/", "Noun", "sự đảo ngược", "Historical reversals.", 4),
                new("contested", "/kənˈtestɪd/", "Adjective", "bị tranh cãi", "Contested edges.", 5),
                new("authorized", "/ˈɔːθəraɪzd/", "Adjective", "được công nhận chính thức", "Authorized conclusions.", 5),
                new("anomaly", "/əˈnɒməli/", "Noun", "dị thường", "When anomalies appear.", 1),
                new("gatekeeping", "/ˈɡeɪtkiːpɪŋ/", "Noun", "kiểm soát cửa ngõ", "Gatekeeping journals.", 2),
                new("criteria", "/kraɪˈtɪəriə/", "Noun", "tiêu chí", "Shared criteria of proof.", 4),
                new("inquiry", "/ɪnˈkwaɪəri/", "Noun", "sự tìm hiểu", "Keep inquiry open.", 1)
            ]),
        ReadingImportWorkbookBuilder.BuildStandardPack(
            "L2", "Taste, Class, and Cultural Criticism", "C2", "Cultural criticism", 28,
            "How judgments of taste often encode social hierarchy.",
            [
                "Cultural criticism frequently claims to judge form, yet taste is rarely innocent. Preferences for certain music, cuisine, or cinema can signal belonging to a social class as much as aesthetic insight, even when critics insist they are discussing pure quality. The vocabulary of refinement often doubles as a map of distinction that sorts people as firmly as it sorts artworks.",
                "Critics who ignore this entanglement risk presenting privilege as universal refinement. A review that mocks popular culture without examining its audiences may confuse exclusion with standards, treating unfamiliar codes as evidence of inferior art. That habit flatters the critic's circle while pretending to serve art itself, and it leaves unequal access to cultural capital unnamed.",
                "At the same time, reducing every judgment to class position can erase craft. Some works repay close attention across audiences; others travel mainly as fashion. A careful critic distinguishes social signalling from compositional achievement without pretending the two never meet. The task is separation under pressure, not purity, and it requires patience with ambiguity.",
                "Taste therefore becomes a site where power and pleasure intersect. Naming that intersection does not abolish enjoyment; it makes criticism more honest about whose pleasure is treated as the default measure. When writers disclose their standpoint, readers can weigh claims instead of mistaking them for nature, and disagreement can become diagnostic rather than merely personal.",
                "For classrooms and public debate, the useful practice is double literacy: read the artwork and read the social frame that elevates or dismisses it. Cultural criticism that holds both lenses resists both naive universalism and cynical reduction. That balance is difficult, which is precisely why it remains necessary for any public that wants judgment without disguise.",
                "Reviewers writing for general audiences can model that balance by naming constraints of access, education, and geography without turning every preference into a confession. Readers then learn to ask what a judgment assumes, whom it includes by default, and what craft evidence would still persuade them if social fashion shifted tomorrow.",
                "Institutions of culture—museums, festivals, streaming platforms—also allocate prestige. Their catalogues teach audiences what counts as serious before any critic writes a word. Awareness of that curation does not cancel pleasure; it situates pleasure inside a history of selection that can be questioned and redesigned.",
                "Algorithmic recommendation intensifies the problem by quietly ranking taste. What appears as personal discovery may be a map of prior purchases, peer groups, and commercial partnerships. Critics who ignore that machinery risk treating platform visibility as aesthetic destiny rather than as a contested distribution system.",
                "A mature cultural public therefore practises two readings at once: the artwork’s internal craft and the social machinery that frames it. Neither reading alone is enough. Together they allow disagreement that is sharper about art and fairer about people. Teachers, reviewers, and platform curators who model that dual attention help audiences enjoy culture without mistaking inherited prestige for inevitable quality. Over time, such habits loosen the grip of unexamined taste hierarchies while still preserving room for craft arguments that can persuade across class lines. The result is not flattened preference, but criticism that can name both pleasure and power without collapsing one into the other, and readers who can ask better questions about why a work travels, who funds its visibility, and what standards would still hold if fashion changed overnight. Practising that habit turns taste from a silent ranking system into a conversation that can still celebrate craft. Practising that habit turns taste from a silent ranking system into a conversation that can still celebrate craft."
            ],
            "What can preferences for music or cinema signal, according to the text?",
            "Belonging to a social class as well as aesthetic insight", "Only technical skill", "Government policy",
            "Mocking popular culture without examining audiences may confuse exclusion with standards.", true,
            "Taste becomes a site where power and _____ intersect.", "pleasure",
            "Match each concept with its Vietnamese meaning.",
            [("refinement", "sự tinh tế", "p1"), ("privilege", "đặc quyền", "p2"), ("standpoint", "lập trường", "p4"), ("universalism", "chủ nghĩa phổ quát", "p5")],
            "What double literacy does the text recommend?", "read the artwork and read the social frame",
            "5", "For classrooms and public debate, the useful practice is double literacy: read the artwork and read the social frame that elevates or dismisses it.",
            [
                new("taste", "/teɪst/", "Noun", "gu thẩm mỹ", "Judgments of taste.", 1),
                new("refinement", "/rɪˈfaɪnmənt/", "Noun", "sự tinh tế", "Vocabulary of refinement.", 1),
                new("privilege", "/ˈprɪvəlɪdʒ/", "Noun", "đặc quyền", "Presenting privilege as refinement.", 2),
                new("exclusion", "/ɪkˈskluːʒn/", "Noun", "sự loại trừ", "Confuse exclusion with standards.", 2),
                new("craft", "/krɑːft/", "Noun", "tay nghề / nghệ thuật chế tác", "Erase craft.", 3),
                new("signalling", "/ˈsɪɡnəlɪŋ/", "Noun", "tín hiệu xã hội", "Social signalling.", 3),
                new("standpoint", "/ˈstændpɔɪnt/", "Noun", "lập trường", "Disclose their standpoint.", 4),
                new("universalism", "/ˌjuːnɪˈvɜːsəlɪzəm/", "Noun", "chủ nghĩa phổ quát", "Naive universalism.", 5),
                new("cynical", "/ˈsɪnɪkl/", "Adjective", "cynic / hoài nghi cay độc", "Cynical reduction.", 5),
                new("intersect", "/ˌɪntəˈsekt/", "Verb", "giao nhau", "Power and pleasure intersect.", 4),
                new("aesthetic", "/iːsˈθetɪk/", "Adjective", "thẩm mỹ", "Aesthetic insight.", 1),
                new("distinction", "/dɪˈstɪŋkʃn/", "Noun", "sự phân biệt địa vị", "Map of distinction.", 1),
                new("compositional", "/ˌkɒmpəˈzɪʃənl/", "Adjective", "thuộc bố cục / cấu trúc", "Compositional achievement.", 3),
                new("literacy", "/ˈlɪtərəsi/", "Noun", "năng lực đọc hiểu", "Double literacy.", 5)
            ])
    ];
}
