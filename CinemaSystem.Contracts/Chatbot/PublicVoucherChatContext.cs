namespace CinemaSystem.Contracts.Chatbot;

public sealed record PublicVoucherChatContext(
    string Code,
    string? Title,
    string Discount,
    decimal? MinOrderAmount,
    DateTime EndDate);
