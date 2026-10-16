using System;
using System.Security.Cryptography;
using System.Text;
using Bear_Browser.Platform.Interfaces;

namespace Bear_Browser.Platform.WinUI
{
    /// <summary>
    /// Windows DPAPI (Data Protection API) kullanarak veri şifreleme ve çözme servisi.
    /// </summary>
    public class WindowsDataProtectionService : IDataProtectionService
    {
        /// <inheritdoc />
        public byte[] Protect(byte[] userData, byte[]? optionalEntropy = null)
        {
            if (userData == null || userData.Length == 0) return Array.Empty<byte>();
            return ProtectedData.Protect(userData, optionalEntropy, DataProtectionScope.CurrentUser);
        }

        /// <inheritdoc />
        public byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null)
        {
            if (encryptedData == null || encryptedData.Length == 0) return Array.Empty<byte>();
            return ProtectedData.Unprotect(encryptedData, optionalEntropy, DataProtectionScope.CurrentUser);
        }

        /// <inheritdoc />
        public string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = Protect(bytes);
            return Convert.ToBase64String(encrypted);
        }

        /// <inheritdoc />
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
                // Şifrelenmemiş düz metin (eski sürüm uyumluluğu) durumunda veriyi olduğu gibi koru
                return cipherText;
            }
        }
    }
}
