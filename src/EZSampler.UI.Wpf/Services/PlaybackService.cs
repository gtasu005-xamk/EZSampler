using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using NAudio.Wave;
using EZSampler.UI.Wpf.Configuration;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Manages audio playback operations using NAudio.
/// Handles play, pause, stop, and seeking functionality.
/// </summary>
public sealed class PlaybackService : IPlaybackService
{
    private WaveOutEvent? _waveOut;
    private MemoryStream? _memoryStream;
    private RawSourceWaveStream? _rawStream;
    private readonly DispatcherTimer _positionTimer;
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _seekOffset = TimeSpan.Zero;
    private TimeSpan _totalDuration = TimeSpan.Zero;
    private WaveFormat? _currentFormat;
    private bool _disposed = false;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackFinished;

    public PlaybackState State => _waveOut?.PlaybackState ?? PlaybackState.Stopped;
    public TimeSpan CurrentPosition => _stopwatch.Elapsed + _seekOffset;
    public TimeSpan TotalDuration => _totalDuration;

    public PlaybackService()
    {
        _positionTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(AppConstants.PlaybackTimerIntervalMs)
        };
        _positionTimer.Tick += PositionTimer_Tick;
    }

    public async Task InitializeAsync(byte[] buffer, WaveFormat format, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        
        await Task.Run(() => Initialize(buffer, format), ct);
    }

    private void Initialize(byte[] buffer, WaveFormat format)
    {
        Cleanup();
        _currentFormat = format;
        _memoryStream = new MemoryStream(buffer);
        _rawStream = new RawSourceWaveStream(_memoryStream, format);
        _totalDuration = TimeSpan.FromSeconds((double)buffer.Length / format.AverageBytesPerSecond);
        _seekOffset = TimeSpan.Zero;
    }

    public void Play()
    {
        ThrowIfDisposed();

        if (_waveOut == null && _rawStream != null)
        {
            // First time playing - initialize WaveOut
            InitializeWaveOut();
        }

        if (_waveOut?.PlaybackState == PlaybackState.Paused)
        {
            _waveOut.Play();
            _stopwatch.Start();
            _positionTimer.Start();
        }
        else if (_waveOut?.PlaybackState == PlaybackState.Stopped)
        {
            _waveOut?.Play();
            _stopwatch.Restart();
            _seekOffset = TimeSpan.Zero;
            _positionTimer.Start();
        }

        StateChanged?.Invoke(this, State);
    }

    public void Pause()
    {
        ThrowIfDisposed();

        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            _stopwatch.Stop();
            _seekOffset += _stopwatch.Elapsed;
            _positionTimer.Stop();
            StateChanged?.Invoke(this, State);
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();

        if (_waveOut != null)
        {
            _waveOut.Stop();
            _stopwatch.Stop();
            _stopwatch.Reset();
            _seekOffset = TimeSpan.Zero;
            _positionTimer.Stop();

            // Reset stream position
            if (_rawStream != null)
            {
                _rawStream.Position = 0;
            }

            StateChanged?.Invoke(this, State);
        }
    }

    public void SeekToPosition(double normalizedPosition)
    {
        ThrowIfDisposed();

        if (_rawStream == null || _currentFormat == null || _totalDuration.TotalSeconds == 0)
        {
            return;
        }

        var targetSeconds = normalizedPosition * _totalDuration.TotalSeconds;
        var targetBytes = (long)(targetSeconds * _currentFormat.AverageBytesPerSecond);

        // Ensure block-aligned position
        var blockAlign = _currentFormat.BlockAlign;
        targetBytes = (targetBytes / blockAlign) * blockAlign;

        _rawStream.Position = Math.Max(0, Math.Min(targetBytes, _rawStream.Length));
        _seekOffset = TimeSpan.FromSeconds(targetSeconds);
        _stopwatch.Restart();

        if (_waveOut?.PlaybackState != PlaybackState.Playing)
        {
            _stopwatch.Stop();
        }
    }

    private void InitializeWaveOut()
    {
        if (_waveOut == null && _rawStream != null)
        {
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_rawStream);
            _waveOut.PlaybackStopped += (s, e) =>
            {
                OnPlaybackFinished();
            };
            StateChanged?.Invoke(this, State);
        }
    }

    private void OnPlaybackFinished()
    {
        _stopwatch.Stop();
        _stopwatch.Reset();
        _seekOffset = TimeSpan.Zero;
        _positionTimer.Stop();
        StateChanged?.Invoke(this, State);
        PlaybackFinished?.Invoke(this, EventArgs.Empty);
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        // Position update feedback for UI
        StateChanged?.Invoke(this, State);
    }

    private void Cleanup()
    {
        _positionTimer.Stop();
        _stopwatch.Stop();
        _stopwatch.Reset();

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _memoryStream?.Dispose();
        _memoryStream = null;

        _rawStream?.Dispose();
        _rawStream = null;

        _seekOffset = TimeSpan.Zero;
        _totalDuration = TimeSpan.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cleanup();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlaybackService));
        }
    }
}
