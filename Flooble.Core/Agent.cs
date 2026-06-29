using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace Flooble.Core;

public class Agent
{
    ChatClientAgent agent;
    public Agent(IConfiguration configuration, string model, string instructions, IList<AITool>? tools)
    {
        var agentSection = configuration.GetSection("Agent");
        var apiKey = agentSection["API_KEY"]
            ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? throw new InvalidOperationException("Set Agent:API_KEY in configuration.json or GROQ_API_KEY.");
        var apiEndpoint = agentSection["API_ENDPOINT"]
            ?? throw new InvalidOperationException("Set Agent:API_ENDPOINT in configuration.json.");

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(apiEndpoint)
            });

        IChatClient chatClient = openAiClient
            .GetChatClient(model)
            .AsIChatClient();

        agent = new ChatClientAgent(
            chatClient,
            instructions: instructions,
            name: "FloobleGroqAgent",
            tools: tools);
    }

    public ChatClientAgent GetAgent()
    {
        return agent;
    }

}
