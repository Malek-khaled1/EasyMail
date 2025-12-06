using MailCore.Models;

namespace MailCore.Services
{
    public class SessionService
    {
        private readonly AppStateService _appState;
        private readonly UserService _userService;

        private const string ActiveUserKey = "ActiveUserId";

        public SessionService(AppStateService appState, UserService userService)
        {
            _appState = appState;
            _userService = userService;
        }

        public UserAccount? GetActiveUser()
        {
            var value = _appState.GetValue(ActiveUserKey);

            if (!int.TryParse(value, out var userId))
                return null;

            return _userService.GetUserById(userId);
        }

        public void SetActiveUser(UserAccount user)
        {
            _appState.SetValue(ActiveUserKey, user.Id.ToString());
        }

        public void ClearActiveUser()
        {
            _appState.SetValue(ActiveUserKey, null);
        }
    }
}
