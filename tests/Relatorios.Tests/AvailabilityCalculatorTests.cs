using Relatorios.Core.Domain;
using Relatorios.Core.Indicators;
using Relatorios.Core.Sources;

namespace Relatorios.Tests;

public sealed class AvailabilityCalculatorTests
{
    private static readonly ContractId Contract = new("contract-a");
    private static readonly VehicleId Vehicle = new("01/150V");
    private static readonly DateTimeOffset MonthStart = At(2026, 10, 1, 0);

    [Fact]
    public void OperatingCalendarUsesFortalezaAndOnlyMondayThroughSaturday()
    {
        Assert.Equal(6, OperatingCalendar.Intersect(At(2026, 10, 19, 5), At(2026, 10, 19, 11)).Sum(Hours));
        Assert.Equal(4, OperatingCalendar.Intersect(At(2026, 10, 19, 5), At(2026, 10, 19, 9)).Sum(Hours));
        Assert.Equal(3.983333m, decimal.Round(OperatingCalendar.Intersect(At(2026, 10, 19, 5), At(2026, 10, 19, 8).AddMinutes(59)).Sum(Hours), 6));
        Assert.Equal(3, OperatingCalendar.Intersect(At(2026, 10, 19, 18), At(2026, 10, 20, 8)).Sum(Hours));
        Assert.Empty(OperatingCalendar.Intersect(At(2026, 10, 18, 5), At(2026, 10, 18, 17)));
    }

    [Fact]
    public void OverlappingStaysAreUnionedPerVehicleAndContract()
    {
        var input = Input(
            [Stay("one", At(2026, 10, 19, 5), At(2026, 10, 19, 11)),
             Stay("two", At(2026, 10, 19, 8), At(2026, 10, 19, 14))],
            assignments: [new Assignment("one-day", Vehicle, Contract, At(2026, 10, 19, 0), At(2026, 10, 20, 0), true)]);

        var result = AvailabilityCalculator.Calculate(input);

        Assert.Equal(25m, Assert.IsType<decimal>(result.Value));
    }

    [Fact]
    public void FourHoursIsEligibleAndIsNotSubtractedAsAFranchise()
    {
        var oneDay = new[] { new Assignment("one-day", Vehicle, Contract, At(2026, 10, 19, 0), At(2026, 10, 20, 0), true) };
        var fourHours = AvailabilityCalculator.Calculate(Input(Stay("four", At(2026, 10, 19, 5), At(2026, 10, 19, 9)), assignments: oneDay));
        Assert.Equal(66.67m, fourHours.Value);

        var shorter = AvailabilityCalculator.Calculate(Input(Stay("short", At(2026, 10, 19, 5), At(2026, 10, 19, 8).AddMinutes(59)), assignments: oneDay));
        Assert.Equal(100, shorter.Value);
    }

    [Fact]
    public void CrossMonthSplitsUnavailableHours()
    {
        var stay = Stay("month-cross", At(2026, 10, 31, 15), At(2026, 11, 2, 7));

        var october = AvailabilityCalculator.Calculate(Input(stay));
        var november = AvailabilityCalculator.Calculate(Input(stay, new ReportMonth(2026, 11)));

        Assert.Equal(99.38m, october.Value);
        Assert.Equal(99.33m, november.Value);
    }

    [Fact]
    public void MidMonthAssignmentChangesExpectedHours()
    {
        var assignment = new Assignment("half-month", Vehicle, Contract,
            At(2026, 10, 15, 0), At(2026, 10, 16, 0), true);
        var result = AvailabilityCalculator.Calculate(Input([], assignments: [assignment]));

        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void TransferWithoutOriginResponsibilityCoverageReturnsPending()
    {
        var first = new Assignment("old", Vehicle, Contract, At(2026, 10, 1, 0), At(2026, 10, 20, 6), true);
        var second = new Assignment("new", Vehicle, new ContractId("contract-b"), At(2026, 10, 20, 6), null, true);
        var stay = Stay("transfer", At(2026, 10, 20, 5), At(2026, 10, 20, 11));

        var result = AvailabilityCalculator.Calculate(Input([stay], assignments: [first, second]));

        Assert.Equal(MetricStatus.Unavailable, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "origin-responsibility-unconfirmed");
    }

    [Fact]
    public void BelowEligibilityThresholdDoesNotRequireOriginResponsibilityCoverage()
    {
        var first = new Assignment("old", Vehicle, Contract, At(2026, 10, 19, 0), At(2026, 10, 19, 6), true);
        var second = new Assignment("new", Vehicle, new ContractId("contract-b"), At(2026, 10, 19, 6), null, true);
        var stay = Stay("short-transfer", At(2026, 10, 19, 5), At(2026, 10, 19, 8));

        var result = AvailabilityCalculator.Calculate(Input([stay], assignments: [first, second]));

        Assert.Equal(MetricStatus.Available, result.Status);
        Assert.Equal(100m, result.Value);
    }

    [Fact]
    public void MissingSourceDoesNotLookLikeFullAvailability()
    {
        var input = Input([], sources: [new SourceState("PCME", null, "Falha de leitura")]);

        var result = AvailabilityCalculator.Calculate(input);

        Assert.Equal(MetricStatus.Unavailable, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "source-unavailable");
    }

    [Fact]
    public void ZeroExpectedHoursIsUnavailable()
    {
        var result = AvailabilityCalculator.Calculate(Input([], assignments: []));

        Assert.Equal(MetricStatus.Unavailable, result.Status);
        Assert.Null(result.Value);
    }

    private static ReportInput Input(OfficeStay stay, ReportMonth? month = null, Assignment[]? assignments = null,
        SourceState[]? sources = null) => Input([stay], month, assignments, sources);

    private static ReportInput Input(OfficeStay[] stays, ReportMonth? month = null, Assignment[]? assignments = null,
        SourceState[]? sources = null) => new(Contract, month ?? new ReportMonth(2026, 10),
        At(2026, 12, 1, 0), assignments ?? [new Assignment("full", Vehicle, Contract, At(2026, 10, 1, 0), null, true)],
        stays, [], [], [], new ContractTargets(90, 18, 70, null), sources ?? [new SourceState("PCME", At(2026, 12, 1, 0), null)]);

    private static OfficeStay Stay(string id, DateTimeOffset start, DateTimeOffset end) =>
        new(id, id, Vehicle, null, start, end, MaintenanceNature.Failure, Contract, true);

    private static DateTimeOffset At(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.FromHours(-3));

    private static decimal Hours(TimeRange range) => (decimal)(range.End - range.Start).TotalHours;
}