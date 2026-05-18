using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using EInvoicing.Validation.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace EInvoicing.Validation.Infrastructure.Artefacts;

public sealed class PeppolArtefactProvider(HttpClient httpClient, IOptions<ValidationOptions> options) : IValidationArtefactProvider
{
    private const string ValidatorRepo = "itplr-kosit/validator";
    private const string ConfigurationRepo = "itplr-kosit/validator-configuration-bis";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<ValidationArtefacts> EnsureLatestAsync(ValidationProfile profile, CancellationToken ct)
    {
        if (profile == ValidationProfile.UblBe)
            throw new NotSupportedException("UBL.be validation is not implemented yet.");

        await _lock.WaitAsync(ct);
        try
        {
            var current = TryReadCurrent(profile);
            if (current is not null)
                return current with { UsedCachedArtefacts = true };

            return await DownloadLatestPeppolAsync(null, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ValidationArtefacts> GetCurrentAsync(ValidationProfile profile, CancellationToken ct)
    {
        if (profile == ValidationProfile.UblBe)
            throw new NotSupportedException("UBL.be validation is not implemented yet.");

        await _lock.WaitAsync(ct);
        try
        {
            return TryReadCurrent(profile)
                ?? throw new FileNotFoundException("Validation artefacts are not available.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ArtefactUpdateResultDto> UpdateAsync(ValidationProfile profile, CancellationToken ct)
    {
        if (profile == ValidationProfile.UblBe)
            throw new NotSupportedException("UBL.be validation is not implemented yet.");

        await _lock.WaitAsync(ct);
        try
        {
            var current = TryReadCurrent(profile);
            var latest = await GetLatestReleaseAsync(ConfigurationRepo, ct);
            if (current is not null && VersionsEqual(current.Version, latest.TagName))
                return new ArtefactUpdateResultDto(false, current.Version, current.Version, current.DownloadedAt);

            var downloaded = await DownloadLatestPeppolAsync(latest, ct);
            return new ArtefactUpdateResultDto(true, current?.Version, downloaded.Version, downloaded.DownloadedAt);
        }
        finally
        {
            _lock.Release();
        }
    }

    private ValidationArtefacts? TryReadCurrent(ValidationProfile profile)
    {
        var basePath = GetProfilePath(profile);
        var metadataPath = Path.Combine(basePath, "metadata.json");
        var jarPath = Path.Combine(basePath, "validator.jar");
        var scenariosPath = Path.Combine(basePath, "scenarios.xml");
        if (!File.Exists(metadataPath) || !File.Exists(jarPath) || !File.Exists(scenariosPath))
            return null;

        var metadata = JsonSerializer.Deserialize<ArtefactMetadata>(File.ReadAllText(metadataPath), _jsonOptions);
        if (metadata is null)
            return null;

        return new ValidationArtefacts(profile, metadata.Version, metadata.MandatoryFrom, metadata.DownloadedAt, metadata.Source, metadata.ValidatorEngine, basePath, jarPath, scenariosPath, true);
    }

    private async Task<ValidationArtefacts> DownloadLatestPeppolAsync(GitHubRelease? knownConfigurationRelease, CancellationToken ct)
    {
        var configurationRelease = knownConfigurationRelease ?? await GetLatestReleaseAsync(ConfigurationRepo, ct);
        var validatorRelease = await GetLatestReleaseAsync(ValidatorRepo, ct);
        var validatorAsset = validatorRelease.Assets.FirstOrDefault(a => a.Name.EndsWith("-distribution.zip", StringComparison.OrdinalIgnoreCase))
            ?? validatorRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No KoSIT validator distribution asset found.");

        var profilePath = GetProfilePath(ValidationProfile.PeppolBis3);
        var staging = Path.Combine(Path.GetTempPath(), $"einvoice-artefacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try
        {
            var validatorZip = Path.Combine(staging, "validator.zip");
            await DownloadFileAsync(validatorAsset.BrowserDownloadUrl, validatorZip, ct);
            ZipFile.ExtractToDirectory(validatorZip, Path.Combine(staging, "validator"), true);
            var jar = Directory.EnumerateFiles(Path.Combine(staging, "validator"), "*standalone.jar", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(Path.Combine(staging, "validator"), "*.jar", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException("No KoSIT validator jar found in distribution.");

            var configZip = Path.Combine(staging, "configuration.zip");
            await DownloadFileAsync(configurationRelease.ZipballUrl, configZip, ct);
            ZipFile.ExtractToDirectory(configZip, Path.Combine(staging, "configuration"), true);
            var configRoot = Directory.EnumerateDirectories(Path.Combine(staging, "configuration")).First();

            var metadata = new ArtefactMetadata(
                "peppol-bis3",
                NormalizeVersion(configurationRelease.TagName),
                ExtractMandatoryFrom(configurationRelease.Body),
                DateTimeOffset.UtcNow,
                "OpenPEPPOL/KoSIT",
                "kosit");

            // Build into a .new sibling so the existing profilePath remains intact on failure.
            var newPath = profilePath + ".new";
            if (Directory.Exists(newPath))
                Directory.Delete(newPath, true);
            Directory.CreateDirectory(newPath);
            File.Copy(jar, Path.Combine(newPath, "validator.jar"));
            CopyDirectory(configRoot, newPath);
            await File.WriteAllTextAsync(Path.Combine(newPath, "metadata.json"), JsonSerializer.Serialize(metadata, _jsonOptions), ct);

            // Atomic swap: old directory stays until new one is fully written.
            var oldPath = profilePath + ".old";
            if (Directory.Exists(oldPath))
                Directory.Delete(oldPath, true);
            if (Directory.Exists(profilePath))
                Directory.Move(profilePath, oldPath);
            Directory.Move(newPath, profilePath);
            if (Directory.Exists(oldPath))
                Directory.Delete(oldPath, true);

            return new ValidationArtefacts(ValidationProfile.PeppolBis3, metadata.Version, metadata.MandatoryFrom, metadata.DownloadedAt, metadata.Source, metadata.ValidatorEngine, profilePath, Path.Combine(profilePath, "validator.jar"), Path.Combine(profilePath, "scenarios.xml"), false);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    private async Task<GitHubRelease> GetLatestReleaseAsync(string repo, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        request.Headers.UserAgent.ParseAdd("EInvoicing.Validation.Api");
        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubRelease>(_jsonOptions, ct) ?? throw new InvalidOperationException($"Could not read latest release for {repo}.");
    }

    private async Task DownloadFileAsync(string url, string destination, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("EInvoicing.Validation.Api");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, ct);
    }

    private string GetProfilePath(ValidationProfile profile) => Path.Combine(options.Value.ArtefactsPath, ProfileParser.ToId(profile));

    private static bool VersionsEqual(string a, string b) => NormalizeVersion(a).Equals(NormalizeVersion(b), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeVersion(string version) => version.Trim().TrimStart('v').Replace("release-", "", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractMandatoryFrom(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var marker = "valid per ";
        var index = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 || body.Length < index + marker.Length + 10 ? null : body.Substring(index + marker.Length, 10);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), true);
    }

    private sealed record ArtefactMetadata(string Profile, string Version, string? MandatoryFrom, DateTimeOffset DownloadedAt, string Source, string ValidatorEngine);

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("zipball_url")] string ZipballUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
