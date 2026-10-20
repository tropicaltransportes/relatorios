using Relatorios.Core.Sources;

namespace Relatorios.Core.Domain;

public readonly record struct VehicleId(string Value);
public readonly record struct ContractId(string Value);

public sealed record ReportMonth(int Year, int Month)
{
    public DateTime Start => new(Year, Month, 1);
    public DateTime EndExclusive => Start.AddMonths(1);

    public static bool TryParse(string? value, out ReportMonth month)
    {
        month = default!;
        if (!DateTime.TryParseExact(value, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
            return false;

        month = new ReportMonth(parsed.Year, parsed.Month);
        return true;
    }
}

public sealed record Assignment(
    string Id,
    VehicleId Vehicle,
    ContractId Contract,
    DateTimeOffset Start,
    DateTimeOffset? End,
    bool Confirmed);

public enum MaintenanceNature { Failure, Planned, Unknown }

public sealed record OfficeStay(
    string Id,
    string OccurrenceId,
    VehicleId Vehicle,
    string? WorkOrderId,
    DateTimeOffset Start,
    DateTimeOffset? End,
    MaintenanceNature Nature,
    ContractId? OriginContract,
    bool Confirmed,
    TimeSpan? AdministrativeDuration = null);

public sealed record ContractTargets(decimal AvailabilityMinimum, decimal MttrMaximumHours,
    decimal RequestsMinimum, decimal? MkbfMinimum);

public enum MetricStatus { Available, Unavailable, NoOccurrences }

public sealed record MetricValue(decimal? Value, MetricStatus Status, IReadOnlyList<PendingIssue> Issues);

public sealed record ReportInput(
    ContractId Contract,
    ReportMonth Month,
    DateTimeOffset AsOf,
    IReadOnlyList<Assignment> Assignments,
    IReadOnlyList<OfficeStay> Stays,
    IReadOnlyList<MileageReading> Readings,
    IReadOnlyList<MileageTotal> ConfirmedMileage,
    IReadOnlyList<MaintenanceRequest> Requests,
    ContractTargets Targets,
    IReadOnlyList<SourceState> Sources,
    IReadOnlyList<PendingIssue>? Issues = null);

public sealed record MileageTotal(VehicleId Vehicle, DateTimeOffset Start, DateTimeOffset End,
    decimal Kilometers, bool Confirmed, string SourceId);

public sealed record MaintenanceRequest(string SourceId, VehicleId Vehicle, DateTimeOffset OpenedAt,
    string Status, string Description, string? ContentRevision = null, bool PossibleResubmission = false);

public sealed record ReportComments(string Summary, IReadOnlyDictionary<string, string> VehicleNotes, string NextActions);

public sealed record VehicleRankingRow(VehicleId Vehicle, decimal UnavailableHours, string? MainCause);

public sealed record ReportResult(ContractId Contract, ReportMonth Month, MetricValue AvailabilityPercent,
    MetricValue MttrHours, MetricValue MkbfKilometers, MetricValue RequestsPercent,
    IReadOnlyList<VehicleRankingRow> Ranking, IReadOnlyList<PendingIssue> Issues,
    IReadOnlyList<SourceState> Sources, ContractTargets Targets);