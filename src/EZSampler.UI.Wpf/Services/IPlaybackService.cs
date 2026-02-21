using NAudio.Wave;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Defines the contract for audio playback operations.
/// Separates playback logic from UI code for better testability and reusability.
/// </summary>
public interface IPlaybackService : IDisposable
{
    /// <summary>
    /// Raised when playback state changes (Playing, Paused, Stopped).
    /// </summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Raised when playback finishes naturally.
    /// </summary>
    event EventHandler? PlaybackFinished;

    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Gets the current playback position.
    /// </summary>
    TimeSpan CurrentPosition { get; }

    /// <summary>
    /// Gets the total duration of the loaded audio.
    /// </summary>
    TimeSpan TotalDuration { get; }

    /// <summary>
    /// Initializes playback with audio buffer and format information.
    /// </summary>
    Task InitializeAsync(byte[] buffer, WaveFormat format, CancellationToken ct = default);

    /// <summary>
    /// Starts or resumes playback.
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses playback (can be resumed).
    /// </summary>
    void Pause();

    /// <summary>
    /// Stops playback and resets to the beginning.
    /// </summary>
    void Stop();

    /// <summary>
    /// Seeks to a position in the playback (0.0 = start, 1.0 = end).
    /// </summary>
    void SeekToPosition(double normalizedPosition);
}
