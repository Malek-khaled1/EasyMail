namespace MailCore.Interfaces
{
    public interface IDialogService
    {
        bool ShowConfirm(string title, string message, string confirmText = "OK", string cancelText = "Cancel");
        void ShowError(string message, string title = "Error");


    }



}
