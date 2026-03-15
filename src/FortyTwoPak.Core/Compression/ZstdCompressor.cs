using ZstdSharp;

namespace FortyTwoPak.Core.Compression;

public static class ZstdCompressor
{
    public static byte[] Compress(byte[] data, int level = 3)
    {
        int clampedLevel = Math.Clamp(level, 1, 19);

        using var compressor = new Compressor(clampedLevel);
        byte[] compressed = compressor.Wrap(data).ToArray();

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

        using var decompressor = new Decompressor();
        byte[] result = decompressor.Unwrap(compressedData.AsSpan(4)).ToArray();

        if (result.Length != expectedSize)
            throw new InvalidDataException(
                $"Decompressed size ({result.Length}) doesn't match expected ({expectedSize}).");

        return result;
    }
}
