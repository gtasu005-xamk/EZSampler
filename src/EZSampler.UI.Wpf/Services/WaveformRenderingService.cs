using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using NAudio.Wave;
using EZSampler.UI.Wpf.Configuration;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Manages waveform rendering and audio visualization.
/// Aggregates audio peaks and generates geometry for rendering.
/// </summary>
public sealed class WaveformRenderingService : IWaveformRenderingService
{
    private readonly object _gate = new();
    private WaveformAggregator? _waveform;
    private DateTime _lastGeometryUpdate = DateTime.MinValue;

    public event EventHandler<Geometry>? GeometryUpdated;

    public int PeakCount
    {
        get
        {
            lock (_gate)
            {
                return _waveform?.Peaks.Count ?? 0;
            }
        }
    }

    public void ProcessAudioChunk(ReadOnlyMemory<byte> buffer)
    {
        WaveformAggregator? aggregator;
        lock (_gate)
        {
            aggregator = _waveform;
        }

        if (aggregator == null)
        {
            return;
        }

        var bytes = buffer.ToArray();
        aggregator.Process(bytes, 0, bytes.Length);

        // Rate-limit geometry updates to reduce rendering overhead
        if ((DateTime.UtcNow - _lastGeometryUpdate).TotalMilliseconds >= AppConstants.WaveformUpdateIntervalMs)
        {
            _lastGeometryUpdate = DateTime.UtcNow;
            // Geometry will be generated on-demand in GetGeometry()
            GeometryUpdated?.Invoke(this, new StreamGeometry());
        }
    }

    public void InitializeAggregator(WaveFormat format)
    {
        lock (_gate)
        {
            _waveform = new WaveformAggregator(format);
        }
    }

    public Geometry? GetGeometry(double width, double height)
    {
        if (width <= 1 || height <= 1)
        {
            return null;
        }

        WaveformAggregator? aggregator;
        List<(float minL, float maxL, float minR, float maxR)> peaks;

        lock (_gate)
        {
            aggregator = _waveform;
            if (aggregator == null)
            {
                return null;
            }

            // Trim peaks if exceeding max
            if (aggregator.Peaks.Count > AppConstants.MaxWaveformPeaks)
            {
                aggregator.Peaks.RemoveRange(0, aggregator.Peaks.Count - AppConstants.MaxWaveformPeaks);
            }

            peaks = aggregator.Peaks.ToList();
        }

        if (peaks.Count == 0)
        {
            return null;
        }

        return GenerateGeometry(peaks, width, height);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _waveform = null;
        }
        _lastGeometryUpdate = DateTime.MinValue;
    }

    private static Geometry GenerateGeometry(IReadOnlyList<(float minL, float maxL, float minR, float maxR)> peaks, double width, double height)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var mid = height / 2.0;
            var step = peaks.Count > 1 ? width / (peaks.Count - 1) : width;

            for (var i = 0; i < peaks.Count; i++)
            {
                var peak = peaks[i];
                var min = Math.Min(peak.minL, peak.minR);
                var max = Math.Max(peak.maxL, peak.maxR);

                // Clamp values to valid range
                min = Math.Clamp(min, -1f, 1f);
                max = Math.Clamp(max, -1f, 1f);

                var x = i * step;
                var y1 = mid - (max * mid);  // Top of line
                var y2 = mid - (min * mid);  // Bottom of line

                context.BeginFigure(new Point(x, y1), false, false);
                context.LineTo(new Point(x, y2), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
