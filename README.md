# PIT

[![Build](https://github.com/ProrokIzajasz/PIT/actions/workflows/build.yml/badge.svg)](https://github.com/ProrokIzajasz/PIT/actions/workflows/build.yml)

A Windows desktop tool for creating, recording and running reusable mouse and keyboard automation.

## Highlights

- macro recording and step editing
- mouse and keyboard actions with configurable timing
- reusable automation schemes and profiles
- mouse-button assignments
- local import and export of configurations

## Technology

C#, .NET 8 and WPF, with Windows input hooks for recording and playback.

## Run locally

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet run --project PIT.csproj
```

Profiles are stored locally and can be exported for transfer to another device. Build output and personal runtime data are excluded from Git.

## Project status

Functional prototype. Macro recording, editing and playback work, while advanced visual recognition and game-specific automation remain experimental and are intentionally not presented as finished features.

> Use automation responsibly and only where it is permitted.
