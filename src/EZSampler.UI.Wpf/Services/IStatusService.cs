namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Defines the contract for status/message notifications.
/// Centralizes all status updates shown to the user.
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// Raised when status message needs to be displayed.
    /// </summary>
    event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Sets the current status message.
    /// </summary>
    void SetStatus(string message);

    /// <summary>
    /// Clears the status message.
    /// </summary>
    void Clear();
}
