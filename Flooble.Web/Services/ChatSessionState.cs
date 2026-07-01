using Flooble.Core;

namespace Flooble.Web.Services;

public sealed class ChatSessionState
{
    private const int MaxMessages = 30;
    private const int MaxCharacters = 20_000;

    private readonly List<ChatMessageRecord> _messages = new();

    public IReadOnlyList<ChatMessageRecord> Messages => _messages;

    public void AddUserMessage(string text) => AddMessage("user", text);

    public void AddAssistantMessage(string text) => AddMessage("assistant", text);

    public void Clear() => _messages.Clear();

    private void AddMessage(string role, string text)
    {
        _messages.Add(new ChatMessageRecord(role, text, DateTimeOffset.UtcNow));
        TrimConversation();
    }

    private void TrimConversation()
    {
        if (_messages.Count <= MaxMessages && GetCharacterCount(_messages) <= MaxCharacters)
            return;

        var trimmed = new List<ChatMessageRecord>();
        var characters = 0;

        for (var index = _messages.Count - 1; index >= 0; index--)
        {
            var message = _messages[index];
            var nextCharacters = characters + message.Text.Length;

            if (trimmed.Count >= MaxMessages || nextCharacters > MaxCharacters)
                break;

            trimmed.Add(message);
            characters = nextCharacters;
        }

        trimmed.Reverse();

        if (trimmed.Count == 0 && _messages.Count > 0)
            trimmed.Add(_messages[^1]);

        _messages.Clear();
        _messages.AddRange(trimmed);
    }

    private static int GetCharacterCount(IEnumerable<ChatMessageRecord> messages)
    {
        var total = 0;
        foreach (var message in messages)
            total += message.Text.Length;

        return total;
    }
}
