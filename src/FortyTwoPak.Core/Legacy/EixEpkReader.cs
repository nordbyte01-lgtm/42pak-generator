using System.Runtime.InteropServices;
using System.Text;

namespace FortyTwoPak.Core.Legacy;

public class EixEpkReader
{
    private const uint EpkMagic = 0x444B5045; // 'EPKD'
    private const uint LzoMagic = 0x5A4F434D; // 'MCOZ'
    private const int EpkVersion = 2;
    private const int FileNameLength = 161;

    public List<EixEntry> Entries { get; } = new();

    public void Open(string eixPath)
    {
        string epkPath = Path.ChangeExtension(eixPath, ".epk");
        if (!File.Exists(eixPath))
            throw new FileNotFoundException("EIX file not found.", eixPath);
        if (!File.Exists(epkPath))
            throw new FileNotFoundException("EPK file not found.", epkPath);

        byte[] eixData = File.ReadAllBytes(eixPath);

        using var ms = new MemoryStream(eixData);
        using var reader = new BinaryReader(ms);

        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        int indexCount = reader.ReadInt32();

        if (magic == LzoMagic)
            throw new NotSupportedException(
                "EIX file is TEA-encrypted. Decrypt it first using EterNexus or the game client's tools.");

        if (magic != EpkMagic)
            throw new InvalidDataException($"Invalid EIX magic: 0x{magic:X8}");

        if (version != EpkVersion)
            throw new InvalidDataException($"Unsupported EIX version: {version}");

        Entries.Clear();

        for (int i = 0; i < indexCount; i++)
        {
            var entry = new EixEntry
            {
                Id = reader.ReadInt32()
            };

            byte[] nameBytes = reader.ReadBytes(FileNameLength);
            int nullIdx = Array.IndexOf(nameBytes, (byte)0);
            entry.FileName = Encoding.ASCII.GetString(nameBytes, 0, nullIdx >= 0 ? nullIdx : FileNameLength);

            entry.FileNameCrc = reader.ReadUInt32();
            entry.RealDataSize = reader.ReadInt32();
            entry.DataSize = reader.ReadInt32();
            entry.DataCrc = reader.ReadUInt32();
            entry.DataPosition = reader.ReadInt32();
            entry.CompressedType = reader.ReadByte();

            // Skip padding to alignment
            int entrySize = 4 + FileNameLength + 4 + 4 + 4 + 4 + 4 + 1;
            int padding = (4 - (entrySize % 4)) % 4;
            if (padding > 0) reader.ReadBytes(padding);

            // Only include non-empty entries
            if (entry.FileNameCrc != 0 && !string.IsNullOrEmpty(entry.FileName))
                Entries.Add(entry);
        }
    }

