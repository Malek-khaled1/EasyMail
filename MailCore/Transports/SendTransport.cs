using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MailCore.Interfaces;
using MailCore.Models;
using MimeKit;

namespace MailCore.Transports
{
    public class SendTransport : IEmailTransport
    {
        public async Task SendAsync(EmailMessage message, string accessToken, CancellationToken ct = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("Access token required.");

            // 1) Opret forbindelse til Google API med access token
            var credential = GoogleCredential.FromAccessToken(accessToken);
            using var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MailCoreApp"
            });

            // 2) Byg MimeMessage (præcis som før)
            var mimeMessage = BuildMimeMessage(message);

            // 3) Konverter MimeMessage til en Base64Url encoded string (Krav fra Google API)
            var rawMessage = Base64UrlEncode(mimeMessage);

            // 4) Opret Google API besked-objekt
            var gMessage = new Message
            {
                Raw = rawMessage
            };

            // 5) Send via API
            await service.Users.Messages.Send(gMessage, "me").ExecuteAsync(ct);
        }

        // Hjælper: Konverterer MimeMessage til Base64Url string
        private static string Base64UrlEncode(MimeMessage message)
        {
            using var memory = new MemoryStream();
            message.WriteTo(memory);
            var bytes = memory.ToArray();
            var base64 = Convert.ToBase64String(bytes);

            // Google kræver "Base64Url" format:
            // '+' erstattes med '-'
            // '/' erstattes med '_'
            // '=' fjernes (padding)
            return base64
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        // --- Samme metode som i din gamle SmtpTransport ---
        // Vi genbruger den her for at bevare logikken for attachments/html etc.
        private static MimeMessage BuildMimeMessage(EmailMessage email)
        {
            var mime = new MimeMessage();
            mime.From.Add(MailboxAddress.Parse(email.From));

            foreach (var addr in email.To.Where(a => !string.IsNullOrWhiteSpace(a)))
                mime.To.Add(MailboxAddress.Parse(addr));
            foreach (var addr in email.Cc.Where(a => !string.IsNullOrWhiteSpace(a)))
                mime.Cc.Add(MailboxAddress.Parse(addr));
            foreach (var addr in email.Bcc.Where(a => !string.IsNullOrWhiteSpace(a)))
                mime.Bcc.Add(MailboxAddress.Parse(addr));

            mime.Subject = email.Subject ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(email.InReplyToMessageId))
                mime.InReplyTo = email.InReplyToMessageId;

            if (!string.IsNullOrWhiteSpace(email.References))
            {
                foreach (var r in email.References.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    mime.References.Add(r);
            }

            var builder = new BodyBuilder
            {
                TextBody = email.TextBody,
                HtmlBody = email.HtmlBody
            };

            foreach (var att in email.Attachments)
            {
                if (att?.Content == null || att.Content.Length == 0) continue;
                
                // Simpel content type logic
                var contentType = ContentType.Parse(
                    string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType);

                builder.Attachments.Add(att.FileName, att.Content, contentType);
            }

            mime.Body = builder.ToMessageBody();
            return mime;
        }
    }
}