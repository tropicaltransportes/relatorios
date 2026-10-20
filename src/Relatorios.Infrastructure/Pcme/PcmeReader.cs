using System.Data.Common;
using Relatorios.Core.Domain;
using Relatorios.Core.Sources;
using FirebirdSql.Data.FirebirdClient;

namespace Relatorios.Infrastructure.Pcme;

public sealed record PcmeFieldEvidence(string Entity, string Field, string Purpose, bool OperationallyValidated);

public sealed class PcmeReader
{
    private static readonly string[] WorkOrderCandidates =
    [
        "OS", "EQUIPAMENTO", "TIPO", "DATA", "HORA_INI", "DATA_FIM", "HORA_FIM", "DATA_ENCERRA",
        "DATA_TEM_INI", "HORA_TEM_INI", "DATA_TEM_FIM", "HORA_TEM_FIM", "HORIMETRO", "ENCERRADA",
        "REABERTURA", "PRE_LIBERACAO", "SITUACAO_EQP", "TIPO_SERVICO", "NUMCHAMADA"
    ];

    private static readonly string[] VehicleCandidates =
        ["EQUIPAMENTO", "PLACA", "DESCRICAO", "HORIMETRO_ATUAL", "SITUACAO_EQP"];

    private static readonly string[] MileageCandidates = ["EQUIPAMENTO", "DATA", "HORA", "HORIMETRO", "FLAG"];

    public static IReadOnlyList<PcmeFieldEvidence> CandidateFieldEvidence =>
    [
        new("EQUIPAMENTOS", "EQUIPAMENTO", "Identificador do veículo", false),
        new("EQUIPAMENTOS", "PLACA", "Placa do veículo", false),
        new("ORDENS_DE_SERVICOS", "OS", "Identificador da OS", false),
        new("ORDENS_DE_SERVICOS", "TIPO", "Classificação candidata da OS", false),
        new("ORDENS_DE_SERVICOS", "DATA_TEM_INI", "Início físico candidato; requer validação", false),
        new("ORDENS_DE_SERVICOS", "DATA_TEM_FIM", "Liberação física candidata; requer validação", false)
    ];

