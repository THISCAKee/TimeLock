using System.IO;

namespace TimeLockApp.Services;

internal sealed class TimelockConfigurationService
{
    private readonly string _path;
    private readonly ProtectedFileStore _store = new();

    internal TimelockConfigurationService(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TimeLockApp");
        _path = Path.Combine(dataDirectory, "device-config.bin");
    }

    internal TimelockDeviceConfiguration? Load() =>
        _store.Load<TimelockDeviceConfiguration>(_path);

    internal void Save(TimelockDeviceConfiguration configuration) =>
        _store.Save(_path, configuration);
}
