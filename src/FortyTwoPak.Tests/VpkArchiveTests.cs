using FortyTwoPak.Core.Compression;
using FortyTwoPak.Core.VpkFormat;
using Xunit;

namespace FortyTwoPak.Tests;

public class VpkArchiveTests
{
    private readonly string _testDir;

    public VpkArchiveTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"42pak-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void BuildAndOpen_Unencrypted_RoundTrip()
    {
        // Arrange: Create test files
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "Hello World");
        File.WriteAllBytes(Path.Combine(sourceDir, "data.bin"), new byte[1024]);

        string subDir = Path.Combine(sourceDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.cfg"), "KEY=VALUE");

        string vpkPath = Path.Combine(_testDir, "test.vpk");

        // Act: Build
        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions
        {
            EnableEncryption = false,
            CompressionLevel = 3
        });

        // Assert: Open and verify
        var opened = VpkArchive.Open(vpkPath);
        Assert.Equal(3, opened.Entries.Count);
        Assert.True(opened.Contains("test.txt"));
        Assert.True(opened.Contains("data.bin"));
        Assert.True(opened.Contains("subdir/nested.cfg"));

        // Extract and verify content
        string extractDir = Path.Combine(_testDir, "extract");
        opened.ExtractAll(extractDir);

        Assert.Equal("Hello World", File.ReadAllText(Path.Combine(extractDir, "test.txt")));
        Assert.Equal(1024, new FileInfo(Path.Combine(extractDir, "data.bin")).Length);
        Assert.Equal("KEY=VALUE", File.ReadAllText(Path.Combine(extractDir, "subdir", "nested.cfg")));

        Cleanup();
    }

    [Fact]
    public void BuildAndOpen_Encrypted_RoundTrip()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "secret.txt"), "Top Secret Data");

        string vpkPath = Path.Combine(_testDir, "encrypted.vpk");

        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions
        {
            EnableEncryption = true,
            Passphrase = "MySecurePassphrase!42",
            CompressionLevel = 0
        });

        // Open with correct passphrase
        var opened = VpkArchive.Open(vpkPath, "MySecurePassphrase!42");
        Assert.Single(opened.Entries);

        byte[] data = opened.ExtractFile("secret.txt", "MySecurePassphrase!42");
        Assert.Equal("Top Secret Data", System.Text.Encoding.UTF8.GetString(data));

        // Wrong passphrase should fail
        Assert.ThrowsAny<Exception>(() => VpkArchive.Open(vpkPath, "WrongPassword"));

        Cleanup();
    }

    [Fact]
    public void Validation_ValidArchive_Passes()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "file.txt"), "test content");

        string vpkPath = Path.Combine(_testDir, "valid.vpk");

        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions
        {
            EnableEncryption = false,
            CompressionLevel = 3
        });

        var opened = VpkArchive.Open(vpkPath);
        var result = opened.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(1, result.ValidFiles);

        Cleanup();
    }

    [Fact]
    public void Header_Metadata_Preserved()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "f.txt"), "x");

        string vpkPath = Path.Combine(_testDir, "meta.vpk");

        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions
        {
            Author = "TestAuthor",
            Comment = "Test comment for archive",
            CompressionLevel = 5
        });

        var opened = VpkArchive.Open(vpkPath);
        Assert.Equal("TestAuthor", opened.Header.Author);
        Assert.Equal("Test comment for archive", opened.Header.Comment);
        Assert.Equal(5, opened.Header.CompressionLevel);
        Assert.Equal(VpkConstants.FormatVersion, opened.Header.Version);

        Cleanup();
    }

    [Fact]
    public void Compression_ReducesSize()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);

        // Create compressible data (repeated pattern)
        byte[] compressible = new byte[10000];
        for (int i = 0; i < compressible.Length; i++)
            compressible[i] = (byte)(i % 10);
        File.WriteAllBytes(Path.Combine(sourceDir, "compressible.bin"), compressible);

        string vpkPath = Path.Combine(_testDir, "compressed.vpk");

        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions { CompressionLevel = 6 });

        var opened = VpkArchive.Open(vpkPath);
        var entry = opened.Entries[0];

        Assert.True(entry.IsCompressed);
        Assert.True(entry.StoredSize < entry.OriginalSize,
            $"Stored size ({entry.StoredSize}) should be less than original ({entry.OriginalSize})");

        Cleanup();
    }

    [Fact]
    public void LZ4_CompressDecompress_RoundTrip()
    {
        byte[] original = System.Text.Encoding.UTF8.GetBytes(
            string.Concat(Enumerable.Repeat("This is test data for LZ4 compression. ", 100)));

        byte[] compressed = Lz4Compressor.Compress(original);
        byte[] decompressed = Lz4Compressor.Decompress(compressed, original.Length);

        Assert.Equal(original, decompressed);
        Assert.True(compressed.Length < original.Length);
    }

    [Fact]
    public void Zstd_CompressDecompress_RoundTrip()
    {
        byte[] original = System.Text.Encoding.UTF8.GetBytes(
            string.Concat(Enumerable.Repeat("This is test data for Zstandard compression. ", 100)));

        byte[] compressed = ZstdCompressor.Compress(original);
        byte[] decompressed = ZstdCompressor.Decompress(compressed, original.Length);

        Assert.Equal(original, decompressed);
        Assert.True(compressed.Length < original.Length);
    }

    [Fact]
    public void Brotli_CompressDecompress_RoundTrip()
    {
        byte[] original = System.Text.Encoding.UTF8.GetBytes(
            string.Concat(Enumerable.Repeat("This is test data for Brotli compression. ", 100)));

        byte[] compressed = BrotliCompressor.Compress(original);
        byte[] decompressed = BrotliCompressor.Decompress(compressed, original.Length);

        Assert.Equal(original, decompressed);
        Assert.True(compressed.Length < original.Length);
    }

    [Fact]
    public void BuildAndOpen_Zstandard_RoundTrip()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "Zstandard round-trip test data");

        string vpkPath = Path.Combine(_testDir, "zstd.vpk");

        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions
        {
            CompressionLevel = 3,
            CompressionAlgorithm = CompressionAlgorithm.Zstandard
        });

        var opened = VpkArchive.Open(vpkPath);
        Assert.Equal(CompressionAlgorithm.Zstandard, opened.Header.CompressionAlgorithm);

        byte[] data = opened.ExtractFile("test.txt");
        Assert.Equal("Zstandard round-trip test data", System.Text.Encoding.UTF8.GetString(data));

        Cleanup();
    }

    [Fact]
    public void BuildAndOpen_Brotli_RoundTrip()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "Brotli round-trip test data");

        string vpkPath = Path.Combine(_testDir, "brotli.vpk");

        var archive = new VpkArchive();
        archive.Build(sourceDir, vpkPath, new VpkBuildOptions
        {
            CompressionLevel = 3,
            CompressionAlgorithm = CompressionAlgorithm.Brotli
        });

        var opened = VpkArchive.Open(vpkPath);
        Assert.Equal(CompressionAlgorithm.Brotli, opened.Header.CompressionAlgorithm);

        byte[] data = opened.ExtractFile("test.txt");
        Assert.Equal("Brotli round-trip test data", System.Text.Encoding.UTF8.GetString(data));

        Cleanup();
    }

    private void Cleanup()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }
}
