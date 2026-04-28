namespace TCTEnglish.Security
{
    public static class PremiumFeatures
    {
        public const string WritingAiGeneration = "writing.ai_generation";
        public const string SpeakingPrivateImport = "speaking.private_import";
        public const string SpeakingPrivatePractice = "speaking.private_practice";
        public const string ListeningPrivateImport = "listening.private_import";
        public const string ListeningAiQuiz = "listening.ai_quiz";
        public const string ListeningTranscriptTranslate = "listening.transcript_translate";

        public static readonly IReadOnlySet<string> AllFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            WritingAiGeneration,
            SpeakingPrivateImport,
            SpeakingPrivatePractice,
            ListeningPrivateImport,
            ListeningAiQuiz,
            ListeningTranscriptTranslate
        };
    }
}
