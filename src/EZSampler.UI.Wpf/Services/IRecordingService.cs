using System.Collections.ObjectModel;
using EZSampler.UI.Wpf.Models;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Defines the contract for recording file management operations.
/// Handles loading, saving, deleting, and renaming recordings.
/// </summary>
public interface IRecordingService
{
    /// <summary>
    /// Raised when the list of recordings changes.
    /// </summary>
    event EventHandler<IReadOnlyList<RecordingMetadata>>? RecordingsChanged;

    /// <summary>
    /// Gets the current recordings folder path.
    /// </summary>
    string RecordingsFolder { get; }

    /// <summary>
    /// Loads all recordings from the recordings folder and returns metadata.
    /// </summary>
    IReadOnlyList<RecordingMetadata> LoadRecordings();

    /// <summary>
    /// Saves audio buffer to a WAV file with optional custom name.
    /// </summary>
    Task SaveRecordingAsync(byte[] audioBuffer, NAudio.Wave.WaveFormat format, string? customName = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a recording file by path.
    /// </summary>
    Task DeleteRecordingAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Renames a recording file.
    /// </summary>
    Task RenameRecordingAsync(string filePath, string newName, CancellationToken ct = default);

    /// <summary>
    /// Ensures the recordings folder exists, creating it if necessary.
    /// </summary>
    void EnsureRecordingsFolder();
}
