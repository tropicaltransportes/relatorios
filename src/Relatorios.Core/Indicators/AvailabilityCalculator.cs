using Relatorios.Core.Domain;
using Relatorios.Core.Sources;

namespace Relatorios.Core.Indicators;

public static class AvailabilityCalculator
{
    private static readonly TimeSpan EligibilityThreshold = TimeSpan.FromHours(4);

    public static MetricValue Calculate(ReportInput input)
    {
        var issues = input.Issues?.ToList() ?? [];
        if (input.Sources.Count == 0 || input.Sources.Any(source => source.LastSuccessAt is null || source.LastError is not null))
            issues.Add(new PendingIssue("source-unavailable", "A cobertura das fontes não está confirmada para este cálculo."));

        var monthStart = OperatingCalendar.MonthStart(input.Month);
        var monthEnd = OperatingCalendar.MonthEnd(input.Month);
        var allAssignments = input.Assignments;
        var assignments = allAssignments
            .Where(assignment => assignment.Contract == input.Contract && Overlaps(assignment.Start, assignment.End ?? input.AsOf, monthStart, monthEnd))
            .ToArray();

        if (assignments.Any(assignment => !assignment.Confirmed))
            issues.Add(new PendingIssue("assignment-unconfirmed", "Há vínculos de veículo e contrato sem confirmação."));

        var confirmedAssignments = assignments.Where(assignment => assignment.Confirmed).ToArray();
        var expectedHours = confirmedAssignments
            .GroupBy(assignment => assignment.Vehicle)
            .Sum(group => CalendarHours(Union(group.Select(assignment => Clip(assignment.Start, assignment.End ?? input.AsOf, monthStart, monthEnd)))));

        var attributableStays = new List<OfficeStay>();
        foreach (var stay in input.Stays)
        {
            if (!stay.Confirmed)
            {
                var couldBelongToContract = stay.OriginContract == input.Contract || stay.OriginContract is null ||
                    allAssignments.Any(assignment => assignment.Vehicle == stay.Vehicle && assignment.Contract == input.Contract &&
                        assignment.Start <= stay.Start && (assignment.End is null || assignment.End > stay.Start));
                if (couldBelongToContract && Overlaps(stay.Start, stay.End ?? input.AsOf, monthStart, monthEnd))
                    issues.Add(new PendingIssue("stay-unconfirmed", "Há intervalo físico de oficina sem confirmação."));
                continue;
            }

            var physicalEnd = stay.End ?? input.AsOf;
            if (physicalEnd <= stay.Start)
            {
                issues.Add(new PendingIssue("invalid-stay-range", "Um intervalo físico termina antes de começar ou não tem duração."));
                continue;
            }

            if (stay.End is null && input.AsOf <= stay.Start) continue;
            if (physicalEnd - stay.Start < EligibilityThreshold) continue;
            ContractId?[] originCandidates = stay.OriginContract is { } explicitOrigin
                ? [explicitOrigin]
                : allAssignments
                .Where(assignment => assignment.Vehicle == stay.Vehicle && assignment.Confirmed && assignment.Start <= stay.Start &&
                    (assignment.End is null || assignment.End > stay.Start))
                .Select(assignment => (ContractId?)assignment.Contract)
                .Distinct()
                .ToArray();

            if (originCandidates.Length != 1)
            {
                if (Overlaps(stay.Start, physicalEnd, monthStart, monthEnd))
                    issues.Add(new PendingIssue("origin-contract-unconfirmed", "Não foi possível confirmar o contrato de origem da parada."));
                continue;
            }
            var origin = originCandidates[0];
            if (origin != input.Contract || !Overlaps(stay.Start, physicalEnd, monthStart, monthEnd)) continue;

            if (!HasContinuousResponsibility(stay.Vehicle, origin.Value, stay.Start, physicalEnd, allAssignments))
            {
                issues.Add(new PendingIssue("origin-responsibility-unconfirmed", "A cobertura confirmada da responsabilidade do contrato de origem não cobre toda a parada."));
                continue;
            }

            attributableStays.Add(stay);
        }

        var unavailableHours = attributableStays
            .GroupBy(stay => stay.Vehicle)
            .Sum(group => CalendarHours(Union(group.Select(stay => Clip(stay.Start, stay.End ?? input.AsOf, monthStart, monthEnd)))));

        if (expectedHours <= 0)
            issues.Add(new PendingIssue("zero-expected-hours", "Não há horas previstas confirmadas para o contrato nesta competência."));
        if (unavailableHours > expectedHours)
            issues.Add(new PendingIssue("availability-integrity", "As horas indisponíveis excedem as horas previstas confirmadas."));

        if (issues.Count > 0 || expectedHours <= 0 || unavailableHours > expectedHours)
            return new MetricValue(null, MetricStatus.Unavailable, issues);

        var percent = decimal.Round((expectedHours - unavailableHours) / expectedHours * 100m, 2, MidpointRounding.AwayFromZero);
        return new MetricValue(percent, MetricStatus.Available, []);
    }

    private static bool HasContinuousResponsibility(VehicleId vehicle, ContractId contract, DateTimeOffset start,
        DateTimeOffset end, IReadOnlyList<Assignment> assignments)
    {
        var cursor = start;
        foreach (var assignment in assignments.Where(item => item.Vehicle == vehicle && item.Contract == contract && item.Confirmed)
                     .OrderBy(item => item.Start))
        {
            if (assignment.Start > cursor) return false;
            var assignmentEnd = assignment.End ?? DateTimeOffset.MaxValue;
            if (assignmentEnd > cursor) cursor = assignmentEnd;
            if (cursor >= end) return true;
        }
        return false;
    }

    private static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset rangeStart, DateTimeOffset rangeEnd) =>
        end > rangeStart && start < rangeEnd;

    private static TimeRange Clip(DateTimeOffset start, DateTimeOffset end, DateTimeOffset rangeStart, DateTimeOffset rangeEnd) =>
        new(start > rangeStart ? start : rangeStart, end < rangeEnd ? end : rangeEnd);

    private static IReadOnlyList<TimeRange> Union(IEnumerable<TimeRange> ranges)
    {
        var ordered = ranges.Where(range => range.End > range.Start).OrderBy(range => range.Start).ToArray();
        if (ordered.Length == 0) return [];
        var merged = new List<TimeRange>();
        var current = ordered[0];
        foreach (var next in ordered.Skip(1))
        {
            if (next.Start <= current.End)
                current = current with { End = next.End > current.End ? next.End : current.End };
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);
        return merged;
    }

    private static decimal CalendarHours(IEnumerable<TimeRange> ranges) => ranges
        .SelectMany(range => OperatingCalendar.Intersect(range.Start, range.End))
        .Sum(range => (decimal)(range.End - range.Start).TotalHours);
}