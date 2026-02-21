using System.Windows;
using System.Windows.Controls;

namespace EZSampler.UI.Wpf.Controls;

public partial class CaptureStatusPanel : UserControl
{
    public CaptureStatusPanel()
    {
        InitializeComponent();
    }

    public string StatusTextValue
    {
        get => StatusText.Text;
        set => StatusText.Text = value;
    }

    public string DetailsTextValue
    {
        get => DetailsText.Text;
        set => DetailsText.Text = value;
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void SetDetails(string details)
    {
        DetailsText.Text = details;
    }

    public void Clear()
    {
        StatusText.Text = "Stopped";
        DetailsText.Text = "";
    }
}
