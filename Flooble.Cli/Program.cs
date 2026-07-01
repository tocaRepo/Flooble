using Flooble.Core;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("configuration.json", optional: false, reloadOnChange: false)
    .Build();

var runtime = new FloobleRuntime(configuration);

var workflowName = args.FirstOrDefault();
var workflowPath = string.IsNullOrWhiteSpace(workflowName)
    ? Path.Combine(AppContext.BaseDirectory, "review.workflow.yaml")
    : Path.IsPathRooted(workflowName)
        ? workflowName
        : Path.Combine(AppContext.BaseDirectory, workflowName);

if (!File.Exists(workflowPath))
    throw new FileNotFoundException($"Workflow file not found: {workflowPath}", workflowPath);

var result = await runtime.RunAsync(workflowPath);

foreach (var step in result.Steps)
{
    Console.WriteLine($"[{step.Key}]");
    Console.WriteLine(step.Value.Output);
    Console.WriteLine();
}
