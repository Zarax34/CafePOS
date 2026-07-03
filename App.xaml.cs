using System.IO;
using System.Windows;
using CafePOS.Data;
using CafePOS.Services;
using CafePOS.Views;

namespace CafePOS;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startupLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "startup.log");
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDir);

        void Log(string msg)
        {
            try { File.AppendAllText(startupLog, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } catch { }
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "cafepos_startup.log"), $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } catch { }
        }

        Log("OnStartup started");

        // Global exception handling
        DispatcherUnhandledException += (_, args) =>
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "error.log");
            try { File.WriteAllText(logPath, $"[{DateTime.Now}] {args.Exception}"); } catch { }
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "error.log");
            try { File.WriteAllText(logPath, $"[{DateTime.Now}] Unhandled: {args.ExceptionObject}"); } catch { }
        };

        Log("Exception handlers registered");

        // Initialize database
        DatabaseContext.Initialize();
        Log("Database initialized");

        // License check
        if (!LicenseService.IsLicensed())
        {
            Log("Not licensed, checking trial...");
            if (!LicenseService.CanRun())
            {
                // Trial expired, must activate
                var licenseWin = new LicenseWindow();
                licenseWin.ShowDialog();

                if (!licenseWin.Authorized)
                {
                    Log("License not authorized — shutting down");
                    Shutdown();
                    return;
                }
            }
            else
            {
                // Trial still active — show license window but allow skip
                var remaining = LicenseService.GetTrialDaysRemaining();
                Log($"Trial active, days remaining: {remaining}");
                if (remaining <= 7) // Show reminder in last 7 days
                {
                    var licenseWin = new LicenseWindow();
                    licenseWin.ShowDialog();

                    if (!licenseWin.Authorized)
                    {
                        Log("License not authorized — shutting down");
                        Shutdown();
                        return;
                    }
                }
            }
        }

        Log("License check done — showing LoginWindow");
        // Show login window
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }
}
