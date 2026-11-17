using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace para.Security;

public sealed class BearerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? [])
            .Concat(context.MethodInfo.GetCustomAttributes(true));
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any()) return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = []
        });
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Требуется действительный JWT" });
        if (metadata.OfType<IAuthorizeData>().Any(a => a.Policy == ApiPolicies.Admin))
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Требуется роль Admin" });
    }
}
