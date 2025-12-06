using MailCore.Interfaces;
using MailCore.Models;
using MailCore.Services;
using MailCore.Transports;
using MalikClient.Services;
using MalikClient.ViewModels;

namespace MalikClient.Boot
{
    public class AppServices
    {
        public UserService UserService { get; private set; } = null!;
        public TokenService TokenService { get; private set; } = null!;
        public ITokenRefreshService TokenRefreshService { get; private set; } = null!;
        public IOAuthService OAuthService { get; private set; } = null!;
        public INetworkService NetworkService { get; private set; } = null!;
        public IDialogService DialogService { get; private set; } = null!;
        public AppStateService AppStateService { get; private set; } = null!;
        public SessionService SessionService { get; private set; } = null!;
        public IBackgroundTokenRefresher BackgroundTokenRefresher { get; private set; } = null!;
        public IEmailService EmailService { get; private set; } = null!;
        public IEmailCacheRepository EmailCacheRepository { get; private set; } = null!;
        public IInboxCacheRepository InboxCacheRepository { get; private set; } = null!;




        public AppServices()
        {
            // Opret Database
            var db = new DatabaseService();

            // Opret UserService
            UserService = new UserService(db);

            // Opret TokenService
            TokenService = new TokenService(db);

            // Opret InboxCacheRepository (SQLite baseret)
            InboxCacheRepository = new InboxCacheRepository(db);


            // Opret email-cache repository (SQLite baseret)
            EmailCacheRepository = new EmailCacheRepository(db);


            // Opret TokenRefreshService
            TokenRefreshService = new TokenRefreshService(TokenService);

            // Opret IOAuthService
            OAuthService = new OAuthService();

            // Opret DialogService
            DialogService = new DialogService();

            // Opret NetworkService
            NetworkService = new NetworkService();

            AppStateService = new AppStateService(db);
            SessionService = new SessionService(AppStateService, UserService);

            // Opret EmailTransport + EmailService (SMTP via Gmail)
            var smtpTransport = new SendTransport();
            // Opret Inbox-transport (via Gmail)
            var inboxTransport = new InboxTransport();
            EmailService = new EmailService(TokenRefreshService,
                                            smtpTransport, 
                                            inboxTransport,
                                            EmailCacheRepository,
                                            InboxCacheRepository
                                            );
            
            // Auto-refresh service
            BackgroundTokenRefresher = new BackgroundTokenRefresher(
                TokenRefreshService,
                NetworkService,
                SessionService
            );


        }

        public LoginViewModel CreateLoginViewModel()
        {
            return new LoginViewModel(
                OAuthService,
                TokenService,
                UserService,
                DialogService,
                NetworkService,
                TokenRefreshService
            );
        }



        public MainViewModel CreateMainViewModel(UserAccount user)
        {
            return new MainViewModel(
                
                user,
                EmailService,
                DialogService,
                NetworkService,
                TokenService,
                SessionService);
        }
    }
}
