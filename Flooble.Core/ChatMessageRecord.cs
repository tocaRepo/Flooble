namespace Flooble.Core;

public sealed record ChatMessageRecord(
    string Role,
    string Text,
    DateTimeOffset TimestampUtc);
