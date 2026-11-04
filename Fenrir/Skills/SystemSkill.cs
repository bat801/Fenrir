using System.Diagnostics;
using System.Runtime.InteropServices;
using Fenrir.Core.Models;
using Fenrir.Core.Routing;

namespace Fenrir.Core.Skills;

public class SystemSkill : ISkill
{
    private enum SystemAction
    {
        Shutdown,
        Restart,
        Sleep,
        Lock,
        VolumeUp,
        VolumeDown,
        Mute,
        Unmute,
        TaskManager
    }

    public string Name => "system";

    public string Description => "Системные команды: громкость, блокировка, сон, выключение";

    public IReadOnlyList<SkillTrigger> Triggers { get; } = new[]
    {
        new SkillTrigger("громче", priority: 20),
        new SkillTrigger("громкость выше", priority: 20),
        new SkillTrigger("сделай громче", priority: 20),
        new SkillTrigger("прибавь звук", priority: 20),

        new SkillTrigger("тише", priority: 20),
        new SkillTrigger("громкость ниже", priority: 20),
        new SkillTrigger("сделай тише", priority: 20),
        new SkillTrigger("убавь звук", priority: 20),

        new SkillTrigger("выключи звук", priority: 20),
        new SkillTrigger("без звука", priority: 20),
        new SkillTrigger("мут", priority: 20),
        new SkillTrigger("mute", priority: 20),

        new SkillTrigger("включи звук", priority: 20),
        new SkillTrigger("верни звук", priority: 20),
        new SkillTrigger("анмут", priority: 20),
        new SkillTrigger("unmute", priority: 20),

        new SkillTrigger("диспетчер задач", priority: 30),
        new SkillTrigger("task manager", priority: 30),

        new SkillTrigger("заблокируй", priority: 20),
        new SkillTrigger("заблокируйте", priority: 20),
        new SkillTrigger("заблокировать", priority: 20),
        new SkillTrigger("блокировка", priority: 20),
        new SkillTrigger("заблокируй экран", priority: 30),

        new SkillTrigger("спящий режим", priority: 20),
        new SkillTrigger("режим сна", priority: 20),
        new SkillTrigger("гибернация", priority: 20),
        new SkillTrigger("усыпи компьютер", priority: 20),
        new SkillTrigger("усыпи", priority: 10),

        new SkillTrigger("выключи компьютер", priority: 30),
        new SkillTrigger("выключи пк", priority: 30),
        new SkillTrigger("выключить компьютер", priority: 30),
        new SkillTrigger("выключить пк", priority: 30),
        new SkillTrigger("выруби компьютер", priority: 30),
        new SkillTrigger("шатдаун", priority: 20),

        new SkillTrigger("перезагрузи", priority: 30),
        new SkillTrigger("перезагрузите", priority: 30),
        new SkillTrigger("перезагрузить", priority: 30),
        new SkillTrigger("перезагрузка", priority: 30),
        new SkillTrigger("ребут", priority: 20)
    };

    public bool CanHandle(string command) => ResolveAction(command) is not null;

    public Task<CommandResult> ExecuteAsync(string command)
    {
        SystemAction? action = ResolveAction(command);

        if (action is null)
            return Task.FromResult(CommandResult.Fail("Неизвестная системная команда."));

        return action.Value switch
        {
            SystemAction.Shutdown => Shutdown("/s", "Выключаю компьютер..."),
            SystemAction.Restart => Shutdown("/r", "Перезагружаю компьютер..."),
            SystemAction.Sleep => Hibernate(),
            SystemAction.Lock => LockWorkstation(),
            SystemAction.VolumeUp => ChangeVolume(10),
            SystemAction.VolumeDown => ChangeVolume(-10),
            SystemAction.Mute => Mute(true),
            SystemAction.Unmute => Mute(false),
            SystemAction.TaskManager => OpenTaskManager(),
            _ => Task.FromResult(CommandResult.Fail("Неизвестная системная команда."))
        };
    }

