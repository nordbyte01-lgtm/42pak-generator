using FortyTwoPak.Core.Compression;

namespace FortyTwoPak.Core.VpkFormat;

public class VpkHeader
{
    public ushort Version { get; set; } = VpkConstants.FormatVersion;
    public int EntryCount { get; set; }
    public long EntryTableOffset { get; set; }
    public int EntryTableSize { get; set; }
    public bool IsEncrypted { get; set; }
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public int CompressionLevel { get; set; }
    public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.LZ4;
    public bool FileNamesMangled { get; set; }
    public string Author { get; set; } = string.Empty;
    public long CreatedAtUtcTicks { get; set; }
    public string Comment { get; set; } = string.Empty;

    public const int SerializedSize = 512;

    public void WriteTo(BinaryWriter writer)
    {
        long startPos = writer.BaseStream.Position;

        writer.Write(VpkConstants.Magic);
        writer.Write(Version);
        writer.Write(EntryCount);
        writer.Write(EntryTableOffset);
        writer.Write(EntryTableSize);
        writer.Write(IsEncrypted);
        writer.Write(CompressionLevel);
        writer.Write((byte)CompressionAlgorithm);
        writer.Write(FileNamesMangled);
        writer.Write(CreatedAtUtcTicks);

        // Salt (32 bytes, zero-padded if empty)
        byte[] saltBlock = new byte[VpkConstants.SaltLength];
        if (Salt.Length > 0)
            Buffer.BlockCopy(Salt, 0, saltBlock, 0, Math.Min(Salt.Length, saltBlock.Length));
        writer.Write(saltBlock);

        // Author (64 bytes, null-padded)
        WriteFixedString(writer, Author, 64);

        // Comment (128 bytes, null-padded)
        WriteFixedString(writer, Comment, 128);

        // Pad remainder to SerializedSize
        long written = writer.BaseStream.Position - startPos;
        if (written < SerializedSize)
            writer.Write(new byte[SerializedSize - written]);
    }

    public static VpkHeader ReadFrom(BinaryReader reader)
    {
        long startPos = reader.BaseStream.Position;

        byte[] magic = reader.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(VpkConstants.Magic))
            throw new InvalidDataException("Not a valid VPK file: invalid magic bytes.");

        var header = new VpkHeader
        {
            Version = reader.ReadUInt16(),
            EntryCount = reader.ReadInt32(),
            EntryTableOffset = reader.ReadInt64(),
            EntryTableSize = reader.ReadInt32(),
            IsEncrypted = reader.ReadBoolean(),
            CompressionLevel = reader.ReadInt32(),
            CompressionAlgorithm = (CompressionAlgorithm)reader.ReadByte(),
            FileNamesMangled = reader.ReadBoolean(),
            CreatedAtUtcTicks = reader.ReadInt64()
        };

        header.Salt = reader.ReadBytes(VpkConstants.SaltLength);
        header.Author = ReadFixedString(reader, 64);
        header.Comment = ReadFixedString(reader, 128);

        // Skip padding to reach full header size
        long read = reader.BaseStream.Position - startPos;
        if (read < SerializedSize)
            reader.ReadBytes((int)(SerializedSize - read));

        if (header.Version > VpkConstants.FormatVersion)
            throw new InvalidDataException($"Unsupported VPK version {header.Version}. Max supported: {VpkConstants.FormatVersion}.");

        return header;
    }

    private static void WriteFixedString(BinaryWriter writer, string value, int fixedLength)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
        byte[] block = new byte[fixedLength];
        Buffer.BlockCopy(utf8, 0, block, 0, Math.Min(utf8.Length, fixedLength));
        writer.Write(block);
    }

    private static string ReadFixedString(BinaryReader reader, int fixedLength)
    {
        byte[] block = reader.ReadBytes(fixedLength);
        int end = Array.IndexOf(block, (byte)0);
        if (end < 0) end = fixedLength;
        return System.Text.Encoding.UTF8.GetString(block, 0, end);
    }
}
