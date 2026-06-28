namespace Flooble.Tools;
using System.ComponentModel;
public class ReadTools
{


    [Description("Reads a local text file from the current working directory.")]
    public string ReadFile(
        [Description("Relative path to the file, for example README.md or docs/intro.md.")]
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required.";

        var root = Directory.GetCurrentDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(root, path));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return "Error: path escapes the working directory.";

        if (!File.Exists(fullPath))
            return $"Error: file not found: {path}";

        return File.ReadAllText(fullPath);
    }
}
