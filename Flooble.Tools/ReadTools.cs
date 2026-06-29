using System.ComponentModel;
using Microsoft.Extensions.Configuration;

namespace Flooble.Tools;

public class ReadTools
{
    private readonly bool _canReadAnywhere;
    private readonly List<string> _allowedPaths;
    private readonly string _workingDirectory;

    public ReadTools(IConfiguration configuration)
    {
        _workingDirectory = Directory.GetCurrentDirectory();

        var readPermissions = configuration.GetSection("Agent:ReadPermissions");
        var permissionType = readPermissions["type"]?.Trim().ToLowerInvariant();

        _canReadAnywhere = permissionType == "global";
        _allowedPaths = readPermissions.GetSection("allowedPaths")
            .Get<string[]>()?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeConfiguredPath)
            .ToList()
            ?? [];
    }

    [Description("Reads a text file according to the configured read permissions.")]
    public string ReadFile(
        [Description("Path to the file, absolute or relative to the current working directory.")]
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required.";

        var fullPath = NormalizeRequestedPath(path);

        if (!_canReadAnywhere && !IsAllowedPath(fullPath))
            return "Error: path is not allowed by the configured read permissions.";

        if (!File.Exists(fullPath))
            return $"Error: file not found: {path}";

        return File.ReadAllText(fullPath);
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
