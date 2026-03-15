using System.Security.Cryptography;

namespace FortyTwoPak.Core.Crypto;

public static class HmacValidator
{
    public static byte[] ComputeHmac(Stream stream, byte[] key, long length)
    {
        stream.Position = 0;

        using var hmac = new HMACSHA256(key);
        byte[] buffer = new byte[81920];
        long remaining = length;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int bytesRead = stream.Read(buffer, 0, toRead);
            if (bytesRead == 0) break;

            hmac.TransformBlock(buffer, 0, bytesRead, null, 0);
            remaining -= bytesRead;
        }

        hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return hmac.Hash!;
    }

    public static bool Verify(string filePath, byte[] key)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        long contentLength = fs.Length - VpkFormat.VpkConstants.HmacSize;
        if (contentLength < 0) return false;

        // Read stored HMAC
        fs.Position = contentLength;
        byte[] storedHmac = new byte[VpkFormat.VpkConstants.HmacSize];
        fs.ReadExactly(storedHmac);

        // Compute actual HMAC
        byte[] computedHmac = ComputeHmac(fs, key, contentLength);

        return CryptographicEquals(storedHmac, computedHmac);
    }

    private static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
