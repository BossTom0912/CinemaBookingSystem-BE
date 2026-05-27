namespace CinemaSystem.Application.Common;

public sealed class ServiceResult<T>
{
    private ServiceResult(
        bool success,
        int statusCode,
        string message,
        T? data,
        string? errorCode,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        Success = success;
        StatusCode = statusCode;
        Message = message;
        Data = data;
        ErrorCode = errorCode;
        Errors = errors;
    }

    public bool Success { get; }

    public int StatusCode { get; }

    public string Message { get; }

    public T? Data { get; }

    public string? ErrorCode { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static ServiceResult<T> Ok(T? data, string message = "Success", int statusCode = 200)
    {
        return new ServiceResult<T>(true, statusCode, message, data, null, null);
    }

    public static ServiceResult<T> Fail(
        int statusCode,
        string message,
        string errorCode,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        return new ServiceResult<T>(false, statusCode, message, default, errorCode, errors);
    }
}
