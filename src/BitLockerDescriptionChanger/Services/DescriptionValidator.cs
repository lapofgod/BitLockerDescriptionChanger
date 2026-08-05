namespace BitLockerDescriptionChanger.Services;

public static class DescriptionValidator
{
    public const int MaximumUtf16CodeUnits = 127;

    public static string? Validate(string? candidate, string? original = null)
    {
        var error = ValidateCore(candidate, allowBlank: false);
        if (error is not null)
        {
            return error;
        }

        return original is not null && string.Equals(candidate, original, StringComparison.Ordinal)
            ? "The new description is identical to the current description."
            : null;
    }

    public static string? ValidateRestore(string? candidate, string? current)
    {
        var error = ValidateCore(candidate, allowBlank: true);
        if (error is not null)
        {
            return error;
        }

        return current is not null && string.Equals(candidate, current, StringComparison.Ordinal)
            ? "The backup Description is already active on this volume."
            : null;
    }

    private static string? ValidateCore(string? candidate, bool allowBlank)
    {
        if (candidate is null)
        {
            return "A description is required.";
        }

        if (!allowBlank && (candidate.Length == 0 || string.IsNullOrWhiteSpace(candidate)))
        {
            return "The description cannot be empty or whitespace only.";
        }

        if (candidate.Length > MaximumUtf16CodeUnits)
        {
            return $"The description cannot exceed {MaximumUtf16CodeUnits} UTF-16 code units.";
        }

        if (candidate.Any(character => character == '\0' || char.IsControl(character)))
        {
            return "The description cannot contain nulls or control characters.";
        }

        for (var index = 0; index < candidate.Length; index++)
        {
            var character = candidate[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= candidate.Length || !char.IsLowSurrogate(candidate[index + 1]))
                {
                    return "The description contains an invalid Unicode surrogate.";
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return "The description contains an invalid Unicode surrogate.";
            }
        }

        return null;
    }
}
