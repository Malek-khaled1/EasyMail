using MalikClient.Boot;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MalikClient
{
    public partial class App : Application
    {
        private AppServices _services = null!;
        private AppNavigator _navigator = null!;

        public App()
        {
            //// -------------- GLOBAL FEJLHÅNDTERING --------------

            //// UI-tråd exceptions (WPF)
            //this.DispatcherUnhandledException += (s, e) =>
            //{
            //    LogException("UI THREAD EXCEPTION", e.Exception);

            //    MessageBox.Show(
            //        "UI FEJL: " + e.Exception.Message +
            //        "\n\nSe detaljer i error.log",
            //        "UI Fejl");

            //    e.Handled = true;
            //};

            //// Task / background thread exceptions
            //TaskScheduler.UnobservedTaskException += (s, e) =>
            //{
            //    LogException("TASK EXCEPTION", e.Exception);
            //    e.SetObserved();
            //};

            //// AppDomain exceptions (threadpool, unmanaged, mv.)
            //AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            //{
            //    LogException("GLOBAL EXCEPTION", e.ExceptionObject as Exception);
            //};

            //// UI Freeze Detector (opdager deadlocks / blokeringer)
            //StartUiFreezeDetector();
        }

        // -------------- LOGGER (skriver ALT til error.log) --------------

        //private void LogException(string type, Exception ex)
        //{
        //    try
        //    {
        //        var sb = new StringBuilder();

        //        sb.AppendLine("========== " + type + " ==========");
        //        sb.AppendLine("Time: " + DateTime.Now);
        //        sb.AppendLine("Message: " + ex?.Message);
        //        sb.AppendLine("Type: " + ex?.GetType().FullName);
        //        sb.AppendLine("Source: " + ex?.Source);
        //        sb.AppendLine("StackTrace:");
        //        sb.AppendLine(ex?.StackTrace);

        //        // Inner exceptions
        //        Exception inner = ex?.InnerException;
        //        while (inner != null)
        //        {
        //            sb.AppendLine("\n--- INNER EXCEPTION ---");
        //            sb.AppendLine("Message: " + inner.Message);
        //            sb.AppendLine("Type: " + inner.GetType().FullName);
        //            sb.AppendLine("StackTrace:");
        //            sb.AppendLine(inner.StackTrace);

        //            inner = inner.InnerException;
        //        }

        //        File.AppendAllText("error.log", sb.ToString() + "\n\n");
        //    }
        //    catch
        //    {
        //        // Log-fejl ignoreres for at undgå secondary crashes
        //    }
        //}

        //// -------------- UI FREEZE DETECTOR --------------

        //private void StartUiFreezeDetector()
        //{
        //    Task.Run(async () =>
        //    {
        //        while (true)
        //        {
        //            var before = DateTime.Now;

        //            // Hvis UI-tråden er blokeret, bliver denne InvokeAsync forsinket
        //            await Dispatcher.InvokeAsync(() => { });

        //            var delta = DateTime.Now - before;

        //            if (delta.TotalSeconds > 1.5)
        //            {
        //                LogException("UI FREEZE DETECTED",
        //                    new Exception($"UI froze for {delta.TotalSeconds:F1} seconds. " +
        //                                  "Der er sandsynligvis et blocking call på UI-tråden."));
        //            }
        //        }
        //    });
        //}

        // -------------- APPLIKATION START/STOP --------------

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Opret services & navigation (composition root)
            _services = new AppServices();
            _navigator = new AppNavigator(_services, this);

            await _navigator.StartAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _navigator?.Stop();
            base.OnExit(e);
        }
    }
}
