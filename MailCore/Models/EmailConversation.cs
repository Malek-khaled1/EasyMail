using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MailCore.Models
{
    /// <summary>
    /// Repræsenterer en hel samtale/tråd (fx Gmail Thread).
    /// Indeholder alle beskeder i tråden.
    /// </summary>
    public class EmailConversation
    {
        /// <summary>
        /// Serverens id for tråden (fx Gmail ThreadId).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Et samlet emne for samtalen.
        /// Som udgangspunkt bruger vi subject fra den seneste besked.
        /// </summary>
        public string? Subject { get; }

        /// <summary>
        /// Alle beskeder i tråden.
        /// Typisk sorteret ældst -> nyest.
        /// </summary>
        public IReadOnlyList<EmailMessage> Messages { get; }

        public EmailConversation(string id, IEnumerable<EmailMessage> messages)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var list = messages.ToList();

            // Hvis ReceivedAt er sat, kan vi sortere efter den.
            if (list.Count > 1 && list.All(m => m.ReceivedAt.HasValue))
            {
                list = list
                    .OrderBy(m => m.ReceivedAt!.Value)
                    .ToList();
            }

            Id = id;
            Messages = new ReadOnlyCollection<EmailMessage>(list);
            Subject = list.LastOrDefault()?.Subject;
        }
    }
}
