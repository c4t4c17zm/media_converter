# C0_0nverter

Нативное настольное приложение Windows для перекодирования аудио и видео. Браузер не используется, файлы обрабатываются локально.

## Запуск

Запустите `dist/C0_0nverter.exe`. Выберите исходный файл, целевой формат и качество, затем нажмите «Конвертировать».

EXE собран меньше 25 МБ. Для запуска нужен **.NET 8 Desktop Runtime**. Если FFmpeg уже доступен в PATH, приложение использует его. Иначе при первой конвертации скачает FFmpeg Essentials с сайта Windows-сборок, на который ссылается FFmpeg.org (около 110 МБ), проверит SHA-256 и сохранит в `%LOCALAPPDATA%\C0_0nverter` для следующих запусков. Для первой загрузки нужен интернет.

Поддерживаемые форматы: MP4, MKV, MOV, WebM, MP3, WAV, FLAC, OGG и AAC. Выберите аудиоформат, чтобы извлечь и перекодировать звук из видео. Максимальный размер исходного файла — 8 ГБ.

## Сборка

Нужен .NET 8 SDK. Команда:

```powershell
dotnet publish C0_0nverter.csproj -c Release -r win-x64 --self-contained false -p:UseAppHost=true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist
```
