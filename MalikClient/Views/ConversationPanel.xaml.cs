using System.Windows;
using System.Windows.Controls;
using MalikClient.ViewModels;
using MalikClient.Helpers; // Husk at inkludere helperen
using MailCore.Models;

namespace MalikClient.Views
{
    public partial class ConversationPanel : UserControl
    {
        public ConversationPanel()
        {
            InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MainViewModel vm)
            {
                // Lyt på ændringer i SelectedConversation
                vm.PropertyChanged -= ViewModel_PropertyChanged;
                vm.PropertyChanged += ViewModel_PropertyChanged;

                // Hvis der allerede er valgt en, render den
                if (vm.SelectedConversation != null)
                {
                    RenderConversation(vm.SelectedConversation);
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedConversation))
            {
                var vm = sender as MainViewModel;
                if (vm?.SelectedConversation != null)
                {
                    RenderConversation(vm.SelectedConversation);
                }
                else
                {
                    // Ryd viewet hvis null
                    RenderEmpty();
                }
            }
        }

        private async void RenderConversation(EmailConversation conversation)
        {
            if (ConversationBrowser == null) return;

            // Sørg for at WebView2 er klar
            try
            {
                if (ConversationBrowser.CoreWebView2 == null)
                    await ConversationBrowser.EnsureCoreWebView2Async();
            }
            catch
            {
                return; // Håndter manglende runtime
            }

            // 1. Byg den store HTML string via vores Helper
            string fullHtml = ConversationHtmlBuilder.BuildConversationHtml(conversation.Messages);

            // 2. Naviger til stringen
            ConversationBrowser.NavigateToString(fullHtml);
        }

        private async void RenderEmpty()
        {
            if (ConversationBrowser?.CoreWebView2 != null)
            {
                ConversationBrowser.NavigateToString("<html><body></body></html>");
            }
        }
    }
}