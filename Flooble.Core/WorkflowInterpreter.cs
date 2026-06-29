using System.Text.RegularExpressions;

namespace Flooble.Core;

public delegate Task<string> WorkflowHandler(
    IReadOnlyDictionary<string, string> arguments,
    CancellationToken cancellationToken);

public sealed class WorkflowStepResult
{
    public string Output { get; set; } = "";
}

public sealed class WorkflowRunResult
{
    public string Name { get; set; } = "";
    public Dictionary<string, string> Input { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, WorkflowStepResult> Steps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkflowInterpreter
{
    private static readonly Regex PlaceholderRegex = new(@"\$\{([^}]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IReadOnlyDictionary<string, WorkflowHandler> _handlers;

    public WorkflowInterpreter(IReadOnlyDictionary<string, WorkflowHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = new Dictionary<string, WorkflowHandler>(handlers, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<WorkflowRunResult> ExecuteAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, string>? input = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (string.IsNullOrWhiteSpace(workflow.Name))
            throw new InvalidOperationException("Workflow name is required.");

        var runtimeInput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in workflow.Input)
            runtimeInput[pair.Key] = pair.Value;

        if (input is not null)
        {
            foreach (var pair in input)
                runtimeInput[pair.Key] = pair.Value;
        }

        var stepResults = new Dictionary<string, WorkflowStepResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in workflow.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(step.Id))
                throw new InvalidOperationException("Every workflow step needs an id.");

            if (stepResults.ContainsKey(step.Id))
                throw new InvalidOperationException($"Duplicate workflow step id: {step.Id}");

            var handlerName = GetHandlerName(step, out var handlerArguments);
            if (!_handlers.TryGetValue(handlerName, out var handler))
                throw new InvalidOperationException($"Unknown workflow handler: {handlerName}");

            var resolvedArguments = ResolveDictionary(handlerArguments, runtimeInput, stepResults);
            var output = await handler(resolvedArguments, cancellationToken).ConfigureAwait(false) ?? string.Empty;

            stepResults[step.Id] = new WorkflowStepResult
            {
                Output = output
            };
        }

        return new WorkflowRunResult
        {
            Name = workflow.Name,
            Input = runtimeInput,
            Steps = stepResults
        };
    }

    private static string GetHandlerName(WorkflowStep step, out IReadOnlyDictionary<string, string> handlerArguments)
    {
        if (!string.IsNullOrWhiteSpace(step.Tool))
        {
            handlerArguments = step.With;
            return step.Tool;
        }

        if (!string.IsNullOrWhiteSpace(step.Agent))
        {
            var arguments = new Dictionary<string, string>(step.With, StringComparer.OrdinalIgnoreCase)
            {
                ["instructions"] = step.Agent
            };

            handlerArguments = arguments;
            return "agent";
        }

        throw new InvalidOperationException($"Workflow step '{step.Id}' must define either tool or agent.");
    }

    private static Dictionary<string, string> ResolveDictionary(
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> input,
        IReadOnlyDictionary<string, WorkflowStepResult> steps)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in source)
            resolved[pair.Key] = ResolveValue(pair.Value, input, steps);

        return resolved;
    }

    private static string ResolveValue(
        string value,
        IReadOnlyDictionary<string, string> input,
        IReadOnlyDictionary<string, WorkflowStepResult> steps)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return PlaceholderRegex.Replace(value, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            var parts = expression.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2 && parts[0].Equals("input", StringComparison.OrdinalIgnoreCase))
            {
                if (!input.TryGetValue(parts[1], out var inputValue))
                    throw new InvalidOperationException($"Unknown input reference: {expression}");

                return inputValue;
            }

            if (parts.Length == 3
                && parts[0].Equals("steps", StringComparison.OrdinalIgnoreCase)
                && parts[2].Equals("output", StringComparison.OrdinalIgnoreCase))
            {
                if (!steps.TryGetValue(parts[1], out var stepResult))
                    throw new InvalidOperationException($"Unknown step reference: {expression}");

                return stepResult.Output;
            }

            throw new InvalidOperationException($"Unsupported workflow expression: {expression}");
        });
    }
}
