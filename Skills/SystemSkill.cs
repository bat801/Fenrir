using Fenrir.Core.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fenrir.Core.Skills;

public class SystemSkill : ISkill
{
    public string Name => "system";
    public string[] Triggers => new[]
    {
        "выключи компьютер", "выключи пк", "выруби компьютер", "шатдаун",
        "перезагрузи", "перезагрузка", "ребут",
        "заблокируй", "блокировка", "заблокируй экран",
        "громче", "громкость выше", "сделай громче", "прибавь звук",
        "тише", "громкость ниже", "сделай тише", "убавь звук",
        "выключи звук", "без звука", "мут", "mute",
        "включи звук", "верни звук", "анмут", "unmute",
        "диспетчер задач", "task manager",
        "спящий режим", "сон", "гибернация", "усыпи"
    };

    public Task<CommandResult> ExecuteAsync(string command)
    {
        string lower = command.ToLowerInvariant();

        // --- Выключение / перезагрузка / сон ---
        if (lower.Contains("выключи") && (lower.Contains("компьютер") || lower.Contains("пк") || lower.Contains("шатдаун")))
        {
            return Shutdown("/s", "Выключаю компьютер...");
        }
        if (lower.Contains("перезагрузи") || lower.Contains("перезагрузка") || lower.Contains("ребут"))
        {
            return Shutdown("/r", "Перезагружаю компьютер...");
        }
        if (lower.Contains("спящий") || lower.Contains("сон") || lower.Contains("гибернаци") || lower.Contains("усыпи"))
        {
            return Hibernate();
        }

        // --- Блокировка экрана ---
        if (lower.Contains("заблокируй") || lower.Contains("блокировка"))
        {
            return LockWorkstation();
        }

        // --- Управление громкостью ---
        if (lower.Contains("громче") || lower.Contains("громкость выше") || lower.Contains("прибавь звук"))
        {
            return ChangeVolume(10);
        }
        if (lower.Contains("тише") || lower.Contains("громкость ниже") || lower.Contains("убавь звук"))
        {
            return ChangeVolume(-10);
        }
        if (lower.Contains("выключи звук") || lower.Contains("без звука") || lower.Contains("мут") || lower.Contains("mute"))
        {
            return Mute(true);
        }
        if (lower.Contains("включи звук") || lower.Contains("верни звук") || lower.Contains("анмут") || lower.Contains("unmute"))
        {
            return Mute(false);
        }

        // --- Диспетчер задач ---
        if (lower.Contains("диспетчер задач") || lower.Contains("task manager"))
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "taskmgr.exe",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(startInfo);
                return Task.FromResult(CommandResult.Ok("Открываю диспетчер задач."));
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return Task.FromResult(CommandResult.Fail(
                    "Недостаточно прав. Запустите Fenrir от имени администратора."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Fail($"Не удалось открыть: {ex.Message}"));
            }
        }

        return Task.FromResult(CommandResult.Fail("Неизвестная системная команда."));
    }

    // ===== Приватные методы =====

    private Task<CommandResult> Shutdown(string flag, string message)
    {
        try
        {
            Process.Start("shutdown", $"{flag} /t 10"); // 10 секунд, чтобы успеть отменить
            return Task.FromResult(CommandResult.Ok($"{message} (через 10 секунд)"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Ошибка: {ex.Message}"));
        }
    }

    private Task<CommandResult> Hibernate()
    {
        try
        {
            Process.Start("shutdown", "/h");
            return Task.FromResult(CommandResult.Ok("Перевожу компьютер в спящий режим..."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Ошибка: {ex.Message}"));
        }
    }

    private Task<CommandResult> LockWorkstation()
    {
        try
        {
            bool locked = LockWorkStationNative();
            if (locked)
                return Task.FromResult(CommandResult.Ok("Экран заблокирован."));
            else
                return Task.FromResult(CommandResult.Fail("Не удалось заблокировать экран."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Ошибка: {ex.Message}"));
        }
    }

    private Task<CommandResult> ChangeVolume(int delta)
    {
        // Отправляем нажатия клавиш громкости через PowerShell + SendKeys
        // Это самый надёжный способ без внешних библиотек
        try
        {
            int presses = Math.Abs(delta) / 2; // Каждое нажатие ~2%
            if (presses == 0) presses = 1;

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"$wshell = New-Object -ComObject WScript.Shell; for($i=0; $i -lt {presses}; $i++) {{ $wshell.SendKeys([char]{(delta > 0 ? 175 : 174)}) }}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);

            string direction = delta > 0 ? "громче" : "тише";
            return Task.FromResult(CommandResult.Ok($"Сделал {direction}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Не удалось изменить громкость: {ex.Message}"));
        }
    }

    private Task<CommandResult> Mute(bool mute)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"$wshell = New-Object -ComObject WScript.Shell; $wshell.SendKeys([char]173)\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);

            string state = mute ? "выключен" : "включен";
            return Task.FromResult(CommandResult.Ok($"Звук {state}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Не удалось переключить звук: {ex.Message}"));
        }
    }

    // Win32 API для блокировки экрана
    [DllImport("user32.dll", EntryPoint = "LockWorkStation")]
    private static extern bool LockWorkStationNative();
}