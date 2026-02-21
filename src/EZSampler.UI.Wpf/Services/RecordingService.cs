using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using EZSampler.UI.Wpf.Configuration;
using EZSampler.UI.Wpf.Extensions;
using EZSampler.UI.Wpf.Models;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Manages recording file operations including load, save, delete, and rename.
/// </summary>
public sealed class RecordingService : IRecordingService
{
    private readonly string _recordingsFolder;
    
    public event EventHandler<IReadOnlyList<RecordingMetadata>>? RecordingsChanged;

    public string RecordingsFolder => _recordingsFolder;

    public RecordingService(string? customFolder = null)
    {
        _recordingsFolder = customFolder ?? AppConstants.Folders.RecordingsFolder;
    }

    public IReadOnlyList<RecordingMetadata> LoadRecordings()
    {
        EnsureRecordingsFolder();
        var recordings = new List<RecordingMetadata>();

        try
        {
            var files = Directory.EnumerateFiles(_recordingsFolder, "*.wav")
                .OrderByDescending(File.GetCreationTimeUtc);

            foreach (var file in files)
            {
                if (TryGetDuration(file, out var duration))
                {
                    var fileInfo = new FileInfo(file);
                    recordings.Add(new RecordingMetadata
                    {
                        Name = Path.GetFileName(file),
                        Duration = duration,
                        CreatedTime = fileInfo.CreationTimeUtc,
                        FileSizeBytes = fileInfo.Length,
                        FullPath = file
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading recordings: {ex.Message}");
        }

        RecordingsChanged?.Invoke(this, recordings.AsReadOnly());
        return recordings.AsReadOnly();
    }

    public async Task SaveRecordingAsync(byte[] audioBuffer, WaveFormat format, string? customName = null, CancellationToken ct = default)
    {
        if (audioBuffer == null || audioBuffer.Length == 0)
        {
            throw new ArgumentException("Audio buffer cannot be empty", nameof(audioBuffer));
        }

        var fileName = customName ?? AppConstants.FileNaming.GetRecordingFileName();
        var outputPath = Path.Combine(_recordingsFolder, fileName).EnsureUniquePath();

        await Task.Run(() =>
        {
            EnsureRecordingsFolder();
            using var writer = new WaveFileWriter(outputPath, format);
            writer.Write(audioBuffer, 0, audioBuffer.Length);
        }, ct);

        // Reload recordings after save
        _ = LoadRecordings();
    }

    public async Task DeleteRecordingAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        await Task.Run(() =>
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }, ct);

        // Reload recordings after deletion
        _ = LoadRecordings();
    }

    public async Task RenameRecordingAsync(string filePath, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New name cannot be empty", nameof(newName));
        }

        await Task.Run(() =>
        {
            var sanitizedName = newName.SanitizeFileName();
            var extension = Path.GetExtension(filePath);
            var newFileName = sanitizedName + extension;
            var newPath = Path.Combine(_recordingsFolder, newFileName);

            if (File.Exists(newPath) && !newPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A file with that name already exists.");
            }

            if (File.Exists(filePath))
            {
                File.Move(filePath, newPath, overwrite: true);
            }
        }, ct);

        // Reload recordings after rename
        _ = LoadRecordings();
    }

    public void EnsureRecordingsFolder()
    {
        if (!Directory.Exists(_recordingsFolder))
        {
            Directory.CreateDirectory(_recordingsFolder);
        }
    }

    private static bool TryGetDuration(string filePath, out TimeSpan duration)
    {
        try
        {
            using var reader = new AudioFileReader(filePath);
            duration = reader.TotalTime;
            return true;
        }
        catch
        {
            duration = TimeSpan.Zero;
            return false;
        }
    }
}
