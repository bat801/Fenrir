# 🐺 Fenrir

Voice-controlled personal assistant for Windows — inspired by J.A.R.V.I.S.

## ✨ What it does

- 🎤 **Voice input** — hold Ctrl+Shift and speak, powered by OpenAI Whisper
- 🔊 **Voice output** — responds in Russian via Windows TTS
- ⌨️ **Text input** — works in parallel with voice
- 🌐 Open browser (Yandex/Google)
- 🧮 Calculate math expressions (including spoken numbers)
- ⏰ Tell time and date
- ⚙️ System control: volume, mute, lock screen, task manager, shutdown
- 🧹 Cleanup: empty recycle bin and Windows/user temp folders (with confirmation)
- 🎮 AMD shader cache cleanup: DxCache, DxcCache, OglCache, VkCache
- 🧨 Full cleanup: recycle bin, temp files, AMD cache, crash dumps and Windows Error Reporting reports

## 🛠 Tech stack

| Component | Technology |
|-----------|------------|
| Core | C# .NET 8 |
| Voice recognition | Python + faster-whisper (small) |
| Speech synthesis | System.Speech |
| Audio capture | NAudio |
| Hotkey detection | Win32 API |

## 🚀 Quick start

1. Install [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) and Python 3.10+
2. Install Python dependencies (creates a persistent venv in `python_stt\.venv`):
   ```bat
   python_stt\setup.bat
   ```
   Or install globally: `pip install -r python_stt/requirements.txt`
3. Build and run `Fenrir.sln` (or launch `Fenrir.exe` from a release folder)
4. For system commands (Task Manager, shutdown) run Fenrir as administrator
5. On the first voice request Whisper downloads the model to `%LOCALAPPDATA%\Fenrir\cache` (~500 MB)

To check which paths and Python interpreter are used, run:
```bat
Fenrir.exe --diagnose
```

To inspect how a command is routed without starting voice input, run:
```bat
Fenrir.exe --route "сделай, пожалуйста, генеральную уборку"
```

## 🧭 Intent routing

Fenrir uses a deterministic two-stage router:

1. The command is normalized (case, `ё` → `е`, punctuation, extra spaces) and matched by whole tokens, not substrings.
2. Each candidate skill is asked `CanHandle` before execution, so a trigger alone is not enough.

This prevents false matches such as «персона» triggering sleep mode or «очисти кэш браузера» cleaning the AMD shader cache.
Say «что ты умеешь» to list the available skills and example phrases.

## 🧹 Cleanup commands

All cleanup actions ask for confirmation first.

| Phrase | What it cleans |
|--------|----------------|
| «очисти мусор», «убери мусор» | Recycle bin + Windows/user temp folders |
| «очисти кэш», «очисти кэш амд», «очисти кэш видеокарты», «шейдерный кэш» | AMD shader cache (`DxCache`, `DxcCache`, `OglCache`, `VkCache`) |
| «генеральная уборка», «полная уборка», «полная очистка», «очисти всё», «приберись» | Everything above + crash dumps + WER reports |

Full cleanup also removes `%LOCALAPPDATA%\CrashDumps`, `%WINDIR%\Minidump`, `%WINDIR%\LiveKernelReports`,
`%WINDIR%\MEMORY.DMP` and Windows Error Reporting queues. Run Fenrir as administrator to clean
system-wide WER data and Windows Temp completely.

## 🧪 Tests

```bash
dotnet test
```

## 📋 Roadmap

- [ ] Wake word activation ("Fenrir")
- [ ] Weather & news skills
- [ ] Notes & reminders
- [ ] NLU intent classification
- [ ] Local LLM integration
- [ ] System tray icon
- [ ] Web settings dashboard

## 📄 License

MIT
