using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Flooble.Core;

public static class WorkflowLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static WorkflowDefinition Load(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new ArgumentException("Workflow YAML is required.", nameof(yaml));

        var definition = Deserializer.Deserialize<WorkflowDefinition>(yaml)
            ?? throw new InvalidOperationException("Workflow YAML did not produce a definition.");

        definition.Input ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        definition.Steps ??= new List<WorkflowStep>();

        foreach (var step in definition.Steps)
            step.With ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return definition;
    }

    public static WorkflowDefinition LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Workflow path is required.", nameof(path));

        return Load(File.ReadAllText(path));
    }
}
