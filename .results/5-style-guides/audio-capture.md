# Style Guide: audio-capture

## Scope
Loopback capture service in core library.

## Observed Conventions
- `CaptureService` is an async-disposable class with `StartAsync`/`StopAsync`.
- Status and errors are surfaced via events (`StatusChanged`, `Faulted`).
- Uses NAudio `WasapiLoopbackCapture` and `WaveFileWriter`.

## Examples
- `./src/EZSampler.core/CaptureService.cs`
