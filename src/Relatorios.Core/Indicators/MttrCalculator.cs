using Relatorios.Core.Domain;
using Relatorios.Core.Sources;

namespace Relatorios.Core.Indicators;

public static class MttrCalculator
{
    public static MetricValue Calculate(ReportInput input)
    {
        var issues = input.Issues?.ToList() ?? [];
        if (input.Sources.Count == 0 || input.Sources.Any(source => source.LastSuccessAt is null || source.LastError is not null))
            issues.Add(new PendingIssue("source-unavailable", "A cobertura das fontes não está confirmada para este cálculo."));

        var monthStart = OperatingCalendar.MonthStart(input.Month);
        var monthEnd = OperatingCalendar.MonthEnd(input.Month);
        foreach (var stay in input.Stays.Where(stay => !stay.Confirmed && stay.Start >= monthStart && stay.Start < monthEnd &&
                     (stay.OriginContract == input.Contract || stay.OriginContract is null || IsAssignedToContractAtStart(input, stay))))
            issues.Add(new PendingIssue("stay-unconfirmed", "Há intervalo físico de oficina sem confirmação para esta competência."));

        foreach (var stay in input.Stays.Where(stay => stay.Confirmed && stay.End is not null && stay.End <= stay.Start &&
                     stay.Start >= monthStart && stay.Start < monthEnd &&
                     (stay.OriginContract == input.Contract || (stay.OriginContract is null && IsAssignedToContractAtStart(input, stay)))))
            issues.Add(new PendingIssue("invalid-stay-range", "Um atendimento concluído tem intervalo físico inválido.", stay.Id, stay.Vehicle));

        foreach (var stay in input.Stays.Where(stay => stay.Confirmed && stay.End is not null && stay.Start >= monthStart &&
                     stay.Start < monthEnd && (stay.OriginContract == input.Contract ||
                         (stay.OriginContract is null && IsAssignedToContractAtStart(input, stay))) &&
                     string.IsNullOrWhiteSpace(stay.OccurrenceId)))
            issues.Add(new PendingIssue("occurrence-id-missing", "Um atendimento concluído não tem identificador estável de ocorrência."));

        var occurrences = input.Stays
            .Where(stay => stay.Confirmed && stay.End is not null && stay.End > stay.Start &&
                stay.Start >= monthStart && stay.Start < monthEnd &&
                (stay.OriginContract == input.Contract || (stay.OriginContract is null && IsAssignedToContractAtStart(input, stay))))
            .GroupBy(stay => stay.OccurrenceId, StringComparer.Ordinal)
            .Select(group => OccurrenceHours(group.Select(stay => new TimeRange(stay.Start, stay.End!.Value))))
            .ToArray();

        if (issues.Count > 0) return new MetricValue(null, MetricStatus.Unavailable, issues);
        if (occurrences.Length == 0) return new MetricValue(null, MetricStatus.NoOccurrences, []);

        return new MetricValue(decimal.Round(occurrences.Average(), 2, MidpointRounding.AwayFromZero), MetricStatus.Available, []);
    }

    private static bool IsAssignedToContractAtStart(ReportInput input, OfficeStay stay) => input.Assignments.Any(assignment =>
        assignment.Confirmed && assignment.Vehicle == stay.Vehicle && assignment.Contract == input.Contract &&
        assignment.Start <= stay.Start && (assignment.End is null || assignment.End > stay.Start));

    private static decimal OccurrenceHours(IEnumerable<TimeRange> ranges)
    {
        var ordered = ranges.OrderBy(range => range.Start).ToArray();
        var merged = new List<TimeRange>();
        foreach (var range in ordered)
        {
            if (merged.Count == 0 || range.Start > merged[^1].End)
                merged.Add(range);
            else if (range.End > merged[^1].End)
                merged[^1] = merged[^1] with { End = range.End };
        }
        return merged.Sum(range => (decimal)(range.End - range.Start).TotalHours);
    }
}