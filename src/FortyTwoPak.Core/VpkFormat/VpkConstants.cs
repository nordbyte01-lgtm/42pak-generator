namespace FortyTwoPak.Core.VpkFormat;

public static class VpkConstants
{
    public static readonly byte[] Magic = "42PK"u8.ToArray();
    public const ushort FormatVersion = 2;
    public const int DataBlockAlignment = 4096;
    public const int MaxFileNameLength = 512;
    public const int HmacSize = 32;
    public const int Pbkdf2Iterations = 200_000;
    public const int SaltLength = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;
}
