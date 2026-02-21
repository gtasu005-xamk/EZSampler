# EZSampler Tech Stack

## Core Technology Analysis
- Languages: C# for application code, XAML for WPF UI markup.
- Platform: .NET 10.0 (console) and .NET 10.0 Windows (WPF).
- UI framework: WPF with code-behind (`App.xaml`, `MainWindow.xaml`).
- Audio library: NAudio (`WasapiLoopbackCapture`, `WaveFileWriter`, `WaveFormat`).
- Concurrency: `async`/`await`, background capture loop, event-driven status updates.

## Domain Specificity Analysis
- Problem domain: sample-based audio capture and simple waveform peak aggregation.
- Core concepts: loopback audio capture, WAV file output, waveform peak (min/max) calculation.
- User interactions: start/stop recording from a host (console now, WPF UI stubbed).
- Primary data structures: byte buffers from NAudio, `WaveFormat`, peak tuples `(minL, maxL, minR, maxR)`.

## Application Boundaries
- In-scope: capturing system audio via WASAPI loopback, writing WAV files to disk, emitting status/fault events, computing waveform peaks from captured buffers.
- Out-of-scope (no evidence in code): audio editing, playback, device selection UI, network APIs, persistence beyond WAV files, or multi-track project management.
- Architectural constraints: capture logic lives in `EZSampler.Core` and is consumed by hosts (console/WPF).