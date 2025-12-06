using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailCore.Interfaces;
using MailCore.Models;

namespace MailCore.Services
{
    /// <summary>
    /// SQLite-baseret implementation af IEmailCacheRepository.
    /// 
    /// - Gemmer tråde i tabellen Threads.
    /// - Gemmer beskeder i tabellen Messages.
    /// - Loader dem tilbage som EmailConversation + EmailMessage.
    /// 
    /// Ingen UI, ingen network. Kan bruges både online (som cache) og offline.
    /// </summary>
    public class EmailCacheRepository : IEmailCacheRepository
    {
        private readonly DatabaseService _database;

        public EmailCacheRepository(DatabaseService database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task SaveConversationAsync(
            UserAccount user,
            EmailConversation conversation,
            CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (conversation == null) throw new ArgumentNullException(nameof(conversation));

            // Vi kører sync ADO.NET her. Det er fint, så længe vi kalder dette
            // fra en baggrundstråd (det sørger vi for i EmailService senere).
            using var conn = _database.GetConnection();
            using var tx = conn.BeginTransaction();

            // 1) Find eller opret thread-row
            var threadId = UpsertThread(conn, tx, user.Id, conversation.Id, conversation.Subject);

            // 2) Gem beskeder (kun nye – ikke dem vi allerede har)
            foreach (var msg in conversation.Messages)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (string.IsNullOrWhiteSpace(msg.Id))
                    continue;

                UpsertMessage(conn, tx, threadId, msg, conversation.Id);
            }

            tx.Commit();

            return Task.CompletedTask;
        }

        public Task<EmailConversation?> GetConversationAsync(
            UserAccount user,
            string threadId,
            CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(threadId))
                throw new ArgumentException("Thread id is required.", nameof(threadId));

            using var conn = _database.GetConnection();

            // 1) Find thread-row
            using var threadCmd = new SQLiteCommand(
                @"SELECT Id, Subject, ServerThreadId 
                  FROM Threads 
                  WHERE UserId = @userId AND ServerThreadId = @threadId",
                conn);

            threadCmd.Parameters.AddWithValue("@userId", user.Id);
            threadCmd.Parameters.AddWithValue("@threadId", threadId);

            using var threadReader = threadCmd.ExecuteReader();
            if (!threadReader.Read())
            {
                // Ikke gemt i cache
                return Task.FromResult<EmailConversation?>(null);
            }

            var dbThreadId = threadReader.GetInt32(0);
            var subject = threadReader.IsDBNull(1) ? null : threadReader.GetString(1);
            var serverThreadId = threadReader.GetString(2);

            threadReader.Close();

            // 2) Hent alle messages til den thread
            using var msgCmd = new SQLiteCommand(
                @"SELECT ServerMessageId, Sender, Recipient, Body, Date 
                  FROM Messages 
                  WHERE ThreadId = @threadId
                  ORDER BY Date ASC",
                conn);

            msgCmd.Parameters.AddWithValue("@threadId", dbThreadId);

            var messages = new List<EmailMessage>();

            using var msgReader = msgCmd.ExecuteReader();
            while (msgReader.Read())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var serverMessageId = msgReader.GetString(0);
                var sender = msgReader.IsDBNull(1) ? string.Empty : msgReader.GetString(1);
                var recipientsRaw = msgReader.IsDBNull(2) ? string.Empty : msgReader.GetString(2);
                var body = msgReader.IsDBNull(3) ? string.Empty : msgReader.GetString(3);
                var dateRaw = msgReader.IsDBNull(4) ? null : msgReader.GetString(4);

                DateTimeOffset? receivedAt = null;
                if (!string.IsNullOrWhiteSpace(dateRaw) &&
                    DateTimeOffset.TryParse(dateRaw, out var parsed))
                {
                    receivedAt = parsed;
                }

                var email = new EmailMessage
                {
                    Id = serverMessageId,
                    From = sender,
                    Subject = subject ?? "(no subject)",
                    HtmlBody = body,
                    TextBody = body,
                    ReceivedAt = receivedAt,
                    ThreadId = serverThreadId
                };

