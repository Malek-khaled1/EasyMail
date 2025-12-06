using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailCore.Interfaces;
using MailCore.Models;
using System.Collections.Generic;
using System.Collections.Concurrent;


namespace MailCore.Services
{
    /// <summary>
    /// Høj-niveau email-service som ViewModels skal bruge.
    ///
    /// Ansvar:
    ///   - Validere EmailMessage
    ///   - Hente/fornye tokens via ITokenRefreshService (TokenRefreshResult)
    ///   - Kalde IEmailTransport til send
    ///   - Kalde IInboxTransport til inbox
    //
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ITokenRefreshService _tokenRefresh;
        private readonly IEmailTransport _transport;
        private readonly IInboxTransport _inboxTransport;
        private readonly IEmailCacheRepository _cacheRepository;
        private readonly IInboxCacheRepository _inboxCacheRepository;


        // Enkel in-memory cache pr. kørende app
        private readonly ConcurrentDictionary<string, EmailMessage> _messageCache = new();
        private readonly ConcurrentDictionary<string, EmailConversation> _conversationCache = new();



        private static string BuildCacheKey(UserAccount user, string id)
            => $"{user.Email}::{id}";


        public EmailService(
            ITokenRefreshService tokenRefresh,
            IEmailTransport transport,
            IInboxTransport inboxTransport,
            IEmailCacheRepository cacheRepository,
            IInboxCacheRepository inboxCacheRepository)
        {
            _tokenRefresh = tokenRefresh ?? throw new ArgumentNullException(nameof(tokenRefresh));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _inboxTransport = inboxTransport ?? throw new ArgumentNullException(nameof(inboxTransport));
            _cacheRepository = cacheRepository ?? throw new ArgumentNullException(nameof(cacheRepository));
            _inboxCacheRepository = inboxCacheRepository ?? throw new ArgumentNullException(nameof(inboxCacheRepository));
        }

        /// <summary>
        /// Fælles helper til at sikre et gyldigt access token til en given bruger.
        /// Al token-fejl håndteres ét sted, så både Send og Inbox opfører sig ens.
        /// </summary>
        private async Task<string> GetAccessTokenOrThrowAsync(
            UserAccount user,
            CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new InvalidOperationException("User has no email address configured.");

            ct.ThrowIfCancellationRequested();

            TokenRefreshResult refresh = await _tokenRefresh.TryRefreshAsync(user, ct);

            if (!refresh.IsSuccess || refresh.OAuth == null)
            {
                switch (refresh.Status)
                {
                    case TokenRefreshStatus.NoTokenData:
                    case TokenRefreshStatus.NoRefreshToken:
                    case TokenRefreshStatus.InvalidGrant:
                        throw new InvalidOperationException(
                            "Your session has expired. Please log in again.");

                    case TokenRefreshStatus.ConfigMissing:
                        throw new InvalidOperationException(
                            "Email configuration is missing (credentials.json).");

                    case TokenRefreshStatus.NetworkError:
                        throw new InvalidOperationException(
                            "Network error. Please check your internet connection.");

                    default:
                        throw new InvalidOperationException(
                            "Unexpected error while preparing email access token.");
                }
            }

            var accessToken = refresh.OAuth.AccessToken;

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("Access token is empty or invalid.");

            return accessToken;
        }

        public async Task SendEmailAsync(
            UserAccount user,
            EmailMessage message,
            CancellationToken ct = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (message == null) throw new ArgumentNullException(nameof(message));

            // === Valider modtagere ===
            if (!message.To.Any() && !message.Cc.Any() && !message.Bcc.Any())
                throw new InvalidOperationException("Email must have at least one recipient.");

            // === Sæt From ===
            if (string.IsNullOrWhiteSpace(message.From))
                message.From = user.Email;

            // HTML må ikke være null
            message.HtmlBody ??= string.Empty;

            // 1) Sørg for gyldigt token (fælles helper)
            var accessToken = await GetAccessTokenOrThrowAsync(user, ct);

            // 2) Send via transporten
            await _transport.SendAsync(message, accessToken, ct);
        }

