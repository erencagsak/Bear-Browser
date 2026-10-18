using System;
using System.Security.Cryptography;
using System.Text;
using Bear_Browser.Platform.Interfaces;

namespace Bear_Browser.Platform.WinUI
{
    public class WindowsDataProtectionService : IDataProtectionService
    {
        public byte[] Protect(byte[] userData, byte[]? optionalEntropy = null)
        {
            if (userData == null || userData.Length == 0) return Array.Empty<byte>();
            return ProtectedData.Protect(userData, optionalEntropy, DataProtectionScope.CurrentUser);
        }

        public byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null)
        {
            if (encryptedData == null || encryptedData.Length == 0) return Array.Empty<byte>();
            return ProtectedData.Unprotect(encryptedData, optionalEntropy, DataProtectionScope.CurrentUser);
        }

        public string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = Protect(bytes);
            return Convert.ToBase64String(encrypted);
        }

        public string UnprotectString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            try
            {
                byte[] bytes = Convert.FromBase64String(cipherText);
                byte[] decrypted = Unprotect(bytes);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return cipherText;
            }
        }
    }
}
