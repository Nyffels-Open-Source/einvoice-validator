using EInvoicing.Validation.Infrastructure.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace EInvoicing.Validation.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequestSizeLimitFromOptionsAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var opts = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<ValidationOptions>>().Value;

        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = opts.MaxRequestSizeBytes;
    }
}
