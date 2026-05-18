using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using EInvoicing.Validation.Application.Services;
using EInvoicing.Validation.Domain.Contracts;
using EInvoicing.Validation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EInvoicing.Validation.Infrastructure.Validation;

public sealed class KositValidationEngine(ILogger<KositValidationEngine> logger) : IXmlValidationEngine
{
    private const string EngineName = "kosit";

    public async Task<ValidationResultDto> ValidateAsync(string xmlFilePath, ValidationProfile profile, DocumentType documentType, ValidationArtefacts artefacts, CancellationToken ct)
    {
        var reportDirectory = Path.Combine(Path.GetTempPath(), $"einvoice-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportDirectory);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    WorkingDirectory = artefacts.BasePath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            process.StartInfo.ArgumentList.Add("-jar");
            process.StartInfo.ArgumentList.Add(artefacts.ValidatorJarPath);
            process.StartInfo.ArgumentList.Add("-s");
            process.StartInfo.ArgumentList.Add(artefacts.ScenariosPath);
            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add(reportDirectory);
            process.StartInfo.ArgumentList.Add(xmlFilePath);

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start KoSIT validator process.");
                return Unavailable(profile, documentType, artefacts);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var report = Directory.EnumerateFiles(reportDirectory, "*.xml", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (report is null)
            {
                if (process.ExitCode == 0)
                    return BuildResult(true, profile, documentType, artefacts, [], []);

                var output = NormalizeWhitespace(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
                logger.LogError("KoSIT validator exited with code {ExitCode}. Output: {Output}", process.ExitCode, output);
                return BuildResult(false, profile, documentType, artefacts,
                    [new ValidationMessageDto("fatal", "VALIDATOR-UNAVAILABLE", "The local validator engine could not complete validation.", null)], []);
            }

            var messages = ParseReport(report);
            return BuildResult(messages.Errors.Count == 0, profile, documentType, artefacts, messages.Errors, messages.Warnings);
        }
        finally
        {
            if (Directory.Exists(reportDirectory))
                Directory.Delete(reportDirectory, true);
        }
    }

    private static (IReadOnlyList<ValidationMessageDto> Errors, IReadOnlyList<ValidationMessageDto> Warnings) ParseReport(string reportPath)
    {
        var xmlSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var xmlReader = XmlReader.Create(reportPath, xmlSettings);
        var document = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);

        var errors = new List<ValidationMessageDto>();
        var warnings = new List<ValidationMessageDto>();

        foreach (var element in document.Descendants())
        {
            var local = element.Name.LocalName;
            if (!local.Equals("failed-assert", StringComparison.OrdinalIgnoreCase)
                && !local.Equals("successful-report", StringComparison.OrdinalIgnoreCase)
                && !local.Equals("error", StringComparison.OrdinalIgnoreCase)
                && !local.Equals("warning", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var severity = InferSeverity(element, local);
            var ruleId = Attribute(element, "id") ?? Attribute(element, "test") ?? Attribute(element, "flag") ?? "VALIDATION";
            var location = Attribute(element, "location");
            var message = element.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("text", StringComparison.OrdinalIgnoreCase))?.Value
                ?? element.Value
                ?? "Validation rule failed.";

            var dto = new ValidationMessageDto(severity, ruleId, NormalizeWhitespace(message), location);
            if (severity is "warning" or "info")
                warnings.Add(dto);
            else
                errors.Add(dto);
        }

        return (errors, warnings);
    }

    private static string InferSeverity(XElement element, string localName)
    {
        var flag = Attribute(element, "flag") ?? Attribute(element, "role") ?? localName;
        return flag.Contains("warn", StringComparison.OrdinalIgnoreCase) ? "warning" :
            flag.Contains("info", StringComparison.OrdinalIgnoreCase) ? "info" :
            flag.Contains("fatal", StringComparison.OrdinalIgnoreCase) ? "fatal" : "error";
    }

    private static string? Attribute(XElement element, string localName)
        => element.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string NormalizeWhitespace(string value)
        => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static ValidationResultDto Unavailable(ValidationProfile profile, DocumentType documentType, ValidationArtefacts artefacts)
        => BuildResult(false, profile, documentType, artefacts,
            [new ValidationMessageDto("fatal", "VALIDATOR-UNAVAILABLE", "The local validator engine could not be started. Check whether Java and validator.jar are available.", null)], []);

    private static ValidationResultDto BuildResult(bool valid, ValidationProfile profile, DocumentType documentType, ValidationArtefacts artefacts, IReadOnlyList<ValidationMessageDto> errors, IReadOnlyList<ValidationMessageDto> warnings)
        => new(valid, ProfileParser.ToId(profile), documentType.ToString(), artefacts.Version, errors, warnings, new ValidationMetadataDto(EngineName, artefacts.UsedCachedArtefacts, artefacts.BasePath));
}
