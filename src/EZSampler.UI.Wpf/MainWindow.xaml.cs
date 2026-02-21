using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using EZSampler.Core.Capture;
using EZSampler.UI.Wpf.Configuration;
using EZSampler.UI.Wpf.Models;
using EZSampler.UI.Wpf.Services;
using NAudio.Wave;

namespace EZSampler.UI.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// 
/// REFACTORED: This window now acts as a coordinator that delegates
/// to specialized services for playback, recording, waveform rendering, etc.
/// This keeps the code-behind focused on UI-related wiring only.
/// </summary>
public partial class MainWindow : Window
{
    // ============================================================
    // CAPTURE & AUDIO CAPTURE
    // ============================================================
    
    private readonly CaptureService _captureService = new();
    private CaptureStatus _lastStatus = new(CaptureState.Stopped);
    
    // Audio buffer for current capture session
    private readonly System.Collections.Generic.List<byte[]> _audioBuffers = new();
    private readonly object _bufferGate = new();
    private WaveFormat? _captureFormat;

    // ============================================================
    // SERVICES
    // ============================================================
    
    private IPlaybackService? _playbackService;
    private IRecordingService? _recordingService;
    private IWaveformRenderingService? _waveformService;
    private IDialogService? _dialogService;
    private readonly IStatusService _statusService = new StatusService();

    // ============================================================
    // OBSERVABLE COLLECTIONS FOR UI BINDING
    // ============================================================
    
    private readonly ObservableCollection<RecordingItemViewModel> _recordingItems = new();


    public MainWindow()
    {
        InitializeComponent();
        InitializeServices();
        HookupEventHandlers();
    }

    private void InitializeServices()
    {
        _playbackService = new PlaybackService();
        _recordingService = new RecordingService();
        _waveformService = new WaveformRenderingService();
        _dialogService = new DialogService(this);
    }

    private void HookupEventHandlers()
    {
        // Capture service
        _captureService.StatusChanged += CaptureService_StatusChanged;
        _captureService.Faulted += CaptureService_Faulted;
        _captureService.AudioChunk += CaptureService_AudioChunk;

        // UI events
        WaveformView.Canvas.SizeChanged += (s, e) => RedrawWaveform();
        Closed += OnClosed;
        Loaded += OnLoaded;

        // Control events - Transport
        TransportControls.StartClicked += TransportControls_StartClicked;
        TransportControls.StopClicked += TransportControls_StopClicked;
        TransportControls.PlayClicked += TransportControls_PlayClicked;

        // Control events - Waveform
        WaveformView.ClearClicked += WaveformView_ClearClicked;
        WaveformView.SaveClicked += WaveformView_SaveClicked;
        WaveformView.PositionClicked += WaveformView_PositionClicked;

        // Control events - Recordings
        RecordingsPanel.RefreshClicked += RecordingsPanel_RefreshClicked;
        RecordingsPanel.DeleteClicked += RecordingsPanel_DeleteClicked;
        RecordingsPanel.RenameClicked += RecordingsPanel_RenameClicked;
        RecordingsPanel.RecordingKeyDown += RecordingsPanel_RecordingKeyDown;

        // Playback service
        if (_playbackService != null)
        {
            _playbackService.StateChanged += (s, state) => UpdatePlaybackUI(state);
        }

        // Status service
        _statusService.StatusChanged += (s, msg) => CaptureStatusPanel.SetDetails(msg);
    }

