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

## 🛠 Tech stack

| Component | Technology |
|-----------|------------|
| Core | C# .NET 8 |
| Voice recognition | Python + faster-whisper (small) |
| Speech synthesis | System.Speech |
| Audio capture | NAudio |
| Hotkey detection | Win32 API |

## 🚀 Quick start

1. Install Python 3.10+ and dependencies:
   ```bash
   pip install faster-whisper
   ```
2. Install NAudio NuGet package in Visual Studio
3. Build and run `Fenrir.sln`

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
