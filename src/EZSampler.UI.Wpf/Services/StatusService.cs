namespace EZSampler.UI.Wpf.Services;

/// <summary>
/// Manages application status messages and notifications.
/// Centralizes all status updates shown to the user.
/// </summary>
public sealed class StatusService : IStatusService
{
    private string _currentStatus = string.Empty;

    public event EventHandler<string>? StatusChanged;

    public void SetStatus(string message)
    {
        if (_currentStatus != message)
        {
            _currentStatus = message;
            StatusChanged?.Invoke(this, message);
        }
    }

    public void Clear()
    {
        SetStatus(string.Empty);
    }
}
