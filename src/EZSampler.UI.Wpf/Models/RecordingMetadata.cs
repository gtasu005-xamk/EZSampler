using System;
using System.IO;
using EZSampler.UI.Wpf.Extensions;

namespace EZSampler.UI.Wpf.Models;

/// <summary>
/// Represents metadata for a recorded audio file.
/// </summary>
public sealed class RecordingMetadata
{
    /// <summary>
    /// Gets the file name including extension (e.g., "ezsampler_20260221_140530.wav")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name without extension.
    /// </summary>
    public string DisplayName => Path.GetFileNameWithoutExtension(Name);

    /// <summary>
    /// Gets the duration of the recording.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the formatted duration string (e.g., "01:23:45" or "1:23").
    /// </summary>
    public string DurationText => Duration.ToFormattedString();

    /// <summary>
    /// Gets the creation time of the file.
    /// </summary>
    public DateTime CreatedTime { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the full path to the file.
    /// </summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a human-readable file size (e.g., "1.5 MB").
    /// </summary>
    public string FileSizeText => FormatFileSize(FileSizeBytes);

    /// <summary>
    /// Gets a human-readable creation time.
    /// </summary>
    public string CreatedText => CreatedTime.ToString("g");

    private static string FormatFileSize(long bytes)
    {
        const int scale = 1024;
        var units = new[] { "B", "KB", "MB", "GB" };
        var scaled = (double)bytes;
        var i = 0;

        while (scaled >= scale && i < units.Length - 1)
        {
            scaled /= scale;
            i++;
        }

        return $"{scaled:F1} {units[i]}";
    }
}

/// <summary>
/// Data transfer object for UI binding (e.g., ListView).
/// </summary>
public sealed record RecordingItemViewModel(
    string Name,
    string DurationText,
    string FullPath);
