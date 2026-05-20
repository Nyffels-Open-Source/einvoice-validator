using System.Xml;
using EInvoicing.Validation.Application.Contracts;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EInvoicing.Validation.Application.Services;

public sealed class ValidationService(
    IDocumentDetector detector,
    IValidationArtefactProvider artefactProvider,
    IXmlValidationEngine validationEngine,
    ILogger<ValidationService> logger) : IValidationService
{
    private const string ValidatorEngineName = "kosit";

    public async Task<ValidationResultDto> ValidateAsync(Stream xmlStream, ValidationProfile profile, CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"einvoice-{Guid.NewGuid():N}.xml");
        try
        {
            await using (var file = File.Create(tempFile))
            {
                await xmlStream.CopyToAsync(file, ct);
            }

            DocumentDetectionResult detection;
            try
            {
                detection = await detector.DetectAsync(tempFile, ct);
            }
            catch (XmlException)
            {
                return Result(false, profile, DocumentType.Unknown, null,
                    [new ValidationMessageDto("fatal", "XML-MALFORMED", "The XML document is malformed.", null)],
                    [], null, false);
            }

            if (!detection.IsSupportedRoot)
            {
                return Result(false, profile, DocumentType.Unknown, null,
                    [new ValidationMessageDto("fatal", "DOCUMENT-TYPE-UNSUPPORTED", "Only UBL Invoice and CreditNote documents are currently supported.", "/")],
                    [], null, false);
            }

            if (profile == ValidationProfile.UblBe)
            {
                return Result(false, profile, detection.DocumentType, null,
                    [new ValidationMessageDto("fatal", "PROFILE-NOT-IMPLEMENTED", "UBL.be validation is not implemented yet.", null)],
                    [], null, false);
            }

            var artefacts = await LoadArtefactsAsync(profile, detection.DocumentType, ct);
            if (artefacts is null)
            {
                return Result(false, profile, detection.DocumentType, null,
                    [new ValidationMessageDto("fatal", "ARTEFACTS-UNAVAILABLE", "Validation artefacts are not available. Call POST /artefacts/update to download them.", null)],
                    [], null, false);
            }

            var validation = await validationEngine.ValidateAsync(tempFile, profile, detection.DocumentType, artefacts, ct);
            if (ShouldRefreshArtefacts(validation))
            {
                logger.LogWarning("Validation failed with validator execution issue for profile {Profile}. Refreshing artefacts and retrying once.", profile);
                var refreshedArtefacts = await LoadArtefactsAsync(profile, detection.DocumentType, ct, forceRefresh: true);
                if (refreshedArtefacts is not null)
                    validation = await validationEngine.ValidateAsync(tempFile, profile, detection.DocumentType, refreshedArtefacts, ct);
            }

            if (!detection.IsClearlyPeppolBis3)
            {
                var warnings = validation.Warnings.Concat([
                    new ValidationMessageDto("warning", "PROFILE-NOT-CLEARLY-PEPPOL-BIS3", "The document is a UBL Invoice/CreditNote, but CustomizationID/ProfileID do not clearly identify it as Peppol BIS Billing 3.", $"/{detection.DocumentType}/cbc:CustomizationID")
                ]).ToArray();

                return validation with { Warnings = warnings };
            }

            return validation;
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private async Task<ValidationArtefacts?> LoadArtefactsAsync(ValidationProfile profile, DocumentType documentType, CancellationToken ct, bool forceRefresh = false)
    {
        try
        {
            if (forceRefresh)
            {
                await artefactProvider.UpdateAsync(profile, ct);
                return await artefactProvider.GetCurrentAsync(profile, ct);
            }

            return await artefactProvider.EnsureLatestAsync(profile, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load validation artefacts for profile {Profile} and document type {DocumentType}.", profile, documentType);
            return null;
        }
    }

    private static bool ShouldRefreshArtefacts(ValidationResultDto validation)
        => validation.Errors.Any(e =>
            e.RuleId.Equals("VALIDATOR-UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
            || e.RuleId.Equals("VALIDATOR-EXECUTION-FAILED", StringComparison.OrdinalIgnoreCase));

    private static ValidationResultDto Result(bool valid, ValidationProfile profile, DocumentType documentType, string? version, IReadOnlyList<ValidationMessageDto> errors, IReadOnlyList<ValidationMessageDto> warnings, string? artefactsPath, bool usedCached)
        => new(valid, ProfileParser.ToId(profile), documentType.ToString(), version, errors, warnings, new ValidationMetadataDto(ValidatorEngineName, usedCached, artefactsPath));
}
