using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using CafePOS.Helpers;

namespace CafePOS.Services;

/// <summary>
/// License system based on Hardware ID + RSA digital signature.
/// Flow: 
///   1. App generates a unique HardwareId from CPU + Disk + Board.
///   2. User sends HardwareId to vendor.
///   3. Vendor signs it with RSA private key → license key.
///   4. User enters license key in app.
///   5. App verifies signature with embedded public key.
/// </summary>
public static class LicenseService
{
    // RSA Public Key (PEM) — Replace with your actual public key before distribution.
    // Generate a keypair: openssl genrsa -out private.pem 2048
    //                     openssl rsa -in private.pem -pubout -out public.pem
    private const string RSA_PUBLIC_KEY = @"
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Z3VS5JJcds3xfn/ygWe
HKbNiFCsfrHhi8UGPHtBNR2cG1Tq7+E6pLLGNqCAL5Q6nGBOUjMB5SXCN+vv5h3
S6aV0TAruGFmeN+jM7QNniKJuWFqGOJ5RLvN4fM3hFziK7xVNmq4MHelMjRNDd3n
Q7FJJq/g4kfFqUNdJXt0y7CQRVdp+P4PkLwHPd/xBN0dSGeCV5yai0JQVbOGRfnJ
G3KGbFjWM9lBsEOXnn2bHDBKq+2M9qeHBYOE3NxM5LYnpLNoCx+BRCF4cT2Rwzs
WjKbnJDOJFPFkHF0cPA5RZQH7NHaGQnBiiVH6SQdMWpMVsOH7oSwlQ1yL2OlQ6U8
oQIDAQAB
-----END PUBLIC KEY-----";

    private static readonly string _licenseFilePath;

    static LicenseService()
    {
        _licenseFilePath = AppPaths.LicenseFile;
    }

    /// <summary>
    /// Generates a unique Hardware ID from CPU, Disk, and Motherboard serials.
    /// </summary>
    public static string GetHardwareId()
    {
        var raw = new StringBuilder();

        // CPU ID
        raw.Append(GetWmiValue("Win32_Processor", "ProcessorId"));

        // Disk serial (first physical disk)
        raw.Append(GetWmiValue("Win32_DiskDrive", "SerialNumber"));

        // Motherboard serial
        raw.Append(GetWmiValue("Win32_BaseBoard", "SerialNumber"));

        // Hash it to a clean, fixed-length string
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw.ToString()));
        var hash = Convert.ToHexString(bytes);

        // Format as XXXX-XXXX-XXXX-XXXX (first 16 hex chars, 4 groups)
        return $"{hash[..4]}-{hash[4..8]}-{hash[8..12]}-{hash[12..16]}";
    }

    /// <summary>
    /// Checks if a valid license exists.
    /// </summary>
    public static bool IsLicensed()
    {
        try
        {
            if (!File.Exists(_licenseFilePath))
                return false;

            var licenseKey = File.ReadAllText(_licenseFilePath).Trim();
            return VerifyLicense(licenseKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Activates a license by verifying the key and saving it.
    /// </summary>
    public static bool ActivateLicense(string licenseKey)
    {
        licenseKey = licenseKey.Trim();

        if (!VerifyLicense(licenseKey))
            return false;

        File.WriteAllText(_licenseFilePath, licenseKey);
        return true;
    }

    /// <summary>
    /// Verifies a license key against the current hardware ID.
    /// The license key is a Base64-encoded RSA signature of the hardware ID.
    /// </summary>
    // Master key for development/in-house deployment.
    // This key bypasses hardware-specific verification.
    private const string MASTER_KEY = "E8F2C4A7-D9B1-4F03-8E65-3A2C7D9B0F41-CAFEPOS-MASTER-KEY-2024";

    public static bool VerifyLicense(string licenseKey)
    {
        try
        {
            // Check master key first (for in-house/dev use)
            if (licenseKey == MASTER_KEY)
                return true;

            var hardwareId = GetHardwareId();
            var signatureBytes = Convert.FromBase64String(licenseKey);
            var dataBytes = Encoding.UTF8.GetBytes(hardwareId);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(RSA_PUBLIC_KEY);

            return rsa.VerifyData(dataBytes, signatureBytes,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets remaining trial days. Returns -1 if licensed.
    /// Trial: 14 days from first run.
    /// </summary>
    public static int GetTrialDaysRemaining()
    {
        if (IsLicensed())
            return -1; // Fully licensed

        var trialStartFile = AppPaths.TrialFile;

        DateTime trialStart;

        if (File.Exists(trialStartFile))
        {
            if (!DateTime.TryParse(File.ReadAllText(trialStartFile).Trim(), out trialStart))
                trialStart = DateTime.Now;
        }
        else
        {
            trialStart = DateTime.Now;
            File.WriteAllText(trialStartFile, trialStart.ToString("yyyy-MM-dd"));
        }

        var elapsed = (DateTime.Now - trialStart).Days;
        var remaining = 14 - elapsed;
        return remaining < 0 ? 0 : remaining;
    }

    /// <summary>
    /// Checks if the app can run (licensed or trial not expired).
    /// </summary>
    public static bool CanRun()
    {
        return IsLicensed() || GetTrialDaysRemaining() > 0;
    }

    // --- WMI Helper ---

    private static string GetWmiValue(string wmiClass, string propertyName)
    {
        try
        {
            var query = $"SELECT {propertyName} FROM {wmiClass}";
            using var searcher = new ManagementObjectSearcher(query);

            var task = Task.Run(() =>
            {
                var result = "UNKNOWN";
                foreach (var obj in searcher.Get())
                {
                    var val = obj[propertyName]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(val))
                    {
                        result = val;
                        break;
                    }
                }
                return result;
            });

            if (task.Wait(TimeSpan.FromSeconds(5)))
                return task.Result;
        }
        catch
        {
            // Silently handle WMI failures
        }
        return "UNKNOWN";
    }
}

/// <summary>
/// Offline license key generator tool (for the vendor/developer).
/// This should be a separate console app, NOT distributed with the product.
/// 
/// Usage example:
///   var privateKeyPem = File.ReadAllText("private.pem");
///   var key = LicenseGenerator.GenerateKey("A1B2-C3D4-E5F6-G7H8", privateKeyPem);
/// </summary>
public static class LicenseGenerator
{
    public static string GenerateKey(string hardwareId, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var dataBytes = Encoding.UTF8.GetBytes(hardwareId);
        var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signature);
    }
}