    // ============================================================
    // WINDOW LIFECYCLE
    // ============================================================

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_recordingService != null)
        {
            _recordingService.EnsureRecordingsFolder();
            RecordingsPanel.FolderPath = _recordingService.RecordingsFolder;
            LoadRecordings();
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _playbackService?.Dispose();
        await _captureService.StopAsync().ConfigureAwait(true);
        await _captureService.DisposeAsync().ConfigureAwait(true);
    }

    // ============================================================
    // CAPTURE CONTROL
    // ============================================================

    private async void TransportControls_StartClicked(object? sender, EventArgs e)
    {
        TransportControls.StartButtonEnabled = false;
        TransportControls.StopButtonEnabled = true;
        _statusService.Clear();
        WaveformView.Clear();

        // Reset audio buffer
        lock (_bufferGate)
        {
            _audioBuffers.Clear();
            _captureFormat = null;
        }

        // Reset waveform
        _waveformService?.Clear();

        await _captureService.StartAsync(new CaptureOptions(EnableFileRecording: false)).ConfigureAwait(true);
    }

    private async void TransportControls_StopClicked(object? sender, EventArgs e)
    {
        TransportControls.StopButtonEnabled = false;
        await _captureService.StopAsync().ConfigureAwait(true);
    }

    private void CaptureService_StatusChanged(object? sender, CaptureStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            _lastStatus = status;
            CaptureStatusPanel.SetStatus(status.State.ToString());

            // Initialize waveform service when format is known
            if (status.State == CaptureState.Capturing && 
                _waveformService?.PeakCount == 0 && 
                status.SampleRate > 0 && status.Channels > 0)
            {
                _waveformService?.InitializeAggregator(
                    WaveFormat.CreateIeeeFloatWaveFormat(status.SampleRate, status.Channels));
            }

            // Update button states
            TransportControls.StartButtonEnabled = status.State is CaptureState.Stopped or CaptureState.Faulted;
            TransportControls.StopButtonEnabled = status.State is CaptureState.Capturing or CaptureState.Starting;

            UpdateSaveAndPlayButtons();

            if (status.State == CaptureState.Stopped)
            {
                LoadRecordings();
            }
        });
    }

    private void CaptureService_Faulted(object? sender, Exception ex)
    {
        Dispatcher.Invoke(() =>
        {
            CaptureStatusPanel.SetStatus(CaptureState.Faulted.ToString());
            _statusService.SetStatus(ex.Message);
            TransportControls.StartButtonEnabled = true;
            TransportControls.StopButtonEnabled = false;
            WaveformView.SaveButtonEnabled = false;
            WaveformView.ClearButtonEnabled = false;
            TransportControls.PlayButtonEnabled = false;
        });
    }

    private void CaptureService_AudioChunk(object? sender, ReadOnlyMemory<byte> buffer)
    {
        var bytes = buffer.ToArray();

        // Store buffer for later save
        lock (_bufferGate)
        {
            if (_captureFormat == null && _lastStatus.SampleRate > 0 && _lastStatus.Channels > 0)
            {
                _captureFormat = WaveFormat.CreateIeeeFloatWaveFormat(_lastStatus.SampleRate, _lastStatus.Channels);
            }
            _audioBuffers.Add(bytes);
        }

        // Update waveform visualization
        _waveformService?.ProcessAudioChunk(buffer);
        Dispatcher.Invoke(RedrawWaveform);
    }

    // ============================================================
    // PLAYBACK CONTROL
    // ============================================================

    private async void TransportControls_PlayClicked(object? sender, EventArgs e)
    {
        if (_playbackService == null)
        {
            return;
        }

        // Toggle play/pause
        if (_playbackService.State == PlaybackState.Playing)
        {
            _playbackService.Pause();
            TransportControls.SetPlayButtonText(AppConstants.PlayButtonPlay);
            _statusService.SetStatus(AppConstants.Messages.PlaybackPaused);
            return;
        }

        if (_playbackService.State == PlaybackState.Paused)
        {
            _playbackService.Play();
            TransportControls.SetPlayButtonText(AppConstants.PlayButtonPause);
            _statusService.SetStatus(AppConstants.Messages.Playing);
            return;
        }

        // Start new playback
        try
        {
            byte[] buffer;
            WaveFormat? format;

            lock (_bufferGate)
            {
                if (_audioBuffers.Count == 0 || _captureFormat == null)
                {
                    return;
                }

                var totalLength = _audioBuffers.Sum(b => b.Length);
                buffer = new byte[totalLength];
                var offset = 0;
                foreach (var buf in _audioBuffers)
                {
                    Buffer.BlockCopy(buf, 0, buffer, offset, buf.Length);
                    offset += buf.Length;
                }

                format = _captureFormat;
            }

            await _playbackService.InitializeAsync(buffer, format);
            _playbackService.Play();
            TransportControls.PlayButtonEnabled = true;
            TransportControls.SetPlayButtonText(AppConstants.PlayButtonPause);
            WaveformView.SaveButtonEnabled = false;
            WaveformView.ClearButtonEnabled = false;
            _statusService.SetStatus(AppConstants.Messages.Playing);
        }
        catch (Exception ex)
        {
            _statusService.SetStatus($"{AppConstants.Messages.ErrorPlayback} {ex.Message}");
        }
    }

    private void UpdatePlaybackUI(PlaybackState state)
    {
        Dispatcher.Invoke(() =>
        {
            if (_playbackService == null)
            {
                return;
            }

            // Update playback position on waveform
            var normalizedPosition = _playbackService.TotalDuration.TotalSeconds > 0
                ? _playbackService.CurrentPosition.TotalSeconds / _playbackService.TotalDuration.TotalSeconds
                : 0;
            WaveformView.SetPlaybackPosition(Math.Clamp(normalizedPosition, 0, 1));

            // Handle playback finished
            if (state == PlaybackState.Stopped && _playbackService.State == PlaybackState.Stopped)
            {
                WaveformView.HidePlaybackPosition();
                TransportControls.PlayButtonEnabled = true;
                WaveformView.SaveButtonEnabled = true;
                WaveformView.ClearButtonEnabled = true;
                TransportControls.SetPlayButtonText(AppConstants.PlayButtonPlay);
                _statusService.SetStatus(AppConstants.Messages.PlayingFinished);
            }
        });
    }

    private void WaveformView_PositionClicked(object? sender, double normalizedPosition)
    {
        _playbackService?.SeekToPosition(normalizedPosition);
    }

