using Microsoft.Extensions.Configuration;

namespace Flooble.Core;

public sealed class FloobleRuntime
{
    private readonly WorkflowInterpreter _interpreter;

    public FloobleRuntime(IConfiguration configuration, string model = "openai/gpt-oss-20b")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var agentService = new AgentChatService(configuration, model);
        Func<string, string, CancellationToken, Task<string>> runAgent = agentService.RunAsync;
        var discoveryTools = new Flooble.Tools.DiscoveryTools(configuration);
        var readTools = new Flooble.Tools.ReadTools(configuration);
        var writeTools = new Flooble.Tools.WriteTools(configuration);

        var handlers = WorkflowHandlers.CreateDefault(
            readTools.ReadFile,
            discoveryTools.ListFiles,
            discoveryTools.SearchText,
            discoveryTools.FileInfo,
            writeTools.WriteFile,
            runAgent);

        _interpreter = new WorkflowInterpreter(handlers);
    }

    public Task<WorkflowRunResult> RunAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        return _interpreter.ExecuteAsync(workflow, cancellationToken: cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        WorkflowDefinition workflow,
        Func<WorkflowRunEvent, CancellationToken, ValueTask>? onEvent,
        CancellationToken cancellationToken = default)
    {
        return _interpreter.ExecuteAsync(workflow, onEvent: onEvent, cancellationToken: cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, string>? input,
        CancellationToken cancellationToken = default)
    {
        return _interpreter.ExecuteAsync(workflow, input, cancellationToken: cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, string>? input,
        Func<WorkflowRunEvent, CancellationToken, ValueTask>? onEvent,
        CancellationToken cancellationToken = default)
    {
        return _interpreter.ExecuteAsync(workflow, input, onEvent, cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        string workflowPath,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(WorkflowLoader.LoadFromFile(workflowPath), cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        string workflowPath,
        Func<WorkflowRunEvent, CancellationToken, ValueTask>? onEvent,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(WorkflowLoader.LoadFromFile(workflowPath), onEvent, cancellationToken: cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        string workflowPath,
        IReadOnlyDictionary<string, string>? input,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(WorkflowLoader.LoadFromFile(workflowPath), input, cancellationToken);
    }

    public Task<WorkflowRunResult> RunAsync(
        string workflowPath,
        IReadOnlyDictionary<string, string>? input,
        Func<WorkflowRunEvent, CancellationToken, ValueTask>? onEvent,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(WorkflowLoader.LoadFromFile(workflowPath), input, onEvent, cancellationToken: cancellationToken);
    }
}
