using System.Security.Cryptography;
using System.Text;

namespace FortyTwoPak.Core.Utils;

public static class FileNameMangler
{
    public static string Mangle(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        byte[] nameBytes = Encoding.UTF8.GetBytes(fileName.ToLowerInvariant());
        byte[] hash = SHA256.HashData(nameBytes);

        // Use first 16 bytes of hash as hex string for the mangled name
        // Prefix with directory depth indicator
        int depth = fileName.Count(c => c == '/');
        string prefix = depth > 0 ? $"d{depth}_" : "";
        string hexName = Convert.ToHexString(hash[..16]).ToLowerInvariant();

        return $"{prefix}{hexName}{ext}";
    }

    public static bool IsMangled(string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        // Mangled names are hex strings, optionally prefixed with d{N}_
        if (nameWithoutExt.StartsWith('d') && nameWithoutExt.Contains('_'))
            nameWithoutExt = nameWithoutExt[(nameWithoutExt.IndexOf('_') + 1)..];

        return nameWithoutExt.Length == 32 &&
               nameWithoutExt.All(c => "0123456789abcdef".Contains(c));
    }
}
