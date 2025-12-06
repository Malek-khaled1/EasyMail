using System;
using System.Data.SQLite;
using System.IO;

namespace MailCore.Services
{
    /// <summary>
    /// DatabaseService:
    /// - Sikrer at SQLite-databasen eksisterer
    /// - Opretter nødvendige tabeller
    /// - Leverer åbne connections til andre services
    /// </summary>
    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly object _lockObj = new object();

        public string DatabasePath => _databasePath;

        public DatabaseService()
        {
            // 1) Find Data-mappen i projektets base-folder
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string folder = Path.Combine(basePath, "Data");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _databasePath = Path.Combine(folder, "mailclient.db");

            // 2) Opret filen hvis den ikke findes (tråd-sikkert)
            lock (_lockObj)
            {
                if (!File.Exists(_databasePath))
                {
                    SQLiteConnection.CreateFile(_databasePath);
                }
            }

            // 3) Sørg for at alle tabeller findes
            CreateTables();
        }

        /// <summary>
        /// Åbner en SQLite forbindelse og returnerer den åben.
        /// Kaldende kode er ansvarlig for at dispose den (via using).
        /// </summary>
        public SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Opretter ALLE tabeller hvis de ikke findes.
        /// </summary>
        private void CreateTables()
        {
            using var conn = GetConnection();

            string sql = @"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;

                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL UNIQUE,
                    DisplayName TEXT,
                    ImagePath TEXT,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Threads (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    ServerThreadId TEXT NOT NULL,
                    Subject TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ThreadId INTEGER NOT NULL,
                    ServerMessageId TEXT NOT NULL,
                    Sender TEXT,
                    Recipient TEXT,
                    Body TEXT,
                    Date TEXT,
                    FOREIGN KEY(ThreadId) REFERENCES Threads(Id)
                );
                
                CREATE TABLE IF NOT EXISTS InboxCache (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    MessageId TEXT NOT NULL,
                    ThreadId TEXT NOT NULL,
                    FromAddress TEXT,
                    Subject TEXT,
                    Preview TEXT,
                    ReceivedAt TEXT,
                    IsRead INTEGER NOT NULL,
                    IsStarred INTEGER NOT NULL,
                    UNIQUE(UserId, MessageId),
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );



                -- HER ER RETTELSEN: Vi har tilføjet UNIQUE til UserId
                CREATE TABLE IF NOT EXISTS Tokens (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL UNIQUE, 
                    AccessToken TEXT,
                    RefreshToken TEXT,
                    ExpiresAt TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS AppState (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NULL
                );
            ";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }
}