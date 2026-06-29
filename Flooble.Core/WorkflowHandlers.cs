namespace Flooble.Core;

public static class WorkflowHandlers
{
    public static IReadOnlyDictionary<string, WorkflowHandler> CreateDefault(
        Func<string, string> readFile,
        Func<string, string, string> writeFile,
        Func<string, string, CancellationToken, Task<string>> runAgent)
    {
        ArgumentNullException.ThrowIfNull(readFile);
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
