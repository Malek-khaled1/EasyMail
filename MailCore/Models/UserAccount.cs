using System;

namespace MailCore.Models
{
    /// <summary>
    /// Repræsenterer en bruger i databasen.
    /// Matcher 1:1 med Users-tabellen i SQLite.
    /// </summary>
    public class UserAccount
    {
        /// <summary>Brugerens Id i databasen (PRIMARY KEY).</summary>
        public int Id { get; set; }

        /// <summary>Brugerens email (unik, NOT NULL).</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Visningsnavn (kan være tom streng, men ikke null).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Sti eller URL til profilbillede (kan være tom streng, men ikke null).</summary>
        public string ImagePath { get; set; } = string.Empty;
  

        /// <summary>Tidspunkt (UTC) hvor brugeren blev oprettet.</summary>
        public DateTime CreatedAt { get; set; }
    }
}
