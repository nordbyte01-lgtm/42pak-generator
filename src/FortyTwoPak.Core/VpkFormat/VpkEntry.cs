namespace FortyTwoPak.Core.VpkFormat;

public class VpkEntry
{
    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public long StoredSize { get; set; }
    public long DataOffset { get; set; }
    public byte[] ContentHash { get; set; } = Array.Empty<byte>();
    public bool IsCompressed { get; set; }
    public bool IsEncrypted { get; set; }
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public byte[] AuthTag { get; set; } = Array.Empty<byte>();

    public void WriteTo(BinaryWriter writer)
    {
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(StoredName);
        writer.Write(nameBytes.Length);
        writer.Write(nameBytes);

        byte[] origNameBytes = System.Text.Encoding.UTF8.GetBytes(FileName);
        writer.Write(origNameBytes.Length);
        writer.Write(origNameBytes);

        writer.Write(OriginalSize);
        writer.Write(StoredSize);
        writer.Write(DataOffset);

        writer.Write(ContentHash.Length);
        writer.Write(ContentHash);

        writer.Write(IsCompressed);
        writer.Write(IsEncrypted);

        writer.Write(Nonce.Length);
        if (Nonce.Length > 0) writer.Write(Nonce);

        writer.Write(AuthTag.Length);
        if (AuthTag.Length > 0) writer.Write(AuthTag);
    }

    public static VpkEntry ReadFrom(BinaryReader reader)
    {
        var entry = new VpkEntry();

        int storedNameLen = reader.ReadInt32();
        entry.StoredName = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(storedNameLen));

        int origNameLen = reader.ReadInt32();
        entry.FileName = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(origNameLen));

        entry.OriginalSize = reader.ReadInt64();
        entry.StoredSize = reader.ReadInt64();
        entry.DataOffset = reader.ReadInt64();

        int hashLen = reader.ReadInt32();
        entry.ContentHash = reader.ReadBytes(hashLen);

        entry.IsCompressed = reader.ReadBoolean();
        entry.IsEncrypted = reader.ReadBoolean();

        int nonceLen = reader.ReadInt32();
        entry.Nonce = nonceLen > 0 ? reader.ReadBytes(nonceLen) : Array.Empty<byte>();

        int tagLen = reader.ReadInt32();
        entry.AuthTag = tagLen > 0 ? reader.ReadBytes(tagLen) : Array.Empty<byte>();

        return entry;
    }
}
