namespace EInvoicing.Validation.Infrastructure.Options;

public sealed class ValidationOptions
{
    public string ArtefactsPath { get; set; } = "/data/artefacts";

    public long MaxRequestSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// When set, POST /artefacts/update requires this value in the X-Admin-Api-Key header.
    /// If null or empty the endpoint is open (suitable for isolated/local deployments only).
    /// </summary>
    public string? AdminApiKey { get; set; }
}
