using System.Security.Cryptography;
using System.Text;

namespace EasyNoteVault
{
    public static class CryptoHelper
    {
        // v1：固定主密码（以后可升级成用户输入）
        private static readonly string Password = "EasyNoteVault@2026";
        private static readonly byte[] Salt =
            Encoding.UTF8.GetBytes("EasyNoteVault_Salt");

        public static byte[] Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            using var key = new Rfc2898DeriveBytes(
                Password, Salt, 10000, HashAlgorithmName.SHA256);

            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);

            using var encryptor = aes.CreateEncryptor();
            var bytes = Encoding.UTF8.GetBytes(plainText);
            return encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        }

        public static string Decrypt(byte[] cipherBytes)
        {
            using var aes = Aes.Create();
            using var key = new Rfc2898DeriveBytes(
                Password, Salt, 10000, HashAlgorithmName.SHA256);

            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);

            using var decryptor = aes.CreateDecryptor();
            var bytes = decryptor.TransformFinalBlock(
                cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
