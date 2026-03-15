using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using FortyTwoPak.Core.Compression;
using FortyTwoPak.Core.Legacy;
using FortyTwoPak.Core.VpkFormat;

namespace FortyTwoPak.UI;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class BridgeService
{
    private readonly Form _owner;
    private VpkArchive? _currentArchive;

    public BridgeService(Form owner) => _owner = owner;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public string BuildVpk(string sourceDir, string outputPath, string optionsJson)
    {
        try
        {
            var options = JsonSerializer.Deserialize<VpkBuildOptions>(optionsJson, _jsonOpts)
                ?? new VpkBuildOptions();

            var archive = new VpkArchive();
            archive.Build(sourceDir, outputPath, options);
            _currentArchive = archive;

            return JsonSerializer.Serialize(new
            {
                success = true,
                entryCount = archive.Entries.Count,
                message = $"VPK created with {archive.Entries.Count} files."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string OpenVpk(string filePath, string passphrase)
    {
        try
        {
            _currentArchive = VpkArchive.Open(filePath,
                string.IsNullOrEmpty(passphrase) ? null : passphrase);

            var entries = _currentArchive.Entries.Select(e => new
            {
                e.FileName,
                e.OriginalSize,
                e.StoredSize,
                e.IsCompressed,
                e.IsEncrypted,
                ratio = e.OriginalSize > 0
                    ? Math.Round((1.0 - (double)e.StoredSize / e.OriginalSize) * 100, 1)
                    : 0
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    _currentArchive.Header.Version,
                    _currentArchive.Header.EntryCount,
                    _currentArchive.Header.IsEncrypted,
                    _currentArchive.Header.CompressionLevel,
                    CompressionAlgorithm = _currentArchive.Header.CompressionAlgorithm.ToString(),
                    _currentArchive.Header.FileNamesMangled,
                    _currentArchive.Header.Author,
                    _currentArchive.Header.Comment,
                    CreatedAt = new DateTime(_currentArchive.Header.CreatedAtUtcTicks, DateTimeKind.Utc).ToString("o")
                },
                entries
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ExtractAll(string outputDir, string passphrase)
    {
        try
        {
            if (_currentArchive == null)
                return JsonSerializer.Serialize(new { success = false, message = "No archive is open." });

            _currentArchive.ExtractAll(outputDir,
                string.IsNullOrEmpty(passphrase) ? null : passphrase);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Extracted {_currentArchive.Entries.Count} files to {outputDir}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ValidateVpk(string passphrase)
    {
        try
        {
            if (_currentArchive == null)
                return JsonSerializer.Serialize(new { success = false, message = "No archive is open." });

            var result = _currentArchive.Validate(
                string.IsNullOrEmpty(passphrase) ? null : passphrase);

            return JsonSerializer.Serialize(new
            {
                success = true,
                isValid = result.IsValid,
                validFiles = result.ValidFiles,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string ConvertEixEpk(string eixPath, string vpkOutputPath, string optionsJson)
    {
        try
        {
            var options = JsonSerializer.Deserialize<VpkBuildOptions>(optionsJson, _jsonOpts)
                ?? new VpkBuildOptions();

            var converter = new EixEpkConverter();
            var result = converter.Convert(eixPath, vpkOutputPath, options);

            return JsonSerializer.Serialize(new
            {
                success = true,
                convertedFiles = result.ConvertedFiles,
                totalEntries = result.TotalEntries,
                skippedFiles = result.SkippedFiles,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }

    public string PickFolder()
    {
        string result = "";
        _owner.Invoke(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select folder",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                result = dialog.SelectedPath;
        });
        return result;
    }

    public string PickFile(string filter)
    {
        string result = "";
        _owner.Invoke(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = filter,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                result = dialog.FileName;
        });
        return result;
    }

    public string PickSaveFile(string filter, string defaultExt)
    {
        string result = "";
        _owner.Invoke(() =>
        {
            using var dialog = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                result = dialog.FileName;
        });
        return result;
    }

    public string ReadEixListing(string eixPath)
    {
        try
        {
            var reader = new EixEpkReader();
            reader.Open(eixPath);

            var entries = reader.Entries.Select(e => new
            {
                e.FileName,
                e.DataSize,
                e.RealDataSize,
                e.CompressedTypeName,
                canExtract = e.CompressedType <= 1
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = reader.Entries.Count,
                entries
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}
