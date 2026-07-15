using System.IO;

namespace CafePOS.Helpers;

public static class AppPaths
{
    private static readonly string _dataDir;

    static AppPaths()
    {
        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CafePOS"
        );
        Directory.CreateDirectory(_dataDir);
    }

    public static string DataDirectory => _dataDir;

    public static string GetPath(string relativePath)
    {
        return Path.Combine(_dataDir, relativePath);
    }

    public static string DatabasePath => GetPath("cafepos.db");

    public static string StartupLog => GetPath("startup.log");

    public static string ErrorLog => GetPath("error.log");

    public static string LogoPath => GetPath("logo.png");

    public static string TrialFile => GetPath(".trial");

    public static string LicenseFile => GetPath("license.dat");
}
