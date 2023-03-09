using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ServerViTrader.Utils
{
    public static class AES
    {
        private const string salt = "hdut9oc4";
        private const string initVector = "fuggja4lg91tsbgz";

        private static readonly byte[] saltBytes;
        private static readonly byte[] initVectorBytes;
        private static byte[] keyBytes;

        private const int keySize = 256;

        static AES()
        {
            using var reg = new ServerRegistry();
            keyBytes = reg.GetAesKey();

            saltBytes = Encoding.UTF8.GetBytes(salt);
            initVectorBytes = Encoding.UTF8.GetBytes(initVector);
        }

        public static void UpdateKey(string password)
        {
            keyBytes = new Rfc2898DeriveBytes(password, saltBytes).GetBytes(keySize / 8);

            using var reg = new ServerRegistry();
            reg.SetAesKey(keyBytes);
        }

        public static string Encrypt(string plainText)
        {
            return Convert.ToBase64String(EncryptToBytes(plainText));
        }

        public static byte[] EncryptToBytes(string plainText)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return EncryptToBytes(plainTextBytes);
        }

        public static byte[] EncryptToBytes(byte[] plainTextBytes)
        {
            using (RijndaelManaged symmetricKey = new RijndaelManaged())
            {
                symmetricKey.Mode = CipherMode.CBC;

                using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            cryptoStream.FlushFinalBlock();

                            return memStream.ToArray();
                        }
                    }
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText.Replace(' ', '+'));
            return Decrypt(cipherTextBytes).TrimEnd('\0');
        }

        public static string Decrypt(byte[] cipherTextBytes)
        {
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];

            using (RijndaelManaged symmetricKey = new RijndaelManaged())
            {
                symmetricKey.Mode = CipherMode.CBC;

                using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
                {
                    using (MemoryStream memStream = new MemoryStream(cipherTextBytes))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read))
                        {
                            int byteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

                            return Encoding.UTF8.GetString(plainTextBytes, 0, byteCount);
                        }
                    }
                }
            }
        }
    }
}
