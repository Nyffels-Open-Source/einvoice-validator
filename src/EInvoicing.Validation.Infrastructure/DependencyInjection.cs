using EInvoicing.Validation.Application.Contracts;
using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Infrastructure.Artefacts;
using EInvoicing.Validation.Infrastructure.Options;
using EInvoicing.Validation.Infrastructure.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EInvoicing.Validation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddValidationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ValidationOptions>(options =>
        {
            configuration.GetSection("Validation").Bind(options);
            options.ArtefactsPath = Environment.GetEnvironmentVariable("VALIDATION_ARTEFACTS_PATH") ?? options.ArtefactsPath;
            options.AdminApiKey = Environment.GetEnvironmentVariable("VALIDATION_ADMIN_API_KEY") ?? options.AdminApiKey;
            if (long.TryParse(Environment.GetEnvironmentVariable("VALIDATION_MAX_REQUEST_SIZE_BYTES"), out var maxSize))
                options.MaxRequestSizeBytes = maxSize;
        });

        services.AddSingleton<IDocumentDetector, DocumentDetector>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddSingleton<IXmlValidationEngine, KositValidationEngine>();
        services.AddHttpClient<IValidationArtefactProvider, PeppolArtefactProvider>();
        services.AddHostedService<ArtefactWarmupService>();
        return services;
    }
}
