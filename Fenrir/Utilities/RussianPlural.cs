namespace Fenrir.Core.Utilities;

internal static class RussianPlural
{
    public static string Files(int count) => $"{count} {Word(count, "файл", "файла", "файлов")}";

    public static string Folders(int count) => $"{count} {Word(count, "папка", "папки", "папок")}";

    public static string Objects(int count) => $"{count} {Word(count, "объект", "объекта", "объектов")}";

    public static string Steps(int count) => $"{count} {Word(count, "шаг", "шага", "шагов")}";

    public static string Word(int count, string one, string few, string many)
    {
        int mod100 = Math.Abs(count) % 100;
        int mod10 = Math.Abs(count) % 10;

        if (mod100 is >= 11 and <= 14)
            return many;

        return mod10 switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }
}
