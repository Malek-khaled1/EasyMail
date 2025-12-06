using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using MailCore.Interfaces;
using MailCore.Models;

namespace MailCore.Services
{
    /// <summary>
    /// SQLite-baseret cache af inbox-listen (EmailSummary).
    /// </summary>
    public class InboxCacheRepository : IInboxCacheRepository
    {
        private readonly DatabaseService _database;

        public InboxCacheRepository(DatabaseService database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task SaveInboxAsync(
            UserAccount user,
            IReadOnlyList<EmailSummary> items,
            CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (items == null) throw new ArgumentNullException(nameof(items));

            using var conn = _database.GetConnection();
            using var tx = conn.BeginTransaction();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var cmd = new SQLiteCommand(@"
                    INSERT INTO InboxCache (
                        UserId,
                        MessageId,
                        ThreadId,
                        FromAddress,
                        Subject,
                        Preview,
                        ReceivedAt,
                        IsRead,
                        IsStarred
                    )
                    VALUES (
                        @userId,
                        @messageId,
                        @threadId,
                        @from,
                        @subject,
                        @preview,
                        @receivedAt,
                        @isRead,
                        @isStarred
                    )
                    ON CONFLICT(UserId, MessageId) DO UPDATE SET
                        ThreadId = excluded.ThreadId,
                        FromAddress = excluded.FromAddress,
                        Subject = excluded.Subject,
                        Preview = excluded.Preview,
                        ReceivedAt = excluded.ReceivedAt,
                        IsRead = excluded.IsRead,
                        IsStarred = excluded.IsStarred;
                ", conn, tx);

                cmd.Parameters.AddWithValue("@userId", user.Id);
                cmd.Parameters.AddWithValue("@messageId", item.Id);
                cmd.Parameters.AddWithValue("@threadId", (object?)item.ThreadId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@from", item.From);
                cmd.Parameters.AddWithValue("@subject", item.Subject);
                cmd.Parameters.AddWithValue("@preview", item.Preview);
                cmd.Parameters.AddWithValue("@receivedAt", item.ReceivedAt.UtcDateTime.ToString("o"));
                cmd.Parameters.AddWithValue("@isRead", item.IsRead ? 1 : 0);
                cmd.Parameters.AddWithValue("@isStarred", item.IsStarred ? 1 : 0);

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<EmailSummary>> GetInboxAsync(
            UserAccount user,
            CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            using var conn = _database.GetConnection();
            using var cmd = new SQLiteCommand(@"
                SELECT
                    MessageId,
                    ThreadId,
                    FromAddress,
                    Subject,
                    Preview,
                    ReceivedAt,
                    IsRead,
                    IsStarred
                FROM InboxCache
                WHERE UserId = @userId
                ORDER BY ReceivedAt DESC;
            ", conn);

            cmd.Parameters.AddWithValue("@userId", user.Id);

            var list = new List<EmailSummary>();

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken)
                                        .ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var messageId = reader.GetString(0);
                var threadId = reader.IsDBNull(1) ? null : reader.GetString(1);
                var from = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var subject = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                var preview = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                var receivedAtText = reader.IsDBNull(5) ? null : reader.GetString(5);
                var isRead = !reader.IsDBNull(6) && reader.GetInt32(6) != 0;
                var isStarred = !reader.IsDBNull(7) && reader.GetInt32(7) != 0;

                DateTimeOffset receivedAt;
                if (!string.IsNullOrWhiteSpace(receivedAtText) &&
                    DateTimeOffset.TryParse(receivedAtText, out var parsed))
                {
                    receivedAt = parsed;
                }
                else
                {
                    receivedAt = DateTimeOffset.UtcNow;
                }

                list.Add(new EmailSummary(
                    id: messageId,
                    from: from,
                    subject: subject,
                    preview: preview,
                    receivedAt: receivedAt,
                    isRead: isRead,
                    isStarred: isStarred,
                    threadId: threadId));
            }

            return list;
        }
    }
}
