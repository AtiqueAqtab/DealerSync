namespace DealerSync.Infrastructure.Exporters;

public static class CsvTextSafety
{
    public static bool IsFormulaLike(string? value)
    {
        if (value is null) return false;
        var first = value.FirstOrDefault(character => !char.IsWhiteSpace(character) && character != '\uFEFF');
        return first is '=' or '+' or '-' or '@';
    }

    // Use only for human-readable diagnostics. Do not alter machine-import identity.
    public static string? Protect(string? value) => IsFormulaLike(value) ? $"'{value}" : value;
}
