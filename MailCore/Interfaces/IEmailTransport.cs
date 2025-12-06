using MailCore.Models;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Lav-niveau email-transport.
    ///
    /// Ansvar:
    ///   - Tage en færdig EmailMessage (med From, To, Cc, Bcc, subject, body, vedhæftninger)
    ///   - Bruge et gyldigt accessToken til at autentificere mod fx Gmail/SMTP
    ///   - Sende mailen afsted.
    ///
    /// Bemærk:
    ///   - Ingen UI, ingen WPF, ingen ViewModels.
    ///   - Ingen token-refresh logik (det håndteres af IEmailService).
    /// </summary>
    public interface IEmailTransport
    {
        /// <summary>
        /// Sender en email ved hjælp af det givne accessToken.
        ///
        /// EmailMessage indeholder alle modtagere (To, Cc, Bcc),
        /// subject, HTML-body, tekst-body og vedhæftninger.
        /// </summary>
        /// <param name="accessToken">
        /// Et gyldigt OAuth2 access token (fx fra Google), som transporten kan bruge
        /// til at autentificere. For andre transports kan dette eventuelt ignoreres.
        /// </param>
        /// <param name="draft">
        /// Den færdige email, inkl. From/To/Cc/Bcc/subject/body/attachments.
        /// </param>
        /// <param name="ct">
        /// Cancellation token, så UI kan afbryde send-operationen.
        /// </param>
        Task SendAsync(EmailMessage message, string accessToken, CancellationToken ct = default);
    }
}
