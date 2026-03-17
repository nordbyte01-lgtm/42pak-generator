using FortyTwoPak.Core.Compression;
using FortyTwoPak.Core.Legacy;
using FortyTwoPak.Core.VpkFormat;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FortyTwoPak.Core.Cli;

/// <summary>
/// Shared CLI command handler used by both 42pak-cli and the GUI's command-line mode.
/// </summary>
public static class CliHandler
{
    private static bool _quiet;
    private static bool _json;

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        // Global flags
        var argList = args.ToList();
        _quiet = argList.Remove("--quiet") || argList.Remove("-q");
        _json = argList.Remove("--json");
        args = argList.ToArray();

        var command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        return command switch
        {
            "build" or "pack" => Build(cmdArgs),
            "extract" or "unpack" => Extract(cmdArgs),
            "info" => Info(cmdArgs),
            "validate" or "verify" => Validate(cmdArgs),
            "convert" => Convert(cmdArgs),
            "list" => List(cmdArgs),
            "search" => Search(cmdArgs),
            "diff" => Diff(cmdArgs),
            "migrate" => Migrate(cmdArgs),
            "check-duplicates" => CheckDuplicates(cmdArgs),
            "watch" => Watch(cmdArgs),
            "help" or "--help" or "-h" => ShowHelp(),
            _ => UnknownCommand(command)
        };
    }

    static int Build(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: 42pak-cli build <sourceDir> <output.vpk> [options]"); return 1; }

        string sourceDir = args[0];
        string outputPath = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Source directory not found: {sourceDir}");
            return 1;
        }

        var options = new VpkBuildOptions
        {
            Passphrase = flags.GetValueOrDefault("passphrase"),
            CompressionLevel = int.TryParse(flags.GetValueOrDefault("compression"), out var cl) ? cl : 3,
            CompressionAlgorithm = Enum.TryParse<CompressionAlgorithm>(flags.GetValueOrDefault("algorithm", "LZ4"), true, out var ca) ? ca : CompressionAlgorithm.LZ4,
            MangleFileNames = flags.ContainsKey("mangle"),
            Author = flags.GetValueOrDefault("author"),
            Comment = flags.GetValueOrDefault("comment")
        };

        if (options.Passphrase != null)
            options.EnableEncryption = true;

        try
        {
            if (!_quiet) Console.WriteLine($"Building VPK from: {sourceDir}");
            var archive = new VpkArchive();
            archive.Build(sourceDir, outputPath, options);
            if (!_quiet)
            {
                Console.WriteLine($"Created {outputPath} with {archive.Entries.Count} files.");
                Console.WriteLine($"  Algorithm: {options.CompressionAlgorithm}, Level: {options.CompressionLevel}");
                Console.WriteLine($"  Encrypted: {(options.EnableEncryption ? "Yes" : "No")}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Build failed: {ex.Message}");
            return 1;
        }
    }

    static int Extract(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: 42pak-cli extract <archive.vpk> <outputDir> [--passphrase X] [--filter <pattern>]"); return 1; }

        string archivePath = args[0];
        string outputDir = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            if (!_quiet) Console.WriteLine($"Opening: {archivePath}");
            var archive = VpkArchive.Open(archivePath, passphrase);
            if (!_quiet) Console.WriteLine($"Extracting {archive.Entries.Count} files to: {outputDir}");
            archive.ExtractAll(outputDir, passphrase);
            if (!_quiet) Console.WriteLine("Extraction complete.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Extraction failed: {ex.Message}");
            return 1;
        }
    }

    static int Info(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: 42pak-cli info <archive.vpk> [--passphrase X]"); return 1; }

        string archivePath = args[0];
        var flags = ParseFlags(args.Skip(1));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            var archive = VpkArchive.Open(archivePath, passphrase);
            var h = archive.Header;

            long totalOriginal = archive.Entries.Sum(e => e.OriginalSize);
            long totalStored = archive.Entries.Sum(e => e.StoredSize);

            if (_json)
            {
                var info = new
                {
                    File = Path.GetFileName(archivePath),
                    h.Version,
                    Files = h.EntryCount,
                    h.IsEncrypted,
                    Algorithm = h.CompressionAlgorithm.ToString(),
                    h.CompressionLevel,
                    h.FileNamesMangled,
                    h.Author,
                    h.Comment,
                    TotalOriginalSize = totalOriginal,
                    TotalStoredSize = totalStored,
                    CompressionRatio = totalOriginal > 0 ? Math.Round((1.0 - (double)totalStored / totalOriginal) * 100, 1) : 0
                };
                Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.WriteLine($"Archive: {Path.GetFileName(archivePath)}");
            Console.WriteLine($"  Version:     {h.Version}");
            Console.WriteLine($"  Files:       {h.EntryCount}");
            Console.WriteLine($"  Encrypted:   {(h.IsEncrypted ? "Yes" : "No")}");
            Console.WriteLine($"  Algorithm:   {h.CompressionAlgorithm}");
            Console.WriteLine($"  Level:       {h.CompressionLevel}");
            Console.WriteLine($"  Mangled:     {(h.FileNamesMangled ? "Yes" : "No")}");
            Console.WriteLine($"  Author:      {h.Author ?? "—"}");
            Console.WriteLine($"  Comment:     {h.Comment ?? "—"}");
            Console.WriteLine($"  Created:     {new DateTime(h.CreatedAtUtcTicks, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();

            Console.WriteLine($"  Total size:  {FormatSize(totalOriginal)} (original)");
            Console.WriteLine($"  Stored size: {FormatSize(totalStored)} (compressed)");
            if (totalOriginal > 0)
                Console.WriteLine($"  Ratio:       {(1.0 - (double)totalStored / totalOriginal) * 100:F1}%");

            Console.WriteLine();
            Console.WriteLine($"  {"File",-50} {"Original",10} {"Stored",10} {"Ratio",7}  Flags");
            Console.WriteLine($"  {new string('-', 50)} {new string('-', 10)} {new string('-', 10)} {new string('-', 7)}  {new string('-', 10)}");
            foreach (var e in archive.Entries)
            {
                double ratio = e.OriginalSize > 0 ? (1.0 - (double)e.StoredSize / e.OriginalSize) * 100 : 0;
                string flagStr = string.Join(" ", new[]
                {
                    e.IsEncrypted ? "ENC" : null,
                    e.IsCompressed ? "CMP" : null
                }.Where(f => f != null));
                Console.WriteLine($"  {e.FileName,-50} {FormatSize(e.OriginalSize),10} {FormatSize(e.StoredSize),10} {ratio,6:F1}%  {flagStr}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read archive: {ex.Message}");
            return 1;
        }
    }

    static int Validate(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: 42pak-cli validate <archive.vpk> [--passphrase X]"); return 1; }

        string archivePath = args[0];
        var flags = ParseFlags(args.Skip(1));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            var archive = VpkArchive.Open(archivePath, passphrase);
            var result = archive.Validate(passphrase);

            if (_json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    result.IsValid,
                    result.ValidFiles,
                    result.Errors
                }, new JsonSerializerOptions { WriteIndented = true }));
                return result.IsValid ? 0 : 2;
            }

            if (result.IsValid)
            {
                Console.WriteLine($"VALID — {result.ValidFiles} files verified OK.");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"INVALID — {result.Errors.Count} error(s):");
                foreach (var err in result.Errors)
                    Console.Error.WriteLine($"  - {err}");
                return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }
    }

    static int Convert(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: 42pak-cli convert <input.eix> <output.vpk> [options]"); return 1; }

        string eixPath = args[0];
        string outputPath = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!File.Exists(eixPath))
        {
            Console.Error.WriteLine($"File not found: {eixPath}");
            return 1;
        }

        var options = new VpkBuildOptions
        {
            Passphrase = flags.GetValueOrDefault("passphrase"),
            CompressionLevel = int.TryParse(flags.GetValueOrDefault("compression"), out var cl) ? cl : 3,
            CompressionAlgorithm = Enum.TryParse<CompressionAlgorithm>(flags.GetValueOrDefault("algorithm", "LZ4"), true, out var ca) ? ca : CompressionAlgorithm.LZ4
        };

        if (options.Passphrase != null)
            options.EnableEncryption = true;

        try
        {
            if (!_quiet) Console.WriteLine($"Converting: {eixPath}");
            var converter = new EixEpkConverter();

            if (Enum.TryParse<EpkFormat>(flags.GetValueOrDefault("format", "Auto"), true, out var epkFmt))
                converter.SourceFormat = epkFmt;

            var result = converter.Convert(eixPath, outputPath, options);
            if (!_quiet)
            {
                Console.WriteLine($"Converted {result.ConvertedFiles}/{result.TotalEntries} files.");
                if (result.SkippedFiles.Count > 0)
                    Console.WriteLine($"Skipped {result.SkippedFiles.Count} encrypted entries.");
            }
            if (result.Errors.Count > 0)
            {
                Console.Error.WriteLine("Errors:");
                foreach (var err in result.Errors)
                    Console.Error.WriteLine($"  - {err}");
            }
            return result.Errors.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Conversion failed: {ex.Message}");
            return 1;
        }
    }

    static int ShowHelp()
    {
        PrintUsage();
        return 0;
    }

    static int List(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: 42pak-cli list <archive.vpk|input.eix> [--passphrase X] [--filter <glob>]"); return 1; }

        string archivePath = args[0];
        var flags = ParseFlags(args.Skip(1));

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"File not found: {archivePath}");
            return 1;
        }

        try
        {
            string? filter = flags.GetValueOrDefault("filter");
            Regex? filterRegex = filter != null
                ? new Regex("^" + Regex.Escape(filter).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase)
                : null;

            var entries = new List<(string Name, long OriginalSize, long StoredSize, string Type)>();

            if (archivePath.EndsWith(".eix", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new EixEpkReader();
                if (Enum.TryParse<EpkFormat>(flags.GetValueOrDefault("format", "Auto"), true, out var fmt))
                    reader.Format = fmt;
                reader.Open(archivePath);
                foreach (var e in reader.Entries)
                {
                    if (filterRegex == null || filterRegex.IsMatch(e.FileName))
                        entries.Add((e.FileName, e.RealDataSize, e.DataSize, e.CompressedTypeName));
                }
            }
            else
            {
                string? passphrase = flags.GetValueOrDefault("passphrase");
                var archive = VpkArchive.Open(archivePath, passphrase);
                foreach (var e in archive.Entries)
                {
                    if (filterRegex == null || filterRegex.IsMatch(e.FileName))
                    {
                        string type = string.Join("+", new[] { e.IsCompressed ? "CMP" : null, e.IsEncrypted ? "ENC" : null }.Where(x => x != null));
                        entries.Add((e.FileName, e.OriginalSize, e.StoredSize, type));
                    }
                }
            }

            if (_json)
            {
                Console.WriteLine(JsonSerializer.Serialize(entries.Select(e => new { e.Name, e.OriginalSize, e.StoredSize, e.Type }),
                    new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            if (!_quiet)
                Console.WriteLine($"{"File",-60} {"Original",10} {"Stored",10} {"Type",-10}");

            foreach (var (name, orig, stored, type) in entries)
                Console.WriteLine($"{name,-60} {FormatSize(orig),10} {FormatSize(stored),10} {type,-10}");

            if (!_quiet)
                Console.WriteLine($"\n{entries.Count} file(s).");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to list: {ex.Message}");
            return 1;
        }
    }

    static int Search(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: 42pak-cli search <pattern> <archive1.vpk> [archive2.vpk ...] [--passphrase X]"); return 1; }

        string pattern = args[0];
        var flags = ParseFlags(args.Skip(1));
        var archiveArgs = args.Skip(1).TakeWhile(a => !a.StartsWith("--")).ToList();

        if (archiveArgs.Count == 0) { Console.Error.WriteLine("No archive files specified."); return 1; }

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            string? passphrase = flags.GetValueOrDefault("passphrase");
            int totalMatches = 0;

            foreach (string archivePath in archiveArgs)
            {
                if (!File.Exists(archivePath))
                {
                    Console.Error.WriteLine($"File not found: {archivePath}");
                    continue;
                }

                var archive = VpkArchive.Open(archivePath, passphrase);
                var matches = archive.Entries.Where(e => regex.IsMatch(e.FileName)).ToList();

                if (matches.Count > 0)
                {
                    if (!_quiet) Console.WriteLine($"{archivePath}:");
                    foreach (var e in matches)
                        Console.WriteLine($"  {e.FileName}  ({FormatSize(e.OriginalSize)})");
                    totalMatches += matches.Count;
                }
            }

            if (!_quiet) Console.WriteLine($"\n{totalMatches} match(es) found.");
            return totalMatches > 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Search failed: {ex.Message}");
            return 1;
        }
    }

    static int Diff(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: 42pak-cli diff <archive1.vpk> <archive2.vpk> [--passphrase X]"); return 1; }

        string path1 = args[0], path2 = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!File.Exists(path1)) { Console.Error.WriteLine($"File not found: {path1}"); return 1; }
        if (!File.Exists(path2)) { Console.Error.WriteLine($"File not found: {path2}"); return 1; }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            var a1 = VpkArchive.Open(path1, passphrase);
            var a2 = VpkArchive.Open(path2, passphrase);

            var files1 = a1.Entries.ToDictionary(e => e.FileName, e => e);
            var files2 = a2.Entries.ToDictionary(e => e.FileName, e => e);

            var allNames = files1.Keys.Union(files2.Keys).OrderBy(n => n).ToList();

            int added = 0, removed = 0, modified = 0, unchanged = 0;
            var changes = new List<object>();

            foreach (string name in allNames)
            {
                bool in1 = files1.ContainsKey(name), in2 = files2.ContainsKey(name);

                if (in1 && !in2)
                {
                    if (!_quiet && !_json) Console.WriteLine($"  - {name}");
                    if (_json) changes.Add(new { Status = "removed", File = name });
                    removed++;
                }
                else if (!in1 && in2)
                {
                    if (!_quiet && !_json) Console.WriteLine($"  + {name}");
                    if (_json) changes.Add(new { Status = "added", File = name });
                    added++;
                }
                else
                {
                    var e1 = files1[name];
                    var e2 = files2[name];
                    bool sizeMatch = e1.OriginalSize == e2.OriginalSize;
                    bool hashMatch = e1.ContentHash.SequenceEqual(e2.ContentHash);

                    if (!sizeMatch || !hashMatch)
                    {
                        if (!_quiet && !_json) Console.WriteLine($"  ~ {name}  ({FormatSize(e1.OriginalSize)} -> {FormatSize(e2.OriginalSize)})");
                        if (_json) changes.Add(new { Status = "modified", File = name, OldSize = e1.OriginalSize, NewSize = e2.OriginalSize });
                        modified++;
                    }
                    else
                    {
                        unchanged++;
                    }
                }
            }

            if (_json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { Added = added, Removed = removed, Modified = modified, Unchanged = unchanged, Changes = changes },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"\n  Added: {added}, Removed: {removed}, Modified: {modified}, Unchanged: {unchanged}");
            }

            return (added + removed + modified) > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Diff failed: {ex.Message}");
            return 1;
        }
    }

    static int Migrate(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: 42pak-cli migrate <inputDir> <outputDir> [options]"); return 1; }

        string inputDir = args[0], outputDir = args[1];
        var flags = ParseFlags(args.Skip(2));

        if (!Directory.Exists(inputDir)) { Console.Error.WriteLine($"Directory not found: {inputDir}"); return 1; }
        Directory.CreateDirectory(outputDir);

        var options = new VpkBuildOptions
        {
            Passphrase = flags.GetValueOrDefault("passphrase"),
            CompressionLevel = int.TryParse(flags.GetValueOrDefault("compression"), out var cl) ? cl : 3,
            CompressionAlgorithm = Enum.TryParse<CompressionAlgorithm>(flags.GetValueOrDefault("algorithm", "LZ4"), true, out var ca) ? ca : CompressionAlgorithm.LZ4
        };

        if (options.Passphrase != null)
            options.EnableEncryption = true;

        var eixFiles = Directory.GetFiles(inputDir, "*.eix", SearchOption.TopDirectoryOnly);
        if (eixFiles.Length == 0) { Console.Error.WriteLine("No .eix files found in input directory."); return 1; }

        Enum.TryParse<EpkFormat>(flags.GetValueOrDefault("format", "Auto"), true, out var epkFormat);

        int success = 0, failures = 0;
        foreach (string eixPath in eixFiles)
        {
            string vpkName = Path.ChangeExtension(Path.GetFileName(eixPath), ".vpk");
            string vpkPath = Path.Combine(outputDir, vpkName);

            try
            {
                if (!_quiet) Console.Write($"  {Path.GetFileName(eixPath)} -> {vpkName} ... ");
                var converter = new EixEpkConverter { SourceFormat = epkFormat };
                var result = converter.Convert(eixPath, vpkPath, options);
                if (!_quiet) Console.WriteLine($"OK ({result.ConvertedFiles} files)");
                success++;
            }
            catch (Exception ex)
            {
                if (!_quiet) Console.WriteLine($"FAILED: {ex.Message}");
                failures++;
            }
        }

        if (!_quiet) Console.WriteLine($"\nMigrated: {success}/{eixFiles.Length}. Failures: {failures}.");
        return failures > 0 ? 1 : 0;
    }

    static int CheckDuplicates(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: 42pak-cli check-duplicates <archive1.vpk> [archive2.vpk ...] [--passphrase X]"); return 1; }

        var flags = ParseFlags(args);
        var archiveArgs = args.TakeWhile(a => !a.StartsWith("--")).ToList();

        if (archiveArgs.Count == 0) { Console.Error.WriteLine("No archive files specified."); return 1; }

        try
        {
            string? passphrase = flags.GetValueOrDefault("passphrase");
            var filesByHash = new Dictionary<string, List<(string Archive, string FileName, long Size)>>();

            foreach (string archivePath in archiveArgs)
            {
                if (!File.Exists(archivePath)) { Console.Error.WriteLine($"File not found: {archivePath}"); continue; }

                var archive = VpkArchive.Open(archivePath, passphrase);
                foreach (var e in archive.Entries)
                {
                    string hashKey = System.Convert.ToHexString(e.ContentHash);
                    if (!filesByHash.TryGetValue(hashKey, out var list))
                    {
                        list = new List<(string, string, long)>();
                        filesByHash[hashKey] = list;
                    }
                    list.Add((Path.GetFileName(archivePath), e.FileName, e.OriginalSize));
                }
            }

            var duplicates = filesByHash.Values.Where(l => l.Count > 1).OrderByDescending(l => l[0].Size).ToList();

            if (duplicates.Count == 0)
            {
                if (_json)
                    Console.WriteLine(JsonSerializer.Serialize(new { DuplicateGroups = 0, RedundantFiles = 0, WastedBytes = 0L }));
                else
                    Console.WriteLine("No duplicates found.");
                return 0;
            }

            int totalDuplicateFiles = duplicates.Sum(g => g.Count - 1);
            long wastedBytes = duplicates.Sum(g => g[0].Size * (g.Count - 1));

            if (_json)
            {
                var groups = duplicates.Select(g => new
                {
                    Size = g[0].Size,
                    Files = g.Select(f => new { f.Archive, f.FileName }).ToList()
                });
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    DuplicateGroups = duplicates.Count,
                    RedundantFiles = totalDuplicateFiles,
                    WastedBytes = wastedBytes,
                    Groups = groups
                }, new JsonSerializerOptions { WriteIndented = true }));
                return 1;
            }

            Console.WriteLine($"Found {duplicates.Count} duplicate groups ({totalDuplicateFiles} redundant files, ~{FormatSize(wastedBytes)} wasted):\n");

            foreach (var group in duplicates.Take(50))
            {
                Console.WriteLine($"  [{FormatSize(group[0].Size)}] Content hash match:");
                foreach (var (archive, fileName, _) in group)
                    Console.WriteLine($"    {archive}: {fileName}");
                Console.WriteLine();
            }

            if (duplicates.Count > 50)
                Console.WriteLine($"  ... and {duplicates.Count - 50} more duplicate groups.");

            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Check failed: {ex.Message}");
            return 1;
        }
    }

    static int Watch(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: 42pak-cli watch <sourceDir> [--output <file.vpk>] [--debounce <ms>] [options]"); return 1; }

        string sourceDir = args[0];
        var flags = ParseFlags(args.Skip(1));

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Directory not found: {sourceDir}");
            return 1;
        }

        string outputPath = flags.GetValueOrDefault("output") ?? Path.Combine(sourceDir, "..", Path.GetFileName(sourceDir) + ".vpk");
        int debounceMs = int.TryParse(flags.GetValueOrDefault("debounce"), out var db) ? db : 500;

        var options = new VpkBuildOptions
        {
            Passphrase = flags.GetValueOrDefault("passphrase"),
            CompressionLevel = int.TryParse(flags.GetValueOrDefault("compression"), out var cl) ? cl : 3,
            CompressionAlgorithm = Enum.TryParse<CompressionAlgorithm>(flags.GetValueOrDefault("algorithm", "LZ4"), true, out var ca) ? ca : CompressionAlgorithm.LZ4
        };

        if (options.Passphrase != null)
            options.EnableEncryption = true;

        Console.WriteLine($"Watching: {Path.GetFullPath(sourceDir)}");
        Console.WriteLine($"Output:   {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"Debounce: {debounceMs}ms");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        // Initial build
        try
        {
            RebuildVpk(sourceDir, outputPath, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Initial build failed: {ex.Message}");
        }

        using var watcher = new FileSystemWatcher(sourceDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName
        };

        var cts = new CancellationTokenSource();
        Timer? debounceTimer = null;
        object timerLock = new();

        void ScheduleRebuild()
        {
            lock (timerLock)
            {
                debounceTimer?.Dispose();
                debounceTimer = new Timer(_ =>
                {
                    try
                    {
                        RebuildVpk(sourceDir, outputPath, options);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Rebuild failed: {ex.Message}");
                    }
                }, null, debounceMs, Timeout.Infinite);
            }
        }

        watcher.Changed += (_, e) => { if (!_quiet) Console.WriteLine($"  Changed: {e.Name}"); ScheduleRebuild(); };
        watcher.Created += (_, e) => { if (!_quiet) Console.WriteLine($"  Created: {e.Name}"); ScheduleRebuild(); };
        watcher.Deleted += (_, e) => { if (!_quiet) Console.WriteLine($"  Deleted: {e.Name}"); ScheduleRebuild(); };
        watcher.Renamed += (_, e) => { if (!_quiet) Console.WriteLine($"  Renamed: {e.OldName} -> {e.Name}"); ScheduleRebuild(); };

        watcher.EnableRaisingEvents = true;

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            cts.Token.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException) { }

        lock (timerLock) { debounceTimer?.Dispose(); }

        Console.WriteLine("\nStopped watching.");
        return 0;
    }

    static void RebuildVpk(string sourceDir, string outputPath, VpkBuildOptions options)
    {
        Console.Write($"[{DateTime.Now:HH:mm:ss}] Rebuilding {Path.GetFileName(outputPath)} ... ");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var archive = new VpkArchive();
        archive.Build(sourceDir, outputPath, options);
        sw.Stop();
        Console.WriteLine($"OK ({archive.Entries.Count} files, {sw.ElapsedMilliseconds}ms)");
    }

    static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintUsage();
        return 1;
    }

    public static void PrintUsage()
    {
        Console.WriteLine(@"42pak-cli — VPK archive tool for Metin2

Usage: 42pak-cli [--quiet|-q] [--json] <command> [arguments] [options]

Commands:
  build | pack       <sourceDir> <output.vpk>         Create a VPK archive
  extract | unpack   <archive.vpk> <outputDir>        Extract files from a VPK archive
  info               <archive.vpk>                    Display archive details
  validate | verify  <archive.vpk>                    Verify archive integrity
  convert            <input.eix> <output.vpk>         Convert EIX/EPK to VPK format
  list               <archive.vpk|input.eix>          List files in an archive
  search             <pattern> <archive ...>          Regex search across archive(s)
  diff               <archive1.vpk> <archive2.vpk>   Compare two VPK archives
  migrate            <inputDir> <outputDir>           Batch convert all EIX/EPK to VPK
  check-duplicates   <archive.vpk ...>                Find duplicate files by content hash
  watch              <sourceDir>                      Watch directory and auto-rebuild
  help                                                Show this help message

Global Flags:
  --quiet, -q              Suppress informational output
  --json                   Output results in JSON format (info, list, validate, diff, check-duplicates)

Options (where applicable):
  --passphrase <value>     Encryption passphrase (enables AES-256-GCM encryption)
  --compression <0-12>     Compression level (default: 3)
  --algorithm <name>       LZ4, Zstandard, or Brotli (default: LZ4)
  --format <name>          Source EPK format: Auto, Standard, FliegeV3, or MartySama58 (default: Auto)
  --mangle                 Obfuscate file names with SHA256
  --filter <glob>          Filter files by glob pattern (list, extract)
  --output <file>          Output file path (watch)
  --debounce <ms>          Rebuild debounce delay in ms (watch, default: 500)
  --author <name>          Author metadata
  --comment <text>         Comment metadata

Exit Codes:
  0  Success
  1  Error (IO, argument, or conversion failure)
  2  Integrity failure (validate/verify)

Examples:
  42pak-cli build ./gamedata output.vpk --passphrase mysecret --algorithm Zstandard
  42pak-cli extract archive.vpk ./output --passphrase mysecret
  42pak-cli info archive.vpk --json
  42pak-cli validate archive.vpk --passphrase mysecret
  42pak-cli convert textures.eix textures.vpk --passphrase newsecret --format FliegeV3
  42pak-cli list archive.vpk --filter ""*.dds""
  42pak-cli search ""weapon.*sword"" pack1.vpk pack2.vpk
  42pak-cli diff old.vpk new.vpk --json
  42pak-cli migrate ./pack/ ./vpk/ --passphrase mysecret --format FliegeV3
  42pak-cli check-duplicates pack1.vpk pack2.vpk pack3.vpk
  42pak-cli watch ./gamedata --output game.vpk --debounce 1000");
    }

    static Dictionary<string, string> ParseFlags(IEnumerable<string> args)
    {
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].StartsWith("--"))
            {
                string key = list[i][2..];
                if (key == "mangle")
                {
                    flags[key] = "true";
                }
                else if (i + 1 < list.Count && !list[i + 1].StartsWith("--"))
                {
                    flags[key] = list[++i];
                }
            }
        }
        return flags;
    }

    static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB"];
        int i = Math.Min((int)Math.Floor(Math.Log(bytes, 1024)), units.Length - 1);
        return $"{bytes / Math.Pow(1024, i):F1} {units[i]}";
    }
}
