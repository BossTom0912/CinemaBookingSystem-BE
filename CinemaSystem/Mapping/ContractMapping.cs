using System.Text.Json;
using CinemaSystem.Application.Common;

namespace CinemaSystem.Mapping;

internal static class ContractMapping
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static TTarget MapTo<TTarget>(this object source)
    {
        var json = JsonSerializer.Serialize(source, Options);
        return JsonSerializer.Deserialize<TTarget>(json, Options)!;
    }

    public static ServiceResult<TTarget> MapDataTo<TSource, TTarget>(this ServiceResult<TSource> result)
    {
        if (!result.Success)
        {
            return ServiceResult<TTarget>.Fail(
                result.StatusCode,
                result.Message,
                result.ErrorCode ?? "ERROR",
                result.Errors);
        }

        var data = result.Data is null ? default : result.Data.MapTo<TTarget>();
        return ServiceResult<TTarget>.Ok(data, result.Message, result.StatusCode);
    }
}
