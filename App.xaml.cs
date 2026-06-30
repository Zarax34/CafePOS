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

        // Ensure data directory exists
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDir);

        // Initialize database
        DatabaseContext.Initialize();

        // License check
        if (!LicenseService.IsLicensed())
        {
            if (!LicenseService.CanRun())
            {
                // Trial expired, must activate
                var licenseWin = new LicenseWindow();
                licenseWin.ShowDialog();

                if (!licenseWin.Authorized)
                {
                    Shutdown();
                    return;
                }
            }
            else
            {
                // Trial still active — show license window but allow skip
                var remaining = LicenseService.GetTrialDaysRemaining();
                if (remaining <= 7) // Show reminder in last 7 days
                {
                    var licenseWin = new LicenseWindow();
                    licenseWin.ShowDialog();

                    if (!licenseWin.Authorized)
                    {
                        Shutdown();
                        return;
                    }
                }
            }
        }

        // Show login window
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }
}
