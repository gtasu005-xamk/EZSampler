using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
namespace EZSampler.Core.Capture;

public enum CaptureState
{
    Stopped = 0,
    Starting = 1,
    Capturing = 2,
    Stopping = 3,
    Faulted = 4
}

public sealed record CaptureStatus(
    CaptureState State,
    string? DeviceName = null,
    int SampleRate = 0,
    int Channels = 0,
    string? LastError = null
);

public sealed record CaptureOptions(
    int? DeviceId = null,
    int? SampleRate = null,
    int? Channels = null,
    bool EnableFileRecording = false,
    string? OutputPath = null
    // lisää tähän myöhemmin: buffer size, output format, loopback on/off, jne.
);

public sealed class CaptureService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly object _writerGate = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private WaveFileWriter? _writer;
    private string? _recordingPath;
    private WaveFormat? _currentFormat;

    private CaptureStatus _status = new(CaptureState.Stopped);
    public CaptureStatus Status
    {
        get { lock (_gate) return _status; }
        private set
        {
            lock (_gate) _status = value;
            StatusChanged?.Invoke(this, value);
        }
    }

    // UI/Console voi kuunnella näitä:
    public event EventHandler<CaptureStatus>? StatusChanged;
    public event EventHandler<Exception>? Faulted;

    // Jos haluat streamata audiobufereita ulos:
    // (Phase 0:ssa tämän voi olla vielä tyhjä / ei käytössä)
    public event EventHandler<ReadOnlyMemory<byte>>? AudioChunk; // vai float[] / short[] riippuen nykyisestä toteutuksesta

    public bool IsRunning => _worker is { IsCompleted: false };
    public bool IsRecording
    {
        get
        {
            lock (_writerGate)
            {
                return _writer != null;
            }
        }
    }

    public string? CurrentRecordingPath
    {
        get
        {
            lock (_writerGate)
            {
                return _recordingPath;
            }
        }
    }

    public Task StartAsync(CaptureOptions options, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (IsRunning) return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Status = new(CaptureState.Starting);

            _worker = Task.Run(() => RunCaptureLoopAsync(options, _cts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }
    }

    public async Task StopAsync()
    {
        Task? worker;
        CancellationTokenSource? cts;

        lock (_gate)
        {
            if (!IsRunning) return;
            Status = Status with { State = CaptureState.Stopping };
            worker = _worker;
            cts = _cts;
        }

        try
        {
            cts?.Cancel();
            if (worker != null) await worker.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _cts?.Dispose();
                _cts = null;
                _worker = null;
                Status = new(CaptureState.Stopped);
            }
        }
    }

    public Task<string?> StartFileRecordingAsync(string? outputPath = null)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Capture is not running.");
        }

        lock (_writerGate)
        {
            if (_writer != null)
            {
                return Task.FromResult<string?>(_recordingPath);
            }

            if (_currentFormat == null)
            {
                throw new InvalidOperationException("Capture format is not available yet.");
            }

            var path = string.IsNullOrWhiteSpace(outputPath) ? BuildDefaultOutputPath() : outputPath;
            _writer = new WaveFileWriter(path, _currentFormat);
            _recordingPath = path;
            return Task.FromResult<string?>(path);
        }
    }

    public Task StopFileRecordingAsync()
    {
        StopFileRecordingInternal();
        return Task.CompletedTask;
    }

    private async Task RunCaptureLoopAsync(CaptureOptions options, CancellationToken ct)
    {
        WasapiLoopbackCapture? capture = null;

        try
        {
            capture = new WasapiLoopbackCapture();
            _currentFormat = capture.WaveFormat;

            if (options.EnableFileRecording)
            {
                var path = string.IsNullOrWhiteSpace(options.OutputPath) ? BuildDefaultOutputPath() : options.OutputPath;
                lock (_writerGate)
                {
                    _writer = new WaveFileWriter(path, capture.WaveFormat);
                    _recordingPath = path;
                }
            }

            Status = new(
                CaptureState.Capturing,
                DeviceName: null,
                SampleRate: capture.WaveFormat.SampleRate,
                Channels: capture.WaveFormat.Channels
            );

            var tcs = new TaskCompletionSource();

            capture.DataAvailable += (s, e) =>
            {
                WaveFileWriter? writer;
                lock (_writerGate)
                {
                    writer = _writer;
                }

                writer?.Write(e.Buffer, 0, e.BytesRecorded);

                // Jos haluat UI:lle audiobufferin
                AudioChunk?.Invoke(this, e.Buffer.AsMemory(0, e.BytesRecorded));
            };

            capture.RecordingStopped += (s, e) =>
            {
                if (e.Exception != null)
                {
                    Status = Status with
                    {
                        State = CaptureState.Faulted,
                        LastError = e.Exception.Message
                    };

                    Faulted?.Invoke(this, e.Exception);
                }

                tcs.TrySetResult();
            };

            capture.StartRecording();

            // Odotetaan cancellationia
            using (ct.Register(() => capture.StopRecording()))
            {
                await tcs.Task.ConfigureAwait(false);
            }

            Status = new(CaptureState.Stopped);
        }
        catch (Exception ex)
        {
            Status = Status with
            {
                State = CaptureState.Faulted,
                LastError = ex.Message
            };

            Faulted?.Invoke(this, ex);
        }
        finally
        {
            StopFileRecordingInternal();
            _currentFormat = null;
            capture?.Dispose();
        }
    }

    private static string BuildDefaultOutputPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ezsampler_{DateTime.Now:yyyyMMdd_HHmmss}.wav"
        );
    }

    private void StopFileRecordingInternal()
    {
        lock (_writerGate)
        {
            _writer?.Dispose();
            _writer = null;
            _recordingPath = null;
        }
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}