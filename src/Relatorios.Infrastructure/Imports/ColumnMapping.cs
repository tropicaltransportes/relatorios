namespace Relatorios.Infrastructure.Imports;

public sealed record ColumnMapping(IReadOnlyDictionary<string, string> Columns)
{
    public bool TryResolve(TabularData data, IEnumerable<string> requiredRoles,
        out IReadOnlyDictionary<string, int> indexes, out IReadOnlyList<string> missing)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var absent = new List<string>();
        foreach (var role in requiredRoles)
        {
            if (!Columns.TryGetValue(role, out var header) || string.IsNullOrWhiteSpace(header))
            {
                absent.Add(role);
                continue;
            }
            var index = data.Headers.Select((name, position) => (name, position))
                .FirstOrDefault(x => string.Equals(x.name.Trim(), header.Trim(), StringComparison.OrdinalIgnoreCase)).position;
            if (!data.Headers.Any(name => string.Equals(name.Trim(), header.Trim(), StringComparison.OrdinalIgnoreCase))) absent.Add(role);
            else result[role] = index;
        }
        indexes = result;
        missing = absent;
        return absent.Count == 0;
    }
}

public sealed record OperationRecord(string SourceId, Relatorios.Core.Domain.VehicleId Vehicle,
    DateTimeOffset At, string ContractLabel, decimal Odometer);