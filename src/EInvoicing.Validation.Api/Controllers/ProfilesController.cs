using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Tags("Configuration")]
public sealed class ProfilesController : ControllerBase
{
    [HttpGet("/profiles")]
    [EndpointSummary("List all available validation profiles")]
    [EndpointDescription("""
        Returns the static catalogue of validation profiles supported by this service.

        Each entry describes a profile that can be referenced in the `X-Validation-Profile` header
        of `POST /validate`. The `id` field is the exact string to use as the header value.

        **Field meanings**
        - `id` — machine-readable identifier to pass in `X-Validation-Profile`.
        - `name` — human-readable display name.
        - `supportedDocumentTypes` — XML document types the profile can validate. Possible values: `Invoice`, `CreditNote`.
        - `enabled` — whether the profile's rule set is currently installed and active.
          Submitting a document to a disabled profile returns HTTP 501 from `POST /validate`.

        Use this endpoint to programmatically discover valid `X-Validation-Profile` values
        and to check which profiles are ready before submitting documents.
        """)]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ProfileDto>> Get()
        => Ok(new[]
        {
            new ProfileDto("peppol-bis3", "Peppol BIS Billing 3", ["Invoice", "CreditNote"], true),
            new ProfileDto("ubl-be", "UBL.be", ["Invoice", "CreditNote"], false)
        });
}
