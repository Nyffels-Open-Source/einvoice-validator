using EInvoicing.Validation.Api.Filters;
using EInvoicing.Validation.Application.Contracts;
using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EInvoicing.Validation.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class ValidationController(IValidationService validationService) : ControllerBase
{
    [HttpPost("/validate")]
    [RequestSizeLimitFromOptions]
    [Consumes("application/xml", "text/xml")]
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
