using Flooble.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using ChatMessageRecord = Flooble.Core.ChatMessageRecord;

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

        var agent = CreateAgent(instructions);

        var userMessage = new ChatMessage(
            ChatRole.User,
            text);

        var response = await agent.GetAgent().RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Text;
    }

    public Task<string> SendConversationAsync(
        IReadOnlyList<ChatMessageRecord> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
            throw new ArgumentException("At least one message is required.", nameof(messages));

        var chatMessages = BuildChatMessages(messages);
        return RunConversationAsync(chatMessages, cancellationToken);
    }

    public Task<string> SendAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendConversationAsync(
            [new ChatMessageRecord("user", text, DateTimeOffset.UtcNow)],
            cancellationToken);
    }

    private async Task<string> RunConversationAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var agentInstance = CreateAgent(string.Empty).GetAgent();

        try
        {
            var response = await agentInstance.RunAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Text;
        }
        catch (MissingMethodException)
        {
            // Fallback for SDK shapes that only accept a single prompt. We preserve the
            // conversation as a transcript inside the user message so follow-up questions still work.
            var transcript = BuildTranscriptPrompt(messages);
            return await RunAsync(string.Empty, transcript, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<ChatMessage> BuildChatMessages(IReadOnlyList<ChatMessageRecord> messages)
    {
        var chatMessages = new List<ChatMessage>(messages.Count);

        foreach (var message in messages)
        {
            chatMessages.Add(new ChatMessage(MapRole(message.Role), message.Text));
        }

        return chatMessages;
    }

    private static ChatRole MapRole(string role)
    {
        var normalized = role.Trim().ToLowerInvariant();

        return normalized switch
        {
            "user" or "you" => ChatRole.User,
            "assistant" or "flooble" => ChatRole.Assistant,
            "system" => ChatRole.System,
            _ => ChatRole.User
        };
    }

    private static string BuildTranscriptPrompt(IReadOnlyList<ChatMessage> messages)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Conversation so far:");

        foreach (var message in messages)
        {
            var prefix = message.Role == ChatRole.Assistant
                ? "Assistant: "
                : message.Role == ChatRole.System
                    ? "System: "
                    : "User: ";

            builder.Append(prefix);
            builder.AppendLine(message.Text);
        }

        builder.AppendLine();
        builder.AppendLine("Respond to the latest user message using the earlier messages as context.");
        return builder.ToString();
    }

    private Agent CreateAgent(string instructions)
    {
        return new Agent(
            _configuration,
            _model,
            instructions ?? string.Empty,
            _tools);
    }
}
