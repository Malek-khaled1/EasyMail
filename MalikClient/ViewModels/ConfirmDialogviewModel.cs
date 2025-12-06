using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MalikClient.ViewModels
{
    /// <summary>
    /// ViewModel for en simpel confirm-dialog.
    /// Har KUN ansvar for tekster og commands.
    /// Selve vindues-lukningen håndteres af View via RequestClose-event.
    /// </summary>
    public partial class ConfirmDialogViewModel : ObservableObject
    {
        /// <summary>
        /// Event der fyres, når dialogen skal lukkes.
        /// bool = true  → DialogResult = true (confirm)
        /// bool = false → DialogResult = false (cancel)
        /// </summary>
        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string message;

        [ObservableProperty]
        private string confirmText;

        [ObservableProperty]
        private string cancelText;

        public ConfirmDialogViewModel(string title, string message, string confirmText, string cancelText)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
        }

        [RelayCommand]
        private void Confirm()
        {
            // Fortæl view at dialogen skal lukkes med "OK"
            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            // Fortæl view at dialogen skal lukkes med "Cancel"
            RequestClose?.Invoke(false);
        }
    }
}
