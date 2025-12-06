using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2.Web;
using MailCore.Interfaces;
using MailCore.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;


namespace MailCore.Services
{
    public class OAuthService : IOAuthService
    {
        public async Task<OAuthResult?> LoginAsync(CancellationToken ct = default)
        {
            // 1. Få fat i den kørende applikation (Assembly)
            var assembly = Assembly.GetExecutingAssembly();

            // 2. Navnet på ressourcen. Formatet er altid: "Namespace.Mappe.Filnavn"
            // Hvis din fil ligger i roden af MailCore projektet, hedder den nok:
            string resourceName = "MailCore.credentials.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            // DEBUG HJÆLP: Hvis stream er null, har vi gættet forkert på navnet.
            if (stream == null)
            {
                // Denne linje vil vise dig alle gyldige navne i Output-vinduet, hvis det fejler
                var allNames = string.Join(", ", assembly.GetManifestResourceNames());
                throw new InvalidOperationException($"Fandt ikke '{resourceName}'. Tilgængelige ressourcer: {allNames}");
            }

            // 3. Google kan læse direkte fra denne stream (samme som før)
            var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

            // 3) Scopes (tilladelser)
            var scopes = new[]
            {
                "openid",
                "https://www.googleapis.com/auth/userinfo.profile",
                "https://www.googleapis.com/auth/userinfo.email",
                "https://mail.google.com/"
            };


            // 4) Lav flow med prompt=select_account (Tving browser login)
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = scopes,
                Prompt = "select_account"
            });

            // 5) Local HTTP server til Google redirect
            var receiver = new LocalServerCodeReceiver();

            // 6) Byg installed-app handler
            var app = new AuthorizationCodeInstalledApp(flow, receiver);

            // Declare UserCredential variable
            UserCredential credential;

            try
            {
                // 2) Her vil den hænge, hvis bruger aldrig logger ind → nu respekterer vi ct
                credential = await app.AuthorizeAsync("user", ct);
            }
            catch (OperationCanceledException)
            {
                // 3) Hvis cancellation/timeout → bare returner null
                return null;
            }

            if (credential == null || credential.Token == null || string.IsNullOrEmpty(credential.Token.AccessToken))
                return null;

            // Hent TokenResponse via credential.Token
            TokenResponse tokenResponse = credential.Token;

            // 8) Hent email navn og billede fra ID token
            string idToken = tokenResponse.IdToken;
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(idToken);
            string email = jwt.Payload["email"]?.ToString();
            string name = jwt.Payload["name"]?.ToString();
            string pictureUrl = jwt.Payload["picture"]?.ToString();


            // 9) Beregn token udløbstid
            DateTime expiresAt = tokenResponse.IssuedUtc
                .AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);

            // 10) Returner samlet resultat
            return new OAuthResult
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = expiresAt,
                Email = email,
                Name = name,
                PictureUrl = pictureUrl
            };
        }
    }
}
