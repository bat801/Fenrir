using Fenrir.Core.Utilities;

namespace Fenrir.Tests;

public sealed class FormattingTests
{
    [Theory]
    [InlineData(512, "512 байт")]
    [InlineData(1024, "1 КБ")]
    [InlineData(1536, "1,5 КБ")]
    [InlineData(1024 * 1024, "1 МБ")]
    [InlineData(1024L * 1024 * 1024, "1 ГБ")]
    public void ByteSizeFormatter_UsesReadableRussianUnits(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes));
    }

    [Theory]
    [InlineData(1, "файл")]
    [InlineData(2, "файла")]
    [InlineData(4, "файла")]
    [InlineData(5, "файлов")]
    [InlineData(11, "файлов")]
    [InlineData(21, "файл")]
    [InlineData(22, "файла")]
    [InlineData(25, "файлов")]
    public void RussianPlural_HandlesRussianRules(int count, string expected)
    {
        Assert.Equal($"{count} {expected}", RussianPlural.Files(count));
    }
}
