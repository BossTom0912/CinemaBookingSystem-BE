using System;

namespace CinemaSystem.Domain.Entities;

public partial class ChatHistory
{
    public string ChatHistoryId { get; set; } = null!;
    public string? UserId { get; set; }
    public string UserMessage { get; set; } = null!;
    public string AiReplyMessage { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    
    public virtual User? User { get; set; }
}
