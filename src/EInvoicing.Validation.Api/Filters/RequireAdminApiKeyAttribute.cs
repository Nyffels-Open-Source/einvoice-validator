using EInvoicing.Validation.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace EInvoicing.Validation.Api.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireAdminApiKeyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var opts = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<ValidationOptions>>().Value;

        if (string.IsNullOrWhiteSpace(opts.AdminApiKey))
            return;

        var provided = context.HttpContext.Request.Headers["X-Admin-Api-Key"].FirstOrDefault();
        if (!string.Equals(provided, opts.AdminApiKey, StringComparison.Ordinal))
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid or missing X-Admin-Api-Key header." });
    }
}
