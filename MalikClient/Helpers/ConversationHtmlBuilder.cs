using System.Text;
using System.Net;
using MailCore.Models;
using System.Collections.Generic;
using System.Linq;

namespace MalikClient.Helpers
{
    public static class ConversationHtmlBuilder
    {
        public static string BuildConversationHtml(IEnumerable<EmailMessage> messages)
        {
            var sb = new StringBuilder();

            sb.Append(@"
<html>
<head>
    <meta charset='utf-8' />
    <style>
        /* --- CUSTOM SCROLLBAR (Start) --- */
        
        /* 1. Bredden på scrollbaren */
        ::-webkit-scrollbar {
            width: 10px;
            height: 10px; /* Til vandret scroll hvis nødvendigt */
        }

        /* 2. Selve sporet (baggrunden) */
        ::-webkit-scrollbar-track {
            background: transparent; 
        }

        /* 3. Selve 'håndtaget' man trækker i */
        ::-webkit-scrollbar-thumb {
            background: #c1c1c1; 
            border-radius: 5px;
        }

        /* 4. Når man holder musen over */
        ::-webkit-scrollbar-thumb:hover {
            background: #a8a8a8; 
        }

        /* --- CUSTOM SCROLLBAR (Slut) --- */


        /* Resten af din CSS (Outlook stil) */
        body { 
            font-family: 'Segoe UI', system-ui, sans-serif; 
            background-color: #ffffff; 
            margin: 0; 
            padding: 0; 
            color: #1f2937;
            overflow-x: hidden; 
        }

        .thread-container { 
            max-width: 100%; 
            padding: 20px 32px; 
            box-sizing: border-box;
        }
        
        .email-section {
            margin-bottom: 32px;
            border-bottom: 1px solid #e5e7eb;
            padding-bottom: 20px;
        }

        .email-section:last-child {
            border-bottom: none;
        }

        .email-header {
            display: flex;
            align-items: center;
            margin-bottom: 16px;
        }

        .avatar {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background-color: #0078d4;
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 600;
            font-size: 14px;
            margin-right: 12px;
            flex-shrink: 0;
        }

        .header-info { flex-grow: 1; min-width: 0; }
        
        .sender-row {
            display: flex;
            justify-content: space-between;
            align-items: baseline;
        }

        .sender-name {
            font-size: 15px;
            font-weight: 600;
            color: #201f1e;
        }

        .email-date {
            font-size: 12px;
            color: #605e5c;
            white-space: nowrap;
            margin-left: 8px;
        }

        .email-body {
            font-size: 14px;
            line-height: 1.6;
            color: #201f1e;
            word-wrap: break-word; 
        }

        .email-body img { 
            max-width: 100% !important; 
            height: auto !important; 
        }
        
        .email-body table {
            max-width: 100% !important;
            table-layout: fixed; 
        }

        a { color: #0078d4; text-decoration: none; }
        a:hover { text-decoration: underline; }

        blockquote {
            border-left: 3px solid #0078d4;
            margin: 10px 0 10px 0;
            padding-left: 15px;
            color: #666;
        }
    </style>
</head>
<body>
    <div class='thread-container'>");

            foreach (var msg in messages)
            {
                var initials = GetInitials(msg.From);
                var dateStr = msg.ReceivedAt?.ToString("dd. MMM yyyy HH:mm") ?? "";
                var senderName = WebUtility.HtmlEncode(msg.From);

                var content = !string.IsNullOrWhiteSpace(msg.HtmlBody)
                    ? ExtractBodyContent(msg.HtmlBody)
                    : $"<pre style='white-space: pre-wrap; font-family: inherit;'>{WebUtility.HtmlEncode(msg.TextBody)}</pre>";

                sb.Append($@"
        <div class='email-section'>
            <div class='email-header'>
                <div class='avatar'>{initials}</div>
                <div class='header-info'>
                    <div class='sender-row'>
                        <span class='sender-name'>{senderName}</span>
                        <span class='email-date'>{dateStr}</span>
                    </div>
                </div>
            </div>
            <div class='email-body'>
                {content}
            </div>
        </div>");
            }

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var clean = name.Trim();
            if (clean.Contains("<")) clean = clean.Split('<')[0].Trim().Replace("\"", "");
            return clean.Length > 0 ? clean.Substring(0, 1).ToUpper() : "?";
        }

        private static string ExtractBodyContent(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var lower = html.ToLowerInvariant();
            var bodyIndex = lower.IndexOf("<body");

            if (bodyIndex >= 0)
            {
                var startContent = lower.IndexOf('>', bodyIndex) + 1;
                var endBody = lower.IndexOf("</body>", startContent);
                if (startContent > 0 && endBody > startContent)
                {
                    return html.Substring(startContent, endBody - startContent);
                }
            }
            return html;
        }
    }
}