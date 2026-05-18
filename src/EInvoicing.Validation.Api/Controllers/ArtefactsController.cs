using EInvoicing.Validation.Api.Filters;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Tags("Administration")]
public sealed class ArtefactsController(IValidationArtefactProvider artefactProvider) : ControllerBase
{
    [HttpPost("/artefacts/update")]
    [RequireAdminApiKey]
    [EndpointSummary("Download and install the latest validation rule set")]
    [EndpointDescription("""
        Fetches the latest Peppol BIS3 KoSIT validator release from GitHub and installs it on disk,
        replacing any previously cached artefacts. The operation is idempotent — if the installed
        version is already the latest, the response reflects that (`updated: false`).

        **What gets updated**: the KoSIT validator JAR and the accompanying `scenarios.xml` rule set file.
        The version is resolved from the latest release tag on the official KoSIT GitHub repository.

        **When to call this**: when a new Peppol BIS3 rule set version is published (typically quarterly).
        The service also auto-downloads artefacts on first startup when none are present on disk.
        After a successful update, subsequent validation requests immediately use the new rule set.

        **Authentication**: when `VALIDATION_ADMIN_API_KEY` is configured, provide the matching value
        in the `X-Admin-Api-Key` header. Returns `401 Unauthorized` if the key is missing or incorrect.
        When no key is configured server-side, the endpoint is accessible without authentication.

        **Response**: includes `previousVersion` and `currentVersion` so callers can confirm which
        rule set version is now active. `downloadedAt` is an ISO 8601 UTC timestamp.
        """)]
    [ProducesResponseType(typeof(ArtefactUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ArtefactUpdateResultDto>> Update(CancellationToken ct)
        => Ok(await artefactProvider.UpdateAsync(ValidationProfile.PeppolBis3, ct));
}
