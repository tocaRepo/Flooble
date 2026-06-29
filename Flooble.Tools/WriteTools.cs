using System.ComponentModel;
using Microsoft.Extensions.Configuration;

namespace Flooble.Tools;

public class WriteTools
{
    private readonly bool _canWriteAnywhere;
    private readonly List<string> _allowedPaths;
    private readonly string _workingDirectory;

    public WriteTools(IConfiguration configuration)
    {
        _workingDirectory = Directory.GetCurrentDirectory();

        var writePermissions = configuration.GetSection("Agent:WritePermissions");
        var permissionType = writePermissions["type"]?.Trim().ToLowerInvariant();

        _canWriteAnywhere = permissionType == "global";
        _allowedPaths = writePermissions.GetSection("allowedPaths")
            .Get<string[]>()?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeConfiguredPath)
            .ToList()
            ?? [];
    }

    [Description("Writes text to a file according to the configured write permissions.")]
    public string WriteFile(
        [Description("Path to the file, absolute or relative to the current working directory.")]
        string path,
        [Description("Text content to write to the file.")]
        string content)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required.";

        var fullPath = NormalizeRequestedPath(path);

        if (!_canWriteAnywhere && !IsAllowedPath(fullPath))
            return "Error: path is not allowed by the configured write permissions.";

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, content ?? string.Empty);

        return $"Wrote {fullPath}";
    }

    private string NormalizeRequestedPath(string path)
    {
        var resolvedPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(_workingDirectory, path);

        return Path.GetFullPath(resolvedPath);
    }

    private string NormalizeConfiguredPath(string path)
    {
        var resolvedPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(_workingDirectory, path);

        return Path.GetFullPath(resolvedPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private bool IsAllowedPath(string fullPath)
    {
        foreach (var allowedPath in _allowedPaths)
        {
            if (PathsMatch(fullPath, allowedPath))
                return true;

            if (IsWithinDirectory(fullPath, allowedPath))
                return true;
        }

        return false;
    }

    private static bool PathsMatch(string left, string right)
    {
        return string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWithinDirectory(string fullPath, string directoryPath)
    {
        var normalizedDirectory = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
