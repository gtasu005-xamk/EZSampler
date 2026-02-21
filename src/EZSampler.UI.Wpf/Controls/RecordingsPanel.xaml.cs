using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EZSampler.UI.Wpf.Controls;

public partial class RecordingsPanel : UserControl
{
    public event EventHandler? RefreshClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? RenameClicked;
    public event KeyEventHandler? RecordingKeyDown;

    public RecordingsPanel()
    {
        InitializeComponent();
    }

    public string FolderPath
    {
        get => RecordingsFolderText.Text;
        set => RecordingsFolderText.Text = value;
    }

    public IEnumerable<object> Recordings
    {
        set => RecordingsList.ItemsSource = value;
    }

    public object? SelectedRecording => RecordingsList.SelectedItem;

    public List<object> SelectedRecordings => RecordingsList.SelectedItems.Cast<object>().ToList();

    private void RefreshRecordingsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshClicked?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteClicked?.Invoke(this, EventArgs.Empty);
    }

    private void RenameRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        RenameClicked?.Invoke(this, EventArgs.Empty);
    }

    private void RecordingsList_KeyDown(object sender, KeyEventArgs e)
    {
        RecordingKeyDown?.Invoke(this, e);
    }

    private Point? _dragStartPoint;

    private void RecordingsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void RecordingsList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        Point currentPosition = e.GetPosition(null);
        if (_dragStartPoint != null && 
            (Math.Abs(currentPosition.X - _dragStartPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(currentPosition.Y - _dragStartPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            var selectedItems = RecordingsList.SelectedItems.Cast<object>().ToList();
            if (selectedItems.Count > 0)
            {
                var data = new DataObject(DataFormats.FileDrop, selectedItems.ToArray());
                DragDrop.DoDragDrop(RecordingsList, data, DragDropEffects.Copy);
            }
        }
    }
}
