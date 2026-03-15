namespace FortyTwoPak.Core.Crypto;

public static class Blake3Hasher
{
    public static byte[] Hash(byte[] data)
    {
        var hasher = Blake3.Hasher.New();
        hasher.Update(data);
        var hash = hasher.Finalize();
        return hash.AsSpan().ToArray();
    }

    public static byte[] Hash(Stream stream, int bufferSize = 81920)
    {
        var hasher = Blake3.Hasher.New();
        byte[] buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, bytesRead));
        }
        var hash = hasher.Finalize();
        return hash.AsSpan().ToArray();
    }

    public static bool Verify(byte[] data, byte[] expectedHash)
    {
        byte[] actualHash = Hash(data);
        return CryptographicEquals(actualHash, expectedHash);
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
