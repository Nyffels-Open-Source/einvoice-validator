namespace EInvoicing.Validation.Domain.Models;

public sealed record ValidationArtefacts(
    ValidationProfile Profile,
    string Version,
    string? MandatoryFrom,
    DateTimeOffset DownloadedAt,
    string Source,
    string ValidatorEngine,
    string BasePath,
    string ValidatorJarPath,
    string ScenariosPath,
    bool UsedCachedArtefacts
);

public sealed record DocumentDetectionResult(
    DocumentType DocumentType,
    bool IsSupportedRoot,
    bool IsClearlyPeppolBis3,
    string? CustomizationId,
    string? ProfileId
);
