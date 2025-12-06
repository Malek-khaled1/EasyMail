using System;

namespace MailCore.Interfaces
{
    /// <summary>
    /// Ansvar:
    ///   - Give et enkelt, UI-uafhængigt svar på om vi har netværksforbindelse.
    ///
    /// Bemærk:
    ///   - Denne service fortæller kun om der er netværk/forbindelse til omverdenen.
    ///   - Den siger ikke noget om hvorvidt Google/Gmail lige nu er oppe.
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// Returnerer true hvis maskinen vurderes at have netværksforbindelse.
        /// Implementationen skal være hurtig og ikke smide exceptions.
        /// </summary>
        bool HasInternet();
    }
}
