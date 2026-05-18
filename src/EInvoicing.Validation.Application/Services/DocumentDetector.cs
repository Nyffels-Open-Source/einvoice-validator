using System.Xml;
using EInvoicing.Validation.Application.Contracts;
using EInvoicing.Validation.Domain.Models;

namespace EInvoicing.Validation.Application.Services;

public sealed class DocumentDetector : IDocumentDetector
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string CreditNoteNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private const string CbcNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public async Task<DocumentDetectionResult> DetectAsync(string xmlFilePath, CancellationToken ct)
    {
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        await using var stream = File.OpenRead(xmlFilePath);
        using var reader = XmlReader.Create(stream, settings);

        await reader.MoveToContentAsync();
        ct.ThrowIfCancellationRequested();

        var documentType = reader.LocalName switch
        {
            "Invoice" when reader.NamespaceURI == InvoiceNamespace => DocumentType.Invoice,
            "CreditNote" when reader.NamespaceURI == CreditNoteNamespace => DocumentType.CreditNote,
            _ => DocumentType.Unknown
        };

        if (documentType == DocumentType.Unknown)
        {
            return new DocumentDetectionResult(DocumentType.Unknown, false, false, null, null);
        }

        string? customizationId = null;
        string? profileId = null;
        var rootDepth = reader.Depth;

        while (await reader.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rootDepth)
            {
                break;
            }

            if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != CbcNamespace)
            {
                continue;
            }

            if (reader.LocalName == "CustomizationID")
            {
                customizationId = await reader.ReadElementContentAsStringAsync();
            }
            else if (reader.LocalName == "ProfileID")
            {
                profileId = await reader.ReadElementContentAsStringAsync();
            }

            if (customizationId is not null && profileId is not null)
            {
                break;
            }
        }

        var combined = $"{customizationId} {profileId}";
        var isPeppol = combined.Contains("urn:cen.eu:en16931", StringComparison.OrdinalIgnoreCase)
            && combined.Contains("peppol.eu:2017:poacc:billing:3.0", StringComparison.OrdinalIgnoreCase);

        return new DocumentDetectionResult(documentType, true, isPeppol, customizationId, profileId);
    }
}
