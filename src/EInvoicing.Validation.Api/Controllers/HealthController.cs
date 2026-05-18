using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class HealthController(IValidationArtefactProvider artefactProvider, ILogger<HealthController> logger) : ControllerBase
{
    private const string ValidatorEngineName = "kosit";

    [HttpGet("/health")]
    [ProducesResponseType(typeof(HealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthDto>> Get(CancellationToken ct)
    {
        try
        {
            var artefacts = await artefactProvider.GetCurrentAsync(ValidationProfile.PeppolBis3, ct);
            return Ok(new HealthDto("ok", ValidatorEngineName, true, artefacts.Version));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve current artefacts for health check.");
            return Ok(new HealthDto("ok", ValidatorEngineName, false, null));
        }
    }
}
