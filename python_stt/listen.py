"""
Fenrir STT — распознаёт речь из WAV-файла.
Вызывается из C#: python listen.py <путь_к_wav>
"""

import sys
import os
from faster_whisper import WhisperModel

# Принудительно UTF-8 для stdout
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

MODEL_SIZE = "tiny"

def main():
    if len(sys.argv) < 2:
        print("❌ Укажите путь к WAV-файлу: python listen.py audio.wav", file=sys.stderr)
        sys.exit(1)

    wav_path = sys.argv[1]

    if not os.path.exists(wav_path):
        print(f"❌ Файл не найден: {wav_path}", file=sys.stderr)
        sys.exit(1)

    # Загружаем модель (кэшируется после первого раза)
    model = WhisperModel(MODEL_SIZE, device="cpu", compute_type="int8")

    # Распознаём
    segments, _ = model.transcribe(wav_path, language="ru", beam_size=5)
    text = " ".join(segment.text for segment in segments)
    text = text.strip()

    # Выводим результат в stdout — C# его прочитает
    if text:
        print(text, flush=True)
    else:
        print("", flush=True)


if __name__ == "__main__":
    main()