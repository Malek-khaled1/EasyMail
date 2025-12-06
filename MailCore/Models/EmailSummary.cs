namespace MailCore.Models
{
    /// <summary>
    /// En letvægts-model til inbox-listen.
    /// Vi viser kun det, der er nødvendigt til oversigten.
    /// </summary>
    public class EmailSummary
    {
        public string Id { get; }
        public string From { get; }
        public string Subject { get; }
        public string Preview { get; }
        public DateTimeOffset ReceivedAt { get; }
        public bool IsRead { get; }
        public bool IsStarred { get; }

        /// <summary>
        /// Id på samtalen/tråden (fx Gmail ThreadId).
        /// Kan være null, hvis transporten ikke kender det.
        /// </summary>
        public string? ThreadId { get; }

        public EmailSummary(
            string id,
            string from,
            string subject,
            string preview,
            DateTimeOffset receivedAt,
            bool isRead,
            bool isStarred,
            string? threadId = null)
        {
            Id = id;
            From = from;
            Subject = subject;
            Preview = preview;
            ReceivedAt = receivedAt;
            IsRead = isRead;
            IsStarred = isStarred;
            ThreadId = threadId;
        }
    }
}
