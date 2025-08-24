using System.Security.Cryptography;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Utils;

[Injectable]
public class HashUtil
{
    /// <summary>
    /// Create a file hash from an inputed file
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>A file hash</returns>
    public string GetFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);

        var hashBytes = sha256.ComputeHash(stream);

        var hash = Convert.ToHexStringLower(hashBytes);
        return hash;
    }
}
