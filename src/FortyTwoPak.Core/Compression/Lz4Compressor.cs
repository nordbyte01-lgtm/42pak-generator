using K4os.Compression.LZ4;

namespace FortyTwoPak.Core.Compression;

public static class Lz4Compressor
{
    public static byte[] Compress(byte[] data, int level = 3)
    {
        var lz4Level = level switch
        {
            0 => LZ4Level.L00_FAST,
            1 => LZ4Level.L03_HC,
            2 => LZ4Level.L04_HC,
            3 => LZ4Level.L06_HC,
            >= 4 and <= 6 => LZ4Level.L09_HC,
            >= 7 and <= 9 => LZ4Level.L10_OPT,
            >= 10 => LZ4Level.L12_MAX,
            _ => LZ4Level.L03_HC
        };

        int maxOutput = LZ4Codec.MaximumOutputSize(data.Length);
        byte[] output = new byte[maxOutput + 4];

        BitConverter.TryWriteBytes(output.AsSpan(0, 4), data.Length);

        int compressedSize = LZ4Codec.Encode(
            data, 0, data.Length,
            output, 4, output.Length - 4,
            lz4Level);

        if (compressedSize <= 0)
            throw new InvalidOperationException("LZ4 compression failed.");

        return output[..(compressedSize + 4)];
    }

    public static byte[] Decompress(byte[] compressedData, int expectedSize)
    {
        // Read original size from prefix
        int storedSize = BitConverter.ToInt32(compressedData, 0);

        if (storedSize != expectedSize)
            throw new InvalidDataException(
                $"Stored size ({storedSize}) doesn't match expected size ({expectedSize}).");

        byte[] output = new byte[storedSize];

        int decoded = LZ4Codec.Decode(
            compressedData, 4, compressedData.Length - 4,
            output, 0, output.Length);

        if (decoded != storedSize)
            throw new InvalidDataException(
                $"LZ4 decompression produced {decoded} bytes, expected {storedSize}.");

        return output;
    }
}
