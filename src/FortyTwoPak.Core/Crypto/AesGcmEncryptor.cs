using System.Security.Cryptography;

namespace FortyTwoPak.Core.Crypto;

public static class AesGcmEncryptor
{
    public static byte[] GenerateNonce()
    {
        byte[] nonce = new byte[VpkFormat.VpkConstants.NonceSize];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

    public static (byte[] Ciphertext, byte[] Tag) Encrypt(byte[] plaintext, byte[] key, byte[] nonce)
    {
        if (key.Length != VpkFormat.VpkConstants.KeySize)
            throw new ArgumentException($"Key must be {VpkFormat.VpkConstants.KeySize} bytes.", nameof(key));
        if (nonce.Length != VpkFormat.VpkConstants.NonceSize)
            throw new ArgumentException($"Nonce must be {VpkFormat.VpkConstants.NonceSize} bytes.", nameof(nonce));

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[VpkFormat.VpkConstants.TagSize];

        using var aes = new AesGcm(key, VpkFormat.VpkConstants.TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return (ciphertext, tag);
    }

    public static byte[] Decrypt(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
    {
        if (key.Length != VpkFormat.VpkConstants.KeySize)
            throw new ArgumentException($"Key must be {VpkFormat.VpkConstants.KeySize} bytes.", nameof(key));

        byte[] plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, VpkFormat.VpkConstants.TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
