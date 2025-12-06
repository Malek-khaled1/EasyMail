using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace MalikClient.Views
{
    /// <summary>
    /// Viser email-body som HTML.
    /// - Bruger HtmlBody hvis den findes
    /// - Falder tilbage til TextBody (som vi selv wrap'er i simpel HTML)
    /// Al logik ligger i view-laget.
    /// </summary>
    public partial class HtmlEmailBodyControl : UserControl
    {
        public HtmlEmailBodyControl()
        {
            InitializeComponent();
            Loaded += HtmlEmailBodyControl_Loaded;
        }

        public static readonly DependencyProperty BodyHtmlProperty =
            DependencyProperty.Register(
                nameof(BodyHtml),
                typeof(string),
                typeof(HtmlEmailBodyControl),
                new PropertyMetadata(string.Empty, OnBodyChanged));

        public static readonly DependencyProperty BodyTextProperty =
            DependencyProperty.Register(
                nameof(BodyText),
                typeof(string),
                typeof(HtmlEmailBodyControl),
                new PropertyMetadata(string.Empty, OnBodyChanged));

        public string? BodyHtml
        {
            get => (string?)GetValue(BodyHtmlProperty);
            set => SetValue(BodyHtmlProperty, value);
        }

        public string? BodyText
        {
            get => (string?)GetValue(BodyTextProperty);
            set => SetValue(BodyTextProperty, value);
        }

        private static void OnBodyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (HtmlEmailBodyControl)d;
            control.Render();
        }

        private async void HtmlEmailBodyControl_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (Browser.CoreWebView2 == null)
                    await Browser.EnsureCoreWebView2Async();
            }
            catch
            {
                // Ingen runtime → ingen crash
                return;
            }

            Render();
        }

        private void Render()
        {
            if (!IsLoaded || Browser.CoreWebView2 == null)
                return;

            var html = BuildHtml();

            try
            {
                Browser.NavigateToString(html);
            }
            catch
            {
                // Vi vil ikke crashe UI'et på fejl i HTML
            }
        }

        private string BuildHtml()
        {
            // 1) Rigtig HTML fra serveren
            if (!string.IsNullOrWhiteSpace(BodyHtml))
            {
                var content = ExtractBodyContent(BodyHtml!);

                return $@"
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        html, body {{
            margin: 0;
            padding: 0;
            background-color: transparent;
        }}

        .mail-container {{
            font-family: 'Segoe UI', system-ui, sans-serif;
            font-size: 13px;
            color: #111827;
            line-height: 1.5;
            padding: 8px 12px;
        }}

        .mail-inner {{
            max-width: 720px;
            margin: 0;
        }}

        img {{
            max-width: 100%;
            height: auto;
            border: 0;
        }}

        table {{
            max-width: 100%;
        }}

        a {{
            color: #2563EB;
            text-decoration: none;
        }}

        a:hover {{
            text-decoration: underline;
        }}
    </style>
</head>
<body>
    <div class=""mail-container"">
        <div class=""mail-inner"">
            {content}
        </div>
    </div>
</body>
</html>";
            }

            // 2) Kun TextBody → simpel HTML
            var text = BodyText ?? string.Empty;
            var encoded = WebUtility.HtmlEncode(text)
                                    .Replace("\r\n", "<br/>")
                                    .Replace("\n", "<br/>");

            return $@"
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        html, body {{
            margin: 0;
            padding: 0;
            background-color: transparent;
        }}

        .mail-container {{
            font-family: 'Segoe UI', system-ui, sans-serif;
            font-size: 13px;
            color: #111827;
            line-height: 1.5;
            padding: 8px 12px;
        }}

        .mail-inner {{
            max-width: 720px;
            margin: 0;
        }}
    </style>
</head>
<body>
    <div class=""mail-container"">
        <div class=""mail-inner"">
            {encoded}
        </div>
    </div>
</body>
</html>";
        }

        private static string ExtractBodyContent(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var lower = html.ToLowerInvariant();
            var bodyIndex = lower.IndexOf("<body", StringComparison.Ordinal);

            if (bodyIndex >= 0)
            {
                var startContent = lower.IndexOf('>', bodyIndex);
                if (startContent >= 0)
                {
                    startContent++;
                    var endBody = lower.IndexOf("</body>", startContent, StringComparison.Ordinal);
                    if (endBody > startContent)
                    {
                        return html.Substring(startContent, endBody - startContent);
                    }
                }
            }

            // Hvis ingen body-tag → brug hele html'en
            return html;
        }
    }
}