// ============================================================
    // RECORDING MANAGEMENT
    // ============================================================

    private async void WaveformView_SaveClicked(object? sender, EventArgs e)
    {
        if (_recordingService == null || _playbackService?.State == PlaybackState.Playing)
        {
            return;
        }

        byte[] buffer;
        WaveFormat? format;

        lock (_bufferGate)
        {
            if (_audioBuffers.Count == 0 || _captureFormat == null)
            {
                return;
            }

            var totalLength = _audioBuffers.Sum(b => b.Length);
            buffer = new byte[totalLength];
            var offset = 0;
            foreach (var buf in _audioBuffers)
            {
                Buffer.BlockCopy(buf, 0, buffer, offset, buf.Length);
                offset += buf.Length;
            }

            format = _captureFormat;
        }

        try
        {
            WaveformView.SaveButtonEnabled = false;
            WaveformView.ClearButtonEnabled = false;
            _statusService.SetStatus(AppConstants.Messages.Saving);

            await _recordingService.SaveRecordingAsync(buffer, format);

            // Clear buffers after save
            lock (_bufferGate)
            {
                _audioBuffers.Clear();
                _captureFormat = null;
            }

            _statusService.SetStatus(AppConstants.Messages.SavedAs + " " + AppConstants.FileNaming.GetRecordingFileName());
            LoadRecordings();
        }
        catch (Exception ex)
        {
            _statusService.SetStatus($"{AppConstants.Messages.ErrorSaving} {ex.Message}");
            UpdateSaveAndPlayButtons();
        }
    }

    private void WaveformView_ClearClicked(object? sender, EventArgs e)
    {
        // Stop playback if running
        if (_playbackService?.State == PlaybackState.Playing)
        {
            _playbackService.Stop();
            WaveformView.HidePlaybackPosition();
            TransportControls.SetPlayButtonText(AppConstants.PlayButtonPlay);
        }

        // Clear audio buffers
        lock (_bufferGate)
        {
            _audioBuffers.Clear();
            _captureFormat = null;
        }

        // Clear waveform
        _waveformService?.Clear();
        WaveformView.Clear();

        WaveformView.SaveButtonEnabled = false;
        WaveformView.ClearButtonEnabled = false;
        TransportControls.PlayButtonEnabled = false;
        _statusService.SetStatus(AppConstants.Messages.RecordingCleared);
    }

    private async void RecordingsPanel_RefreshClicked(object? sender, EventArgs e)
    {
        LoadRecordings();
    }

    private async void RecordingsPanel_DeleteClicked(object? sender, EventArgs e)
    {
        await DeleteSelectedRecordings();
    }

    private async void RecordingsPanel_RenameClicked(object? sender, EventArgs e)
    {
        await RenameSelectedRecording();
    }

    private void RecordingsPanel_RecordingKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete)
        {
            _ = DeleteSelectedRecordings();
            e.Handled = true;
        }
    }

    private async Task DeleteSelectedRecordings()
    {
        if (_recordingService == null)
        {
            return;
        }

        var selectedItems = RecordingsPanel.SelectedRecordings.Cast<RecordingItemViewModel>().ToList();

        if (selectedItems.Count == 0)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowInfoDialogAsync(
                    AppConstants.Messages.ConfirmDelete,
                    AppConstants.Messages.SelectToDelete);
            }
            return;
        }

        var message = selectedItems.Count == 1
            ? string.Format(AppConstants.Messages.ConfirmDeleteSingle, selectedItems[0].Name)
            : string.Format(AppConstants.Messages.ConfirmDeleteMultiple, selectedItems.Count);

        var confirmed = false;
        if (_dialogService != null)
        {
            confirmed = await _dialogService.ShowConfirmDialogAsync(
                AppConstants.Messages.ConfirmDelete, message);
        }

        if (!confirmed)
        {
            return;
        }

        var deleteCount = 0;
        var errors = new System.Collections.Generic.List<string>();

        foreach (var item in selectedItems)
        {
            try
            {
                await _recordingService.DeleteRecordingAsync(item.FullPath);
                deleteCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Name}: {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            _statusService.SetStatus(deleteCount == 1
                ? string.Format(AppConstants.Messages.DeletedRecording, selectedItems[0].Name)
                : string.Format(AppConstants.Messages.DeletedMultiple, deleteCount));
        }
        else
        {
            var errorMsg = string.Join("\n", errors);
            if (_dialogService != null)
            {
                await _dialogService.ShowErrorDialogAsync(
                    AppConstants.Messages.PartialDeleteFailed,
                    $"Deleted {deleteCount} recordings.\n\nErrors:\n{errorMsg}");
            }
            _statusService.SetStatus($"Deleted {deleteCount}/{selectedItems.Count} recordings");
        }

        LoadRecordings();
    }

    private async Task RenameSelectedRecording()
    {
        if (_recordingService == null)
        {
            return;
        }

        var selectedItem = RecordingsPanel.SelectedRecording as RecordingItemViewModel;

        if (selectedItem == null)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowInfoDialogAsync(
                    "Rename",
                    AppConstants.Messages.SelectToRename);
            }
            return;
        }

        var newName = string.Empty;
        if (_dialogService != null)
        {
            newName = await _dialogService.ShowRenameDialogAsync(selectedItem.Name);
        }

        if (string.IsNullOrEmpty(newName))
        {
            return;
        }

        try
        {
            await _recordingService.RenameRecordingAsync(selectedItem.FullPath, newName);
            _statusService.SetStatus(string.Format(AppConstants.Messages.RenamedTo, newName));
            LoadRecordings();
        }
        catch (Exception ex)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowErrorDialogAsync(
                    AppConstants.Messages.RenameFailed,
                    $"{AppConstants.Messages.ErrorRenaming} {ex.Message}");
            }
        }
    }

    // ============================================================
    // UI UPDATES
    // ============================================================

    private void LoadRecordings()
    {
        if (_recordingService == null)
        {
            return;
        }

        _recordingItems.Clear();
        var recordings = _recordingService.LoadRecordings();

        foreach (var rec in recordings)
        {
            _recordingItems.Add(new RecordingItemViewModel(rec.Name, rec.DurationText, rec.FullPath));
        }

        RecordingsPanel.Recordings = _recordingItems;
    }

    private void RedrawWaveform()
    {
        if (_waveformService?.PeakCount == 0)
        {
            WaveformView.WaveformPathElement.Data = null;
            return;
        }

        var width = WaveformView.Canvas.ActualWidth;
        var height = WaveformView.Canvas.ActualHeight;

        var geometry = _waveformService?.GetGeometry(width, height);
        WaveformView.WaveformPathElement.Data = geometry;
    }

    private void UpdateSaveAndPlayButtons()
    {
        bool hasAudioData;
        lock (_bufferGate)
        {
            hasAudioData = _audioBuffers.Count > 0 && _captureFormat != null;
        }

        bool isPlaying = _playbackService?.State == PlaybackState.Playing;
        bool isStopped = _lastStatus.State == CaptureState.Stopped;

        WaveformView.SaveButtonEnabled = isStopped && hasAudioData && !isPlaying;
        WaveformView.ClearButtonEnabled = isStopped && hasAudioData && !isPlaying;
        TransportControls.PlayButtonEnabled = isStopped && hasAudioData;
    }
}
