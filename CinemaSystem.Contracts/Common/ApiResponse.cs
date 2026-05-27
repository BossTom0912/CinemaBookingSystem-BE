namespace CinemaSystem.Contracts.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public string? ErrorCode { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T? data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors
        };
    }
}
