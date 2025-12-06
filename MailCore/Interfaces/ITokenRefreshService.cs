using System.Threading;
using System.Threading.Tasks;
using MailCore.Models;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Status for et forsøg på at refreshe tokens.
    /// </summary>
    public enum TokenRefreshStatus
    {
        /// <summary>Refresh lykkedes og vi har fået nye tokens.</summary>
        Success,

        /// <summary>Refresh var ikke nødvendigt – eksisterende token er stadig gyldigt.</summary>
        NotNeeded,

        /// <summary>Der findes ingen tokens i databasen for denne bruger.</summary>
        NoTokenData,

        /// <summary>Der er ingen refresh token gemt for denne bruger.</summary>
        NoRefreshToken,

        /// <summary>Konfiguration mangler (fx credentials.json findes ikke).</summary>
        ConfigMissing,

        /// <summary>Netværksfejl – kunne ikke nå Google eller lignende.</summary>
        NetworkError,

        /// <summary>Google svarer "invalid_grant" – refresh token er ugyldig/tilbagekaldt.</summary>
        InvalidGrant,

        /// <summary>Ukendt fejl (alt andet, der gik galt).</summary>
        UnknownError
    }

    /// <summary>
    /// Resultat af et forsøg på at refreshe tokens.
    /// Status fortæller hvad der skete.
    /// OAuth indeholder data, hvis vi har et gyldigt token.
    /// </summary>
    public record TokenRefreshResult(TokenRefreshStatus Status, OAuthResult? OAuth = null)
    {
        /// <summary>
        /// True hvis vi har et gyldigt token efter kaldet
        /// (enten fordi det var i orden i forvejen, eller fordi refresh lykkedes).
        /// </summary>
        public bool IsSuccess =>
            Status == TokenRefreshStatus.Success ||
            Status == TokenRefreshStatus.NotNeeded;
    }

    /// <summary>
    /// Ansvar: Forny OAuth tokens for en given bruger (hvis muligt).
    /// Ingen UI- eller WPF-afhængighed – ren domain service.
    /// </summary>
    public interface ITokenRefreshService
    {
        /// <summary>
        /// Forsøger at sikre et gyldigt access token for brugeren ved hjælp af refresh token.
        /// Returnerer altid et TokenRefreshResult med Status + evt. OAuthResult.
        /// Annulleres via CancellationToken.
        /// </summary>
        Task<TokenRefreshResult> TryRefreshAsync(UserAccount user, CancellationToken ct = default);
    }
}
