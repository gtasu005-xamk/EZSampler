# Style Guide: audio-processing

## Scope
Waveform aggregation utilities.

## Observed Conventions
- Uses NAudio `WaveFormat` to parse buffers.
- Stores peak data as tuples of min/max per channel.

## Examples
- `./src/EZSampler.core/waveformAggregator.cs`
