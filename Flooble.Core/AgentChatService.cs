using Flooble.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace Flooble.Core;

public sealed class AgentChatService
{
    private const string DefaultModel = "openai/gpt-oss-20b";

    private readonly IConfiguration _configuration;
    private readonly string _model;
    private readonly IList<AITool> _tools;

    public AgentChatService(IConfiguration configuration, string model = DefaultModel)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;
        _model = model;

        var discoveryTools = new DiscoveryTools(configuration);
        var readTools = new ReadTools(configuration);
        var writeTools = new WriteTools(configuration);

        _tools = new List<AITool>
        {
            AIFunctionFactory.Create(readTools.ReadFile),
            AIFunctionFactory.Create(writeTools.WriteFile),
            AIFunctionFactory.Create(discoveryTools.ListFiles),
            AIFunctionFactory.Create(discoveryTools.SearchText),
            AIFunctionFactory.Create(discoveryTools.FileInfo)
        };
    }

    public async Task<string> RunAsync(
        string instructions,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        var agent = new Agent(
            _configuration,
            _model,
            instructions ?? string.Empty,
            _tools);

        var userMessage = new ChatMessage(
            ChatRole.User,
            text);

        var response = await agent.GetAgent().RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Text;
    }

    public Task<string> SendAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(string.Empty, text, cancellationToken);
    }
}
