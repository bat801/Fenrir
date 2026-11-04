using System.Globalization;

namespace Fenrir.Core.Utilities;

internal static class ByteSizeFormatter
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024 * 1024)
            return $"{(bytes / (1024.0 * 1024 * 1024 * 1024)).ToString("0.#", Russian)} ТБ";

        if (bytes >= 1024L * 1024 * 1024)
            return $"{(bytes / (1024.0 * 1024 * 1024)).ToString("0.#", Russian)} ГБ";

        if (bytes >= 1024 * 1024)
            return $"{(bytes / (1024.0 * 1024)).ToString("0.#", Russian)} МБ";

        if (bytes >= 1024)
            return $"{(bytes / 1024.0).ToString("0.#", Russian)} КБ";

        return $"{bytes} байт";
    }
}
