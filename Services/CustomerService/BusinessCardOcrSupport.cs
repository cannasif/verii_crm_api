using System.Globalization;
using System.Text;

namespace crm_api.Services;

internal static class BusinessCardOcrDefaults
{
    public const string UnknownTitleLocalizationKey = "General.Unknown";
    public const string UnknownTitleFallback = "Bilinmeyen";
}

internal static class BusinessCardOcrActions
{
    public const string Created = "Created";
    public const string Reused = "Reused";
    public const string Reactivated = "Reactivated";
}

internal static class BusinessCardOcrMessageKeys
{
    public const string CustomerAndContactCreated = "CustomerService.MobileOcrCustomerAndContactCreated";
    public const string ContactCreatedForExistingCustomer = "CustomerService.MobileOcrContactCreatedForExistingCustomer";
    public const string ExistingCustomerAndContactReused = "CustomerService.MobileOcrExistingCustomerAndContactReused";
    public const string CustomerReactivatedAndContactResolved = "CustomerService.MobileOcrCustomerReactivatedAndContactResolved";
}

internal sealed class BusinessCardOcrProcessingState
{
    public long CustomerId { get; set; }
    public string CustomerAction { get; set; } = BusinessCardOcrActions.Reused;
    public long? ContactId { get; set; }
    public string? ContactAction { get; set; }
    public long TitleId { get; set; }
    public string TitleAction { get; set; } = BusinessCardOcrActions.Reused;
    public string ResolvedTitleName { get; set; } = string.Empty;
    public bool UsedFallbackTitle { get; set; }
}

internal sealed class BusinessCardOcrConflictException : Exception
{
    public BusinessCardOcrConflictException(string message, string detail)
        : base(detail)
    {
        MessageText = message;
        Detail = detail;
    }

    public string MessageText { get; }
    public string Detail { get; }
}

internal static class BusinessCardOcrNormalizer
{
    public static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return CollapseWhitespace(value);
    }

    public static string CollapseWhitespace(string value)
    {
        var parts = value
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(" ", parts);
    }

    public static string NormalizeEmail(string? value)
    {
        return NormalizeForLookup(value, keepOnlyLettersAndDigits: false, preserveWhitespace: false);
    }

    public static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        if (digits.Length >= 12 && digits.StartsWith("90", StringComparison.Ordinal))
        {
            digits = digits[^10..];
        }
        else if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = digits[1..];
        }
        else if (digits.Length > 10)
        {
            digits = digits[^10..];
        }

        return digits;
    }

    public static string NormalizePersonName(string? value)
    {
        return NormalizeForLookup(value, keepOnlyLettersAndDigits: false, preserveWhitespace: true);
    }

    public static string NormalizeTitleKey(string? value)
    {
        return NormalizeForLookup(value, keepOnlyLettersAndDigits: false, preserveWhitespace: true);
    }

    public static string NormalizeCompanyKey(string? value)
    {
        return NormalizeForLookup(value, keepOnlyLettersAndDigits: true, preserveWhitespace: false);
    }

    private static string NormalizeForLookup(string? value, bool keepOnlyLettersAndDigits, bool preserveWhitespace)
    {
        var normalized = NormalizeNullable(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var upper = ReplaceTurkishCharacters(normalized).ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(upper.Length);

        foreach (var character in upper)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (!keepOnlyLettersAndDigits && preserveWhitespace && char.IsWhiteSpace(character))
            {
                builder.Append(' ');
            }
        }

        var result = builder.ToString();
        return preserveWhitespace ? CollapseWhitespace(result) : result;
    }

    private static string ReplaceTurkishCharacters(string value)
    {
        return value
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ü', 'u')
            .Replace('Ü', 'U')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ö', 'o')
            .Replace('Ö', 'O')
            .Replace('ç', 'c')
            .Replace('Ç', 'C');
    }
}