    private static SystemAction? ResolveAction(string command)
    {
        var tokens = TextNormalizer.Tokenize(command);

        if (tokens.Count == 0)
            return null;

        if (IsNegated(tokens))
            return null;

        if (TokenMatcher.Contains(tokens, "выключи", "компьютер") ||
            TokenMatcher.Contains(tokens, "выключи", "пк") ||
            TokenMatcher.Contains(tokens, "выключить", "компьютер") ||
            TokenMatcher.Contains(tokens, "выключить", "пк") ||
            TokenMatcher.Contains(tokens, "выруби", "компьютер") ||
            TokenMatcher.Contains(tokens, "шатдаун"))
        {
            return SystemAction.Shutdown;
        }

        if (TokenMatcher.ContainsAny(
                tokens,
                "перезагрузи", "перезагрузите", "перезагрузить",
                "перезагрузка", "ребут"))
        {
            return SystemAction.Restart;
        }

        if (TokenMatcher.Contains(tokens, "спящий", "режим") ||
            TokenMatcher.Contains(tokens, "режим", "сна") ||
            TokenMatcher.ContainsAny(tokens, "гибернация") ||
            TokenMatcher.Contains(tokens, "усыпи", "компьютер") ||
            TokenMatcher.ContainsAny(tokens, "усыпи"))
        {
            return SystemAction.Sleep;
        }

        if (TokenMatcher.ContainsAny(
                tokens,
                "заблокируй", "заблокируйте", "заблокировать", "блокировка"))
        {
            return SystemAction.Lock;
        }

        if (TokenMatcher.Contains(tokens, "громкость", "выше") ||
            TokenMatcher.ContainsAny(tokens, "громче") ||
            TokenMatcher.Contains(tokens, "прибавь", "звук") ||
            TokenMatcher.Contains(tokens, "сделай", "громче"))
        {
            return SystemAction.VolumeUp;
        }

        if (TokenMatcher.Contains(tokens, "громкость", "ниже") ||
            TokenMatcher.ContainsAny(tokens, "тише") ||
            TokenMatcher.Contains(tokens, "убавь", "звук") ||
            TokenMatcher.Contains(tokens, "сделай", "тише"))
        {
            return SystemAction.VolumeDown;
        }

        if (TokenMatcher.Contains(tokens, "выключи", "звук") ||
            TokenMatcher.Contains(tokens, "без", "звука") ||
            TokenMatcher.ContainsAny(tokens, "мут", "mute"))
        {
            return SystemAction.Mute;
        }

        if (TokenMatcher.Contains(tokens, "включи", "звук") ||
            TokenMatcher.Contains(tokens, "верни", "звук") ||
            TokenMatcher.ContainsAny(tokens, "анмут", "unmute"))
        {
            return SystemAction.Unmute;
        }

        if (TokenMatcher.Contains(tokens, "диспетчер", "задач") ||
            TokenMatcher.Contains(tokens, "task", "manager"))
        {
            return SystemAction.TaskManager;
        }

        return null;
    }

    private static bool IsNegated(IReadOnlyList<string> tokens)
    {
        if (TokenMatcher.Contains(tokens, "не", "надо") ||
            TokenMatcher.Contains(tokens, "не", "нужно") ||
            TokenMatcher.ContainsAny(tokens, "нельзя"))
        {
            return true;
        }

        for (int i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i] != "не")
                continue;

            string next = tokens[i + 1];

            if (TokenMatcher.MatchesToken(next, "выключ*") ||
                TokenMatcher.MatchesToken(next, "перезагруз*") ||
                TokenMatcher.MatchesToken(next, "заблокируй*") ||
                TokenMatcher.MatchesToken(next, "блокировк*") ||
                TokenMatcher.MatchesToken(next, "усыпи*") ||
                TokenMatcher.MatchesToken(next, "громче") ||
                TokenMatcher.MatchesToken(next, "тише") ||
                TokenMatcher.MatchesToken(next, "прибавь") ||
                TokenMatcher.MatchesToken(next, "убавь") ||
                TokenMatcher.MatchesToken(next, "включи") ||
                TokenMatcher.MatchesToken(next, "верни") ||
                TokenMatcher.MatchesToken(next, "мут") ||
                TokenMatcher.MatchesToken(next, "mute") ||
                TokenMatcher.MatchesToken(next, "unmute") ||
                TokenMatcher.MatchesToken(next, "анмут") ||
                TokenMatcher.MatchesToken(next, "диспетчер") ||
                TokenMatcher.MatchesToken(next, "task"))
            {
                return true;
            }
        }

        return false;
    }

    private Task<CommandResult> OpenTaskManager()
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

            return Task.FromResult(CommandResult.Fail("Не удалось заблокировать экран."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Ошибка: {ex.Message}"));
        }
    }

    private Task<CommandResult> ChangeVolume(int delta)
    {
        try
        {
            int presses = Math.Abs(delta) / 2;
            if (presses == 0)
                presses = 1;

            byte keyCode = (byte)(delta > 0 ? 0xAF : 0xAE); // VK_VOLUME_UP / VK_VOLUME_DOWN
            string direction = delta > 0 ? "громче" : "тише";

            for (int i = 0; i < presses; i++)
            {
                keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                Thread.Sleep(50);
                keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(50);
            }

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
            keybd_event(0xAD, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(0xAD, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);

            string state = mute ? "выключен" : "включен";
            return Task.FromResult(CommandResult.Ok($"Звук {state}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Fail($"Не удалось переключить звук: {ex.Message}"));
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", EntryPoint = "LockWorkStation")]
    private static extern bool LockWorkStationNative();
}
