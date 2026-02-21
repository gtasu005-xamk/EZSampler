using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EZSampler.UI.Wpf.Controls;

public partial class WaveformView : UserControl
{
    public event EventHandler? ClearClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler<double>? PositionClicked;

    private bool _isDragging;

    public WaveformView()
    {
        InitializeComponent();
    }

    public Canvas Canvas => WaveformCanvas;

    public Path WaveformPathElement => WaveformPath;

    public bool ClearButtonEnabled
    {
        get => ClearButton.IsEnabled;
        set => ClearButton.IsEnabled = value;
    }

    public bool SaveButtonEnabled
    {
        get => SaveButton.IsEnabled;
        set => SaveButton.IsEnabled = value;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearClicked?.Invoke(this, EventArgs.Empty);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveClicked?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        WaveformPath.Data = null;
        HidePlaybackPosition();
    }

    public void SetPlaybackPosition(double normalizedPosition)
    {
        if (normalizedPosition < 0 || normalizedPosition > 1)
        {
            HidePlaybackPosition();
            return;
        }

        PlaybackPositionLine.Visibility = Visibility.Visible;
        var x = normalizedPosition * WaveformCanvas.ActualWidth;
        PlaybackPositionTransform.X = x;
        PlaybackPositionLine.Y2 = WaveformCanvas.ActualHeight;
    }

    public void HidePlaybackPosition()
    {
        PlaybackPositionLine.Visibility = Visibility.Collapsed;
    }

    private void WaveformCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDragging = true;
        WaveformCanvas.CaptureMouse();
        
        var position = e.GetPosition(WaveformCanvas);
        var normalizedPosition = position.X / WaveformCanvas.ActualWidth;
        PositionClicked?.Invoke(this, normalizedPosition);
    }

    private void WaveformCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetPosition(WaveformCanvas);
        var normalizedPosition = Math.Max(0, Math.Min(1, position.X / WaveformCanvas.ActualWidth));
        PositionClicked?.Invoke(this, normalizedPosition);
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            WaveformCanvas.ReleaseMouseCapture();
        }
    }
}
