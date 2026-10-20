using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Relatorios.Core.Sources;

namespace Relatorios.Infrastructure.Imports;

internal static class ImportParsing
{
    public static SourceSnapshot<T> Failure<T>(string source, string code, string message, DateTimeOffset? lastSuccess = null,
        string? lastError = null, IReadOnlyList<T>? preserved = null) =>
        new(preserved ?? [], new SourceState(source, lastSuccess, lastError ?? message), [new PendingIssue(code, message)], false);

    public static SourceSnapshot<T> ReadFailure<T>(string source, Exception error, SourceSnapshot<T>? previous) =>
        Failure(source, error is NotSupportedException ? "unsupported_tabular_format" : "tabular_read_failure",
            error is NotSupportedException ? "O formato de planilha selecionado não é suportado." : "O arquivo não pôde ser lido integralmente; o snapshot anterior foi mantido.",
            previous?.State.LastSuccessAt, error is NotSupportedException ? "Formato não suportado." : "Falha na leitura do arquivo.", previous?.Records);

    public static DateTimeOffset? Date(object? value)
    {
        if (value is DateTime date) return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(-3));
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        var formats = new[] { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy H:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm", "dd/MM/yyyy", "d/M/yyyy HH:mm:ss", "d/M/yyyy HH:mm", "d/M/yyyy" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.AllowWhiteSpaces, out date) ||
            DateTime.TryParse(text, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.AllowWhiteSpaces, out date) ||
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(-3));
        return null;
    }

    public static bool Decimal(object? value, out decimal result)
    {
        if (value is decimal decimalValue) { result = decimalValue; return true; }
        if (value is double doubleValue) { result = Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture); return true; }
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out result) ||
               decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    public static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..20];
    public static string CanonicalStatus(string raw) => raw.Trim().ToUpperInvariant() switch
    {
        "FINALIZADO" => "FINALIZADO",
        "PENDENTE" => "PENDENTE",
        "EM ANDAMENTO" => "EM ANDAMENTO",
        _ => raw.Trim()
    };
}