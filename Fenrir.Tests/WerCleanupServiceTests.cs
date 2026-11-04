using System.Runtime.Versioning;
using Fenrir.Core.Services;

namespace Fenrir.Tests;

[SupportedOSPlatform("windows")]
public sealed class WerCleanupServiceTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly string _reportQueue;
    private readonly string _reportArchive;
    private readonly string _temp;

    public WerCleanupServiceTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "FenrirTests", Guid.NewGuid().ToString("N"));
        _reportQueue = Path.Combine(_baseDirectory, "WER", "ReportQueue");
        _reportArchive = Path.Combine(_baseDirectory, "WER", "ReportArchive");
        _temp = Path.Combine(_baseDirectory, "WER", "Temp");

        Directory.CreateDirectory(_reportQueue);
        Directory.CreateDirectory(_reportArchive);
        Directory.CreateDirectory(_temp);
    }

    [Fact]
    public void Clean_DeletesWerContentsAndKeepsFolders()
    {
        File.WriteAllText(Path.Combine(_reportQueue, "queue.wer"), new string('q', 100));
        File.WriteAllText(Path.Combine(_reportArchive, "archive.wer"), new string('a', 100));
        File.WriteAllText(Path.Combine(_temp, "temp.wer"), new string('t', 100));

        string unknownFolder = Path.Combine(_baseDirectory, "WER", "Other");
        Directory.CreateDirectory(unknownFolder);
        string keepFile = Path.Combine(unknownFolder, "keep.txt");
        File.WriteAllText(keepFile, "keep");

        var service = new WerCleanupService(new[] { _reportQueue, _reportArchive, _temp });

        var report = service.Clean();

        Assert.True(service.HasTargets);
        Assert.Equal(3, report.DeletedFiles);
        Assert.True(report.FreedBytes > 0);
        Assert.True(File.Exists(keepFile));

        foreach (string folder in new[] { _reportQueue, _reportArchive, _temp })
        {
            Assert.True(Directory.Exists(folder));
            Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
        }
    }

    [Fact]
    public void Clean_SkipsLockedFile()
    {
        string lockedFile = Path.Combine(_reportQueue, "locked.wer");
        File.WriteAllText(lockedFile, "locked");

        var service = new WerCleanupService(new[] { _reportQueue });

        using (var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var report = service.Clean();

            Assert.True(File.Exists(lockedFile));
            Assert.Equal(0, report.DeletedFiles);
            Assert.True(report.SkippedEntries >= 1);
        }
    }

    [Fact]
    public void HasTargets_FalseWhenNothingExists()
    {
        var service = new WerCleanupService(new[] { Path.Combine(_baseDirectory, "missing") });

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
