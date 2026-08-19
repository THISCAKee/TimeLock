using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TimeLockApp.Services;

internal sealed class ProtectedFileStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TimeLockApp-v2");

    internal void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value);
        byte[] protectedData = ProtectedData.Protect(json, Entropy, DataProtectionScope.LocalMachine);
        string temporaryPath = path + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedData);
        File.Move(temporaryPath, path, overwrite: true);
    }

    internal T? Load<T>(string path)
    {
        if (!File.Exists(path)) return default;
        byte[] json = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.LocalMachine);
        return JsonSerializer.Deserialize<T>(json);
    }
}
