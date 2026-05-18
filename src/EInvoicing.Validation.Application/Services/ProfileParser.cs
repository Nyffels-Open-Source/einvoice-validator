using EInvoicing.Validation.Domain.Models;

namespace EInvoicing.Validation.Application.Services;

public static class ProfileParser
{
    public static string ToId(ValidationProfile profile) => profile switch
    {
        ValidationProfile.PeppolBis3 => "peppol-bis3",
        ValidationProfile.UblBe => "ubl-be",
        _ => "unknown"
    };

    public static bool TryParse(string? value, out ValidationProfile profile)
    {
        profile = ValidationProfile.PeppolBis3;
        if (string.IsNullOrWhiteSpace(value) || value.Equals("peppol-bis3", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("ubl-be", StringComparison.OrdinalIgnoreCase))
        {
            profile = ValidationProfile.UblBe;
            return true;
        }

        return false;
    }
}
