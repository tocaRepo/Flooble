namespace Flooble.Core;

public static class WorkflowHandlers
{
    public static IReadOnlyDictionary<string, WorkflowHandler> CreateDefault(
        Func<string, string> readFile,
        Func<string, string, string, string> listFiles,
        Func<string, string, string, string, string, string, string> searchText,
        Func<string, string> fileInfo,
        Func<string, string, string> writeFile,
        Func<string, string, CancellationToken, Task<string>> runAgent)
    {
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(listFiles);
        ArgumentNullException.ThrowIfNull(searchText);
        ArgumentNullException.ThrowIfNull(fileInfo);
        ArgumentNullException.ThrowIfNull(writeFile);
        ArgumentNullException.ThrowIfNull(runAgent);

        return new Dictionary<string, WorkflowHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = (arguments, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = arguments.TryGetValue("path", out var value) ? value : "";
                return Task.FromResult(readFile(path));
            },
            ["write_file"] = (arguments, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = arguments.TryGetValue("path", out var pathValue) ? pathValue : "";
                var content = arguments.TryGetValue("content", out var contentValue) ? contentValue : "";
                return Task.FromResult(writeFile(path, content));
            },
            ["list_files"] = (arguments, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = arguments.TryGetValue("path", out var pathValue) ? pathValue : "";
                var maxDepth = arguments.TryGetValue("max_depth", out var maxDepthValue) ? maxDepthValue : "3";
                var ignore = arguments.TryGetValue("ignore", out var ignoreValue) ? ignoreValue : "";
                return Task.FromResult(listFiles(path, maxDepth, ignore));
            },
            ["search_text"] = (arguments, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = arguments.TryGetValue("path", out var pathValue) ? pathValue : "";
                var query = arguments.TryGetValue("query", out var queryValue) ? queryValue : "";
                var mode = arguments.TryGetValue("mode", out var modeValue) ? modeValue : "substring";
                var maxResults = arguments.TryGetValue("max_results", out var maxResultsValue) ? maxResultsValue : "20";
                var ignore = arguments.TryGetValue("ignore", out var ignoreValue) ? ignoreValue : "";
                var caseSensitive = arguments.TryGetValue("case_sensitive", out var caseSensitiveValue) ? caseSensitiveValue : "false";
                return Task.FromResult(searchText(path, query, mode, maxResults, ignore, caseSensitive));
            },
            ["file_info"] = (arguments, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var paths = arguments.TryGetValue("paths", out var pathsValue)
                    ? pathsValue
                    : arguments.TryGetValue("path", out var pathValue)
                        ? pathValue
                        : "";
                return Task.FromResult(fileInfo(paths));
            },
            ["agent"] = async (arguments, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var instructions = arguments.TryGetValue("instructions", out var instructionsValue)
                    ? instructionsValue
                    : "";

                if (!arguments.TryGetValue("text", out var text) || string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("Agent step requires a resolved 'text' value from a previous step.");
 
                return await runAgent(instructions, text, cancellationToken).ConfigureAwait(false);
            }
        };
    }
}
