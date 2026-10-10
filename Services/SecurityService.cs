using System.Security.Cryptography;
using System.Text;

namespace Uttu.Services;

public class SecurityService
{
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipherBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecurityService] Failed to encrypt data with DPAPI: {ex.Message}");
            return string.Empty;
        }
    }

    public static string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecurityService] Failed to decrypt data with DPAPI: {ex.Message}");
            return string.Empty;
        }
    }
}
