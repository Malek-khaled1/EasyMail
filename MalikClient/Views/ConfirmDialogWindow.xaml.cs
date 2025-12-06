using System.Windows;
using MalikClient.ViewModels;

namespace MalikClient.Views
{
    public partial class ConfirmDialogWindow : Window
    {
        public ConfirmDialogWindow(string title, string message, string confirmText, string cancelText)
        {
            InitializeComponent();

            var vm = new ConfirmDialogViewModel(title, message, confirmText, cancelText);

            // Abonnér på ViewModelens RequestClose-event
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };

            DataContext = vm;
        }
    }
}
