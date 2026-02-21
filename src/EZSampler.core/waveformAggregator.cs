using NAudio.Wave;

public class WaveformAggregator
{
    private readonly WaveFormat _format;

    public List<(float minL, float maxL, float minR, float maxR)> Peaks { get; } 
        = new();

    private int _samplesPerPeak = 1024;
    private int _sampleCounter = 0;

    private float _minL = float.MaxValue;
    private float _maxL = float.MinValue;
    private float _minR = float.MaxValue;
    private float _maxR = float.MinValue;

    public WaveformAggregator(WaveFormat format)
    {
        _format = format;
    }

    public void Process(byte[] buffer, int offset, int bytesRecorded)
    {
        int bytesPerSample = _format.BitsPerSample / 8;
        int channels = _format.Channels;
        int blockAlign = bytesPerSample * channels;

        for (int i = offset; i < offset + bytesRecorded; i += blockAlign)
        {
            float left = BitConverter.ToSingle(buffer, i);

            float right = left;
            if (channels > 1)
                right = BitConverter.ToSingle(buffer, i + bytesPerSample);

            _minL = Math.Min(_minL, left);
            _maxL = Math.Max(_maxL, left);
            _minR = Math.Min(_minR, right);
            _maxR = Math.Max(_maxR, right);

            _sampleCounter++;

            if (_sampleCounter >= _samplesPerPeak)
            {
                Peaks.Add((_minL, _maxL, _minR, _maxR));

                _minL = float.MaxValue;
                _maxL = float.MinValue;
                _minR = float.MaxValue;
                _maxR = float.MinValue;
                _sampleCounter = 0;
            }
        }
    }
}