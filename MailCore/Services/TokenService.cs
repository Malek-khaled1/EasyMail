using System;
using System.Collections.Concurrent; // Til Dictionary
using System.Data.SQLite;
using System.Globalization;
using MailCore.Models;
using MailCore.Helpers;

namespace MailCore.Services
{
    public class TokenService
    {
        private readonly DatabaseService _db;

        // ✅ FIX 1: Brug en Dictionary, så vi kan skelne mellem brugere. 
        // Key = UserId, Value = TokenRecord
        private readonly ConcurrentDictionary<int, TokenRecord> _tokenCache = new();

        public TokenService(DatabaseService db)
        {
            _db = db;
        }

        // ------------------ LOAD TOKENS ------------------
        public TokenRecord? GetTokensForUser(int userId)
        {
            // ✅ FIX 2: Tjek cachen specifikt for DENNE bruger
            if (_tokenCache.TryGetValue(userId, out var cachedRecord))
            {
                return cachedRecord;
            }

            using var conn = _db.GetConnection();
            using var cmd = new SQLiteCommand(
                "SELECT Id, AccessToken, RefreshToken, ExpiresAt FROM Tokens WHERE UserId = @uid LIMIT 1", conn);

            cmd.Parameters.AddWithValue("@uid", userId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            DateTime? expires = null;
            if (!reader.IsDBNull(3) &&
                DateTime.TryParse(reader.GetString(3), null, DateTimeStyles.RoundtripKind, out var dt))
            {
                expires = dt;
            }

            var encryptedAccess = reader["AccessToken"] as string;
            var encryptedRefresh = reader["RefreshToken"] as string;

            // DPAPI Decryption
            var access = encryptedAccess != null ? EncryptionHelper.Unprotect(encryptedAccess) : "";
            var refresh = encryptedRefresh != null ? EncryptionHelper.Unprotect(encryptedRefresh) : "";

            var record = new TokenRecord
            {
                Id = reader.GetInt32(0), // Dette er Token-tabellens ID (ikke UserID)
                AccessToken = access ?? "",
                RefreshToken = refresh ?? "",
                ExpiresAt = expires
            };

            // ✅ Gem i cachen tilknyttet userId
            _tokenCache[userId] = record;

            return record;
        }

        // ------------------ SAVE TOKENS ------------------
        public void SaveToken(int userId, OAuthResult result)
        {
            SaveTokensInternal(userId, result.AccessToken, result.RefreshToken, result.ExpiresAt);
        }

        private void SaveTokensInternal(int userId, string access, string refresh, DateTime expires)
        {
            // Kryptering (samme som før)
            string encryptedAccess = EncryptionHelper.Protect(access) ?? "";

            // Håndter tom refresh token logic
            string encryptedRefresh = string.IsNullOrWhiteSpace(refresh)
                ? null
                : EncryptionHelper.Protect(refresh);

            using var conn = _db.GetConnection();

            // SQL "Upsert" (samme som før)
            var sql = @"
                INSERT INTO Tokens (UserId, AccessToken, RefreshToken, ExpiresAt)
                VALUES (@uid, @acc, @ref, @exp)
                ON CONFLICT(UserId) DO UPDATE SET
                    AccessToken = @acc,
                    RefreshToken = CASE 
                                     WHEN @ref IS NULL OR @ref = '' THEN Tokens.RefreshToken 
                                     ELSE @ref 
                                   END,
                    ExpiresAt = @exp";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@acc", encryptedAccess);
            cmd.Parameters.AddWithValue("@ref", encryptedRefresh != null ? encryptedRefresh : DBNull.Value);
            cmd.Parameters.AddWithValue("@exp", expires.ToString("o"));

            cmd.ExecuteNonQuery();

            // ✅ FIX 3: Opdater cachen smart
            // Vi skal vide, hvad den gamle refresh token var, hvis den nye er tom
            string finalRefreshToken = refresh;

            if (string.IsNullOrWhiteSpace(finalRefreshToken))
            {
                // Hvis vi allerede har den i cachen, snup den gamle refresh token derfra
                if (_tokenCache.TryGetValue(userId, out var oldRecord))
                {
                    finalRefreshToken = oldRecord.RefreshToken;
                }
            }

            // Opdater eller tilføj til cachen
            // Bemærk: Vi kender ikke Token-tabellens ID her (SQL auto-increment), 
            // men det er sjældent vigtigt for cachen. Vi sætter Id til 0 eller ignorerer det.
            _tokenCache[userId] = new TokenRecord
            {
                Id = 0, // Vi ved ikke SQL ID'et her uden at lave en SELECT, men det betyder sjældent noget i koden.
                AccessToken = access,
                RefreshToken = finalRefreshToken,
                ExpiresAt = expires
            };
        }

        // ------------------ SCENARIE A: LOGOUT ------------------
        // Vi vil kun "låse" appen. Data skal blive i databasen.
        public void DeleteCacheToken(int userId)
        {
            _tokenCache.TryRemove(userId, out _);
        }

        // ------------------ SCENARIE B: SLET / FEJL ------------------
        // Vi vil fjerne alt, fordi brugeren slettes ELLER tokenet er dødt.
        public void DeleteTokens(int userId)
        {
            // 1. Slet fra DB (Vigtigt hvis kaldet kommer fra RefreshService)
            using var conn = _db.GetConnection();
            using var cmd = new SQLiteCommand("DELETE FROM Tokens WHERE UserId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();

            // 2. Slet fra RAM
            _tokenCache.TryRemove(userId, out _);
        }
        public class TokenRecord
        {
            public int Id { get; init; }
            public string AccessToken { get; init; } = "";
            public string RefreshToken { get; init; } = "";
            public DateTime? ExpiresAt { get; init; }
        }
    }
}