using System.Runtime.Versioning;
using Fenrir.Core.Services;

namespace Fenrir.Tests;

[SupportedOSPlatform("windows")]
public sealed class CrashDumpCleanupServiceTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly string _crashDumps;
    private readonly string _minidump;
    private readonly string _memoryDump;

    public CrashDumpCleanupServiceTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "FenrirTests", Guid.NewGuid().ToString("N"));
        _crashDumps = Path.Combine(_baseDirectory, "CrashDumps");
        _minidump = Path.Combine(_baseDirectory, "Minidump");
        _memoryDump = Path.Combine(_baseDirectory, "MEMORY.DMP");

        Directory.CreateDirectory(_crashDumps);
        Directory.CreateDirectory(_minidump);
        File.WriteAllText(_memoryDump, new string('m', 256));
    }

    [Fact]
    public void Clean_DeletesDumpsAndKeepsFolders()
    {
        File.WriteAllText(Path.Combine(_crashDumps, "app.dmp"), new string('a', 128));
        File.WriteAllText(Path.Combine(_minidump, "kernel.dmp"), new string('k', 128));

        var service = new CrashDumpCleanupService(
            new[] { _crashDumps, _minidump },
            new[] { _memoryDump });

        var report = service.Clean();

        Assert.True(service.HasTargets);
        Assert.Equal(3, report.DeletedFiles);
        Assert.True(report.FreedBytes > 0);
        Assert.True(Directory.Exists(_crashDumps));
        Assert.True(Directory.Exists(_minidump));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_crashDumps));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_minidump));
        Assert.False(File.Exists(_memoryDump));
    }

    [Fact]
    public void Clean_SkipsLockedFile()
    {
        string lockedFile = Path.Combine(_crashDumps, "locked.dmp");
        File.WriteAllText(lockedFile, "locked");

        var service = new CrashDumpCleanupService(
            new[] { _crashDumps },
            Array.Empty<string>());

        using (var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var report = service.Clean();

            Assert.True(File.Exists(lockedFile));
            Assert.Equal(0, report.DeletedFiles);
            Assert.True(report.SkippedEntries >= 1);
        }
    }

    [Fact]
    public void Clean_DoesNotTouchSiblingFiles()
    {
        string sibling = Path.Combine(_baseDirectory, "keep.txt");
        File.WriteAllText(sibling, "keep");
        File.WriteAllText(Path.Combine(_crashDumps, "delete.dmp"), "delete");

        var service = new CrashDumpCleanupService(
            new[] { _crashDumps },
            Array.Empty<string>());

        service.Clean();

        Assert.True(File.Exists(sibling));
    }

    [Fact]
    public void HasTargets_FalseWhenNothingExists()
    {
        var service = new CrashDumpCleanupService(
            new[] { Path.Combine(_baseDirectory, "missing") },
            new[] { Path.Combine(_baseDirectory, "missing.dmp") });

        Assert.False(service.HasTargets);
        Assert.False(service.Clean().HasChanges);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDirectory))
                Directory.Delete(_baseDirectory, recursive: true);
        }
        catch
        {
        }
    }
}
