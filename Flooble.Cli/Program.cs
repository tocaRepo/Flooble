using Flooble.Core;
using Flooble.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("configuration.json", optional: false, reloadOnChange: false)
    .Build();

var discoveryTools = new DiscoveryTools(configuration);
var readTools = new ReadTools(configuration);
var writeTools = new WriteTools(configuration);
var listFilesTool = AIFunctionFactory.Create(discoveryTools.ListFiles);
var searchTextTool = AIFunctionFactory.Create(discoveryTools.SearchText);
var fileInfoTool = AIFunctionFactory.Create(discoveryTools.FileInfo);
var readFileTool = AIFunctionFactory.Create(readTools.ReadFile);
var writeFileTool = AIFunctionFactory.Create(writeTools.WriteFile);

var workflowName = args.FirstOrDefault();
var workflowPath = string.IsNullOrWhiteSpace(workflowName)
    ? Path.Combine(AppContext.BaseDirectory, "review.workflow.yaml")
    : Path.IsPathRooted(workflowName)
        ? workflowName
        : Path.Combine(AppContext.BaseDirectory, workflowName);

if (!File.Exists(workflowPath))
    throw new FileNotFoundException($"Workflow file not found: {workflowPath}", workflowPath);

var workflow = WorkflowLoader.LoadFromFile(workflowPath);

var handlers = WorkflowHandlers.CreateDefault(
    readTools.ReadFile,
    discoveryTools.ListFiles,
    discoveryTools.SearchText,
    discoveryTools.FileInfo,
    writeTools.WriteFile,
    async (instructions, text, cancellationToken) =>
    {
        var agent = new Agent(
            configuration,
            "meta-llama/llama-4-scout-17b-16e-instruct",
            instructions,
            [readFileTool, writeFileTool, listFilesTool, searchTextTool, fileInfoTool]);

        var userMessage = new ChatMessage(
            ChatRole.User,
            text);

        var response = await agent.GetAgent().RunAsync(userMessage, cancellationToken: cancellationToken);
        return response.Text;
    });

var interpreter = new WorkflowInterpreter(handlers);

var result = await interpreter.ExecuteAsync(workflow);

foreach (var step in result.Steps)
{
    Console.WriteLine($"[{step.Key}]");
    Console.WriteLine(step.Value.Output);
    Console.WriteLine();
}
