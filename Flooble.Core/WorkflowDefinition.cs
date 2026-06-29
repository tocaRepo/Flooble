namespace Flooble.Core;

public sealed class WorkflowDefinition
{
    public string Name { get; set; } = "";
    public Dictionary<string, string> Input { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<WorkflowStep> Steps { get; set; } = new();
}

public sealed class WorkflowStep
{
    public string Id { get; set; } = "";
    public string Tool { get; set; } = "";
    public string Agent { get; set; } = "";
    public Dictionary<string, string> With { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

