using EInvoicing.Validation.Infrastructure;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(aspNetCoreUrls))
{
    var port = Environment.GetEnvironmentVariable("VALIDATION_PORT");
    if (!string.IsNullOrWhiteSpace(port))
    {
        builder.WebHost.UseUrls($"http://+:{port}");
    }
}

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "EInvoice Validation API";
        document.Info.Version = "v1";
        document.Info.Description = """
            REST API for validating electronic invoice (e-invoice) XML documents against standardized rule sets.

            ## Supported profiles
            - **peppol-bis3** — Peppol BIS Billing 3.0, the pan-European e-invoicing standard used across the Peppol network. Supports UBL 2.1 Invoice and CreditNote documents. Rule set is active.
            - **ubl-be** — Belgian domestic UBL e-invoice standard. Configured but not yet active; submitting against this profile returns HTTP 501.

            ## Validation engine
            Documents are validated by the [KoSIT Validator](https://github.com/itplr-kosit/validator), an open-source Java-based XML validation engine. Rule set artefacts (validator JAR + scenarios.xml) are downloaded from the official KoSIT GitHub releases and cached on disk. Use `POST /artefacts/update` to pull a newer rule set without restarting the service.

            ## Intended LLM workflow
            1. Call `GET /profiles` to discover which profiles are enabled and which document types each supports.
            2. Call `GET /health` to confirm the engine and artefacts are ready before submitting documents.
            3. Submit the raw XML body to `POST /validate` with the target profile in the `X-Validation-Profile` header.
            4. Inspect `valid`, `errors`, and `warnings` in the response. `valid: true` means the document is fully conformant; `errors` contains the violated rule assertions otherwise.
            5. To upgrade the rule set after a new Peppol release, call `POST /artefacts/update` (requires `X-Admin-Api-Key` when the server is configured with one).
            """;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["AdminApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Admin-Api-Key",
            Description = """
                Static API key protecting administrative endpoints.
                Configure the key by setting the `VALIDATION_ADMIN_API_KEY` environment variable
                (or `Validation:AdminApiKey` in appsettings.json).
                When no key is configured server-side, the header is ignored and all callers are allowed.
                When a key is configured, this header must be present and match exactly (case-sensitive).
                """
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, ct) =>
    {
        var path = context.Description.RelativePath ?? string.Empty;
        var method = context.Description.HttpMethod ?? string.Empty;

        if (method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && path.Equals("validate", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters ??= [];
            operation.Parameters.Insert(0, new OpenApiParameter
            {
                Name = "X-Validation-Profile",
                In = ParameterLocation.Header,
                Required = false,
                Description = """
                    Selects the validation rule set to apply to the submitted document.
                    Omit or leave blank to default to `peppol-bis3`.
                    Allowed values: `peppol-bis3`, `ubl-be`.
                    Using a recognized but inactive profile (e.g. `ubl-be`) returns HTTP 501.
                    Using an unrecognized value returns HTTP 400.
                    Available profile IDs are returned by `GET /profiles`.
                    """
            });
        }

        if (method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && path.Equals("artefacts/update", StringComparison.OrdinalIgnoreCase))
        {
            operation.Security ??= [];
            var requirement = new OpenApiSecurityRequirement();
            requirement[new OpenApiSecuritySchemeReference("AdminApiKey", null, null)] = [];
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddValidationServices(builder.Configuration);

var app = builder.Build();

app.MapOpenApi("/openapi/v1.json");
app.MapScalarApiReference();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
