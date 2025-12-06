using System.Windows;
using MailCore.Interfaces;
using MalikClient.Views;

namespace MalikClient.Services
{
    public class DialogService : IDialogService
    {
        public bool ShowConfirm(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
        {
            var dialog = new ConfirmDialogWindow(title, message, confirmText, cancelText)
            {
                Owner = Application.Current.MainWindow
            };

            return dialog.ShowDialog() == true;
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

       

    }


}