    public Task<SourceSnapshot<PcmeWorkOrder>> ReadWorkOrdersAsync(
        ConnectionOptions options, ReportMonth month, CancellationToken cancellationToken = default) =>
        ReadSnapshotAsync(options, "PCME - ordens de serviço", async (connection, transaction, ct) =>
        {
            var available = await ReadColumnNamesAsync(connection, transaction, "ORDENS_DE_SERVICOS", ct);
            var selected = WorkOrderCandidates.Where(available.Contains).ToArray();
            RequireFields("ORDENS_DE_SERVICOS", available, "OS", "EQUIPAMENTO", "TIPO", "DATA", "DATA_FIM");

            await using var command = new FbCommand(
                $"SELECT {string.Join(", ", selected)} FROM ORDENS_DE_SERVICOS " +
                "WHERE DATA < @periodEnd AND (DATA_FIM IS NULL OR DATA_FIM >= @periodStart) " +
                "ORDER BY DATA, OS", connection, transaction)
            {
                CommandTimeout = options.TimeoutSeconds
            };
            command.Parameters.AddWithValue("@periodStart", month.Start);
            command.Parameters.AddWithValue("@periodEnd", month.EndExclusive);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var records = new List<PcmeWorkOrder>();
            while (await reader.ReadAsync(ct))
            {
                var fields = ReadFields(reader);
                var id = ReadText(reader, "OS");
                var vehicle = ReadText(reader, "EQUIPAMENTO");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(vehicle))
                    throw new InvalidDataException("A leitura da OS contém identificador ou veículo vazio.");
                records.Add(new PcmeWorkOrder(id, vehicle, ReadText(reader, "TIPO"), fields,
                    ReadDecimal(reader, "HORIMETRO")));
            }
            return records;
        }, cancellationToken);

    public Task<SourceSnapshot<PcmeVehicle>> ReadVehiclesAsync(
        ConnectionOptions options, CancellationToken cancellationToken = default) =>
        ReadSnapshotAsync(options, "PCME - veículos", async (connection, transaction, ct) =>
        {
            var available = await ReadColumnNamesAsync(connection, transaction, "EQUIPAMENTOS", ct);
            RequireFields("EQUIPAMENTOS", available, "EQUIPAMENTO");
            var selected = VehicleCandidates.Where(available.Contains).ToArray();
            await using var command = new FbCommand(
                $"SELECT {string.Join(", ", selected)} FROM EQUIPAMENTOS ORDER BY EQUIPAMENTO", connection, transaction)
            {
                CommandTimeout = options.TimeoutSeconds
            };
            await using var reader = await command.ExecuteReaderAsync(ct);
            var records = new List<PcmeVehicle>();
            while (await reader.ReadAsync(ct))
            {
                var vehicle = ReadText(reader, "EQUIPAMENTO");
                if (!string.IsNullOrWhiteSpace(vehicle))
                    records.Add(new PcmeVehicle(vehicle, ReadText(reader, "PLACA"), ReadText(reader, "DESCRICAO"),
                        ReadDecimal(reader, "HORIMETRO_ATUAL"), ReadText(reader, "SITUACAO_EQP")));
                else
                    throw new InvalidDataException("A leitura de veículos contém identificador vazio.");
            }
            return records;
        }, cancellationToken);

    public Task<SourceSnapshot<MileageReading>> ReadMileageReadingsAsync(
        ConnectionOptions options, ReportMonth month, CancellationToken cancellationToken = default) =>
        ReadSnapshotAsync(options, "PCME - leituras de hodômetro", async (connection, transaction, ct) =>
        {
            var available = await ReadColumnNamesAsync(connection, transaction, "HORIMETRO", ct);
            RequireFields("HORIMETRO", available, "EQUIPAMENTO", "DATA", "HORIMETRO");
            var selected = MileageCandidates.Where(available.Contains).ToArray();
            await using var command = new FbCommand(
                $"SELECT {string.Join(", ", selected)} FROM HORIMETRO " +
                "WHERE DATA >= @periodStart AND DATA < @periodEnd ORDER BY DATA, EQUIPAMENTO", connection, transaction)
            {
                CommandTimeout = options.TimeoutSeconds
            };
            command.Parameters.AddWithValue("@periodStart", month.Start);
            command.Parameters.AddWithValue("@periodEnd", month.EndExclusive);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var records = new List<MileageReading>();
            while (await reader.ReadAsync(ct))
            {
                var vehicle = ReadText(reader, "EQUIPAMENTO");
                var at = ReadDateTime(reader, "DATA");
                var odometer = ReadDecimal(reader, "HORIMETRO");
                if (string.IsNullOrWhiteSpace(vehicle) || at is null || odometer is null)
                    throw new InvalidDataException("A leitura de hodômetro contém veículo, data ou valor inválido.");
                var time = ReadTime(reader, "HORA");
                records.Add(new MileageReading(vehicle, at.Value.Date + time, odometer.Value, ReadText(reader, "FLAG")));
            }
            return records;
        }, cancellationToken);

    private static async Task<SourceSnapshot<T>> ReadSnapshotAsync<T>(
        ConnectionOptions options, string sourceName,
        Func<FbConnection, FbTransaction, CancellationToken, Task<List<T>>> read,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new FbConnection(options.ToBuilder().ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(ConnectionProbe.CreateReadOnlyTransactionOptions(), cancellationToken);
            var records = await read(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(records, new SourceState(sourceName, DateTimeOffset.UtcNow, null), [], true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new([], new SourceState(sourceName, null, "Não foi possível atualizar esta fonte. O último carregamento válido foi preservado."),
                [new PendingIssue("PCME_READ_FAILED", "Falha de leitura; os dados atuais não foram substituídos.")], false);
        }
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(
        FbConnection connection, FbTransaction transaction, string relation, CancellationToken cancellationToken)
    {
        await using var command = new FbCommand(
            "SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @relation " +
            "ORDER BY RDB$FIELD_POSITION", connection, transaction);
        command.Parameters.AddWithValue("@relation", relation);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
            if (!reader.IsDBNull(0)) result.Add(reader.GetString(0).Trim());
        return result;
    }

    private static void RequireFields(string relation, HashSet<string> available, params string[] required)
    {
        var missing = required.Where(field => !available.Contains(field)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Estrutura incompleta para {relation}.");
    }

    private static Dictionary<string, object?> ReadFields(DbDataReader reader)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
            fields[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return fields;
    }

    private static string? ReadText(DbDataReader reader, string name)
    {
        var ordinal = FindOrdinal(reader, name);
        return ordinal < 0 || reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture)?.Trim();
    }

    private static decimal? ReadDecimal(DbDataReader reader, string name)
    {
        var ordinal = FindOrdinal(reader, name);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        try { return Convert.ToDecimal(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture); }
        catch (FormatException) { return null; }
        catch (InvalidCastException) { return null; }
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string name)
    {
        var ordinal = FindOrdinal(reader, name);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return null;
        try { return Convert.ToDateTime(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture); }
        catch (FormatException) { return null; }
        catch (InvalidCastException) { return null; }
    }

    private static TimeSpan ReadTime(DbDataReader reader, string name)
    {
        var ordinal = FindOrdinal(reader, name);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return TimeSpan.Zero;
        var value = reader.GetValue(ordinal);
        if (value is TimeSpan time) return time;
        if (value is DateTime dateTime) return dateTime.TimeOfDay;
        if (value is TimeOnly timeOnly) return timeOnly.ToTimeSpan();
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        return TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException("A leitura de hodômetro contém hora inválida.");
    }

    private static int FindOrdinal(DbDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
            if (reader.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}