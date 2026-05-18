using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class ProfilesController : ControllerBase
{
    [HttpGet("/profiles")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ProfileDto>> Get()
        => Ok(new[]
        {
            new ProfileDto("peppol-bis3", "Peppol BIS Billing 3", ["Invoice", "CreditNote"], true),
            new ProfileDto("ubl-be", "UBL.be", ["Invoice", "CreditNote"], false)
        });
}
