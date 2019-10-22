using System;
using System.Security.Cryptography;
using System.Text;

namespace KeySmith.Internals.Locks
{
    class IdentifierGenerator : IDisposable
    {
        private static readonly char[] _chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-_".ToCharArray();

        private readonly RNGCryptoServiceProvider _rngCryptoServiceProvider;

        public IdentifierGenerator()
        {
            _rngCryptoServiceProvider = new RNGCryptoServiceProvider();
        }

        public void Dispose() => _rngCryptoServiceProvider?.Dispose();

        public string GetUniqueKey(int size)
        {
            var data = new byte[size];
            _rngCryptoServiceProvider.GetBytes(data);
            var result = new StringBuilder(size);
            foreach (var b in data)
            {
                result.Append(_chars[b % _chars.Length]);
            }
            return result.ToString();
        }
    }
}