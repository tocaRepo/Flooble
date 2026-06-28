using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;
using Flooble.Tools;

var groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")
    ?? throw new InvalidOperationException("Set GROQ_API_KEY.");

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(groqApiKey),
    new OpenAIClientOptions
    {
        Endpoint = new Uri("https://api.groq.com/openai/v1")
    });

IChatClient chatClient = openAiClient
    .GetChatClient("openai/gpt-oss-120b")
    .AsIChatClient();
var readTools = new ReadTools();
var readFileTool = AIFunctionFactory.Create(readTools.ReadFile);

var agent = new ChatClientAgent(
    chatClient,
    instructions: """
    You are a friendly assistant running inside Flooble.
    Use the read_file tool when the user asks about a local file.
    Keep your answers brief.
    """,
    name: "FloobleGroqAgent",
    tools: [readFileTool]);

var response = await agent.RunAsync("Read test.txt and summarize it.");
Console.WriteLine(response);
