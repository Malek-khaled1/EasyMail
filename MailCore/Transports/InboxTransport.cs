using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MailCore.Interfaces;
using MailCore.Models;
// Removed: using System.Windows.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailCore.Transports
{
    public class InboxTransport : IInboxTransport
    {
        // Højere concurrency, men stadig rimeligt ift. Google
        private readonly SemaphoreSlim _fetchSemaphore = new(20);

        private GmailService CreateService(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token required.", nameof(accessToken));

            var credential = GoogleCredential.FromAccessToken(accessToken);

            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MailCoreApp"
            });
        }

        public async Task<IReadOnlyList<EmailSummary>> GetInboxAsync(
            string emailAddress,
            string accessToken,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            using var service = CreateService(accessToken);

            // 1. Hent liste af ID'er (kun INBOX)
            var listRequest = service.Users.Messages.List("me");
            listRequest.LabelIds = new[] { "INBOX" };
            listRequest.MaxResults = maxResults;
            // Vi skal bruge både id og threadId
            listRequest.Fields = "messages(id,threadId)";

            var listResponse = await listRequest.ExecuteAsync(cancellationToken);

            if (listResponse.Messages == null || listResponse.Messages.Count == 0)
                return Array.Empty<EmailSummary>();

            // 2. Hent metadata for hver mail parallelt (20 ad gangen)
            var tasks = listResponse.Messages.Select(m =>
                FetchMessageFastAndSafe(service, m.Id, m.ThreadId, cancellationToken));

            var results = await Task.WhenAll(tasks);

            return results
                .Where(r => r != null)
                .OrderByDescending(r => r!.ReceivedAt)
                .ToList()!;
        }

        private async Task<EmailSummary?> FetchMessageFastAndSafe(
            GmailService service,
            string messageId,
            string? threadId,
            CancellationToken ct)
        {
            try
            {
                await _fetchSemaphore.WaitAsync(ct);

                var request = service.Users.Messages.Get("me", messageId);

                // Kun metadata (ingen body/attachments) = hurtigt
                request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;

                // Vi beder om threadId, så vi kan binde til samtaler
                request.Fields = "id,threadId,snippet,internalDate,labelIds,payload(headers)";

                var message = await request.ExecuteAsync(ct);
                if (message == null) return null;

                var headers = message.Payload?.Headers;

                var subjectHeader = GetHeader(headers, "Subject");
                var subject = string.IsNullOrWhiteSpace(subjectHeader)
                    ? "(No subject)"
                    : subjectHeader;

                var fromRaw = GetHeader(headers, "From") ?? "(unknown)";
                var fromDisplay = CleanSenderName(fromRaw);

                var snippetRaw = message.Snippet ?? "";
                var previewDecoded = System.Net.WebUtility.HtmlDecode(snippetRaw);
                var preview = string.IsNullOrWhiteSpace(previewDecoded)
                    ? "There is no content"
                    : previewDecoded;

                var receivedAt = message.InternalDate.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value)
                    : DateTimeOffset.UtcNow;

                var labels = message.LabelIds ?? new List<string>();
                var isRead = !labels.Contains("UNREAD");
                var isStarred = labels.Contains("STARRED");

                var effectiveThreadId = message.ThreadId ?? threadId ?? string.Empty;

                return new EmailSummary(
                    id: message.Id,
                    from: fromDisplay,
                    subject: subject,
                    preview: preview,
                    receivedAt: receivedAt,
                    isRead: isRead,
                    isStarred: isStarred,
                    threadId: effectiveThreadId // kræver at EmailSummary har ThreadId-parameter
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FETCH ERROR] {messageId}: {ex.Message}");
                return null;
            }
            finally
            {
                _fetchSemaphore.Release();
            }
        }

        // Lille hjælper der gør koden mere læsbar og undgår gentagelser
        private static string? GetHeader(IList<MessagePartHeader>? headers, string name)
        {
            return headers?.FirstOrDefault(h =>
                string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private string CleanSenderName(string rawFrom)
        {
            var idx = rawFrom.IndexOf('<');
            return idx > 1 ? rawFrom[..idx].Trim().Trim('"') : rawFrom;
        }

        // ===== NYT: Hent én fuld besked =====
        public async Task<EmailMessage> GetMessageAsync(
            string emailAddress,
            string accessToken,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            using var service = CreateService(accessToken);

            var request = service.Users.Messages.Get("me", messageId);
            request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

            var gm = await request.ExecuteAsync(cancellationToken);

            return MapToEmailMessage(gm);
        }

        // ===== NYT: Hent en hel samtale / thread =====
        public async Task<EmailConversation> GetConversationAsync(
            string emailAddress,
            string accessToken,
            string threadId,
            CancellationToken cancellationToken = default)
        {
            using var service = CreateService(accessToken);

            var request = service.Users.Threads.Get("me", threadId);
            request.Format = UsersResource.ThreadsResource.GetRequest.FormatEnum.Full;

            var thread = await request.ExecuteAsync(cancellationToken);

            var messages = thread.Messages
                .Select(MapToEmailMessage)
                .ToList();

            return new EmailConversation(thread.Id, messages);
        }

        // ===== Mapper fra Gmail Message -> EmailMessage =====
        private EmailMessage MapToEmailMessage(Message gm)
        {
            var headers = gm.Payload?.Headers ?? new List<MessagePartHeader>();

            string? GetHeaderLocal(string name) =>
                headers.FirstOrDefault(h =>
                    string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

            var from = GetHeaderLocal("From") ?? string.Empty;
            var subject = GetHeaderLocal("Subject") ?? "(no subject)";
            var toRaw = GetHeaderLocal("To");
            var ccRaw = GetHeaderLocal("Cc");
            var bccRaw = GetHeaderLocal("Bcc");

            var message = new EmailMessage
            {
                Id = gm.Id,
                ThreadId = gm.ThreadId,
                From = from,
                Subject = subject,
                HtmlBody = ExtractHtmlBody(gm.Payload),
                TextBody = ExtractPlainTextBody(gm.Payload),
                ReceivedAt = gm.InternalDate.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(gm.InternalDate.Value)
                    : (DateTimeOffset?)null
            };

            if (!string.IsNullOrWhiteSpace(toRaw))
                message.To.AddRange(SplitAddresses(toRaw));
            if (!string.IsNullOrWhiteSpace(ccRaw))
                message.Cc.AddRange(SplitAddresses(ccRaw));
            if (!string.IsNullOrWhiteSpace(bccRaw))
                message.Bcc.AddRange(SplitAddresses(bccRaw));

            return message;
        }

        private static IEnumerable<string> SplitAddresses(string raw)
        {
            // Meget simpel splitter – virker fint for "a@b.com, c@d.com"
            return raw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }

        private string ExtractHtmlBody(MessagePart? part)
        {
            if (part == null) return string.Empty;

            if (part.MimeType == "text/html" && part.Body?.Data != null)
                return DecodeBody(part.Body.Data);

            if (part.Parts != null)
            {
                foreach (var p in part.Parts)
                {
                    var html = ExtractHtmlBody(p);
                    if (!string.IsNullOrEmpty(html))
                        return html;
                }
            }

            return string.Empty;
        }

        private string ExtractPlainTextBody(MessagePart? part)
        {
            if (part == null) return string.Empty;

            if (part.MimeType == "text/plain" && part.Body?.Data != null)
                return DecodeBody(part.Body.Data);

            if (part.Parts != null)
            {
                foreach (var p in part.Parts)
                {
                    var text = ExtractPlainTextBody(p);
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }

            return string.Empty;
        }

        private string DecodeBody(string bodyData)
        {
            if (string.IsNullOrWhiteSpace(bodyData))
                return string.Empty;

            // Gmail bruger base64url
            var fixedData = bodyData.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(fixedData);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
