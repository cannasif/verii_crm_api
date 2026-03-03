using System.Security.Cryptography;
using System.Text;
using crm_api.Interfaces;

namespace crm_api.Services
{
    public class AesGcmEncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly ILogger<AesGcmEncryptionService> _logger;

        public AesGcmEncryptionService(IConfiguration configuration, ILogger<AesGcmEncryptionService> logger)
        {
            _logger = logger;
            var configuredKey = configuration["Security:DataProtectionKey"];
            _key = NormalizeKey(configuredKey);
        }

        public string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain))
            {
                return string.Empty;
            }

            var nonce = RandomNumberGenerator.GetBytes(12);
            var plainBytes = Encoding.UTF8.GetBytes(plain);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[16];

            using var aesGcm = new AesGcm(_key, 16);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            var payload = new byte[1 + nonce.Length + tag.Length + cipherBytes.Length];
            payload[0] = 1;
            Buffer.BlockCopy(nonce, 0, payload, 1, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, 1 + nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherBytes, 0, payload, 1 + nonce.Length + tag.Length, cipherBytes.Length);

            return Convert.ToBase64String(payload);
        }

        public string Decrypt(string cipher)
        {
            if (string.IsNullOrEmpty(cipher))
            {
                return string.Empty;
            }

            var payload = Convert.FromBase64String(cipher);
            if (payload.Length < 29)
            {
                throw new InvalidOperationException("Encrypted payload is invalid.");
            }

            var version = payload[0];
            if (version != 1)
            {
                throw new InvalidOperationException("Encrypted payload version is not supported.");
            }

            var nonce = new byte[12];
            var tag = new byte[16];
            var cipherBytes = new byte[payload.Length - 29];

            Buffer.BlockCopy(payload, 1, nonce, 0, nonce.Length);
            Buffer.BlockCopy(payload, 13, tag, 0, tag.Length);
            Buffer.BlockCopy(payload, 29, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];
            using var aesGcm = new AesGcm(_key, 16);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        private byte[] NormalizeKey(string? configuredKey)
        {
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                _logger.LogWarning("Security:DataProtectionKey is not configured. Falling back to deterministic key hash.");
                return SHA256.HashData(Encoding.UTF8.GetBytes("crm-api-google-integration-default-key"));
            }

            byte[] rawKey;
            try
            {
                rawKey = Convert.FromBase64String(configuredKey);
            }
            catch (FormatException)
            {
                rawKey = Encoding.UTF8.GetBytes(configuredKey);
            }

            if (rawKey.Length is 16 or 24 or 32)
            {
                return rawKey;
            }

            return SHA256.HashData(rawKey);
        }
    }
}
