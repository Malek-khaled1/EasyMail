using System.Threading;
using System.Threading.Tasks;
using MailCore.Models;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Ansvar:
    ///   - Gemme samtaler (threads + messages) i SQLite.
    ///   - Læse samtaler igen, så de kan bruges offline.
    /// 
    ///
    /// </summary>
    public interface IEmailCacheRepository
    {
        /// <summary>
        /// Gemmer/opsdaterer en hel samtale for en given bruger.
        /// Overskriver ikke eksisterende beskeder; tilføjer kun dem, der mangler.
        /// </summary>
        Task SaveConversationAsync(
            UserAccount user,
            EmailConversation conversation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Forsøger at hente en samtale fra den lokale cache.
        /// Returnerer null, hvis den ikke findes.
        /// </summary>
        Task<EmailConversation?> GetConversationAsync(
            UserAccount user,
            string threadId,
            CancellationToken cancellationToken = default);
    }
}
