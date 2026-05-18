using EInvoicing.Validation.Domain.Models;

namespace EInvoicing.Validation.Application.Contracts;

public interface IValidationService
{
    Task<ValidationResultDto> ValidateAsync(Stream xmlStream, ValidationProfile profile, CancellationToken ct);
}

public interface IDocumentDetector
{
    Task<DocumentDetectionResult> DetectAsync(string xmlFilePath, CancellationToken ct);
}
