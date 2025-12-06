using System;
using System.Collections.Generic;


namespace MailCore.Models
{
    /// <summary>
    /// Høj-niveau model for en udgående email.
    ///
    /// Indeholder:
    ///   - Fra / To / Cc / Bcc
    ///   - Subject
    ///   - HTML-body (fonte, størrelser, bullets osv.)
    ///   - Plain text-body (fallback)
    ///   - Vedhæftninger
    ///   - Threading-info til reply / reply all / forward
    ///
    /// Denne klasse er UI-uafhængig og bruges af både EmailService og IEmailTransport.
    /// </summary>
    public class EmailMessage
    {
        /// <summary>
        /// Serverens unikke id for denne mail (fx Gmail messageId).
        /// Bruges når vi læser beskeder fra serveren.
        /// Ved send kan den være null.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Hvornår mailen blev modtaget (servertid).
        /// Bruges til at sortere beskeder i en samtale.
        /// </summary>
        public DateTimeOffset? ReceivedAt { get; set; }

        /// <summary>
        /// Afsender-adresse.
        /// Hvis tom, vil EmailService som regel sætte den til brugerens email.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Primære modtagere (To).
        /// </summary>
        public List<string> To { get; } = new();

        /// <summary>
        /// Kopimodtagere (Cc).
        /// </summary>
        public List<string> Cc { get; } = new();

        /// <summary>
        /// Skjulte kopimodtagere (Bcc).
        /// </summary>
        public List<string> Bcc { get; } = new();

        /// <summary>
        /// Emnefeltet på mailen.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Plain text-version af mailens indhold (valgfrit, men god til kompatibilitet).
        /// </summary>
        public string? TextBody { get; set; }

        /// <summary>
        /// HTML-version af mailens indhold.
        /// Her kan du have fonte, farver, lister, osv.
        /// </summary>
        public string? HtmlBody { get; set; }

        /// <summary>
        /// Liste over vedhæftninger (png, pdf, docx, zip osv.).
        /// EmailAttachment definerer filnavn, MIME-type og bytes.
        /// </summary>
        public List<EmailAttachment> Attachments { get; } = new();

        // ===== Threading / reply / forward info =====

        /// <summary>
        /// Serverens ThreadId (fx Gmail ThreadId).
        /// Hvis sat, sendes mailen som en del af en eksisterende tråd (reply/reply all).
        /// Hvis null, oprettes en ny tråd.
        /// </summary>
        public string? ThreadId { get; set; }

        /// <summary>
        /// Message-ID for den mail, der svares på (In-Reply-To header i MIME).
        /// </summary>
        public string? InReplyToMessageId { get; set; }

        /// <summary>
        /// References-header til bedre threading i andre klienter.
        /// Typisk en liste af Message-Ids separeret med mellemrum.
        /// </summary>
        public string? References { get; set; }
    }
}
