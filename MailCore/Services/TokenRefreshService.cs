
using System.Reflection;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using MailCore.Interfaces;
using MailCore.Models;

namespace MailCore.Services
{
    public class TokenRefreshService : ITokenRefreshService
    {
        private readonly TokenService _tokenService;

        // Vigtigt: Scopes skal matche dem fra OAuthService for at undgå problemer
        private static readonly string[] Scopes = { "https://mail.google.com/" };

        public TokenRefreshService(TokenService tokenService)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        public async Task<TokenRefreshResult> TryRefreshAsync(UserAccount user, CancellationToken ct = default)
        {
            // 1) Hent eksisterende tokens
            var token = _tokenService.GetTokensForUser(user.Id);

            if (token == null)
                return new(TokenRefreshStatus.NoTokenData);

            if (string.IsNullOrWhiteSpace(token.RefreshToken))
                return new(TokenRefreshStatus.NoRefreshToken);

            // 2) SKIP refresh hvis token stadig er gyldigt (> 60 sekunder tilbage)
            if (token.ExpiresAt.HasValue && token.ExpiresAt.Value > DateTime.UtcNow.AddSeconds(60))
            {
                var validOauth = new OAuthResult
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = token.ExpiresAt.Value,
                    Email = user.Email,
                    Name = user.DisplayName,
                    PictureUrl = user.ImagePath
                };
                return new(TokenRefreshStatus.NotNeeded, validOauth);
            }

            // 3) Læs credentials
            // 3) Læs credentials fra Embedded Resource
            var assembly = Assembly.GetExecutingAssembly();
            // VIGTIGT: Ret "MailCore" hvis dit namespace er anderledes.
            string resourceName = "MailCore.credentials.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                // Hvis stream er null, er filen ikke bagt ind korrekt
                return new(TokenRefreshStatus.ConfigMissing);
            }

            var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

            // 4) Byg Flow - MED NullDataStore
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = Scopes,
                DataStore = new NullDataStore() // <--- VIGTIGT: Hold disken ren
            });

            // 5) Wrap refresh-token
            var tokenResponse = new TokenResponse { RefreshToken = token.RefreshToken };
            var credential = new UserCredential(flow, "user", tokenResponse);

            // 6) Forsøg at refreshe
            try
            {
                bool ok = await credential.RefreshTokenAsync(ct).ConfigureAwait(false);

                if (!ok || credential.Token == null || string.IsNullOrEmpty(credential.Token.AccessToken))
                {
                    return new(TokenRefreshStatus.UnknownError);
                }
            }
            catch (TokenResponseException tre) when (tre.Error != null && tre.Error.Error == "invalid_grant")
            {
                // Token er dødt/tilbagekaldt -> Ryd op
                _tokenService.DeleteCacheToken(user.Id);
                return new(TokenRefreshStatus.InvalidGrant);
            }
            catch
            {
                return new(TokenRefreshStatus.NetworkError);
            }

            var newToken = credential.Token;

            // 7) Beregn ny udløbstid
            DateTime expiresAtNew = newToken.IssuedUtc
                .AddSeconds(newToken.ExpiresInSeconds ?? 3600);

            // 8) Håndter refresh token rotation (behold gammel hvis ny mangler)
            string refreshToSave = string.IsNullOrEmpty(newToken.RefreshToken)
                ? token.RefreshToken
                : newToken.RefreshToken;

            // 9) Byg det nye resultat-objekt FØRST
            var resultOauth = new OAuthResult
            {
                AccessToken = newToken.AccessToken,
                RefreshToken = refreshToSave,
                ExpiresAt = expiresAtNew,
                Email = user.Email,
                Name = user.DisplayName,
                PictureUrl = user.ImagePath
            };

            // 10) Gem med den nye, simple metode (Consistency!)
            _tokenService.SaveToken(user.Id, resultOauth);

            return new(TokenRefreshStatus.Success, resultOauth);
        }
    }
}