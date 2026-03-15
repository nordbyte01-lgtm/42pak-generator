using System.Security.Cryptography;
using System.Text;

namespace FortyTwoPak.Core.Crypto;

public static class KeyDerivation
{
    public static byte[] GenerateSalt()
    {
        byte[] salt = new byte[VpkFormat.VpkConstants.SaltLength];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    public static byte[] DeriveKey(string passphrase, byte[] salt, int outputLength = 32)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be empty.", nameof(passphrase));
        if (salt.Length < 16)
            throw new ArgumentException("Salt must be at least 16 bytes.", nameof(salt));

        byte[] contextualPassphrase = Encoding.UTF8.GetBytes($"42PK-v1:{passphrase}");

        using var pbkdf2 = new Rfc2898DeriveBytes(
            contextualPassphrase,
            salt,
            VpkFormat.VpkConstants.Pbkdf2Iterations,
            HashAlgorithmName.SHA512);

        return pbkdf2.GetBytes(outputLength);
    }
}
