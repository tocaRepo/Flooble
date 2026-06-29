using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Flooble.Tools;

public class DiscoveryTools
{
    private readonly bool _canReadAnywhere;
    private readonly List<string> _allowedPaths;
    private readonly string _workingDirectory;

    public DiscoveryTools(IConfiguration configuration)
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

    [Description("Returns a compact directory tree for a path, honoring read permissions, ignore patterns, and maximum depth.")]
    public string ListFiles(
        [Description("Root path to inspect, absolute or relative to the current working directory. Defaults to the working directory.")]
        string path = "",
        [Description("Maximum depth below the root path. Defaults to 3.")]
        string maxDepth = "3",
        [Description("Ignore patterns separated by commas, semicolons, or new lines.")]
        string ignore = "")
    {
        if (!TryResolveRootPath(path, out var rootPath, out var error))
            return error;

        if (!TryParseNonNegativeInt(maxDepth, 3, out var depth))
            return "Error: maxDepth must be a non-negative integer.";

        var ignorePatterns = ParsePatterns(ignore);

        if (File.Exists(rootPath))
            return BuildFileTree(rootPath);

        if (!Directory.Exists(rootPath))
            return $"Error: path not found: {path}";

        var lines = new List<string>
        {
            rootPath + Path.DirectorySeparatorChar
        };

        AppendDirectoryTree(lines, rootPath, rootPath, depth, 0, ignorePatterns);

        return string.Join(Environment.NewLine, lines);
    }

    [Description("Searches text files for substring, regex, or glob matches and returns file and line snippets.")]
    public string SearchText(
        [Description("Root path to inspect, absolute or relative to the current working directory.")]
        string path,
        [Description("Text, regex, or glob pattern to search for.")]
        string query,
        [Description("Search mode: substring, regex, or glob. Defaults to substring.")]
        string mode = "substring",
        [Description("Maximum number of results to return. Defaults to 20.")]
        string maxResults = "20",
        [Description("Ignore patterns separated by commas, semicolons, or new lines.")]
        string ignore = "",
        [Description("True for case-sensitive matching. Defaults to false.")]
        string caseSensitive = "false")
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query is required.";

        if (!TryResolveRootPath(path, out var rootPath, out var error))
            return error;

        if (!TryParseNonNegativeInt(maxResults, 20, out var resultCap))
            return "Error: maxResults must be a non-negative integer.";

        var ignorePatterns = ParsePatterns(ignore);
        var searchMode = mode.Trim().ToLowerInvariant();
        var isCaseSensitive = bool.TryParse(caseSensitive, out var parsedCaseSensitive) && parsedCaseSensitive;

        Regex? regex = null;
        if (searchMode == "regex")
        {
            var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
            if (!isCaseSensitive)
                options |= RegexOptions.IgnoreCase;

            regex = new Regex(query, options, TimeSpan.FromSeconds(2));
        }
        else if (searchMode is not ("substring" or "glob"))
        {
            return "Error: mode must be substring, regex, or glob.";
        }

        var searchFiles = GetSearchFiles(rootPath, ignorePatterns).ToList();
        var results = new List<string>();
        var truncated = false;

