using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Models;

namespace EInvoicing.Validation.Tests;

public sealed class DocumentDetectorTests
{
    private readonly DocumentDetector _detector = new();

    [Fact]
    public async Task Invoice_root_is_detected()
    {
        var result = await _detector.DetectAsync(Fixture("valid-peppol-invoice.xml"), CancellationToken.None);

        Assert.Equal(DocumentType.Invoice, result.DocumentType);
        Assert.True(result.IsClearlyPeppolBis3);
    }

    [Fact]
    public async Task CreditNote_root_is_detected()
    {
        var result = await _detector.DetectAsync(Fixture("valid-creditnote.xml"), CancellationToken.None);

        Assert.Equal(DocumentType.CreditNote, result.DocumentType);
        Assert.True(result.IsClearlyPeppolBis3);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
