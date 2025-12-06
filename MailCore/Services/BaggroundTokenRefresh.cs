using MailCore.Interfaces;

namespace MailCore.Services
{
    /// <summary>
    /// Baggrunds-service der automatisk forsøger at refreshe tokens
    /// for alle kendte brugere med et fast interval.
    /// </summary>
    public class BackgroundTokenRefresher : IBackgroundTokenRefresher
    {
        private readonly ITokenRefreshService _refresh;
        private readonly INetworkService _network;
        private readonly SessionService _session;


        private CancellationTokenSource? _cts;
        private Task? _worker;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(55);

        public BackgroundTokenRefresher(
            ITokenRefreshService refresh,
            INetworkService network,
            SessionService session)
        {
            _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Starter baggrunds-loopen, hvis den ikke allerede kører.
        /// </summary>
        public void Start()
        {
            // Idempotent: hvis den allerede kører, gør ingenting
            if (_worker != null && !_worker.IsCompleted)
                return;

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _worker = Task.Run(() => RunAsync(token), token);
        }

        /// <summary>
        /// Stopper baggrunds-loopen ved at cancelle dens CancellationToken.
        /// </summary>
        public void Stop()
        {
            if (_cts == null)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _worker = null;
        }

        /// <summary>
        /// Selve baggrunds-loopen:
        /// - checker internet
        /// - forsøger at refreshe tokens for alle brugere
        /// - venter _interval og gentager
        /// </summary>
        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_network.HasInternet())
                    {
                        var user = _session.GetActiveUser();

                        if (user != null)
                        {
                            await _refresh.TryRefreshAsync(user, token).ConfigureAwait(false);
                        }

                    }
                }
                catch (OperationCanceledException)
                {
                    // Stop pænt
                    break;
                }
                catch (Exception)
                {
                    // Log fejl her hvis nødvendigt.
                    // Vi gør intet, men vi forhindrer crash.
                }

                // --- VIGTIG ÆNDRING HER ---
                // Vi venter HER, uden for try/catch blokken.
                // Det sikrer, at selv hvis koden crasher, venter den 50 minutter før den prøver igen.
                // Det stopper din CPU fra at overophede.
                try
                {
                    await Task.Delay(_interval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
