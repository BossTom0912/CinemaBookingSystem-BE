using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CinemaSystem.Filters;

public sealed class SepayWebhookExampleOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.RelativePath, "api/Payment/sepay-webhook", StringComparison.OrdinalIgnoreCase))
            return;

        var schema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "content", "transferAmount" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["content"] = new()
                {
                    Type = "string",
                    Example = new OpenApiString("Thanh toan ve phim TSEPAY150K1")
                },
                ["transferAmount"] = new()
                {
                    Type = "number",
                    Format = "decimal",
                    Example = new OpenApiDouble(150700)
                },
                ["referenceCode"] = new()
                {
                    Type = "string",
                    Nullable = true,
                    Example = new OpenApiString("SEPAY_BANK_REF_150K_001")
                }
            },
            Example = new OpenApiObject
            {
                ["content"] = new OpenApiString("Thanh toan ve phim TSEPAY150K1"),
                ["transferAmount"] = new OpenApiDouble(150700),
                ["referenceCode"] = new OpenApiString("SEPAY_BANK_REF_150K_001")
            }
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = schema,
                    Example = schema.Example
                }
            }
        };
    }
}
