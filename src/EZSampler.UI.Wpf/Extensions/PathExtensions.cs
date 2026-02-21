using System;
using System.IO;
using System.Linq;

namespace EZSampler.UI.Wpf.Extensions;

/// <summary>
/// Extension methods for file path manipulation and validation.
/// </summary>
public static class PathExtensions
{
    /// <summary>
    /// Ensures that a file path is unique by appending a counter if necessary.
    /// </summary>
    public static string EnsureUniquePath(this string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        var index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name}_{index}{extension}");
            index++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    public static string SanitizeFileName(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(input.Trim().Where(ch => !invalid.Contains(ch)).ToArray());
        return cleaned;
    }
}
