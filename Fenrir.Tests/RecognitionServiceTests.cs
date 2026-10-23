using System.Runtime.Versioning;
using Fenrir.Core.Services;

namespace Fenrir.Tests;

[SupportedOSPlatform("windows")]
public sealed class RecognitionServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"fenrir_test_{Guid.NewGuid():N}.wav");

    [Fact]
    public void TryDeleteFile_DeletesUnlockedFile()
    {
        File.WriteAllText(_path, "test");

        RecognitionService.TryDeleteFile(_path);

        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void TryDeleteFile_DoesNotThrowForLockedFile()
    {
        File.WriteAllText(_path, "test");

        using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => RecognitionService.TryDeleteFile(_path));

            Assert.Null(exception);
        }

        Assert.True(File.Exists(_path));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch
        {
        }
    }
}