                // Parse recipients (gemmes som "a@b.com;c@d.com")
                if (!string.IsNullOrWhiteSpace(recipientsRaw))
                {
                    var parts = recipientsRaw
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim());

                    foreach (var r in parts)
                        email.To.Add(r);
                }

                messages.Add(email);
            }

            if (messages.Count == 0)
                return Task.FromResult<EmailConversation?>(null);

            var conversation = new EmailConversation(serverThreadId, messages);
            return Task.FromResult<EmailConversation?>(conversation);
        }

        // ----------------- Hjælper-metoder -----------------

        private static int UpsertThread(
            SQLiteConnection conn,
            SQLiteTransaction tx,
            int userId,
            string serverThreadId,
            string? subject)
        {
            // 1) Find eksisterende
            using var selectCmd = new SQLiteCommand(
                @"SELECT Id FROM Threads 
                  WHERE UserId = @userId AND ServerThreadId = @serverThreadId",
                conn, tx);

            selectCmd.Parameters.AddWithValue("@userId", userId);
            selectCmd.Parameters.AddWithValue("@serverThreadId", serverThreadId);

            var existing = selectCmd.ExecuteScalar();
            if (existing != null && existing != DBNull.Value)
            {
                var id = Convert.ToInt32(existing);

                // Opdater subject hvis der er ændringer
                using var updateCmd = new SQLiteCommand(
                    @"UPDATE Threads 
                      SET Subject = @subject 
                      WHERE Id = @id",
                    conn, tx);

                updateCmd.Parameters.AddWithValue("@subject", (object?)subject ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@id", id);
                updateCmd.ExecuteNonQuery();

                return id;
            }

            // 2) Ellers indsæt ny
            using var insertCmd = new SQLiteCommand(
                @"INSERT INTO Threads (UserId, ServerThreadId, Subject)
                  VALUES (@userId, @serverThreadId, @subject);
                  SELECT last_insert_rowid();",
                conn, tx);

            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@serverThreadId", serverThreadId);
            insertCmd.Parameters.AddWithValue("@subject", (object?)subject ?? DBNull.Value);

            var newId = insertCmd.ExecuteScalar();
            return Convert.ToInt32(newId);
        }

        private static void UpsertMessage(
            SQLiteConnection conn,
            SQLiteTransaction tx,
            int dbThreadId,
            EmailMessage msg,
            string serverThreadId)
        {
            // Tjek om vi allerede har denne besked
            using var selectCmd = new SQLiteCommand(
                @"SELECT Id FROM Messages WHERE ServerMessageId = @mid",
                conn, tx);

            selectCmd.Parameters.AddWithValue("@mid", msg.Id!);

            var existing = selectCmd.ExecuteScalar();
            if (existing != null && existing != DBNull.Value)
            {
                // Vi opdaterer ikke lige nu – vi antager at body osv. ikke ændrer sig.
                // Hvis du vil, kan du lave en UPDATE her.
                return;
            }

            var allRecipients = string.Join(";",
                msg.To.Concat(msg.Cc).Concat(msg.Bcc));

            var body = msg.HtmlBody ?? msg.TextBody ?? string.Empty;
            var dateText = (msg.ReceivedAt ?? DateTimeOffset.UtcNow)
                .UtcDateTime.ToString("o");

            using var insertCmd = new SQLiteCommand(
                @"INSERT INTO Messages 
                    (ThreadId, ServerMessageId, Sender, Recipient, Body, Date)
                  VALUES
                    (@threadId, @serverMessageId, @sender, @recipient, @body, @date);",
                conn, tx);

            insertCmd.Parameters.AddWithValue("@threadId", dbThreadId);
            insertCmd.Parameters.AddWithValue("@serverMessageId", msg.Id!);
            insertCmd.Parameters.AddWithValue("@sender", msg.From ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@recipient", allRecipients);
            insertCmd.Parameters.AddWithValue("@body", body);
            insertCmd.Parameters.AddWithValue("@date", dateText);

            insertCmd.ExecuteNonQuery();
        }
    }
}
