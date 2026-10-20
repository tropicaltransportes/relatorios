using Relatorios.Core.Domain;
using Relatorios.Core.Sources;

namespace Relatorios.Infrastructure.Imports;

public static class OperationImporter
{
    private static readonly string[] Required = ["vehicle", "at", "contract", "odometer"];

    public static SourceSnapshot<OperationRecord> Import(Stream input, string extension, ColumnMapping mapping,
        SourceSnapshot<OperationRecord>? previous = null)
    {
        try { return Import(TabularReader.Read(input, extension), mapping, previous); }
        catch (Exception error) when (error is IOException or NotSupportedException or System.Xml.XmlException or UnauthorizedAccessException)
        { return ImportParsing.ReadFailure("planilha de operação", error, previous); }
    }

    public static SourceSnapshot<OperationRecord> Import(TabularData data, ColumnMapping mapping,
        SourceSnapshot<OperationRecord>? previous = null)
    {
        if (!mapping.TryResolve(data, Required, out var indexes, out var missing))
            return ImportParsing.Failure<OperationRecord>("planilha de operação", "column_mapping_missing",
                $"Mapeie explicitamente as colunas obrigatórias: {string.Join(", ", missing)}.",
                previous?.State.LastSuccessAt, preserved: previous?.Records);

        var records = new List<OperationRecord>();
        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var row = data.Rows[rowIndex];
            var vehicle = ImportParsing.Text(Cell(row, indexes["vehicle"]));
            var contract = ImportParsing.Text(Cell(row, indexes["contract"]));
            var at = ImportParsing.Date(Cell(row, indexes["at"]));
            var odometerValue = Cell(row, indexes["odometer"]);
            if (vehicle.Length == 0 || contract.Length == 0 || at is null || !ImportParsing.Decimal(odometerValue, out var odometer) || odometer < 0)
                return ImportParsing.Failure<OperationRecord>("planilha de operação", "invalid_operation_row",
                    $"A linha {rowIndex + 2} não contém veículo, contrato, data ou hodômetro válido.",
                    previous?.State.LastSuccessAt, preserved: previous?.Records);

            var canonical = string.Join("|", vehicle, at.Value.ToString("O"), contract, odometer.ToString(System.Globalization.CultureInfo.InvariantCulture));
            records.Add(new OperationRecord("operation:" + ImportParsing.Hash(canonical), new VehicleId(vehicle), at.Value, contract, odometer));
        }
        return new SourceSnapshot<OperationRecord>(records, new SourceState("planilha de operação", DateTimeOffset.Now, null), [], true);
    }

    private static object? Cell(IReadOnlyList<object?> row, int index) => index < row.Count ? row[index] : null;
}