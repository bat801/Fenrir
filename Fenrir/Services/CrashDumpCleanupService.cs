using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

/// <summary>
/// Крэш-дампы приложений и системы: %LOCALAPPDATA%\CrashDumps,
/// %WINDIR%\Minidump и %WINDIR%\MEMORY.DMP.
/// </summary>
public sealed class CrashDumpCleanupService
{
    private readonly IReadOnlyList<string> _directories;
    private readonly IReadOnlyList<string> _files;

    public CrashDumpCleanupService()
        : this(GetDefaultDirectories(), GetDefaultFiles())
    {
    }

    internal CrashDumpCleanupService(
        IEnumerable<string> directories,
        IEnumerable<string> files)
    {
        _directories = directories.ToArray();
        _files = files.ToArray();
    }

    public bool HasTargets =>
        _directories.Any(Directory.Exists) || _files.Any(File.Exists);

    public CleanupReport Clean()
    {
        return PathSetCleaner.Clean(_directories, _files);
    }

    private static IEnumerable<string> GetDefaultDirectories()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "CrashDumps");

        if (!string.IsNullOrWhiteSpace(windows))
        {
            yield return Path.Combine(windows, "Minidump");
            yield return Path.Combine(windows, "LiveKernelReports");
        }
    }

    private static IEnumerable<string> GetDefaultFiles()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (!string.IsNullOrWhiteSpace(windows))
            yield return Path.Combine(windows, "MEMORY.DMP");
    }
}
