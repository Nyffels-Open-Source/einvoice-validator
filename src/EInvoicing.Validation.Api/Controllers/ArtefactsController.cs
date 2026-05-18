using EInvoicing.Validation.Api.Filters;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class ArtefactsController(IValidationArtefactProvider artefactProvider) : ControllerBase
{
    [HttpPost("/artefacts/update")]
    [RequireAdminApiKey]
    [ProducesResponseType(typeof(ArtefactUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ArtefactUpdateResultDto>> Update(CancellationToken ct)
        => Ok(await artefactProvider.UpdateAsync(ValidationProfile.PeppolBis3, ct));
}
