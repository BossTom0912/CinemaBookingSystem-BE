namespace CinemaSystem.Domain.Utilities;

public static class IdGenerator
{
    /// <summary>
    /// Tạo mã ID ngắn gọn, chuyên nghiệp và dễ đọc với tiền tố (Ví dụ: BOK_A1B2C3D4, SHW_9F8E7D6C)
    /// </summary>
    public static string NewId(string prefix, int randomLength = 8)
    {
        var randomPart = Guid.NewGuid().ToString("N")[..randomLength].ToUpperInvariant();
        return string.IsNullOrEmpty(prefix) ? randomPart : $"{prefix}_{randomPart}";
    }
}
