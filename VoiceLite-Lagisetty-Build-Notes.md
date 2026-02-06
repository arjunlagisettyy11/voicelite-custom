# VoiceLite - Lagisetty Build

Custom build with all models unlocked, CUDA GPU acceleration, and hot-swap model switching.

## Hardware
- NVIDIA RTX 5080 (16GB VRAM, Compute 12.0, CUDA 13.0, Driver 581.15)
- 32 CPU cores
- Microphone: Insta360 Link

## Best Config (Recommended)
- **Model:** Pro (ggml-small.bin, 466MB)
- **Preset:** Balanced (beam_size=3, best_of=2, temperature fallback)
- **VAD:** On, threshold 0.35
- **Result:** ~760ms per transcription, accurate, no hallucination

## Model Comparison (from testing on 2026-02-06)

| Model | Preset | Time | Result | Notes |
|-------|--------|------|--------|-------|
| Swift (base, 78MB) | Speed | 435ms | "So let's test the quantity right now of this." | Minor accuracy misses, fast |
| Swift (base, 78MB) | Balanced | worse | Quality degraded | Not recommended |
| Pro (small, 466MB) | Speed | 707ms | "What model am I using right now?" | Perfect, fast |
| Pro (small, 466MB) | Speed | 1,020ms | 21-second technical dictation with numbers | Accurate, captured "215", "14.5", "30k" |
| Pro (small, 466MB) | Balanced | 761ms | Long natural sentence | Perfect accuracy, sweet spot |
| Elite (medium, 1.5GB) | Balanced | 1,438ms | "Okay, let's check it again to see if there is a difference." | Hallucinated "difference" instead of "issue" |
| Pro (small, 466MB) | Accuracy | 11,650ms | Short sentence | Way too slow, 5x beam search not worth it |

## Key Findings
1. **Small model is the sweet spot** - faithful to what you say, no hallucination, fast with CUDA
2. **Medium model hallucinated words** - replaced "issue" with "difference" (semantic substitution)
3. **Accuracy preset (beam=5) is not worth it** - 10x slower for negligible improvement
4. **Balanced preset is the right tradeoff** - temperature fallback catches edge cases, ~760ms
5. **Speed preset works fine too** - if you want even faster (~700ms vs ~760ms)
6. **Base model is too inaccurate** - misses words, "It's not a nice" instead of actual speech

## Available Models (all in whisper/ directory)
- ggml-tiny.bin (42MB) - Lite - not recommended
- ggml-base.bin (78MB) - Swift - too inaccurate
- **ggml-small.bin (466MB) - Pro - RECOMMENDED**
- ggml-medium.bin (1.5GB) - Elite - hallucination risk
- ggml-large-v3.bin (2.9GB) - Ultra - untested, likely slower
- ggml-silero-vad.bin (865KB) - VAD model - always needed

## Build Details
- Source: C:\github\voicelite-custom\
- Build output: C:\github\voicelite-custom\VoiceLite\VoiceLite\bin\Release\net8.0-windows\
- whisper.exe: CUDA 12.4 build (whisper.cpp v1.8.3) from ggml-org/whisper.cpp releases
- VoiceLite: .NET 8, WPF, original source with Pro gate + hash check + license popup removed
- Settings: C:\Users\arjun\AppData\Local\VoiceLite\settings.json

## Modifications from Original VoiceLite
1. ProFeatureService.cs - CanUseModel() returns true, GetAvailableModels() returns all 5
2. ModelResolverService.cs - Removed Pro license check in ResolveModelPath(), hash validation returns true
3. MainWindow.xaml.cs - ValidateWhisperModel() stripped of license checks and upgrade popups
4. PersistentWhisperService.cs - Model path no longer cached, enables hot-swap from Settings UI
5. MainWindow.xaml - Title: "VoiceLite - Lagisetty Build"
6. SettingsWindowNew.xaml - Header: "VoiceLite - Lagisetty Build", removed Pro upsell text
7. SystemTrayManager.cs - Tray tooltip: "VoiceLite - Lagisetty Build"
8. whisper.exe replaced with CUDA 12.4 cuBLAS build (v1.8.3)
