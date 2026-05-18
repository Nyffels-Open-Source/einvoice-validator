using EInvoicing.Validation.Domain.Models;

namespace EInvoicing.Validation.Domain.Contracts;

public interface IXmlValidationEngine
{
    Task<ValidationResultDto> ValidateAsync(
        string xmlFilePath,
        ValidationProfile profile,
        DocumentType documentType,
        ValidationArtefacts artefacts,
        CancellationToken ct);
}

public interface IValidationArtefactProvider
{
    Task<ValidationArtefacts> EnsureLatestAsync(ValidationProfile profile, CancellationToken ct);

    Task<ValidationArtefacts> GetCurrentAsync(ValidationProfile profile, CancellationToken ct);

    Task<ArtefactUpdateResultDto> UpdateAsync(ValidationProfile profile, CancellationToken ct);
}
