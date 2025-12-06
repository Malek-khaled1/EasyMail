using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailCore.Models;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Håndterer cache af selve INBOX-listen (EmailSummary) i SQLite.
    /// </summary>
    public interface IInboxCacheRepository
    {
        /// <summary>
        /// Gemmer/opfresher hele inbox-listen for en given bruger.
        /// </summary>
        Task SaveInboxAsync(
            UserAccount user,
            IReadOnlyList<EmailSummary> items,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Henter inbox-listen fra lokal cache.
        /// Returnerer tom liste, hvis der ikke er noget gemt.
        /// </summary>
        Task<IReadOnlyList<EmailSummary>> GetInboxAsync(
            UserAccount user,
            CancellationToken cancellationToken = default);
    }
}
