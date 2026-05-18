using System.Net;
using System.Net.Http.Json;
using System.Text;
using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EInvoicing.Validation.Tests;

public sealed class ValidationApiTests : IClassFixture<ValidationApiFactory>
{
    private readonly HttpClient _client;

    public ValidationApiTests(ValidationApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Malformed_xml_returns_XML_MALFORMED()
    {
        var response = await PostXmlAsync(await File.ReadAllTextAsync(Fixture("malformed.xml"), CancellationToken.None));
        var body = await response.Content.ReadFromJsonAsync<ValidationResultDto>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(body!.Errors, e => e.RuleId == "XML-MALFORMED");
    }

    [Fact]
    public async Task Unsupported_root_returns_DOCUMENT_TYPE_UNSUPPORTED()
    {
        var response = await PostXmlAsync("<Order xmlns=\"urn:test\" />");
        var body = await response.Content.ReadFromJsonAsync<ValidationResultDto>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(body!.Errors, e => e.RuleId == "DOCUMENT-TYPE-UNSUPPORTED");
    }

    [Fact]
    public async Task Missing_peppol_identifiers_gives_warning()
    {
        const string xml = """
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                     xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
              <cbc:ID>INV-1</cbc:ID>
            </Invoice>
            """;

        var response = await PostXmlAsync(xml);
        var body = await response.Content.ReadFromJsonAsync<ValidationResultDto>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(body!.Warnings, e => e.RuleId == "PROFILE-NOT-CLEARLY-PEPPOL-BIS3");
    }

    [Fact]
    public async Task Profiles_returns_peppol_bis3()
    {
        var profiles = await _client.GetFromJsonAsync<ProfileDto[]>("/profiles", CancellationToken.None);

        Assert.Contains(profiles!, p => p.Id == "peppol-bis3" && p.Enabled);
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var health = await _client.GetFromJsonAsync<HealthDto>("/health", CancellationToken.None);

        Assert.Equal("ok", health!.Status);
        Assert.True(health.ArtefactsAvailable);
    }

    [Fact]
    public async Task Scalar_api_reference_is_available()
    {
        var response = await _client.GetAsync("/scalar/v1", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Scalar", body);
    }

    [Fact]
    public async Task Validation_maps_validator_errors_correctly()
    {
        var xml = await File.ReadAllTextAsync(Fixture("invalid-missing-buyer-endpoint.xml"), CancellationToken.None);
        var response = await PostXmlAsync(xml);
        var body = await response.Content.ReadFromJsonAsync<ValidationResultDto>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body!.Valid);
        Assert.Contains(body.Errors, e => e.RuleId == "PEPPOL-EN16931-R010" && e.Location == "/Invoice/cac:AccountingCustomerParty/cac:Party");
    }

    private Task<HttpResponseMessage> PostXmlAsync(string xml)
        => _client.PostAsync("/validate", new StringContent(xml, Encoding.UTF8, "application/xml"), CancellationToken.None);

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}

public sealed class ValidationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IValidationArtefactProvider>();
            services.RemoveAll<IXmlValidationEngine>();
            services.AddSingleton<IValidationArtefactProvider, FakeArtefactProvider>();
            services.AddSingleton<IXmlValidationEngine, FakeValidationEngine>();
        });
    }
}

public sealed class FakeArtefactProvider : IValidationArtefactProvider
{
    private static readonly ValidationArtefacts Artefacts = new(ValidationProfile.PeppolBis3, "test-rules", null, DateTimeOffset.UtcNow, "test", "kosit", "/tmp/artefacts", "/tmp/validator.jar", "/tmp/scenarios.xml", true);

    public Task<ValidationArtefacts> EnsureLatestAsync(ValidationProfile profile, CancellationToken ct) => Task.FromResult(Artefacts);

    public Task<ValidationArtefacts> GetCurrentAsync(ValidationProfile profile, CancellationToken ct) => Task.FromResult(Artefacts);

    public Task<ArtefactUpdateResultDto> UpdateAsync(ValidationProfile profile, CancellationToken ct) => Task.FromResult(new ArtefactUpdateResultDto(false, "test-rules", "test-rules", Artefacts.DownloadedAt));
}

public sealed class FakeValidationEngine : IXmlValidationEngine
{
    public async Task<ValidationResultDto> ValidateAsync(string xmlFilePath, ValidationProfile profile, DocumentType documentType, ValidationArtefacts artefacts, CancellationToken ct)
    {
        var xml = await File.ReadAllTextAsync(xmlFilePath, ct);
        var errors = xml.Contains("INV-MISSING-BUYER-ENDPOINT", StringComparison.Ordinal)
            ? [new ValidationMessageDto("fatal", "PEPPOL-EN16931-R010", "Buyer electronic address MUST be provided", "/Invoice/cac:AccountingCustomerParty/cac:Party")]
            : Array.Empty<ValidationMessageDto>();

        return new ValidationResultDto(errors.Length == 0, ProfileParser.ToId(profile), documentType.ToString(), artefacts.Version, errors, [], new ValidationMetadataDto("kosit", true, artefacts.BasePath));
    }
}
