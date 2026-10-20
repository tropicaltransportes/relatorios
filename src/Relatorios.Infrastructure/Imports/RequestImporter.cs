using System.Text.RegularExpressions;
using Relatorios.Core.Domain;
using Relatorios.Core.Sources;

namespace Relatorios.Infrastructure.Imports;

public static class RequestImporter
{
    private static readonly string[] Required = ["vehicle", "openedAt", "status", "description"];

    public static SourceSnapshot<MaintenanceRequest> Import(Stream input, string extension, ColumnMapping mapping,
        string source = "arquivo de solicitações", SourceSnapshot<MaintenanceRequest>? previous = null)
    {
        try { return Import(TabularReader.Read(input, extension), mapping, source, previous); }
        catch (Exception error) when (error is IOException or NotSupportedException or System.Xml.XmlException or UnauthorizedAccessException)
        { return ImportParsing.ReadFailure(source, error, previous); }
    }

    public static SourceSnapshot<MaintenanceRequest> Import(TabularData data, ColumnMapping mapping,
        string source = "arquivo de solicitações", SourceSnapshot<MaintenanceRequest>? previous = null)
    {
        if (!mapping.TryResolve(data, Required, out var indexes, out var missing))
            return ImportParsing.Failure<MaintenanceRequest>(source, "column_mapping_missing",
                $"Mapeie explicitamente as colunas obrigatórias: {string.Join(", ", missing)}.",
                previous?.State.LastSuccessAt, preserved: previous?.Records);

        var sourceKey = ImportParsing.Hash(NormalizeSource(source));
        var raw = new List<(VehicleId Vehicle, DateTimeOffset OpenedAt, string Status, string Description, int Row)>();
        var issues = new List<PendingIssue>();
        for (var i = 0; i < data.Rows.Count; i++)
        {
            var row = data.Rows[i];
            var vehicleText = ImportParsing.Text(Cell(row, indexes["vehicle"]));
            var openedAt = ImportParsing.Date(Cell(row, indexes["openedAt"]));
            var status = ImportParsing.CanonicalStatus(ImportParsing.Text(Cell(row, indexes["status"])));
            var description = ImportParsing.Text(Cell(row, indexes["description"]));
            if (vehicleText.Length == 0 || openedAt is null || description.Length == 0)
                return ImportParsing.Failure<MaintenanceRequest>(source, "invalid_request_row",
                    $"A linha {i + 2} não contém veículo, carimbo de abertura ou descrição válida.",
                    previous?.State.LastSuccessAt, preserved: previous?.Records);
            if (status is not ("FINALIZADO" or "PENDENTE" or "EM ANDAMENTO"))
                issues.Add(new PendingIssue("unexpected_status", $"A situação '{status}' da linha {i + 2} não é reconhecida; será mantida como não concluída."));
            raw.Add((new VehicleId(vehicleText), openedAt.Value, status, description, i + 2));
        }

        var collisionGroups = raw.GroupBy(r => (r.Vehicle.Value, r.OpenedAt)).Where(g => g.Count() > 1).ToArray();
        foreach (var group in collisionGroups)
            issues.Add(new PendingIssue("request_identity_collision",
                $"Há {group.Count()} pedidos para o mesmo veículo e carimbo (linhas {string.Join(", ", group.Select(item => item.Row))}). Revise-os na fonte antes de importar; nenhum deles foi unido.",
                $"{group.Key.Value}:{group.Key.OpenedAt:O}", new VehicleId(group.Key.Value)));
        if (collisionGroups.Length > 0)
        {
            const string error = "A importação contém colisões de identidade que precisam ser resolvidas na fonte.";
            return new SourceSnapshot<MaintenanceRequest>(previous?.Records ?? [],
                new SourceState(source, previous?.State.LastSuccessAt, error), issues, false);
        }

        var records = new List<MaintenanceRequest>();
        foreach (var item in raw)
        {
            var identity = $"request:{sourceKey}:{item.OpenedAt:O}:{item.Vehicle.Value}";
            var contentRevision = ImportParsing.Hash(item.Status + "\u001f" + item.Description);
            var sourceId = identity;
            var possibleResubmission = raw.Any(other => other.Vehicle == item.Vehicle && other.OpenedAt != item.OpenedAt &&
                Math.Abs((other.OpenedAt - item.OpenedAt).TotalDays) <= 30 && Similar(item.Description, other.Description));
            if (possibleResubmission)
                issues.Add(new PendingIssue("possible_resubmission",
                    "Este pedido é semelhante a outro envio do mesmo veículo e deve ser conferido; nenhum deles foi removido.",
                    sourceId, item.Vehicle));
            records.Add(new MaintenanceRequest(sourceId, item.Vehicle, item.OpenedAt, item.Status, item.Description,
                contentRevision, possibleResubmission));
        }

        var success = new SourceSnapshot<MaintenanceRequest>(records, new SourceState(source, DateTimeOffset.Now, null), issues, true);
        return success;
    }

    private static object? Cell(IReadOnlyList<object?> row, int index) => index < row.Count ? row[index] : null;
    private static string NormalizeSource(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return source.Trim();
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
    }

    private static bool Similar(string left, string right)
    {
        static HashSet<string> Tokens(string value) => Regex.Matches(value.ToLowerInvariant(), "[\\p{L}\\p{N}]+").Select(m => m.Value).ToHashSet();
        var a = Tokens(left); var b = Tokens(right);
        if (a.Count == 0 || b.Count == 0) return false;
        return a.Intersect(b).Count() / (double)a.Union(b).Count() >= 0.5;
    }
}