        foreach (var file in searchFiles)
        {
            if (resultCap >= 0 && results.Count >= resultCap)
            {
                truncated = true;
                break;
            }

            try
            {
                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;

                    if (!IsMatch(line, query, searchMode, isCaseSensitive, regex))
                        continue;

                    results.Add($"{GetDisplayPath(file)}:{lineNumber}: {CreateSnippet(line, query, searchMode, isCaseSensitive, regex)}");

                    if (results.Count >= resultCap)
                    {
                        truncated = true;
                        break;
                    }
                }
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or DecoderFallbackException or NotSupportedException)
            {
                continue;
            }
        }

        var output = new List<string>
        {
            $"results={results.Count} truncated={(truncated ? "yes" : "no")} mode={searchMode}"
        };

        output.AddRange(results);
        return string.Join(Environment.NewLine, output);
    }

    [Description("Returns file size, modified time, extension, and SHA-256 hash for one or more files.")]
    public string FileInfo(
        [Description("One or more file paths, separated by commas, semicolons, or new lines.")]
        string paths)
    {
        var requestedPaths = ParsePaths(paths);
        if (requestedPaths.Count == 0)
            return "Error: at least one file path is required.";

        var lines = new List<string>();

        foreach (var requestedPath in requestedPaths)
        {
            if (!TryResolveRequestedPath(requestedPath, out var fullPath, out var error))
            {
                lines.Add($"path={requestedPath}|error={error}");
                continue;
            }

            if (!File.Exists(fullPath))
            {
                lines.Add($"path={fullPath}|error=file not found");
                continue;
            }

            var info = new System.IO.FileInfo(fullPath);
            var hash = ComputeSha256(fullPath);
            var extension = string.IsNullOrEmpty(info.Extension) ? "" : info.Extension;

            lines.Add(
                $"path={fullPath}|size={info.Length}|modified={info.LastWriteTimeUtc:O}|ext={extension}|sha256={hash}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildFileTree(string filePath)
    {
        var info = new System.IO.FileInfo(filePath);
        return info.FullName;
    }

    private void AppendDirectoryTree(
        List<string> lines,
        string rootPath,
        string currentPath,
        int maxDepth,
        int depth,
        IReadOnlyList<string> ignorePatterns)
    {
        if (depth >= maxDepth)
            return;

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(currentPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lines.Add($"{GetTreeIndent(depth + 1)}[unreadable]");
            return;
        }

        foreach (var entry in OrderEntries(entries))
        {
            var relativePath = Path.GetRelativePath(rootPath, entry);
            if (IsIgnored(relativePath, ignorePatterns))
                continue;

            var indent = GetTreeIndent(depth + 1);

            if (Directory.Exists(entry))
            {
                lines.Add($"{indent}{Path.GetFileName(entry)}{Path.DirectorySeparatorChar}");
                AppendDirectoryTree(lines, rootPath, entry, maxDepth, depth + 1, ignorePatterns);
                continue;
            }

            lines.Add($"{indent}{Path.GetFileName(entry)}");
        }
    }

    private IEnumerable<string> GetSearchFiles(string rootPath, IReadOnlyList<string> ignorePatterns)
    {
        if (File.Exists(rootPath))
        {
            if (!IsIgnored(Path.GetFileName(rootPath), ignorePatterns))
                yield return rootPath;

            yield break;
        }

        var stack = new Stack<(string Path, int Depth)>();
        stack.Push((rootPath, 0));

        while (stack.Count > 0)
        {
            var (currentPath, depth) = stack.Pop();
            IEnumerable<string> entries;

            try
            {
                entries = Directory.EnumerateFileSystemEntries(currentPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in OrderEntries(entries).Reverse())
            {
                var relativePath = Path.GetRelativePath(rootPath, entry);
                if (IsIgnored(relativePath, ignorePatterns))
                    continue;

                if (Directory.Exists(entry))
                {
                    stack.Push((entry, depth + 1));
                    continue;
                }

                yield return entry;
            }
        }
    }

    private static IEnumerable<string> OrderEntries(IEnumerable<string> entries)
    {
        return entries
            .OrderBy(entry => Directory.Exists(entry) ? 0 : 1)
            .ThenBy(entry => Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetDisplayPath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static bool IsMatch(
        string line,
        string query,
        string mode,
        bool caseSensitive,
        Regex? regex)
    {
        return mode switch
        {
            "substring" => caseSensitive
                ? line.Contains(query, StringComparison.Ordinal)
                : line.Contains(query, StringComparison.OrdinalIgnoreCase),
            "regex" => regex is not null && regex.IsMatch(line),
            "glob" => MatchGlob(line, query, caseSensitive),
            _ => false
        };
    }

    private static string CreateSnippet(
        string line,
        string query,
        string mode,
        bool caseSensitive,
        Regex? regex)
    {
        var snippet = line.Trim();

        if (mode == "substring")
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var index = snippet.IndexOf(query, comparison);
            if (index >= 0)
                snippet = ExpandSnippet(snippet, index, query.Length);
        }
        else if (mode == "regex" && regex is not null)
        {
            var match = regex.Match(snippet);
            if (match.Success)
                snippet = ExpandSnippet(snippet, match.Index, match.Length);
        }

        if (snippet.Length > 180)
            snippet = snippet[..177] + "...";

        return snippet;
    }

    private static string ExpandSnippet(string line, int index, int length)
    {
        const int window = 80;

        var start = Math.Max(0, index - window);
        var end = Math.Min(line.Length, index + length + window);
        var snippet = line[start..end];

        if (start > 0)
            snippet = "..." + snippet;

        if (end < line.Length)
            snippet += "...";

        return snippet;
    }

    private static bool MatchGlob(string value, string pattern, bool caseSensitive)
    {
        return GlobRegex(pattern, caseSensitive).IsMatch(value);
    }

    private bool TryResolveRootPath(string path, out string fullPath, out string error)
    {
        var requestedPath = string.IsNullOrWhiteSpace(path) ? _workingDirectory : path;

        if (!TryResolveRequestedPath(requestedPath, out fullPath, out error))
            return false;

        if (!_canReadAnywhere && !IsAllowedPath(fullPath))
        {
            error = "Error: path is not allowed by the configured read permissions.";
            return false;
        }

        return true;
    }

    private bool TryResolveRequestedPath(string path, out string fullPath, out string error)
    {
        try
        {
            var resolvedPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(_workingDirectory, path);

            fullPath = Path.GetFullPath(resolvedPath);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = "";
            error = $"Error: invalid path: {path}";
            return false;
        }
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

    private static List<string> ParsePatterns(string value)
    {
        return SplitValues(value)
            .Select(NormalizePattern)
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToList();
    }

    private static List<string> ParsePaths(string value)
    {
        return SplitValues(value)
            .Select(path => path.Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }

    private static IEnumerable<string> SplitValues(string value)
    {
        return (value ?? string.Empty)
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizePattern(string pattern)
    {
        return pattern.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    private static bool IsIgnored(string relativePath, IReadOnlyList<string> ignorePatterns)
    {
        if (ignorePatterns.Count == 0)
            return false;

        var normalized = NormalizePattern(relativePath);
        var fileName = Path.GetFileName(normalized);

        foreach (var pattern in ignorePatterns)
        {
            if (GlobRegex(pattern, caseSensitive: false).IsMatch(normalized))
                return true;

            if (GlobRegex(pattern, caseSensitive: false).IsMatch(fileName))
                return true;
        }

        return false;
    }

    private static Regex GlobRegex(string pattern, bool caseSensitive)
    {
        var normalizedPattern = NormalizePattern(pattern);
        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^" + Regex.Escape(Path.DirectorySeparatorChar.ToString() + Path.AltDirectorySeparatorChar) + "]*")
            .Replace(@"\?", ".") + "$";

        var options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
        if (!caseSensitive)
            options |= RegexOptions.IgnoreCase;

        return new Regex(regexPattern, options, TimeSpan.FromSeconds(1));
    }

    private static string GetTreeIndent(int depth)
    {
        return new string(' ', Math.Max(0, depth) * 2);
    }

    private static bool TryParseNonNegativeInt(string value, int defaultValue, out int parsedValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsedValue = defaultValue;
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue) && parsedValue >= 0)
            return true;

        parsedValue = defaultValue;
        return false;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
