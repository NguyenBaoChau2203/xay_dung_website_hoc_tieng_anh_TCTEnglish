namespace TCTVocabulary.ViewModels;

public class NotificationViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? RelatedUrl { get; set; }
    public string? IconClass { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo { get; set; } = string.Empty;  // "5 phút trước", "2 giờ trước"
}

public class NotificationListViewModel
{
    public List<NotificationViewModel> Items { get; set; } = new();
    public int UnreadCount { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public bool HasMore { get; set; }
}
