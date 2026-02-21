using System.Windows;
using System.Windows.Controls;

namespace EZSampler.UI.Wpf.Controls;

public partial class TransportControls : UserControl
{
    public event EventHandler? StartClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? PlayClicked;

    public TransportControls()
    {
        InitializeComponent();
    }

    public bool StartButtonEnabled
    {
        get => StartButton.IsEnabled;
        set => StartButton.IsEnabled = value;
    }

    public bool StopButtonEnabled
    {
        get => StopButton.IsEnabled;
        set => StopButton.IsEnabled = value;
    }

    public bool PlayButtonEnabled
    {
        get => PlayButton.IsEnabled;
        set => PlayButton.IsEnabled = value;
    }

    public void SetPlayButtonText(string text)
    {
        PlayButton.Content = text;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartClicked?.Invoke(this, EventArgs.Empty);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopClicked?.Invoke(this, EventArgs.Empty);
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        PlayClicked?.Invoke(this, EventArgs.Empty);
    }
}
