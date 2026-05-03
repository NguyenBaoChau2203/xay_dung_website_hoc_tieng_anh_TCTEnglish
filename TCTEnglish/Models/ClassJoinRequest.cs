using TCTVocabulary.Models;

    public enum JoinRequestStatus
    {
        Pending = 0,
        Approved = 1,
        Declined = 2
    }

    public class ClassJoinRequest
    {
        public int RequestId { get; set; }
        public int ClassId { get; set; }
        public int UserId { get; set; }

        public string? RequestMessage { get; set; } // Lời nhắn gửi kèm (nếu muốn)
        public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Class Class { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
