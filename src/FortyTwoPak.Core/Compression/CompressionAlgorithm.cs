namespace FortyTwoPak.Core.Compression;

public enum CompressionAlgorithm : byte
{
    None = 0,
    LZ4 = 1,
    Zstandard = 2,
    Brotli = 3
}
