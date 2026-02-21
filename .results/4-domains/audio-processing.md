# Domain: audio-processing

## Purpose
Aggregates waveform peaks from audio buffers using NAudio `WaveFormat` metadata.

## Key Files
- `./src/EZSampler.core/waveformAggregator.cs`

## Observed Patterns
- Peak aggregation is block-based, using `_samplesPerPeak` and a rolling min/max.
- Buffer parsing depends on `BitsPerSample` and `Channels`.

## Code Examples
```csharp
int bytesPerSample = _format.BitsPerSample / 8;
int channels = _format.Channels;
int blockAlign = bytesPerSample * channels;

for (int i = offset; i < offset + bytesRecorded; i += blockAlign)
{
    float left = BitConverter.ToSingle(buffer, i);

    float right = left;
    if (channels > 1)
        right = BitConverter.ToSingle(buffer, i + bytesPerSample);

    _minL = Math.Min(_minL, left);
    _maxL = Math.Max(_maxL, left);
    _minR = Math.Min(_minR, right);
    _maxR = Math.Max(_maxR, right);

    _sampleCounter++;

    if (_sampleCounter >= _samplesPerPeak)
    {
        Peaks.Add((_minL, _maxL, _minR, _maxR));
        _minL = float.MaxValue;
        _maxL = float.MinValue;
        _minR = float.MaxValue;
        _maxR = float.MinValue;
        _sampleCounter = 0;
    }
}
```

## Notes
- The aggregator currently assumes float samples via `BitConverter.ToSingle`.
- Results are stored as a list of peak tuples for left/right channels.