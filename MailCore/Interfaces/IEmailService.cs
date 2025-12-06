using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailCore.Models;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Høj-niveau email-service som ViewModels skal bruge.
    /// Ansvar:
    ///   - Modtage en bruger (UserAccount) + en EmailMessage
    ///   - Sørge for at der findes et gyldigt access token via ITokenRefreshService
    ///   - Kalde IEmailTransport / IInboxTransport
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sender en email på vegne af den angivne bruger.
        /// </summary>
        Task SendEmailAsync(UserAccount user, EmailMessage message, CancellationToken ct = default);

        /// <summary>
        /// Henter en liste af mails til inbox-oversigten for den aktive bruger.
        /// </summary>
        Task<IReadOnlyList<EmailSummary>> GetInboxAsync(
            UserAccount user,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Henter en fuld mail inkl. body for den angivne bruger.
        /// </summary>
        Task<EmailMessage> GetMessageAsync(
            UserAccount user,
            string messageId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Henter en hel samtale (thread) for den angivne bruger.
        /// </summary>
        Task<EmailConversation> GetConversationAsync(
            UserAccount user,
            string threadId,
            CancellationToken cancellationToken = default);
    }
}
