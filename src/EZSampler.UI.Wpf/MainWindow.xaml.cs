using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EZSampler.Core.Capture;
using EZSampler.UI.Wpf.Controls;
using Microsoft.VisualBasic;
using NAudio.Wave;

namespace EZSampler.UI.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly CaptureService _captureService = new();
    // Lukitus, kun luetaan/piirretään waveform-dataa taustasäikeiltä.
    private readonly object _waveformGate = new();
    // Waveformin huippuarvot piirtoa varten.
    private WaveformAggregator? _waveform;
    private DateTime _lastWaveformUiUpdate = DateTime.MinValue;
    private const int MaxPeaks = 900;
    private CaptureStatus _lastStatus = new(CaptureState.Stopped);
    private string? _recordingFileName;
    private readonly ObservableCollection<RecordingItem> _recordings = new();
    private readonly string _recordingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "EZSampler Recordings");
    
    // Audio bufferit tallennusta varten
    private readonly List<byte[]> _audioBuffers = new();
    private readonly object _bufferGate = new();
    private WaveFormat? _captureFormat;
    
    // Playback
    private WaveOutEvent? _waveOut;
    private MemoryStream? _playbackMemoryStream;
    private RawSourceWaveStream? _playbackRawStream;
    private readonly DispatcherTimer _playbackTimer;
    private TimeSpan _totalDuration;
    private readonly Stopwatch _playbackStopwatch = new();
    private TimeSpan _playbackSeekOffset = TimeSpan.Zero;

    public MainWindow()
    {
        InitializeComponent();

        // Luo playback timer
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)  // ~60 FPS
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        // Kytke palvelun tapahtumat ja ikkunan elinkaari.
        _captureService.StatusChanged += CaptureService_StatusChanged;
        _captureService.Faulted += CaptureService_Faulted;
        _captureService.AudioChunk += CaptureService_AudioChunk;
        WaveformView.Canvas.SizeChanged += WaveformCanvas_SizeChanged;
        Closed += OnClosed;

        RecordingsPanel.Recordings = _recordings;
        Loaded += OnLoaded;

        // Kytke komponenttien tapahtumat
        TransportControls.StartClicked += TransportControls_StartClicked;
        TransportControls.StopClicked += TransportControls_StopClicked;
        TransportControls.PlayClicked += TransportControls_PlayClicked;

        WaveformView.ClearClicked += WaveformView_ClearClicked;
        WaveformView.SaveClicked += WaveformView_SaveClicked;
        WaveformView.PositionClicked += WaveformView_PositionClicked;

        RecordingsPanel.RefreshClicked += RecordingsPanel_RefreshClicked;
        RecordingsPanel.DeleteClicked += RecordingsPanel_DeleteClicked;
        RecordingsPanel.RenameClicked += RecordingsPanel_RenameClicked;
        RecordingsPanel.RecordingKeyDown += RecordingsPanel_RecordingKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureRecordingsFolder();
        RecordingsPanel.FolderPath = _recordingsFolder;
        LoadRecordings();
    }

    private async void TransportControls_StartClicked(object? sender, EventArgs e)
    {
        // Käynnistä kaappaus ilman automaattista tallennusta.
        TransportControls.StartButtonEnabled = false;
        TransportControls.StopButtonEnabled = true;
        CaptureStatusPanel.SetDetails("");
        WaveformView.Clear();
        lock (_waveformGate)
        {
            _waveform = null;
        }
        
        // Tyhjennä audio bufferit
        lock (_bufferGate)
        {
            _audioBuffers.Clear();
            _captureFormat = null;
        }

        await _captureService.StartAsync(new CaptureOptions(EnableFileRecording: false)).ConfigureAwait(true);
    }

    private async void TransportControls_StopClicked(object? sender, EventArgs e)
    {
        // Pysäytä kaappaus.
        TransportControls.StopButtonEnabled = false;

        await _captureService.StopAsync().ConfigureAwait(true);
    }

    private void CaptureService_StatusChanged(object? sender, CaptureStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            _lastStatus = status;
            CaptureStatusPanel.SetStatus(status.State.ToString());

            UpdateDetailsText(status);

            // Luodaan aggregator, kun formaatti selviää.
            if (status.State == CaptureState.Capturing && _waveform == null && status.SampleRate > 0 && status.Channels > 0)
            {
                lock (_waveformGate)
                {
                    _waveform = new WaveformAggregator(
                        WaveFormat.CreateIeeeFloatWaveFormat(status.SampleRate, status.Channels));
                }
            }

            TransportControls.StartButtonEnabled = status.State is CaptureState.Stopped or CaptureState.Faulted;
            TransportControls.StopButtonEnabled = status.State is CaptureState.Capturing or CaptureState.Starting;
            
            // Save-, Clear- ja Play-napit käytössä kun pysäytetty JA on audiodataa
            bool hasAudioData;
            lock (_bufferGate)
            {
                hasAudioData = _audioBuffers.Count > 0 && _captureFormat != null;
            }
            bool isPlaying = _waveOut?.PlaybackState == PlaybackState.Playing;
            WaveformView.SaveButtonEnabled = status.State == CaptureState.Stopped && hasAudioData && !isPlaying;
            WaveformView.ClearButtonEnabled = status.State == CaptureState.Stopped && hasAudioData && !isPlaying;
            TransportControls.PlayButtonEnabled = status.State == CaptureState.Stopped && hasAudioData;

            if (status.State == CaptureState.Stopped)
            {
                _recordingFileName = null;
                LoadRecordings();
            }
        });
    }

    private void CaptureService_Faulted(object? sender, Exception ex)
    {
        Dispatcher.Invoke(() =>
        {
            // Näytä virhe UI:ssa.
            CaptureStatusPanel.SetStatus(CaptureState.Faulted.ToString());
            CaptureStatusPanel.SetDetails(ex.Message);
            TransportControls.StartButtonEnabled = true;
            TransportControls.StopButtonEnabled = false;
            WaveformView.SaveButtonEnabled = false;
            WaveformView.ClearButtonEnabled = false;
            TransportControls.PlayButtonEnabled = false;
            _recordingFileName = null;
        });
    }

    private async void WaveformView_SaveClicked(object? sender, EventArgs e)
    {
        // Tallenna kaapattu audio tiedostoon.
        List<byte[]> buffers;
        WaveFormat? format;
        
        lock (_bufferGate)
        {
            if (_audioBuffers.Count == 0 || _captureFormat == null)
            {
                return;
            }
            
            buffers = new List<byte[]>(_audioBuffers);
            format = _captureFormat;
        }

        var outputPath = BuildRecordingPath();
        
        try
        {
            WaveformView.SaveButtonEnabled = false;
            WaveformView.ClearButtonEnabled = false;
            CaptureStatusPanel.SetDetails("Tallennetaan...");
            
            await Task.Run(() =>
            {
                using var writer = new WaveFileWriter(outputPath, format);
                foreach (var buffer in buffers)
                {
                    writer.Write(buffer, 0, buffer.Length);
                }
            }).ConfigureAwait(true);
            
            // Tyhjennä bufferit tallennuksen jälkeen
            lock (_bufferGate)
            {
                _audioBuffers.Clear();
                _captureFormat = null;
            }
            
            CaptureStatusPanel.SetDetails($"Tallennettu: {Path.GetFileName(outputPath)}");
            LoadRecordings();
        }
        catch (Exception ex)
        {
            CaptureStatusPanel.SetDetails($"Virhe tallennuksessa: {ex.Message}");
            MessageBox.Show(ex.Message, "Tallennus epäonnistui", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Palauta napit käyttöön virheen sattuessa
            lock (_bufferGate)
            {
                bool hasData = _audioBuffers.Count > 0 && _captureFormat != null;
                WaveformView.SaveButtonEnabled = hasData;
                WaveformView.ClearButtonEnabled = hasData;
            }
        }
    }
    
    private void WaveformView_ClearClicked(object? sender, EventArgs e)
    {
        // Pysäytä playback jos käynnissä
        if (_waveOut != null)
        {
            _playbackStopwatch.Stop();
            _playbackStopwatch.Reset();
            _playbackSeekOffset = TimeSpan.Zero;
            _playbackTimer.Stop();
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
            _playbackMemoryStream?.Dispose();
            _playbackMemoryStream = null;
            _playbackRawStream?.Dispose();
            _playbackRawStream = null;
            TransportControls.SetPlayButtonText("▶ Play");
        }
        
        // Tyhjennä audiobufferit ilman tallennusta.
        lock (_bufferGate)
        {
            _audioBuffers.Clear();
            _captureFormat = null;
        }
        
        // Tyhjennä myös waveform
        lock (_waveformGate)
        {_waveform = null;}
        WaveformView.Clear();
        
        WaveformView.SaveButtonEnabled = false;
        WaveformView.ClearButtonEnabled = false;
        TransportControls.PlayButtonEnabled = false;
        CaptureStatusPanel.SetDetails("Recording cleared");
    }

    private void UpdateDetailsText(CaptureStatus status)
    {
        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            CaptureStatusPanel.SetDetails(status.LastError);
            return;
        }

        if (status.State == CaptureState.Capturing)
        {
            var details = $"SampleRate: {status.SampleRate}, Channels: {status.Channels}";
            if (_captureService.IsRecording && !string.IsNullOrWhiteSpace(_recordingFileName))
            {
                details = $"{details}, Recording: {_recordingFileName}";
            }

            CaptureStatusPanel.SetDetails(details);
            return;
        }

        CaptureStatusPanel.SetDetails("");
    }

    private void CaptureService_AudioChunk(object? sender, ReadOnlyMemory<byte> buffer)
    {
        var bytes = buffer.ToArray();
        
        // Tallenna bufferit muistiin myöhempää tallennusta varten
        lock (_bufferGate)
        {
            if (_captureFormat == null && _lastStatus.SampleRate > 0 && _lastStatus.Channels > 0)
            {
                _captureFormat = WaveFormat.CreateIeeeFloatWaveFormat(_lastStatus.SampleRate, _lastStatus.Channels);
            }
            
            _audioBuffers.Add(bytes);
        }
        
        // Päivitä waveform-data ja rajoita piirtotahti.
        WaveformAggregator? aggregator;
        lock (_waveformGate)
        {
            aggregator = _waveform;
        }

        if (aggregator == null)
        {
            return;
        }

        aggregator.Process(bytes, 0, bytes.Length);

        if ((DateTime.UtcNow - _lastWaveformUiUpdate).TotalMilliseconds < 60)
        {
            return;
        }

        _lastWaveformUiUpdate = DateTime.UtcNow;
        Dispatcher.Invoke(RedrawWaveform);
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawWaveform();
    }

    private async void TransportControls_PlayClicked(object? sender, EventArgs e)
    {
        // Jos jo soitetaan, tauko
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            // Tallenna nykyinen aika offsettiin
            _playbackSeekOffset += _playbackStopwatch.Elapsed;
            _playbackStopwatch.Stop();
            _playbackTimer.Stop();
            TransportControls.SetPlayButtonText("▶ Play");
            CaptureStatusPanel.SetDetails("Playback paused");
            return;
        }
        
        // Jos tauolla, jatka
        if (_waveOut?.PlaybackState == PlaybackState.Paused)
        {
            _waveOut.Play();
            _playbackStopwatch.Restart();
            _playbackTimer.Start();
            TransportControls.SetPlayButtonText("⏸ Pause");
            CaptureStatusPanel.SetDetails("Playing...");
            return;
        }
        
        // Muuten aloita alusta
        List<byte[]> buffers;
        WaveFormat? format;
        
        lock (_bufferGate)
        {
            if (_audioBuffers.Count == 0 || _captureFormat == null)
            {
                return;
            }
            
            buffers = new List<byte[]>(_audioBuffers);
            format = _captureFormat;
        }
        
        try
        {
            // Yhdistä bufferit yhdeksi byte-arrayiksi
            var totalLength = buffers.Sum(b => b.Length);
            var fullBuffer = new byte[totalLength];
            var offset = 0;
            foreach (var buffer in buffers)
            {
                Buffer.BlockCopy(buffer, 0, fullBuffer, offset, buffer.Length);
                offset += buffer.Length;
            }
            
            // Luo stream muistista
            _playbackMemoryStream = new MemoryStream(fullBuffer);
            _playbackRawStream = new RawSourceWaveStream(_playbackMemoryStream, format);
            _totalDuration = TimeSpan.FromSeconds((double)fullBuffer.Length / format.AverageBytesPerSecond);
            
            // Luo toisto-objekti
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_playbackRawStream);
            _waveOut.PlaybackStopped += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _playbackStopwatch.Stop();
                    _playbackStopwatch.Reset();
                    _playbackSeekOffset = TimeSpan.Zero;
                    _playbackTimer.Stop();
                    WaveformView.HidePlaybackPosition();
                    _playbackMemoryStream?.Dispose();
                    _playbackMemoryStream = null;
                    _playbackRawStream?.Dispose();
                    _playbackRawStream = null;
                    _waveOut?.Dispose();
                    _waveOut = null;
                    TransportControls.PlayButtonEnabled = true;
                    TransportControls.SetPlayButtonText("▶ Play");
                    WaveformView.SaveButtonEnabled = true;
                    WaveformView.ClearButtonEnabled = true;
                    CaptureStatusPanel.SetDetails("Playing finished");
                });
            };
            
            _waveOut.Play();
            _playbackSeekOffset = TimeSpan.Zero;
            _playbackStopwatch.Restart();
            _playbackTimer.Start();
            TransportControls.PlayButtonEnabled = true;
            TransportControls.SetPlayButtonText("⏸ Pause");
            WaveformView.SaveButtonEnabled = false;
            WaveformView.ClearButtonEnabled = false;
            CaptureStatusPanel.SetDetails("Playing...");
        }
        catch (Exception ex)
        {
            CaptureStatusPanel.SetDetails($"Error during playback: {ex.Message}");
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (_waveOut == null || _totalDuration.TotalSeconds == 0)
        {
            return;
        }

        // Käytä Stopwatch-aikaa + seek-offsettia Position:n sijaan sujuvampaan liikkeeseen
        var elapsed = _playbackStopwatch.Elapsed + _playbackSeekOffset;
        if (elapsed > _totalDuration)
        {
            elapsed = _totalDuration;
        }
        
        var normalizedPosition = elapsed.TotalSeconds / _totalDuration.TotalSeconds;
        WaveformView.SetPlaybackPosition(normalizedPosition);
    }

    private void WaveformView_PositionClicked(object? sender, double normalizedPosition)
    {
        if (_playbackRawStream == null || _totalDuration.TotalSeconds == 0)
        {
            return;
        }

        SeekToPosition(normalizedPosition);
    }

    private void SeekToPosition(double normalizedPosition)
    {
        if (_playbackRawStream == null || _captureFormat == null)
        {
            return;
        }

        // Laske uusi positio byteissä
        var targetSeconds = normalizedPosition * _totalDuration.TotalSeconds;
        var targetBytes = (long)(targetSeconds * _captureFormat.AverageBytesPerSecond);
        
        // Varmista että positio on block-aligned
        var blockAlign = _captureFormat.BlockAlign;
        targetBytes = (targetBytes / blockAlign) * blockAlign;
        
        // Aseta stream position
        _playbackRawStream.Position = Math.Max(0, Math.Min(targetBytes, _playbackRawStream.Length));
        
        // Päivitä seek-offset ja nollaa Stopwatch
        _playbackSeekOffset = TimeSpan.FromSeconds(targetSeconds);
        _playbackStopwatch.Restart();
        if (_waveOut?.PlaybackState != PlaybackState.Playing)
        {
            _playbackStopwatch.Stop();
        }
        
        // Päivitä UI
        WaveformView.SetPlaybackPosition(normalizedPosition);
    }
    
    private void RecordingsPanel_RefreshClicked(object? sender, EventArgs e)
    {
        LoadRecordings();
    }

    private void RecordingsPanel_DeleteClicked(object? sender, EventArgs e)
    {
        DeleteSelectedRecordings();
    }

    private void RecordingsPanel_RenameClicked(object? sender, EventArgs e)
    {
        RenameSelectedRecording();
    }
    
    private void DeleteSelectedRecordings()
    {
        var selectedItems = RecordingsPanel.SelectedRecordings.Cast<RecordingItem>().ToList();
        
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Select a recording to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var message = selectedItems.Count == 1
            ? $"Are you sure you want to delete the recording '{selectedItems[0].Name}'?"
            : $"Are you sure you want to delete {selectedItems.Count} recordings?";
            
        var result = MessageBox.Show(
            message, 
            "Confirm Delete", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var deletedCount = 0;
        var errors = new List<string>();
        
        foreach (var item in selectedItems)
        {
            try
            {
                File.Delete(item.FullPath);
                deletedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Name}: {ex.Message}");
            }
        }
        
        LoadRecordings();
        
        if (errors.Count == 0)
        {
            CaptureStatusPanel.SetDetails(deletedCount == 1
                ? $"Deleted: {selectedItems[0].Name}"
                : $"Deleted {deletedCount} recordings");
        }
        else
        {
            var errorMessage = string.Join("\n", errors);
            MessageBox.Show($"Deleted {deletedCount} recordings.\n\nErrors:\n{errorMessage}", "Partial Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            CaptureStatusPanel.SetDetails($"Deleted {deletedCount}/{selectedItems.Count} recordings");
        }
    }

    private void RenameSelectedRecording()
    {
        var selectedItem = RecordingsPanel.SelectedRecording as RecordingItem;
        
        if (selectedItem == null)
        {
            MessageBox.Show("Select a recording to rename.", "Rename", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Window
        {
            Title = "Rename Recording",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var stack = new System.Windows.Controls.StackPanel 
        { 
            Margin = new Thickness(20) 
        };

        var label = new System.Windows.Controls.TextBlock 
        { 
            Text = "New name:",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var textBox = new System.Windows.Controls.TextBox 
        { 
            Text = Path.GetFileNameWithoutExtension(selectedItem.Name),
            Margin = new Thickness(0, 0, 0, 12)
        };
        textBox.SelectAll();

        var buttonPanel = new System.Windows.Controls.StackPanel 
        { 
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var okButton = new System.Windows.Controls.Button 
        { 
            Content = "OK",
            Width = 80,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };

        var cancelButton = new System.Windows.Controls.Button 
        { 
            Content = "Cancel",
            Width = 80,
            Height = 32,
            IsCancel = true
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        
        stack.Children.Add(label);
        stack.Children.Add(textBox);
        stack.Children.Add(buttonPanel);
        
        dialog.Content = stack;
        dialog.Loaded += (s, e) => textBox.Focus();

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newName = textBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("Name cannot be empty.", "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var extension = Path.GetExtension(selectedItem.Name);
        var newFileName = newName + extension;
        var newPath = Path.Combine(_recordingsFolder, newFileName);

        if (File.Exists(newPath))
        {
            MessageBox.Show("A file with that name already exists.", "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            File.Move(selectedItem.FullPath, newPath);
            LoadRecordings();
            CaptureStatusPanel.SetDetails($"Renamed to: {newFileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to rename: {ex.Message}", "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RecordingsPanel_RecordingKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteSelectedRecordings();
            e.Handled = true;
        }
    }


    private void RedrawWaveform()
    {
        // Piirrä jokaiselle huipulle pystyviiva.
        WaveformAggregator? aggregator;
        lock (_waveformGate)
        {
            aggregator = _waveform;
        }

        if (aggregator == null)
        {
            WaveformView.WaveformPathElement.Data = null;
            return;
        }

        var width = WaveformView.Canvas.ActualWidth;
        var height = WaveformView.Canvas.ActualHeight;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        List<(float minL, float maxL, float minR, float maxR)> peaks;
        lock (_waveformGate)
        {
            if (aggregator.Peaks.Count > MaxPeaks)
            {
                aggregator.Peaks.RemoveRange(0, aggregator.Peaks.Count - MaxPeaks);
            }

            peaks = aggregator.Peaks.ToList();
        }

        if (peaks.Count == 0)
        {
            WaveformView.WaveformPathElement.Data = null;
            return;
        }

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

                min = Math.Clamp(min, -1f, 1f);
                max = Math.Clamp(max, -1f, 1f);

                var x = i * step;
                var y1 = mid - (max * mid);
                var y2 = mid - (min * mid);

                context.BeginFigure(new Point(x, y1), false, false);
                context.LineTo(new Point(x, y2), true, false);
            }
        }

        geometry.Freeze();
        WaveformView.WaveformPathElement.Data = geometry;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        await _captureService.StopAsync().ConfigureAwait(true);
        await _captureService.DisposeAsync().ConfigureAwait(true);
    }

    private void LoadRecordings()
    {
        EnsureRecordingsFolder();
        _recordings.Clear();

        var files = Directory.EnumerateFiles(_recordingsFolder, "*.wav")
            .OrderByDescending(File.GetCreationTimeUtc);

        foreach (var file in files)
        {
            if (!TryGetDuration(file, out var duration))
            {
                continue;
            }

            _recordings.Add(new RecordingItem
            {
                Name = Path.GetFileName(file),
                DurationText = FormatDuration(duration),
                FullPath = file
            });
        }
    }

    private void EnsureRecordingsFolder()
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

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private string BuildRecordingPath()
    {
        EnsureRecordingsFolder();
        var fileName = $"ezsampler_{DateTime.Now:yyyyMMdd_HHmmss}.wav";

        var fullPath = Path.Combine(_recordingsFolder, fileName);
        return EnsureUniquePath(fullPath);
    }

    private static string SanitizeFileName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {return string.Empty;}

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(input.Trim().Where(ch => !invalid.Contains(ch)).ToArray());
        return cleaned;
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
        {return path;}

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        var index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name}_{index}{extension}");
            index++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    private sealed class RecordingItem
    {
        public string Name { get; init; } = string.Empty;
        public string DurationText { get; init; } = "--:--";
        public string FullPath { get; init; } = string.Empty;
    }
}
