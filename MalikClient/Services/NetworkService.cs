using System;
using System.Net.NetworkInformation;
using MailCore.Interfaces;

namespace MalikClient.Services
{
    /// <summary>
    /// NetworkService
    /// --------------
    /// Ansvar:
    ///   - Give en hurtig indikation af om der er netværksforbindelse.
    ///
    /// Implementation:
    ///   - Bruger .NET's NetworkInterface.GetIsNetworkAvailable(),
    ///     som tjekker om der er mindst ét aktivt netværksinterface.
    ///
    /// Bemærk:
    ///   - Det er et "best effort" check og garanterer ikke,
    ///     at en specifik server (fx Google) kan nås.
    ///   - Det er til gengæld hurtigt og UI-venligt.
    /// </summary>
    public class NetworkService : INetworkService
    {
        public bool HasInternet()
        {
            try
            {
                // Returnerer true hvis der er mindst ét aktivt netværksinterface
                // (WiFi, LAN, mobil osv.)
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                // Vi vil aldrig kaste videre til UI-laget pga. et netværkscheck.
                return false;
            }
        }
    }
}
