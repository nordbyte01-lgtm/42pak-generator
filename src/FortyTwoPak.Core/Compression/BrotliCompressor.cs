using System.IO.Compression;

namespace FortyTwoPak.Core.Compression;

public static class BrotliCompressor
{
    public static byte[] Compress(byte[] data, int level = 3)
    {
        int quality = Math.Clamp(level, 0, 11);

        using var outputStream = new MemoryStream();
        using (var brotli = new BrotliStream(outputStream, (CompressionLevel)quality, leaveOpen: true))
        {
            brotli.Write(data, 0, data.Length);
        }

        byte[] compressed = outputStream.ToArray();
        byte[] output = new byte[compressed.Length + 4];
        BitConverter.TryWriteBytes(output.AsSpan(0, 4), data.Length);
        Buffer.BlockCopy(compressed, 0, output, 4, compressed.Length);
        return output;
    }

    public static byte[] Decompress(byte[] compressedData, int expectedSize)
    {
        int storedSize = BitConverter.ToInt32(compressedData, 0);
        if (storedSize != expectedSize)
            throw new InvalidDataException(
                $"Stored size ({storedSize}) doesn't match expected size ({expectedSize}).");

        using var inputStream = new MemoryStream(compressedData, 4, compressedData.Length - 4);
        using var brotli = new BrotliStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        brotli.CopyTo(outputStream);

        byte[] result = outputStream.ToArray();
        if (result.Length != expectedSize)
            throw new InvalidDataException(
                $"Decompressed size ({result.Length}) doesn't match expected ({expectedSize}).");

        return result;
    }
}
