using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Tags("System")]
public sealed class HealthController(IValidationArtefactProvider artefactProvider, ILogger<HealthController> logger) : ControllerBase
{
    private const string ValidatorEngineName = "kosit";

    [HttpGet("/health")]
    [EndpointSummary("Check service health and readiness")]
    [EndpointDescription("""
        Returns the operational status of the validation service.

        This endpoint verifies that:
        1. The KoSIT validator JAR is present and accessible on disk.
        2. The rule set artefacts (scenarios.xml) have been downloaded and are readable.

        **HTTP status** is always `200 OK` — inspect the response body to determine readiness.

        **`status`** is always `"ok"` as long as the service process is running.
        **`artefactsAvailable: false`** means validation requests will fail; call `POST /artefacts/update`
        or restart the service so it can auto-download the artefacts on startup.

        Use this endpoint as a readiness probe before directing traffic to the service, and to
        confirm which rule set version (`ruleSetVersion`) is currently installed.
        """)]
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
