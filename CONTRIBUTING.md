# Contributing to FanDrop

Thanks for your interest! Bug reports, ideas and pull requests are welcome.

## Building

Requirements: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), and optionally [FFmpeg](https://ffmpeg.org/) for media actions.

```bash
cd FanDropWin
dotnet build -c Release
dotnet run -c Release
```

Self-contained single-file build (used for the installer):

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
```

The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) from `FanDropWin/installer.iss`; pass the path to `ffmpeg.exe` with `/DFFMPEG=C:\path\to\ffmpeg.exe`.

## Pull requests

1. Fork the repo and create a branch from `main`.
2. Keep changes focused; match the existing code style.
3. Describe what you changed and how you tested it.
4. Update `CHANGELOG.md` under an "Unreleased" heading.

## Reporting bugs

Please use the issue templates and attach the log (tray icon → **Open log**, or `%TEMP%\fandrop.log`).
