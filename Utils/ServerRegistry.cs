using Microsoft.Win32;
using System;

namespace ServerViTrader.Utils
{
    class ServerRegistry : IDisposable
    {
        readonly RegistryKey key;

        static string registryLocation = @"SOFTWARE\serversettings";
        static string addressKey = "address";
        static string portKey = "port";
        static string emailKey = "email";
        static string passwordKey = "password";
        static string aesKey = "aes";

        public ServerRegistry()
        {
            key = Registry.CurrentUser.CreateSubKey(registryLocation);
        }

        public string GetAddress() => key.GetValue(addressKey).ToString();

        public int GetPort() => int.Parse(key.GetValue(portKey).ToString());

        public string GetEmail() => key.GetValue(emailKey).ToString();

        public string GetPassword() => key.GetValue(passwordKey).ToString();

        public byte[] GetAesKey() => (byte[])key.GetValue(aesKey);

        public void SetAddress(string address) => key.SetValue(addressKey, address);

        public void SetPort(int port) => key.SetValue(portKey, port);

        public void SetEmail(string email) => key.SetValue(emailKey, email);

        public void SetPassword(string password) => key.SetValue(passwordKey, password);

        public void SetAesKey(byte[] aes) => key.SetValue(aesKey, aes);

        public void Dispose() => key.Close();
    }
}
