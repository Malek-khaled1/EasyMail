using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using MailCore.Models;

namespace MailCore.Services
{
    /// <summary>
    /// UserService har ét ansvar:
    /// At tale med databasen om brugere (Users + relaterede data).
    /// </summary>
    public class UserService
    {
        private readonly DatabaseService _db;

        public UserService(DatabaseService db)
        {
            _db = db;
        }

        /// <summary>
        /// Gemmer bruger i databasen, eller returnerer eksisterende bruger-Id hvis email allerede findes.
        /// </summary>
        public int SaveUser(UserAccount user)
        {
            using var conn = _db.GetConnection();

            // 1. Tjek om brugeren findes allerede (via email)
            using (var checkCmd = new SQLiteCommand(
                        "SELECT Id FROM Users WHERE Email = @mail LIMIT 1;", conn))
            {
                checkCmd.Parameters.AddWithValue("@mail", user.Email);
                var existingId = checkCmd.ExecuteScalar();

                if (existingId != null && existingId != DBNull.Value)
                {
                    int id = Convert.ToInt32(existingId);
                    user.Id = id;
                    return id;
                }
            }

            // 2. Opret ny bruger
            user.CreatedAt = DateTime.UtcNow;

            using (var insertCmd = new SQLiteCommand(
                        @"INSERT INTO Users (Email, DisplayName, ImagePath, CreatedAt)
                          VALUES (@mail, @name, @img, @created);
                          SELECT last_insert_rowid();",
                        conn))
            {
                insertCmd.Parameters.AddWithValue("@mail", user.Email);
                insertCmd.Parameters.AddWithValue("@name", user.DisplayName ?? string.Empty);
                insertCmd.Parameters.AddWithValue("@img", user.ImagePath ?? string.Empty);
                insertCmd.Parameters.AddWithValue("@created", user.CreatedAt.ToString("o"));

                long id = (long)insertCmd.ExecuteScalar();
                user.Id = (int)id;
                return user.Id;
            }
        }

        /// <summary>
        /// Henter en bruger baseret på email.
        /// Returnerer null hvis ingen bruger findes.
        /// </summary>
        public UserAccount? GetUserByEmail(string email)
        {
            using var conn = _db.GetConnection();

            using var cmd = new SQLiteCommand(
                @"SELECT Id, Email, DisplayName, ImagePath, CreatedAt
                  FROM Users 
                  WHERE Email = @mail
                  LIMIT 1;",
                conn);

            cmd.Parameters.AddWithValue("@mail", email);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return null;

            return MapUser(reader);
        }

        /// <summary>
        /// Henter en bruger baseret på Id.
        /// Returnerer null hvis ingen bruger findes.
        /// </summary>
        public UserAccount? GetUserById(int id)
        {
            using var conn = _db.GetConnection();

            using var cmd = new SQLiteCommand(
                @"SELECT Id, Email, DisplayName, ImagePath, CreatedAt 
                  FROM Users 
                  WHERE Id = @id
                  LIMIT 1;",
                conn);

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return null;

            return MapUser(reader);
        }

        /// <summary>
        /// Sletter en bruger.
        /// Fordi vi har aktiveret "ON DELETE CASCADE" i databasen, slettes
        /// Tokens, Threads og Messages automatisk.
        /// </summary>
        public void DeleteUserById(int id)
        {
            using var conn = _db.GetConnection();

            // Vi behøver kun én kommando nu!
            // SQLite sørger selv for at rydde op i Tokens, Threads og Messages tabellerne.
            using var cmd = new SQLiteCommand("DELETE FROM Users WHERE Id = @id;", conn);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Henter alle brugere i databasen.
        /// </summary>
        public List<UserAccount> GetAllUsers()
        {
            var users = new List<UserAccount>();

            using var conn = _db.GetConnection();
            using var cmd = new SQLiteCommand(
                @"SELECT Id, Email, DisplayName, ImagePath, CreatedAt
                  FROM Users;",
                conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                users.Add(MapUser(reader));
            }

            return users;
        }

        /// <summary>
        /// Fælles mapping fra SQLiteDataReader → UserAccount.
        /// Sikrer ensartet håndtering af null/empty og datoer.
        /// </summary>
        private static UserAccount MapUser(SQLiteDataReader reader)
        {
            // Id, Email er NOT NULL i schemaet
            int id = reader.GetInt32(0);
            string email = reader.GetString(1);

            string displayName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            string imagePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

            DateTime createdAt = DateTime.MinValue;
            if (!reader.IsDBNull(4))
            {
                var createdString = reader.GetString(4);
                if (DateTime.TryParse(
                        createdString,
                        null,
                        DateTimeStyles.RoundtripKind,
                        out var parsed))
                {
                    createdAt = parsed;
                }
            }

            return new UserAccount
            {
                Id = id,
                Email = email,
                DisplayName = displayName,
                ImagePath = imagePath,
                CreatedAt = createdAt
            };
        }
    }
}