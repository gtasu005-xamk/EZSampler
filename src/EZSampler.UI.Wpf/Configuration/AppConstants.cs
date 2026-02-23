using System;
using System.IO;

namespace EZSampler.UI.Wpf.Configuration;

/// <summary>
/// Application-wide constants and configuration values.
/// Centralizes magic strings and numbers for easier maintenance and consistency.
/// </summary>
public static class AppConstants
{
    // ============================================================
    // WAVEFORM & AUDIO VISUALIZATION
    // ============================================================
    
    public const int MaxWaveformPeaks = 900;
    public const int WaveformUpdateIntervalMs = 60;
    public const int PlaybackTimerIntervalMs = 16;  // ~60 FPS

    // ============================================================
    // PLAYBACK CONTROL
    // ============================================================
    
    public const string PlayButtonPlay = "▶ Play";
    public const string PlayButtonPause = "⏸ Pause";
    
    // ============================================================
    // FILE NAMING & PATHS
    // ============================================================
    
    public static class FileNaming
    {
        public const string RecordingPrefix = "ezsampler";
        public const string RecordingExtension = ".wav";
        public const string DateTimeFormat = "yyyyMMdd_HHmmss";
        public const string DefaultRecordingsFolderName = "EZSampler Recordings";
        
        public static string GetRecordingFileName()
        {
            return $"{RecordingPrefix}_{DateTime.Now:yyyyMMdd_HHmmss}{RecordingExtension}";
        }
    }
    
    public static class Folders
    {
        public static string RecordingsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileNaming.DefaultRecordingsFolderName);
    }
    
    // ============================================================
    // USER MESSAGES & DIALOGS
    // ============================================================
    
    public static class Messages
    {
        // Status messages
        public const string RecordingCleared = "Recording cleared";
        public const string PlayingFinished = "Playing finished";
        public const string PlaybackPaused = "Playback paused";
        public const string Playing = "Playing...";
        public const string Saving = "Saving...";
        
        // Confirmation dialogs
        public const string ConfirmDeleteSingle = "Are you sure you want to delete '{0}'?";
        public const string ConfirmDeleteMultiple = "Are you sure you want to delete {0} recordings?";
        public const string ConfirmDelete = "Confirm Delete";
        
        // Button labels
        public const string SelectToDelete = "Select a recording to delete.";
        public const string SelectToRename = "Select a recording to rename.";
        public const string NameCannotBeEmpty = "Name cannot be empty.";
        public const string FileAlreadyExists = "A file with that name already exists.";
        
        // Error messages
        public const string SavingFailed = "Saving Failed";
        public const string RenameFailed = "Rename Error";
        public const string ErrorSaving = "Error saving: {0}";
        public const string ErrorRenaming = "Failed to rename: {0}";
        public const string ErrorPlayback = "Error during playback: {0}";
        
        // Success messages
        public const string PartialDeleteFailed = "Partial Delete Failed";
        public const string RenamedTo = "Renamed to: {0}";
        public const string SavedAs = "Saved as: {0}";
        public const string DeletedRecording = "Deleted: {0}";
        public const string DeletedMultiple = "Deleted {0} recordings";
    }
    
    // ============================================================
    // WINDOW & UI DIMENSIONS
    // ============================================================
    
    public static class Window
    {
        public const int MinWidth = 820;
        public const int MinHeight = 420;
        public const int DefaultWidth = 1000;
        public const int DefaultHeight = 600;
    }
    
    public static class Spacing
    {
        public const int Small = 8;
        public const int Medium = 16;
        public const int Large = 24;
    }
}
