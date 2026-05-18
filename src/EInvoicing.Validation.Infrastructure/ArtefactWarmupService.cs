using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EInvoicing.Validation.Infrastructure;

public sealed class ArtefactWarmupService(IServiceProvider serviceProvider, ILogger<ArtefactWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IValidationArtefactProvider>();
            await provider.EnsureLatestAsync(ValidationProfile.PeppolBis3, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Validation artefact warmup failed. The first update call will retry.");
        }
    }
}
