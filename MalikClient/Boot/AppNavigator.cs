using System.Threading.Tasks;
using System.Windows;
using MailCore.Interfaces;
using MailCore.Models;
using MalikClient.Views;

namespace MalikClient.Boot
{
    /// <summary>
    /// Styrer hele flowet:
    /// - auto-login ved opstart
    /// - viser login-dialog
    /// - viser main window
    /// - håndterer logout
    /// </summary>
    public class AppNavigator
    {
        private readonly AppServices _services;
        private readonly Application _app;

        public AppNavigator(AppServices services, Application app)
        {
            _services = services;
            _app = app;
        }

        /// <summary>
        /// Startes fra App.OnStartup:
        /// - starter baggrunds-refresh
        /// - forsøger auto-login
        /// - falder tilbage til login-dialog hvis nødvendigt
        /// </summary>
        public async Task StartAsync()
        {
            // Start auto-refresh i baggrunden
            _services.BackgroundTokenRefresher.Start();

            // 1) Prøv at hente aktiv session
            var activeUser = _services.SessionService.GetActiveUser();

            if (activeUser != null)
            {
                // Brug den nye TokenRefreshResult-kontrakt
                var refreshResult = await _services.TokenRefreshService.TryRefreshAsync(activeUser);

                if (refreshResult.IsSuccess)
                {
                    // Auto-login OK → direkte til main
                    ShowMainWindow(activeUser);
                    return;
                }

                // Tokens virker ikke (eller konfig/problem) → ryd aktiv session ved permanente fejl
                switch (refreshResult.Status)
                {
                    case TokenRefreshStatus.NoTokenData:
                    case TokenRefreshStatus.NoRefreshToken:
                    case TokenRefreshStatus.InvalidGrant:
                        // Der er reelt ikke mere brugbar auth for denne bruger
                        _services.SessionService.ClearActiveUser();
                        break;

                    // NetworkError / ConfigMissing / UnknownError:
                    // Vi lader sessionen være, men falder tilbage til login-vinduet.
                    default:
                        break;
                }
            }

            // 2) Ellers: vis login-dialog
            var (success, user) = ShowLoginDialog();

            if (!success || user == null)
            {
                _app.Shutdown();
                return;
            }

            // 3) Gem aktiv bruger og åbn main
            _services.SessionService.SetActiveUser(user);
            ShowMainWindow(user);
        }

        public void Stop()
        {
            _services.BackgroundTokenRefresher.Stop();
        }

        /// <summary>
        /// Viser login-dialog og returnerer om login lykkedes + brugeren.
        /// </summary>
        private (bool success, UserAccount? user) ShowLoginDialog()
        {
            var vm = _services.CreateLoginViewModel();

            var window = new LoginWindow
            {
                DataContext = vm
            };

            UserAccount? loggedInUser = null;

            vm.LoginSucceeded += user =>
            {
                loggedInUser = user;
                window.DialogResult = true;   // fortæller ShowDialog at login var OK
            };

            bool? result = window.ShowDialog();

            return (result == true && loggedInUser != null, loggedInUser);
        }

        /// <summary>
        /// Åbner hovedvinduet og håndterer logout.
        /// </summary>
        private void ShowMainWindow(UserAccount user)
        {
            var mainVm = _services.CreateMainViewModel(user);

            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            // Handle logout requests
            mainVm.LogoutRequested += () =>
            {
                // Change ShutdownMode to prevent app from closing
                _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Clear active user session
                _services.SessionService.ClearActiveUser();

                // Close main window
                mainWindow.Close();

                // Show the login dialog again
                var (success, newUser) = ShowLoginDialog();
                if (!success || newUser == null)
                {
                    _app.Shutdown();
                    return;
                }

                // New user logged in, set active session
                _services.SessionService.SetActiveUser(newUser);

                // Show main window with the new user
                ShowMainWindow(newUser);
            };

            // Set the MainWindow property
            _app.MainWindow = mainWindow;   // vigtigt for WPF ShutdownMode

            // Set shutdown mode to close app when main window closes
            _app.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Show the main window
            mainWindow.Show();
        }
    }
}
