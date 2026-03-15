using FortyTwoPak.Core.Compression;
using FortyTwoPak.Core.Crypto;
using FortyTwoPak.Core.Utils;

namespace FortyTwoPak.Core.VpkFormat;

public class VpkArchive
{
    public VpkHeader Header { get; private set; } = new();
    public List<VpkEntry> Entries { get; } = new();

    private readonly Dictionary<string, VpkEntry> _entryLookup = new(StringComparer.OrdinalIgnoreCase);
    private string? _filePath;

    public event Action<ProgressInfo>? OnProgress;

    public void Build(string sourceDirectory, string outputPath, VpkBuildOptions options)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        int totalFiles = files.Length;

        Header = new VpkHeader
        {
            IsEncrypted = options.EnableEncryption,
            CompressionLevel = options.CompressionLevel,
            CompressionAlgorithm = options.CompressionAlgorithm,
            FileNamesMangled = options.MangleFileNames,
            Author = options.Author ?? string.Empty,
            Comment = options.Comment ?? string.Empty,
            CreatedAtUtcTicks = DateTime.UtcNow.Ticks
        };

        byte[]? derivedKey = null;
        byte[]? hmacKey = null;

        if (options.EnableEncryption)
        {
            if (string.IsNullOrEmpty(options.Passphrase))
                throw new ArgumentException("Passphrase is required when encryption is enabled.");

            Header.Salt = KeyDerivation.GenerateSalt();

            byte[] keyMaterial = KeyDerivation.DeriveKey(options.Passphrase, Header.Salt, 64);
            derivedKey = keyMaterial[..32];
            hmacKey = keyMaterial[32..];
        }

        Entries.Clear();
        _entryLookup.Clear();

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var writer = new BinaryWriter(fs);

        Header.WriteTo(writer);

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string relativePath = Path.GetRelativePath(sourceDirectory, file)
                .Replace('\\', '/');

            byte[] rawData = File.ReadAllBytes(file);

            byte[] contentHash = Blake3Hasher.Hash(rawData);

            bool compressed = false;
            byte[] processedData = rawData;

            if (options.CompressionLevel > 0 && options.CompressionAlgorithm != CompressionAlgorithm.None)
            {
                byte[] compressedData = options.CompressionAlgorithm switch
                {
                    CompressionAlgorithm.LZ4 => Lz4Compressor.Compress(rawData, options.CompressionLevel),
                    CompressionAlgorithm.Zstandard => ZstdCompressor.Compress(rawData, options.CompressionLevel),
                    CompressionAlgorithm.Brotli => BrotliCompressor.Compress(rawData, options.CompressionLevel),
                    _ => Lz4Compressor.Compress(rawData, options.CompressionLevel)
                };
                if (compressedData.Length < rawData.Length)
                {
                    processedData = compressedData;
                    compressed = true;
                }
            }

            byte[] nonce = Array.Empty<byte>();
            byte[] authTag = Array.Empty<byte>();            if (options.EnableEncryption && derivedKey != null)
            {
                nonce = AesGcmEncryptor.GenerateNonce();
                (processedData, authTag) = AesGcmEncryptor.Encrypt(processedData, derivedKey, nonce);
            }

            string storedName = options.MangleFileNames
                ? FileNameMangler.Mangle(relativePath)
                : relativePath;

            long currentPos = writer.BaseStream.Position;
            long alignedPos = AlignOffset(currentPos, VpkConstants.DataBlockAlignment);
            if (alignedPos > currentPos)
                writer.Write(new byte[alignedPos - currentPos]);

            long dataOffset = writer.BaseStream.Position;
            writer.Write(processedData);

            var entry = new VpkEntry
            {
                FileName = relativePath,
                StoredName = storedName,
                OriginalSize = rawData.Length,
                StoredSize = processedData.Length,
                DataOffset = dataOffset,
                ContentHash = contentHash,
                IsCompressed = compressed,
                IsEncrypted = options.EnableEncryption,
                Nonce = nonce,
                AuthTag = authTag
            };

