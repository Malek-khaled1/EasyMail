using System;
using System.Security.Cryptography;
using System.Text;

namespace MailCore.Helpers
{
    public static class EncryptionHelper
    {
        // Vi bruger 'CurrentUser', så kun den bruger der er logget ind i Windows kan læse dataen.
        // Hvis en anden logger ind på samme PC, kan de IKKE læse tokens.
        private const DataProtectionScope Scope = DataProtectionScope.CurrentUser;

        public static string? Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return null;

            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);

                // Her sker magien - Windows krypterer for os
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, Scope);

                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return null; // Eller throw, afhængig af strategi
            }
        }

        public static string? Unprotect(string? encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return null;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);

                // Dekryptering - virker kun på samme maskine med samme Windows-bruger
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, Scope);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // Hvis dekryptering fejler (f.eks. flyttet database), returner null
                return null;
            }
        }
    }
}