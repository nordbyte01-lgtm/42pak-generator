using FortyTwoPak.Core.Crypto;
using FortyTwoPak.Core.VpkFormat;
using Xunit;

namespace FortyTwoPak.Tests;

public class CryptoTests
{
    [Fact]
    public void AesGcm_EncryptDecrypt_RoundTrip()
    {
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, 42pak-generator!");
        byte[] key = new byte[32];
        Random.Shared.NextBytes(key);
        byte[] nonce = AesGcmEncryptor.GenerateNonce();

        var (ciphertext, tag) = AesGcmEncryptor.Encrypt(plaintext, key, nonce);
        byte[] decrypted = AesGcmEncryptor.Decrypt(ciphertext, key, nonce, tag);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void AesGcm_TamperedCiphertext_ThrowsException()
    {
        byte[] plaintext = new byte[128];
        Random.Shared.NextBytes(plaintext);
        byte[] key = new byte[32];
        Random.Shared.NextBytes(key);
        byte[] nonce = AesGcmEncryptor.GenerateNonce();

        var (ciphertext, tag) = AesGcmEncryptor.Encrypt(plaintext, key, nonce);

        // Tamper with ciphertext
        ciphertext[0] ^= 0xFF;

        Assert.ThrowsAny<Exception>(() =>
            AesGcmEncryptor.Decrypt(ciphertext, key, nonce, tag));
    }

    [Fact]
    public void AesGcm_WrongKey_ThrowsException()
    {
        byte[] plaintext = new byte[64];
        Random.Shared.NextBytes(plaintext);
        byte[] key1 = new byte[32];
        byte[] key2 = new byte[32];
        Random.Shared.NextBytes(key1);
        Random.Shared.NextBytes(key2);
        byte[] nonce = AesGcmEncryptor.GenerateNonce();

        var (ciphertext, tag) = AesGcmEncryptor.Encrypt(plaintext, key1, nonce);

        Assert.ThrowsAny<Exception>(() =>
            AesGcmEncryptor.Decrypt(ciphertext, key2, nonce, tag));
    }

    [Fact]
    public void KeyDerivation_SameInput_SameOutput()
    {
        byte[] salt = KeyDerivation.GenerateSalt();
        string passphrase = "test-passphrase-42";

        byte[] key1 = KeyDerivation.DeriveKey(passphrase, salt);
        byte[] key2 = KeyDerivation.DeriveKey(passphrase, salt);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void KeyDerivation_DifferentSalt_DifferentOutput()
    {
        byte[] salt1 = KeyDerivation.GenerateSalt();
        byte[] salt2 = KeyDerivation.GenerateSalt();
        string passphrase = "test-passphrase-42";

        byte[] key1 = KeyDerivation.DeriveKey(passphrase, salt1);
        byte[] key2 = KeyDerivation.DeriveKey(passphrase, salt2);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void KeyDerivation_DifferentPassphrase_DifferentOutput()
    {
        byte[] salt = KeyDerivation.GenerateSalt();

        byte[] key1 = KeyDerivation.DeriveKey("passphrase-A", salt);
        byte[] key2 = KeyDerivation.DeriveKey("passphrase-B", salt);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Blake3_Hash_Deterministic()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("test data for hashing");

        byte[] hash1 = Blake3Hasher.Hash(data);
        byte[] hash2 = Blake3Hasher.Hash(data);

        Assert.Equal(32, hash1.Length);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Blake3_DifferentData_DifferentHash()
    {
        byte[] hash1 = Blake3Hasher.Hash(new byte[] { 1, 2, 3 });
        byte[] hash2 = Blake3Hasher.Hash(new byte[] { 4, 5, 6 });

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Blake3_Verify_CorrectHash()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("integrity check");
        byte[] hash = Blake3Hasher.Hash(data);

        Assert.True(Blake3Hasher.Verify(data, hash));
    }

    [Fact]
    public void Blake3_Verify_TamperedData()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("original data");
        byte[] hash = Blake3Hasher.Hash(data);

        byte[] tampered = System.Text.Encoding.UTF8.GetBytes("tampered data");
        Assert.False(Blake3Hasher.Verify(tampered, hash));
    }

    [Fact]
    public void Nonce_IsUnique()
    {
        var nonces = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            string hex = Convert.ToHexString(AesGcmEncryptor.GenerateNonce());
            Assert.True(nonces.Add(hex), "Generated duplicate nonce");
        }
    }

    [Fact]
    public void Salt_IsUnique()
    {
        var salts = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            string hex = Convert.ToHexString(KeyDerivation.GenerateSalt());
            Assert.True(salts.Add(hex), "Generated duplicate salt");
        }
    }
}
