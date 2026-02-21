namespace EZSampler.UI.Wpf.Extensions;

/// <summary>
/// Extension methods for TimeSpan formatting and utilities.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Formats TimeSpan to a readable string (HH:MM:SS or MM:SS).
    /// </summary>
    public static string ToFormattedString(this TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }
}
