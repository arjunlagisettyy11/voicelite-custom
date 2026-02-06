# VoiceLite Custom

Windows speech-to-text app with AI rewrite. Hold a key, speak, release — text appears wherever your cursor is. 100% offline transcription powered by [whisper.cpp](https://github.com/ggerganov/whisper.cpp), with optional local LLM rewrite via [Ollama](https://ollama.com).

Forked from [mikha08-rgb/VoiceLite](https://github.com/mikha08-rgb/VoiceLite).

[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## What's Different in This Fork

This fork adds an **AI Rewrite pipeline** and removes all licensing restrictions. See [NOTICE](NOTICE) for full attribution and changelog.

| Enhancement | Description |
|-------------|-------------|
| **AI Rewrite Mode** | Second hotkey: Record → Transcribe → Rewrite with local LLM → Inject polished text |
| **Ollama Backend** | GPU-accelerated LLM via `ollama run`. Works with any Ollama model (gemma3, qwen2.5, llama3.2, etc.) |
| **Mic Warm-Up Fix** | Fixed cold-start bug where first recording captured 0 bytes of audio |
| **All Models Unlocked** | Tiny, Base, Small, Medium, Large-v3 — all available, no license required |
| **CUDA GPU Support** | GPU-accelerated whisper.cpp inference via configurable `-ngl` layers |
| **Model Hot-Swap** | Switch whisper models at runtime without restarting |

## Install

**Requirements:** Windows 10/11 (64-bit), 4GB RAM

1. Install [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) (required)
2. Download the latest release or [build from source](#building-from-source)
3. For AI Rewrite: install [Ollama](https://ollama.com/download) and pull a model (`ollama pull gemma3:4b`)

If VoiceLite won't start: install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) (Windows x64)

## Usage

### Transcription (voice → text)

**Default hotkey:** `Ctrl+Alt+R` (configurable in Settings)

Hold hotkey → speak → release → text appears in active window.

### AI Rewrite (voice → improved text)

**Separate configurable hotkey** (set in Settings → AI Rewrite)

Hold rewrite hotkey → speak → release → transcription → LLM rewrites → polished text appears.

**Prompt presets:** Improve, Formalize, Simplify, Summarize, Fix Grammar — or write your own.

**Requires:** [Ollama](https://ollama.com/download) running locally with at least one model installed.

### Recommended Ollama Models

| Model | VRAM | Quality | Speed |
|-------|------|---------|-------|
| `qwen2.5:7b` | ~5GB | Best | Fast (GPU) |
| `gemma3:4b` | ~3GB | Good | Fast (GPU) |
| `llama3.2:3b` | ~2.5GB | Good | Fastest (GPU) |
| `gemma3:12b` | ~8GB | Excellent | Moderate |

```bash
ollama pull qwen2.5:7b    # recommended default
```

## Models (Whisper)

| Model | Size | Speed | Accuracy |
|-------|------|-------|----------|
| Tiny | 42MB | Fastest | ~80-85% |
| Base | 74MB | Fast | ~87% |
| Small | 244MB | Medium | ~90% |
| Medium | 769MB | Slow | ~95% |
| Large-v3 | 1.5GB | Slowest | ~98% |

All models included — no license required.

## Features

- Works in any Windows application
- Customizable hotkeys (transcription + rewrite)
- AI rewrite with configurable prompts and temperature
- 99 languages supported
- CUDA GPU acceleration
- Low resource usage when idle
- 100% offline — voice never leaves your PC

## FAQ

**Offline?** Transcription is 100% offline. AI rewrite uses Ollama which also runs locally — nothing leaves your PC.

**Languages?** 99 supported for transcription. Change in Settings → Language.

**Works in games?** Yes. Use windowed mode if fullscreen blocks hotkey.

**Need a GPU for rewrite?** Ollama works on CPU too, but GPU is significantly faster. Any NVIDIA GPU with 4GB+ VRAM works well.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| "VCRUNTIME140_1.dll not found" | Install [VC++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) |
| Won't start | Install [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) |
| Windows Defender warning | Click "More info" → "Run anyway" |
| Hotkey doesn't work | Change in Settings — another app may use it |
| First recording empty | Should be fixed by mic warm-up. Restart the app if it persists |
| Rewrite stuck/slow | Check Ollama is running (`ollama list`). GPU models are faster |
| Low transcription accuracy | Use larger whisper model or speak more clearly |

## Building from Source

```bash
# Build
dotnet build VoiceLite/VoiceLite.sln

# Run
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Test (~412 tests)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Release build (self-contained, no .NET install needed)
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained

# Installer (requires Inno Setup)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup.iss
```

## Tech Stack

- .NET 8 / WPF
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) — speech recognition (subprocess)
- [Ollama](https://github.com/ollama/ollama) — local LLM runtime for AI rewrite
- [NAudio](https://github.com/naudio/NAudio) — audio capture

## Attribution

This is a fork of [VoiceLite](https://github.com/mikha08-rgb/VoiceLite) by **Mikhail Lev** (mikha08-rgb).

See [NOTICE](NOTICE) for full attribution, authors, and detailed enhancement descriptions.

## Authors

- **Mikhail Lev** ([@mikha08-rgb](https://github.com/mikha08-rgb)) — Original VoiceLite
- **Arjun Lagisetty** ([@arjunlagisettyy11](https://github.com/arjunlagisettyy11)) — Fork enhancements
- **Claude** (Anthropic) — Co-authored fork implementation

## License

[MIT](LICENSE)
