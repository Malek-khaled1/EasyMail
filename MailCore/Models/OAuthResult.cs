using System;

namespace MailCore.Models
{
    public class OAuthResult
    {
        // "init" gør, at værdien kun kan sættes når objektet oprettes via "new OAuthResult { ... }"
        // " = string.Empty;" fjerner advarsler om null-værdier.

        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public string? Email { get; init; }

        // --- Nye felter (fra Googles ID-token) ---

        // Fulde navn som Gmail viser (fx "Malik Abdulkader")
        public string? Name { get; init; }

        // URL til profilbillede hos Google (kan være null)
        public string? PictureUrl { get; init; }
    }
}