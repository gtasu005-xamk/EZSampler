using System.Windows.Media;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Defines the contract for waveform rendering and audio visualization.
/// Handles audio data aggregation and geometry generation for UI display.
/// </summary>
public interface IWaveformRenderingService
{
    /// <summary>
    /// Raised when the waveform geometry is updated and ready for rendering.
    /// </summary>
    event EventHandler<Geometry>? GeometryUpdated;

    /// <summary>
    /// Initializes the waveform aggregator with audio format information.
    /// </summary>
    void InitializeAggregator(NAudio.Wave.WaveFormat format);

    /// <summary>
    /// Processes raw audio buffer and updates peak data.
    /// </summary>
    void ProcessAudioChunk(ReadOnlyMemory<byte> buffer);

    /// <summary>
    /// Generates a StreamGeometry for rendering waveform at specified dimensions.
    /// </summary>
    Geometry? GetGeometry(double width, double height);

    /// <summary>
    /// Clears all waveform data.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the number of peaks currently stored.
    /// </summary>
    int PeakCount { get; }
}
