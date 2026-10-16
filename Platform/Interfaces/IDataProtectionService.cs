namespace Bear_Browser.Platform.Interfaces
{
    /// <summary>
    /// Veri şifreleme ve çözme işlemleri için platform bağımsız sözleşme.
    /// </summary>
    public interface IDataProtectionService
    {
        /// <summary>
        /// Ham byte dizisini şifreler.
        /// </summary>
        byte[] Protect(byte[] userData, byte[]? optionalEntropy = null);

        /// <summary>
        /// Şifrelenmiş byte dizisini çözer.
        /// </summary>
        byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null);

        /// <summary>
        /// Düz metni şifreleyip Base64 formatında döndürür.
        /// </summary>
        string ProtectString(string plainText);

        /// <summary>
        /// Base64 formatındaki şifreli metni çözer.
        /// </summary>
        string UnprotectString(string cipherText);
    }
}
