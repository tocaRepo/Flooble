using Flooble.Core;
using Flooble.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("configuration.json", optional: false, reloadOnChange: false)
    .Build();

var readTools = new ReadTools(configuration);
var writeTools = new WriteTools(configuration);
var readFileTool = AIFunctionFactory.Create(readTools.ReadFile);
var writeFileTool = AIFunctionFactory.Create(writeTools.WriteFile);

var agent = new Agent(
    configuration,
    "openai/gpt-oss-120b",
    """
    You are a friendly assistant running inside Flooble.
    Use the read_file tool when the user asks about a local file.
    Use the write_file tool when the user asks you to create or update a local file.
    Keep your answers brief.
    """,
    [readFileTool, writeFileTool]);

var response = await agent.GetAgent().RunAsync("Read test.txt and summarize it.");
Console.WriteLine(response);
