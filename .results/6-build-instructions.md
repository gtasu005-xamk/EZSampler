# EZSampler Copilot Instructions

## Overview
This file guides AI assistants to generate features that match the existing EZSampler architecture and style. It is based only on observed patterns in the repository.

## File Category Reference
- **solution**: Visual Studio solution definition.
  - Examples: `./EZSampler.sln`
  - Conventions: single root solution; no project entries listed yet.
- **repo-docs**: Repository overview and layout.
  - Examples: `./README.md`
  - Conventions: short description with directory bullets.
- **editorconfig**: Editor defaults.
  - Examples: `./.editorconfig`
  - Conventions: CRLF, final newline, 4-space indent for C#.
- **project-files**: SDK-style .NET project definitions.
  - Examples: `./src/EZSampler.Console/EZSampler.Console.csproj`, `./src/EZSampler.core/EZSampler.Core.csproj`
  - Conventions: nullable + implicit usings enabled; console/WPF reference `EZSampler.Core`; core references NAudio.
- **console-host**: Console entrypoint and capture driver.
  - Examples: `./src/EZSampler.Console/Program.cs`
  - Conventions: top-level statements; user prompts via `Console.ReadLine()`; direct `CaptureService` usage.
- **audio-capture**: Loopback capture service.
  - Examples: `./src/EZSampler.core/CaptureService.cs`
  - Conventions: `CaptureService` with `StartAsync`/`StopAsync`; events for status/faults; uses `WasapiLoopbackCapture` + `WaveFileWriter`.
- **audio-processing**: Waveform aggregation utilities.
  - Examples: `./src/EZSampler.core/waveformAggregator.cs`
  - Conventions: parse buffers using `WaveFormat`; store peak tuples.
- **core-placeholder**: Empty core types.
  - Examples: `./src/EZSampler.core/Class1.cs`
  - Conventions: no special patterns.
- **wpf-xaml**: WPF UI markup.
  - Examples: `./src/EZSampler.UI.Wpf/App.xaml`, `./src/EZSampler.UI.Wpf/MainWindow.xaml`
  - Conventions: minimal markup; `StartupUri` for main window; empty `Grid` root.
- **wpf-codebehind**: WPF code-behind.
  - Examples: `./src/EZSampler.UI.Wpf/App.xaml.cs`, `./src/EZSampler.UI.Wpf/MainWindow.xaml.cs`
  - Conventions: minimal partial classes calling `InitializeComponent()`.
- **assemblyinfo**: Assembly metadata.
  - Examples: `./src/EZSampler.UI.Wpf/AssemblyInfo.cs`
  - Conventions: `ThemeInfo` attribute only.

## Feature Scaffold Guide
When adding a new feature, decide which host and core layers are involved:
- If the feature changes audio capture, add or extend a class under `src/EZSampler.core/` (audio-capture category) and keep capture lifecycle in `CaptureService` style.
- If the feature adds new audio analysis, add a utility under `src/EZSampler.core/` (audio-processing category) and keep `WaveFormat`-driven parsing.
- If the feature is console-driven, update `src/EZSampler.Console/Program.cs` with new prompts and lifecycle steps.
- If the feature is UI-driven, update `MainWindow.xaml` and `MainWindow.xaml.cs` directly; there is no MVVM or DI pattern to follow.

Example scaffolds:
- **New capture option**: add a new core class (audio-capture) and wire it from `Program.cs` or `MainWindow.xaml.cs`.
- **New waveform visualization data**: add a new processing helper (audio-processing) and connect it to `CaptureService.AudioChunk`.

## Integration Rules
- Use `CaptureService` as the capture entry point; control recording via `StartAsync`/`StopAsync`.
- Capture uses NAudio `WasapiLoopbackCapture` and writes WAV via `WaveFileWriter` with the capture `WaveFormat`.
- Status updates and faults are raised via `StatusChanged` and `Faulted` events.
- Console host remains interactive and blocks on `Console.ReadLine()` prompts.
- WPF UI remains code-behind driven; `App.xaml` uses `StartupUri=MainWindow.xaml`.

## Example Prompt Usage
**User prompt**:
"Add a Start/Stop button to the WPF window that uses `CaptureService` and shows the current capture state in the UI."

**Expected response outline**:
- Update `./src/EZSampler.UI.Wpf/MainWindow.xaml` to add buttons and a status text block.
- Update `./src/EZSampler.UI.Wpf/MainWindow.xaml.cs` to create and manage a `CaptureService` instance and update UI on `StatusChanged`.
- (Optional) Add a small helper under `./src/EZSampler.core/` if new capture-related logic is needed (audio-capture category).
