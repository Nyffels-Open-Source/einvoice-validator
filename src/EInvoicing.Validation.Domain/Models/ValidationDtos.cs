namespace EInvoicing.Validation.Domain.Models;

public sealed record ValidationResultDto(
    bool Valid,
    string Profile,
    string DocumentType,
    string? RuleSetVersion,
    IReadOnlyList<ValidationMessageDto> Errors,
    IReadOnlyList<ValidationMessageDto> Warnings,
    ValidationMetadataDto Metadata
);

public sealed record ValidationMessageDto(
    string Severity,
    string RuleId,
    string Message,
    string? Location
);

public sealed record ValidationMetadataDto(
    string ValidatorEngine,
    bool UsedCachedArtefacts,
    string? ArtefactsPath
);

public sealed record ProfileDto(
    string Id,
    string Name,
    IReadOnlyList<string> SupportedDocumentTypes,
    bool Enabled
);

public sealed record HealthDto(
    string Status,
    string ValidatorEngine,
    bool ArtefactsAvailable,
    string? RuleSetVersion
);

public sealed record ArtefactUpdateResultDto(
    bool Updated,
    string? PreviousVersion,
    string? CurrentVersion,
    DateTimeOffset DownloadedAt
);