        public async Task<IReadOnlyList<EmailSummary>> GetInboxAsync(
                    UserAccount account,
                    CancellationToken cancellationToken = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            try
            {
                // 1) Online: hent fra Gmail
                var accessToken = await GetAccessTokenOrThrowAsync(account, cancellationToken);

                const int defaultMaxResults = 50;

                var serverItems = await _inboxTransport.GetInboxAsync(
                    account.Email,
                    accessToken,
                    defaultMaxResults,
                    cancellationToken);

                // 2) Gem snapshot i SQLite til offline-brug
                await _inboxCacheRepository.SaveInboxAsync(account, serverItems, cancellationToken);

                return serverItems;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // 3) Fejl (typisk netværk / token / config) → prøv offline cache
                var cachedItems = await _inboxCacheRepository.GetInboxAsync(account, cancellationToken);

                if (cachedItems.Count > 0)
                    return cachedItems;

                // Ingen cache at falde tilbage på → boble fejlen videre
                throw;
            }
        }


        public async Task<EmailMessage> GetMessageAsync(
             UserAccount user,
             string messageId,
             CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("Message id is required.", nameof(messageId));

            var cacheKey = BuildCacheKey(user, messageId);

            // 1) Prøv cache først
            if (_messageCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // 2) Ellers hent fra Gmail
            var accessToken = await GetAccessTokenOrThrowAsync(user, cancellationToken);

            var message = await _inboxTransport.GetMessageAsync(
                user.Email,
                accessToken,
                messageId,
                cancellationToken);

            // 3) Læg i cache
            _messageCache[cacheKey] = message;

            // 4) Hvis samtalen allerede er i cache, så sync den op
            if (!string.IsNullOrWhiteSpace(message.ThreadId))
            {
                var convKey = BuildCacheKey(user, message.ThreadId!);

                if (_conversationCache.TryGetValue(convKey, out var existingConv))
                {
                    // Undgå dubletter
                    if (!existingConv.Messages.Any(m => m.Id == message.Id))
                    {
                        var combined = existingConv.Messages.Concat(new[] { message });
                        _conversationCache[convKey] = new EmailConversation(existingConv.Id, combined);
                    }
                }
            }

            return message;
        }


        public async Task<EmailConversation> GetConversationAsync(
            UserAccount user,
            string threadId,
            CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(threadId))
                throw new ArgumentException("Thread id is required.", nameof(threadId));

            var cacheKey = BuildCacheKey(user, threadId);

            // 1) Prøv RAM-cache først (hurtigst)
            if (_conversationCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // 2) Prøv SQLite-cache (offline support)
            var dbConversation = await _cacheRepository.GetConversationAsync(
                user,
                threadId,
                cancellationToken);

            if (dbConversation != null)
            {
                // Læg i RAM-cache
                _conversationCache[cacheKey] = dbConversation;

                // Læg alle beskeder i message-cachen også
                foreach (var msg in dbConversation.Messages)
                {
                    if (!string.IsNullOrWhiteSpace(msg.Id))
                    {
                        var msgKey = BuildCacheKey(user, msg.Id);
                        _messageCache[msgKey] = msg;
                    }
                }

                return dbConversation;
            }

            // 3) Ellers hent fra Gmail (kræver internet)
            var accessToken = await GetAccessTokenOrThrowAsync(user, cancellationToken);

            var conversation = await _inboxTransport.GetConversationAsync(
                user.Email,
                accessToken,
                threadId,
                cancellationToken);

            // 4) Gem i SQLite-cache, så vi kan læse den offline næste gang
            await _cacheRepository.SaveConversationAsync(user, conversation, cancellationToken);

            // 5) Læg samtalen i RAM-cache
            _conversationCache[cacheKey] = conversation;

            // 6) Læg alle beskeder i message-cachen også
            foreach (var msg in conversation.Messages)
            {
                if (!string.IsNullOrWhiteSpace(msg.Id))
                {
                    var msgKey = BuildCacheKey(user, msg.Id);
                    _messageCache[msgKey] = msg;
                }
            }

            return conversation;
        }



    }
}
