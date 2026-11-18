namespace TCTVocabulary.Services.Vocabulary
{
    /// <summary>
    /// The picture of a system word: the card's own emoji when it has one, otherwise a static term-to-emoji
    /// dictionary, otherwise nothing (the page shows a monogram tile). Emoji are decorative; the term is always text.
    /// No flags and no skin-tone variants. Abstract words are left out on purpose.
    /// </summary>
    public static class VocabularyEmojiMap
    {
        private const int MaxCardEmojiLength = 16;

        private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            // Animals
            ["Dog"] = "🐶", ["Cat"] = "🐱", ["Bird"] = "🐦", ["Fish"] = "🐟", ["Horse"] = "🐴",
            ["Elephant"] = "🐘", ["Lion"] = "🦁", ["Tiger"] = "🐯", ["Monkey"] = "🐵", ["Bear"] = "🐻",

            // Greetings and polite phrases
            ["Hello"] = "👋", ["Goodbye"] = "👋", ["Good morning"] = "🌅", ["Good night"] = "🌙",
            ["See you later"] = "👋", ["Thank you"] = "😊", ["Please"] = "🙏", ["Sorry"] = "🙇",
            ["Excuse me"] = "🙋", ["You're welcome"] = "🤝",
            ["How are you?"] = "💬", ["What's your name?"] = "🪪", ["Where are you from?"] = "🌍",
            ["I don't understand"] = "❓", ["Can you repeat?"] = "🔁", ["Speak slowly"] = "🐢",
            ["Nice to meet you"] = "🤝", ["Have a nice day"] = "☀️",

            // Family
            ["Family"] = "👪", ["Mother"] = "👩", ["Father"] = "👨", ["Brother"] = "👦", ["Sister"] = "👧",
            ["Grandmother"] = "👵", ["Grandfather"] = "👴", ["Son"] = "👦", ["Daughter"] = "👧",
            ["Parents"] = "👫",

            // School
            ["Education"] = "🎓", ["School"] = "🏫", ["Teacher"] = "🧑‍🏫", ["Student"] = "🧑‍🎓",
            ["Book"] = "📖", ["Pen"] = "🖊️", ["Homework"] = "📝", ["Exam"] = "📝", ["Lesson"] = "📚",

            // Food and drink
            ["Rice"] = "🍚", ["Bread"] = "🍞", ["Chicken"] = "🍗", ["Beef"] = "🥩", ["Vegetable"] = "🥦",
            ["Fruit"] = "🍎", ["Water"] = "💧", ["Coffee"] = "☕", ["Tea"] = "🍵", ["Milk"] = "🥛",
            ["Juice"] = "🧃",

            // Travel and places
            ["Airport"] = "✈️", ["Hotel"] = "🏨", ["Beach"] = "🏖️", ["Mountain"] = "⛰️",
            ["Restaurant"] = "🍽️", ["Museum"] = "🏛️", ["Passport"] = "🛂", ["Ticket"] = "🎫",
            ["Luggage"] = "🧳", ["Tourist"] = "📷",

            // Jobs
            ["Doctor"] = "🧑‍⚕️", ["Engineer"] = "👷", ["Nurse"] = "🩺", ["Police officer"] = "👮",
            ["Chef"] = "🧑‍🍳", ["Driver"] = "🚗", ["Farmer"] = "🧑‍🌾", ["Artist"] = "🎨", ["Musician"] = "🎸",

            // Colours
            ["Red"] = "🔴", ["Blue"] = "🔵", ["Yellow"] = "🟡", ["Green"] = "🟢", ["Black"] = "⚫",
            ["White"] = "⚪", ["Orange"] = "🟠", ["Purple"] = "🟣", ["Pink"] = "🌸", ["Brown"] = "🟤"
        };

        /// <summary>Every term the dictionary knows (for tests and the content validator).</summary>
        public static IReadOnlyCollection<string> KnownTerms => Map.Keys;

        /// <summary>The dictionary entry for a term (case and surrounding spaces ignored), or null.</summary>
        public static string? Lookup(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            return Map.TryGetValue(term.Trim(), out var emoji) ? emoji : null;
        }

        /// <summary>The card's own emoji when set (and short), otherwise the dictionary entry, otherwise null.</summary>
        public static string? For(string? cardEmoji, string? term)
        {
            var own = cardEmoji?.Trim();
            if (!string.IsNullOrEmpty(own) && own.Length <= MaxCardEmojiLength)
            {
                return own;
            }

            return Lookup(term);
        }
    }
}
