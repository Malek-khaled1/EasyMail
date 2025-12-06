using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailCore.Models;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Lav-niveau transport til at læse emails (inbox).
    ///
    /// Implementeres typisk i infra-laget:
    ///   - Gmail via REST
    ///   - Evt. senere Exchange, Outlook, osv.
    /// </summary>
    public interface IInboxTransport
    {
        /// <summary>
        /// Henter inbox-oversigten som letvægts-summaries (uden body).
        /// </summary>
        Task<IReadOnlyList<EmailSummary>> GetInboxAsync(
            string emailAddress,
            string accessToken,
            int maxResults,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Henter en enkelt mail inkl. body/HTML ud fra message-id.
        /// </summary>
        Task<EmailMessage> GetMessageAsync(
            string emailAddress,
            string accessToken,
            string messageId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Henter en hel samtale/tråd ud fra thread-id.
        /// </summary>
        Task<EmailConversation> GetConversationAsync(
            string emailAddress,
            string accessToken,
            string threadId,
            CancellationToken cancellationToken = default);
    }
}
