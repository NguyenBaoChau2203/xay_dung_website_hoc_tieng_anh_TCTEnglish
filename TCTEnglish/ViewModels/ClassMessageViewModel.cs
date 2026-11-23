namespace TCTVocabulary.ViewModels
{
    public class ClassMessageViewModel
    {
        public int MessageId { get; set; }
        public int UserId { get; set; }

        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public string FullName { get; set; } = "";
        public bool IsMine { get; set; }
    }

    public class ClassMessagesPageViewModel
    {
        public IReadOnlyList<ClassMessageViewModel> Messages { get; set; } = Array.Empty<ClassMessageViewModel>();
        public bool HasMore { get; set; }
    }
}