            Entries.Add(entry);
            _entryLookup[relativePath] = entry;

            OnProgress?.Invoke(new ProgressInfo
            {
                CurrentFile = relativePath,
                ProcessedFiles = i + 1,
                TotalFiles = totalFiles
            });
        }

        long entryTableOffset = writer.BaseStream.Position;
        using (var entryTableStream = new MemoryStream())
        using (var entryWriter = new BinaryWriter(entryTableStream))
        {
            foreach (var entry in Entries)
                entry.WriteTo(entryWriter);

            byte[] entryTableData = entryTableStream.ToArray();

            if (options.EnableEncryption && derivedKey != null)
            {
                byte[] tableNonce = AesGcmEncryptor.GenerateNonce();
                var (encrypted, tag) = AesGcmEncryptor.Encrypt(entryTableData, derivedKey, tableNonce);

                writer.Write(tableNonce);
                writer.Write(tag);
                writer.Write(encrypted);

                Header.EntryTableSize = 12 + 16 + encrypted.Length;
            }
            else
            {
                writer.Write(entryTableData);
                Header.EntryTableSize = entryTableData.Length;
            }
        }

        Header.EntryCount = Entries.Count;
        Header.EntryTableOffset = entryTableOffset;

        writer.Flush();
        long contentEnd = writer.BaseStream.Position;
        fs.Position = 0;
        Header.WriteTo(writer);
        writer.Flush();

        if (hmacKey != null)
        {
            fs.Position = 0;
            byte[] hmac = HmacValidator.ComputeHmac(fs, hmacKey, contentEnd);
            fs.Position = contentEnd;
            writer.Write(hmac);
        }
        else
        {
            fs.Position = contentEnd;
            writer.Write(new byte[VpkConstants.HmacSize]);
        }

        _filePath = outputPath;
    }

    public static VpkArchive Open(string filePath, string? passphrase = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("VPK file not found.", filePath);

        var archive = new VpkArchive { _filePath = filePath };

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        archive.Header = VpkHeader.ReadFrom(reader);

        if (archive.Header.IsEncrypted)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase required to open encrypted VPK.");

            byte[] keyMaterial = KeyDerivation.DeriveKey(passphrase, archive.Header.Salt, 64);
            byte[] hmacKey = keyMaterial[32..];

            long contentEnd = fs.Length - VpkConstants.HmacSize;
            fs.Position = contentEnd;
            byte[] storedHmac = reader.ReadBytes(VpkConstants.HmacSize);

            fs.Position = 0;
            byte[] computedHmac = HmacValidator.ComputeHmac(fs, hmacKey, contentEnd);

            if (!CryptographicEquals(storedHmac, computedHmac))
                throw new InvalidDataException("HMAC verification failed. The archive may be tampered or the passphrase is incorrect.");
        }

        fs.Position = archive.Header.EntryTableOffset;

        byte[] entryTableData;
        if (archive.Header.IsEncrypted)
        {
            byte[] keyMaterial = KeyDerivation.DeriveKey(passphrase!, archive.Header.Salt, 64);
            byte[] aesKey = keyMaterial[..32];

            byte[] tableNonce = reader.ReadBytes(VpkConstants.NonceSize);
            byte[] tableTag = reader.ReadBytes(VpkConstants.TagSize);
            int encryptedLen = archive.Header.EntryTableSize - VpkConstants.NonceSize - VpkConstants.TagSize;
            byte[] encryptedTable = reader.ReadBytes(encryptedLen);

            entryTableData = AesGcmEncryptor.Decrypt(encryptedTable, aesKey, tableNonce, tableTag);
        }
        else
        {
            entryTableData = reader.ReadBytes(archive.Header.EntryTableSize);
        }

        using var entryStream = new MemoryStream(entryTableData);
        using var entryReader = new BinaryReader(entryStream);

        for (int i = 0; i < archive.Header.EntryCount; i++)
        {
            var entry = VpkEntry.ReadFrom(entryReader);
            archive.Entries.Add(entry);
            archive._entryLookup[entry.FileName] = entry;
        }

        return archive;
    }

    public byte[] ExtractFile(string fileName, string? passphrase = null)
    {
        if (_filePath == null)
            throw new InvalidOperationException("No archive file is open.");

        if (!_entryLookup.TryGetValue(fileName, out var entry))
            throw new FileNotFoundException($"Entry not found in archive: {fileName}");

        using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Position = entry.DataOffset;

        byte[] storedData = new byte[entry.StoredSize];
        fs.ReadExactly(storedData);

        byte[] data = storedData;

        if (entry.IsEncrypted)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase required to extract encrypted file.");

            byte[] keyMaterial = KeyDerivation.DeriveKey(passphrase, Header.Salt, 64);
            byte[] aesKey = keyMaterial[..32];

            data = AesGcmEncryptor.Decrypt(data, aesKey, entry.Nonce, entry.AuthTag);
        }

        if (entry.IsCompressed)
        {
            data = Header.CompressionAlgorithm switch
            {
                CompressionAlgorithm.Zstandard => ZstdCompressor.Decompress(data, (int)entry.OriginalSize),
                CompressionAlgorithm.Brotli => BrotliCompressor.Decompress(data, (int)entry.OriginalSize),
                _ => Lz4Compressor.Decompress(data, (int)entry.OriginalSize)
            };
        }

        byte[] hash = Blake3Hasher.Hash(data);
        if (!hash.AsSpan().SequenceEqual(entry.ContentHash))
            throw new InvalidDataException($"Content hash mismatch for {fileName}. File may be corrupted.");

        return data;
    }

    public void ExtractAll(string outputDirectory, string? passphrase = null)
    {
        Directory.CreateDirectory(outputDirectory);

        for (int i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            byte[] data = ExtractFile(entry.FileName, passphrase);

            string outputPath = Path.Combine(outputDirectory, entry.FileName.Replace('/', Path.DirectorySeparatorChar));
            string? dir = Path.GetDirectoryName(outputPath);
            if (dir != null) Directory.CreateDirectory(dir);

            File.WriteAllBytes(outputPath, data);

            OnProgress?.Invoke(new ProgressInfo
            {
                CurrentFile = entry.FileName,
                ProcessedFiles = i + 1,
                TotalFiles = Entries.Count
            });
        }
    }

    public bool Contains(string fileName) => _entryLookup.ContainsKey(fileName);

    public IReadOnlyList<string> GetFileNames() => Entries.Select(e => e.FileName).ToList();

    public VpkValidationResult Validate(string? passphrase = null)
    {
        var result = new VpkValidationResult();

        try
        {
            if (Header.IsEncrypted && string.IsNullOrEmpty(passphrase))
            {
                result.Errors.Add("Archive is encrypted but no passphrase provided.");
                return result;
            }

            foreach (var entry in Entries)
            {
                try
                {
                    byte[] data = ExtractFile(entry.FileName, passphrase);
                    result.ValidFiles++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{entry.FileName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Archive-level error: {ex.Message}");
        }

        return result;
    }

    private static long AlignOffset(long offset, int alignment)
    {
        long remainder = offset % alignment;
        return remainder == 0 ? offset : offset + (alignment - remainder);
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

public class VpkBuildOptions
{
    public bool EnableEncryption { get; set; }
    public string? Passphrase { get; set; }
    public int CompressionLevel { get; set; } = 3;
    public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.LZ4;
    public bool MangleFileNames { get; set; }
    public string? Author { get; set; }
    public string? Comment { get; set; }
}

public class VpkValidationResult
{
    public int ValidFiles { get; set; }
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}
