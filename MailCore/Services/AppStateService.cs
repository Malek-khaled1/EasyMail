using System;
using System.Collections.Concurrent; // Til Thread-safe caching
using System.Data.SQLite;

namespace MailCore.Services
{
    /// <summary>
    /// AppStateService (Optimeret med Caching)
    /// ----------------
    /// Ansvar:
    ///   - Gemme og læse applikations-tilstand.
    ///   - Bruger RAM-cache for lynhurtig læsning (0ms).
    ///   - Skriver altid til disk for persistens.
    /// </summary>
    public class AppStateService
    {
        private readonly DatabaseService _db;

        // Trådsikker cache til at gemme værdier i hukommelsen
        private readonly ConcurrentDictionary<string, string?> _cache = new();

        public AppStateService(DatabaseService db)
        {
            _db = db;
        }

        /// <summary>
        /// Henter en værdi. Tjekker først RAM, derefter Database.
        /// </summary>
        public string? GetValue(string key)
        {
            // 1. Er den allerede i cachen? Så returner den straks (ingen disk I/O)
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }

            // 2. Hvis ikke, hent fra databasen
            using var conn = _db.GetConnection();
            using var cmd = new SQLiteCommand(
                "SELECT Value FROM AppState WHERE Key = @key LIMIT 1;",
                conn);

            cmd.Parameters.AddWithValue("@key", key);

            var result = cmd.ExecuteScalar();
            string? dbValue = (result == null || result == DBNull.Value) ? null : Convert.ToString(result);

            // 3. Gem i cachen til næste gang (selv hvis den er null)
            _cache[key] = dbValue;

            return dbValue;
        }

        /// <summary>
        /// Sætter en værdi. Opdaterer både Database og RAM.
        /// </summary>
        public void SetValue(string key, string? value)
        {
            // 1. Opdater cachen med det samme
            _cache[key] = value;

            // 2. Skriv til disken (Persistens)
            using var conn = _db.GetConnection();
            using var cmd = new SQLiteCommand(
                @"INSERT INTO AppState(Key, Value) VALUES(@key, @value)
                  ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;",
                conn);

            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        // --------------------------------------------------------------------
        // Typed helpers (int/bool) - Bruger nu den cachede GetValue
        // --------------------------------------------------------------------

        public int? GetInt(string key)
        {
            var s = GetValue(key);
            return int.TryParse(s, out var i) ? i : (int?)null;
        }

        public void SetInt(string key, int? value) => SetValue(key, value?.ToString());

        public bool? GetBool(string key)
        {
            var s = GetValue(key);
            return bool.TryParse(s, out var b) ? b : (bool?)null;
        }

        public void SetBool(string key, bool? value) => SetValue(key, value?.ToString());

        // --------------------------------------------------------------------
        // User-scoped helpers
        // --------------------------------------------------------------------

        private static string BuildUserKey(int userId, string settingKey) =>
            $"user:{userId}:{settingKey}";

        public string? GetUserValue(int userId, string settingKey) =>
            GetValue(BuildUserKey(userId, settingKey));

        public void SetUserValue(int userId, string settingKey, string? value) =>
            SetValue(BuildUserKey(userId, settingKey), value);

        public int? GetUserInt(int userId, string settingKey) =>
            GetInt(BuildUserKey(userId, settingKey));

        public void SetUserInt(int userId, string settingKey, int? value) =>
            SetInt(BuildUserKey(userId, settingKey), value);

        public bool? GetUserBool(int userId, string settingKey) =>
            GetBool(BuildUserKey(userId, settingKey));

        public void SetUserBool(int userId, string settingKey, bool? value) =>
            SetBool(BuildUserKey(userId, settingKey), value);
    }
}