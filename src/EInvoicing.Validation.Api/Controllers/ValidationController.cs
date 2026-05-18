using EInvoicing.Validation.Api.Filters;
using EInvoicing.Validation.Application.Contracts;
using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Tags("Validation")]
public sealed class ValidationController(IValidationService validationService) : ControllerBase
{
    [HttpPost("/validate")]
    [RequestSizeLimitFromOptions]
    [Consumes("application/xml", "text/xml")]
    [EndpointSummary("Validate an e-invoice XML document")]
    [EndpointDescription("""
        Accepts a raw XML e-invoice document in the request body and validates it against the selected rule set.

        The service first detects the document type (Invoice or CreditNote) by inspecting the
        `CustomizationID` and `ProfileID` XML elements. It then invokes the KoSIT engine to run
        Schematron and XSD assertions from the chosen profile's rule set.

        **Request body**: raw XML bytes — do not wrap in JSON.
        Content-Type must be `application/xml` or `text/xml`.
        Maximum body size is configurable via `VALIDATION_MAX_REQUEST_SIZE_BYTES` (default 10 MB).

        **Profile selection**: pass the desired profile id in the `X-Validation-Profile` request header.
        Omit the header to default to `peppol-bis3`. Call `GET /profiles` to enumerate valid values.

        **Response interpretation**
        - `valid: true` — the document satisfies all mandatory and recommended rules.
        - `valid: false` — one or more rule violations were found; inspect `errors` for details.
        - `warnings` contains informational notices that do not affect the `valid` flag.
        - The full `ValidationResultDto` is always returned, regardless of HTTP status.

        **Status codes**
        - `200 OK` — document was processed; check `valid` to determine conformance.
        - `400 Bad Request` — XML is malformed, document type could not be detected, or the `X-Validation-Profile` value is not recognized.
        - `501 Not Implemented` — profile is recognized but not yet active (e.g. `ubl-be`).
        """)]
    [ProducesResponseType(typeof(ValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationResultDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationResultDto), StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<ValidationResultDto>> Validate(CancellationToken ct)
    {
        var header = Request.Headers["X-Validation-Profile"].FirstOrDefault();
        if (!ProfileParser.TryParse(header, out var profile))
        {
            return BadRequest(new ValidationResultDto(false, header ?? "unknown", DocumentType.Unknown.ToString(), null,
                [new ValidationMessageDto("fatal", "PROFILE-UNSUPPORTED", "The requested validation profile is not supported.", null)],
                [], new ValidationMetadataDto("kosit", false, null)));
        }

        var result = await validationService.ValidateAsync(Request.Body, profile, ct);
        if (result.Errors.Any(e => e.RuleId is "XML-MALFORMED" or "DOCUMENT-TYPE-UNSUPPORTED" or "PROFILE-UNSUPPORTED"))
        {
            return BadRequest(result);
        }

        if (result.Errors.Any(e => e.RuleId == "PROFILE-NOT-IMPLEMENTED"))
        {
            return StatusCode(StatusCodes.Status501NotImplemented, result);
        }

        return Ok(result);
    }
}
