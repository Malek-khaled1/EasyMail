using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailCore.Interfaces;
using MailCore.Models;
using MailCore.Services;

namespace MalikClient.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        public event Action<UserAccount>? LoginSucceeded;

        private readonly IOAuthService _auth;
        private readonly IDialogService _dialogService;
        private readonly INetworkService _network;
        private readonly ITokenRefreshService _tokenRefresh;
        private readonly TokenService _tokenService;
        private readonly UserService _userService;

        [ObservableProperty]
        private bool loginSuccessful;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private UserAccount? selectedUser;

        [ObservableProperty]
        private bool isBusy;

        public ObservableCollection<UserAccount> Users { get; } = new();

        public bool HasExistingUsers => Users.Count > 0;

        public LoginViewModel(
            IOAuthService auth,
            TokenService tokenService,
            UserService userService,
            IDialogService dialogService,
            INetworkService network,
            ITokenRefreshService tokenRefresh)
        {
            _auth = auth;
            _tokenService = tokenService;
            _userService = userService;
            _dialogService = dialogService;
            _network = network;
            _tokenRefresh = tokenRefresh;

            // Lynhurtig opstart uden pauser
            _ = LoadExistingUsersAsync();
        }

        private async Task LoadExistingUsersAsync()
        {
            try
            {
                IsBusy = true;
                // Ingen delay her - bare hent data
                var usersFromDb = await Task.Run(() => _userService.GetAllUsers());

                Users.Clear();
                foreach (var user in usersFromDb)
                {
                    Users.Add(user);
                }
                OnPropertyChanged(nameof(HasExistingUsers));
            }
            catch (Exception)
            {
                StatusMessage = "Failed to load users.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoogleLoginAsync()
        {
            if (IsBusy) return;

            LoginSuccessful = false;
            StatusMessage = "Redirecting to Google...";

            if (!_network.HasInternet())
            {
                StatusMessage = "No internet connection.";
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            OAuthResult? result;

            try
            {
                IsBusy = true;
                result = await _auth.LoginAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Login interrupted.";
                IsBusy = false;
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Login failed: {ex.Message}";
                IsBusy = false;
                return;
            }

            if (result == null)
            {
                StatusMessage = "Login cancelled.";
                IsBusy = false;
                return;
            }

            // Opret brugerobjektet (UDEN PinHash)
            var user = new UserAccount
            {
                Email = result.Email,
                DisplayName = result.Name ?? string.Empty,
                ImagePath = string.IsNullOrWhiteSpace(result.PictureUrl)
                    ? "/Assets/default-user.png"
                    : result.PictureUrl,
                CreatedAt = DateTime.Now
                // PinHash linjen er fjernet her
            };

            try
            {
                StatusMessage = "Saving account...";

                // Kør DB arbejde
                int userId = await Task.Run(() =>
                {
                    int id = _userService.SaveUser(user);
                    _tokenService.SaveToken(id, result);
                    return id;
                });

                // --- UX PAUSE 1 ---
                await Task.Delay(400);

                user.Id = userId;

                if (!Users.Any(u => u.Email == user.Email))
                {
                    Users.Add(user);
                    OnPropertyChanged(nameof(HasExistingUsers));
                }

                SelectedUser = user;
                StatusMessage = "Login successful.";

                // --- UX PAUSE 2 ---
                await Task.Delay(300);

                LoginSuccessful = true;
                LoginSucceeded?.Invoke(user);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Database error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SelectUser(UserAccount user)
        {
            if (user == null || IsBusy) return;

            LoginSuccessful = false;
            StatusMessage = "Checking session...";
            IsBusy = true;

            try
            {
                var refreshResult = await _tokenRefresh.TryRefreshAsync(user);

                if (!refreshResult.IsSuccess)
                {
                    StatusMessage = refreshResult.Status switch
                    {
                        TokenRefreshStatus.InvalidGrant => "Session expired. Please log in again.",
                        TokenRefreshStatus.ConfigMissing => "Configuration missing.",
                        TokenRefreshStatus.NetworkError => "No internet connection.",
                        _ => "Login error."
                    };
                    return;
                }

                // --- UX PAUSE: Gør oplevelsen "blød" ved eksisterende login ---
                StatusMessage = $"Logging in as {user.Email}…";
                await Task.Delay(400);

                StatusMessage = "Loading inbox...";
                await Task.Delay(300);

                SelectedUser = user;
                LoginSuccessful = true;
                LoginSucceeded?.Invoke(user);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteUser(UserAccount user)
        {
            if (user == null || IsBusy) return;

            bool confirmed = _dialogService.ShowConfirm(
                title: "Remove account",
                message: $"Remove {user.Email}?",
                confirmText: "Remove",
                cancelText: "Cancel"
            );

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Removing account...";

                // Sletning sker med det samme uden delay
                await Task.Run(() =>
                {
                    _userService.DeleteUserById(user.Id);
                    _tokenService.DeleteCacheToken(user.Id);
                });
                Users.Remove(user);
                OnPropertyChanged(nameof(HasExistingUsers));

                // Beskeden bliver stående, så man kan se, hvad der skete
                StatusMessage = $"Removed {user.Email}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing user: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}