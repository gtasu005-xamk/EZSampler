# Domain: audio-capture

## Purpose
Provides loopback audio capture and WAV file output via `CaptureService` in `EZSampler.Core`.

## Key Files
- `./src/EZSampler.core/CaptureService.cs`

## Observed Patterns
- Capture lifecycle is managed by `StartAsync` and `StopAsync` with an internal worker task.
- Status is surfaced through the `StatusChanged` event and fault details through `Faulted`.
- Recording uses `WasapiLoopbackCapture` and writes to `WaveFileWriter`.

## Code Examples
```csharp
public Task StartAsync(CaptureOptions options, CancellationToken ct = default)
{
    lock (_gate)
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Status = new(CaptureState.Starting);

        _worker = Task.Run(() => RunCaptureLoopAsync(options, _cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }
}
```

```csharp
capture = new WasapiLoopbackCapture();
writer = new WaveFileWriter(outputPath, capture.WaveFormat);

capture.DataAvailable += (s, e) =>
{
    writer.Write(e.Buffer, 0, e.BytesRecorded);
    AudioChunk?.Invoke(this, e.Buffer.AsMemory(0, e.BytesRecorded));
};
```

## Notes
- Output is written to the Desktop with a timestamped filename.
- Cancellation is handled by stopping the capture and awaiting a completion task.