    public byte[] ExtractFile(string eixPath, EixEntry entry)
    {
        string epkPath = Path.ChangeExtension(eixPath, ".epk");

        if (entry.CompressedType > 1)
            throw new NotSupportedException(
                $"Encrypted entry (type {entry.CompressedType}) cannot be extracted without decryption keys. " +
                "Use EterNexus or the game client to decrypt first.");

        using var fs = new FileStream(epkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Position = entry.DataPosition;
        byte[] rawData = new byte[entry.DataSize];
        fs.ReadExactly(rawData);

        if (entry.CompressedType == 0)
            return rawData;

        // Type 1: LZO compressed (with MCOZ header)
        if (entry.CompressedType == 1)
            return DecompressLzo(rawData);

        return rawData;
    }

    private static byte[] DecompressLzo(byte[] data)
    {
        if (data.Length < 16)
            throw new InvalidDataException("LZO data too short.");

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        uint fourCC = reader.ReadUInt32();
        if (fourCC != LzoMagic)
            throw new InvalidDataException("Expected MCOZ header in LZO data.");

        uint encryptSize = reader.ReadUInt32(); // Should be 0 for type 1
        uint compressedSize = reader.ReadUInt32();
        uint realSize = reader.ReadUInt32();

        if (encryptSize != 0)
            throw new NotSupportedException("LZO data is TEA-encrypted (type 2). Cannot decompress.");

        // Skip the second MCOZ marker
        uint secondFourCC = reader.ReadUInt32();

        byte[] compressedData = reader.ReadBytes((int)compressedSize);
        byte[] output = new byte[realSize];

        // Simple LZO1X decompression
        int outputLen = Lzo1xDecompress(compressedData, output);
        if (outputLen != realSize)
            throw new InvalidDataException($"LZO decompression size mismatch: {outputLen} vs {realSize}");

        return output;
    }

    private static int Lzo1xDecompress(byte[] src, byte[] dst)
    {
        int srcPos = 0, dstPos = 0;
        int srcEnd = src.Length;

        if (srcPos >= srcEnd) return 0;

        int t = src[srcPos++] & 0xFF;
        bool skipLiteral = false;
        bool doMatchNext = false;

        if (t > 17)
        {
            t -= 17;
            if (t < 4)
            {
                doMatchNext = true;
            }
            else
            {
                do { dst[dstPos++] = src[srcPos++]; } while (--t > 0);
                skipLiteral = true;
            }
        }

        while (true)
        {
            if (doMatchNext)
            {
                doMatchNext = false;
                // Copy t literal bytes then read next token
                if (t > 0)
                    do { dst[dstPos++] = src[srcPos++]; } while (--t > 0);
                t = src[srcPos++] & 0xFF;
                // Fall through to match processing
            }
            else if (!skipLiteral)
            {
                // Literal run
                if (t >= 16) goto match;

                if (t == 0)
                {
                    while (src[srcPos] == 0) { t += 255; srcPos++; }
                    t += 15 + (src[srcPos++] & 0xFF);
                }
                t += 3;

                do { dst[dstPos++] = src[srcPos++]; } while (--t > 0);
            }
            else
            {
                skipLiteral = false;
            }

            // first_literal_run equivalent
            t = src[srcPos++] & 0xFF;
            if (t < 16)
            {
                int mPos = dstPos - (1 + 0x0800) - (t >> 2) - ((src[srcPos++] & 0xFF) << 2);
                if (mPos < 0) throw new InvalidDataException("LZO decompression error.");
                dst[dstPos++] = dst[mPos++];
                dst[dstPos++] = dst[mPos++];
                dst[dstPos++] = dst[mPos];
                t = src[srcPos - 2] & 3;
                if (t == 0) continue;
                do { dst[dstPos++] = src[srcPos++]; } while (--t > 0);
                t = src[srcPos++] & 0xFF;
                if (t < 16) break; // match_done_lzo
            }

            match:
            while (true)
            {
                int mLen, mOff;
                if (t >= 64)
                {
                    mLen = (t >> 2) & 7;
                    mOff = (t >> 5) + ((src[srcPos++] & 0xFF) << 3);
                    mOff = dstPos - mOff - 1;
                    mLen = mLen == 0 ? 2 : mLen + 2;
                }
                else if (t >= 32)
                {
                    mLen = t & 31;
                    if (mLen == 0)
                    {
                        while (src[srcPos] == 0) { mLen += 255; srcPos++; }
                        mLen += 31 + (src[srcPos++] & 0xFF);
                    }
                    mLen += 2;
                    mOff = (src[srcPos] & 0xFF) >> 2;
                    mOff += (src[srcPos + 1] & 0xFF) << 6;
                    srcPos += 2;
                    mOff = dstPos - mOff - 1;
                }
                else if (t >= 16)
                {
                    mOff = (t & 8) << 11;
                    mLen = t & 7;
                    if (mLen == 0)
                    {
                        while (src[srcPos] == 0) { mLen += 255; srcPos++; }
                        mLen += 7 + (src[srcPos++] & 0xFF);
                    }
                    mLen += 2;
                    mOff += (src[srcPos] & 0xFF) >> 2;
                    mOff += (src[srcPos + 1] & 0xFF) << 6;
                    srcPos += 2;
                    if (mOff == 0)
                        return dstPos; // End marker
                    mOff = dstPos - mOff - 0x4000;
                }
                else
                {
                    mOff = (t >> 2) + ((src[srcPos++] & 0xFF) << 2);
                    mOff = dstPos - mOff - 1;
                    mLen = 2;

                    if (mOff < 0 || mOff >= dstPos)
                        throw new InvalidDataException("LZO decompression: invalid back-reference.");
                    dst[dstPos++] = dst[mOff++];
                    dst[dstPos++] = dst[mOff];

                    t = src[srcPos - 1] & 3;
                    if (t == 0) break;
                    do { dst[dstPos++] = src[srcPos++]; } while (--t > 0);
                    t = src[srcPos++] & 0xFF;
                    continue;
                }

                // Copy match bytes
                if (mOff < 0 || mOff >= dstPos)
                    throw new InvalidDataException("LZO decompression: invalid back-reference.");
                for (int i = 0; i < mLen; i++)
                    dst[dstPos++] = dst[mOff + i];

                t = src[srcPos - 2] & 3;
                if (t == 0) break;

                // match_next equivalent
                do { dst[dstPos++] = src[srcPos++]; } while (--t > 0);
                t = src[srcPos++] & 0xFF;
            }
        }

        return dstPos;
    }
}

public class EixEntry
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public uint FileNameCrc { get; set; }
    public int RealDataSize { get; set; }
    public int DataSize { get; set; }
    public uint DataCrc { get; set; }
    public int DataPosition { get; set; }
    public byte CompressedType { get; set; }

    public string CompressedTypeName => CompressedType switch
    {
        0 => "None",
        1 => "LZO",
        2 => "LZO+TEA",
        3 => "Panama",
        4 => "HybridCrypt",
        5 => "HybridCrypt+SDB",
        _ => $"Unknown({CompressedType})"
    };
}
