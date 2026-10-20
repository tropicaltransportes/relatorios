using Relatorios.Core.Domain;
using Relatorios.Core.Indicators;
using Relatorios.Core.Sources;

namespace Relatorios.Tests;

public sealed class MttrCalculatorTests
{
    [Fact]
    public void AveragesCompletedPhysicalIntervalsIncludingWaitingTime()
    {
        var input = Input(
            Stay("two-hours", "occ-1", At(2026, 10, 5, 6), At(2026, 10, 5, 8)),
            Stay("six-hours", "occ-2", At(2026, 10, 6, 7), At(2026, 10, 6, 13)));

        Assert.Equal(4, MttrCalculator.Calculate(input).Value);
    }

    [Fact]
    public void OpenPhysicalIntervalDoesNotEnterCompletedMttr()
    {
        var result = MttrCalculator.Calculate(Input(Stay("open", "occ-open", At(2026, 10, 5, 6), null)));

        Assert.Equal(MetricStatus.NoOccurrences, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void AdministrativeWorkOrderDurationDoesNotReplacePhysicalInterval()
    {
        var stay = Stay("physical", "occ-1", At(2026, 10, 5, 6), At(2026, 10, 5, 12), TimeSpan.FromHours(48));

        Assert.Equal(6, MttrCalculator.Calculate(Input(stay)).Value);
    }

    [Fact]
    public void InvalidCompletedPhysicalIntervalReturnsPendingInsteadOfNoOccurrences()
    {
        var malformed = Stay("invalid", "occ-invalid", At(2026, 10, 5, 6), At(2026, 10, 5, 5));

        var result = MttrCalculator.Calculate(Input(malformed));

        Assert.Equal(MetricStatus.Unavailable, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-stay-range");
    }

    [Fact]
    public void DuplicateOccurrenceIdIsCountedOnce()
    {
        var start = At(2026, 10, 5, 6);
        var end = At(2026, 10, 5, 12);
        var first = Stay("service-1", "same-occurrence", start, end);
        var duplicate = Stay("service-2", "same-occurrence", start, end);

        var result = MttrCalculator.Calculate(Input([first, duplicate]));

        Assert.Equal(6, result.Value);
    }

    [Fact]
    public void CompetenceUsesPhysicalIntervalStart()
    {
        var previousMonthStay = Stay("cross", "occ-cross", At(2026, 9, 30, 16), At(2026, 10, 1, 4));

        var result = MttrCalculator.Calculate(Input((IReadOnlyList<OfficeStay>)[previousMonthStay], new ReportMonth(2026, 10)));

        Assert.Equal(MetricStatus.NoOccurrences, result.Status);
    }

    private static ReportInput Input(params OfficeStay[] stays) => Input((IReadOnlyList<OfficeStay>)stays, new ReportMonth(2026, 10));

    private static ReportInput Input(IReadOnlyList<OfficeStay> stays, ReportMonth month) =>
        new(new ContractId("contract-a"), month, At(2026, 11, 1, 0), [], stays, [], [], [],
            new ContractTargets(90, 18, 70, null), [new SourceState("PCME", At(2026, 11, 1, 0), null)]);

    private static OfficeStay Stay(string id, string occurrenceId, DateTimeOffset start, DateTimeOffset? end,
        TimeSpan? administrativeDuration = null) =>
        new(id, occurrenceId, new VehicleId("01/150V"), null, start, end, MaintenanceNature.Failure,
            new ContractId("contract-a"), true, AdministrativeDuration: administrativeDuration);

    private static DateTimeOffset At(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.FromHours(-3));
}