using System;

namespace Roam.Contracts.Chat;

public class ChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class SendMessageRequest
{
    public string Text { get; set; } = string.Empty;
}
