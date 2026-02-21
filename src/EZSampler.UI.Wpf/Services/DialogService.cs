using System.Windows;

namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Manages user dialogs and messageboxes.
/// Abstracts platform-specific dialog implementations for better testability.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Window? _ownerWindow;

    public DialogService(Window? ownerWindow = null)
    {
        _ownerWindow = ownerWindow;
    }

    public async Task<string?> ShowRenameDialogAsync(string currentName, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var dialog = new Window
            {
                Title = "Rename Recording",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _ownerWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
            var label = new System.Windows.Controls.TextBlock
            {
                Text = "New name:",
                Margin = new Thickness(0, 0, 0, 8)
            };

            var textBox = new System.Windows.Controls.TextBox
            {
                Text = System.IO.Path.GetFileNameWithoutExtension(currentName),
                Margin = new Thickness(0, 0, 0, 12)
            };
            textBox.SelectAll();

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
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

            var result = dialog.ShowDialog();
            return result == true ? textBox.Text.Trim() : null;
        }, ct);
    }

    public async Task<bool> ShowConfirmDialogAsync(string title, string message, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var result = MessageBox.Show(
                _ownerWindow,
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }, ct);
    }

    public async Task ShowErrorDialogAsync(string title, string message, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            MessageBox.Show(
                _ownerWindow,
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }, ct);
    }

    public async Task ShowInfoDialogAsync(string title, string message, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            MessageBox.Show(
                _ownerWindow,
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }, ct);
    }
}
