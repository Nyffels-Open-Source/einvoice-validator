using System.ComponentModel;

namespace EInvoicing.Validation.Domain.Models;

public sealed record ValidationResultDto(
    [property: Description("True when the document satisfies all mandatory and recommended rules of the selected profile. False when at least one rule violation was found.")]
    bool Valid,

    [property: Description("The validation profile that was applied. Mirrors the X-Validation-Profile request header value, or 'peppol-bis3' when the header was omitted.")]
    string Profile,

    [property: Description("XML document type detected from the document's CustomizationID element. Possible values: Invoice, CreditNote, Unknown.")]
    string DocumentType,

    [property: Description("Version string of the rule set that was used for validation (e.g. '2024-11-15'). Null when artefacts were unavailable.")]
    string? RuleSetVersion,

    [property: Description("List of rule violations found in the document. Each entry identifies the broken business rule by its RuleId. A non-empty list implies Valid is false.")]
    IReadOnlyList<ValidationMessageDto> Errors,

    [property: Description("List of informational notices produced during validation. Warnings do not affect the Valid flag and do not prevent a document from being considered conformant.")]
    IReadOnlyList<ValidationMessageDto> Warnings,

    [property: Description("Technical metadata about this validation run: which engine was used, whether cached artefacts were used, and the artefacts directory path.")]
    ValidationMetadataDto Metadata
);

public sealed record ValidationMessageDto(
    [property: Description("Severity level. One of: fatal, error, warning. Fatal and error both cause Valid to be false; warning is informational only.")]
    string Severity,

    [property: Description("Machine-readable rule identifier as defined in the Schematron rule set (e.g. BR-01, PEPPOL-EN16931-R001, XML-MALFORMED). Use this field to programmatically identify which business rule was violated.")]
    string RuleId,

    [property: Description("Human-readable explanation of the rule violation or notice, taken verbatim from the rule set definition.")]
    string Message,

    [property: Description("XPath expression pointing to the XML element or attribute that triggered this message. Null when no specific location can be attributed.")]
    string? Location
);

public sealed record ValidationMetadataDto(
    [property: Description("Name of the underlying XML validation engine. Currently always 'kosit' (KoSIT Validator).")]
    string ValidatorEngine,

    [property: Description("True when validation used artefacts that were already present on disk (cache hit). False when artefacts were freshly resolved or unavailable.")]
    bool UsedCachedArtefacts,

    [property: Description("Absolute file system path to the artefacts directory that was used during this validation run. Null when artefacts were unavailable.")]
    string? ArtefactsPath
);

public sealed record ProfileDto(
    [property: Description("Unique identifier for this profile. Pass this exact value in the X-Validation-Profile request header when calling POST /validate.")]
    string Id,

    [property: Description("Human-readable display name of the validation profile.")]
    string Name,

    [property: Description("XML document types this profile can validate. Possible values: Invoice, CreditNote.")]
    IReadOnlyList<string> SupportedDocumentTypes,

    [property: Description("Whether this profile's rule set is currently installed and active. When false, submitting a document against this profile returns HTTP 501 Not Implemented.")]
    bool Enabled
);

public sealed record HealthDto(
    [property: Description("Service status string. Always 'ok' — the HTTP status is always 200 regardless of artefact availability. Check artefactsAvailable to determine whether validation requests can be accepted.")]
    string Status,

    [property: Description("Name of the underlying validation engine. Currently always 'kosit'.")]
    string ValidatorEngine,

    [property: Description("True when rule set artefacts are present on disk and the service can accept validation requests. False when artefacts have not been downloaded yet; call POST /artefacts/update to install them.")]
    bool ArtefactsAvailable,

    [property: Description("Version string of the currently installed Peppol BIS3 rule set (e.g. '2024-11-15'). Null when artefacts are not available.")]
    string? RuleSetVersion
);

public sealed record ArtefactUpdateResultDto(
    [property: Description("True when new artefacts were downloaded and installed. False when the version already on disk matched the latest available release.")]
    bool Updated,

    [property: Description("Version string of the rule set that was installed before this update. Null when no artefacts were previously installed.")]
    string? PreviousVersion,

    [property: Description("Version string of the rule set that is now installed after this update.")]
    string? CurrentVersion,

    [property: Description("ISO 8601 UTC timestamp of when the artefacts were downloaded from the source repository.")]
    DateTimeOffset DownloadedAt
);
