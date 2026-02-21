namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Defines the contract for managing dialog interactions (rename, confirm, etc.).
/// Abstracts platform-specific dialog implementations.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a rename dialog and returns the new name or null if cancelled.
    /// </summary>
    Task<string?> ShowRenameDialogAsync(string currentName, CancellationToken ct = default);

    /// <summary>
    /// Shows a confirmation dialog and returns true if user confirmed.
    /// </summary>
    Task<bool> ShowConfirmDialogAsync(string title, string message, CancellationToken ct = default);

    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    Task ShowErrorDialogAsync(string title, string message, CancellationToken ct = default);

    /// <summary>
    /// Shows an info dialog.
    /// </summary>
    Task ShowInfoDialogAsync(string title, string message, CancellationToken ct = default);
}
