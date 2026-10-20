using Relatorios.Core.Domain;

namespace Relatorios.Core.Indicators;

public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);

public static class OperatingCalendar
{
    public const string IanaTimeZoneId = "America/Fortaleza";
    private static readonly TimeZoneInfo OperationTimeZone = ResolveTimeZone();

    public static IReadOnlyList<TimeRange> Intersect(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start) return [];

        var firstDate = TimeZoneInfo.ConvertTime(start, OperationTimeZone).Date;
        var lastDate = TimeZoneInfo.ConvertTime(end, OperationTimeZone).Date;
        var result = new List<TimeRange>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Sunday) continue;

            var windowStart = ToOperationInstant(date.AddHours(5));
            var windowEnd = ToOperationInstant(date.AddHours(17));
            var intersectionStart = start > windowStart ? start : windowStart;
            var intersectionEnd = end < windowEnd ? end : windowEnd;
            if (intersectionEnd > intersectionStart)
                result.Add(new TimeRange(intersectionStart, intersectionEnd));
        }
        return result;
    }

    public static DateTimeOffset ToOperationTime(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, OperationTimeZone);

    public static DateTimeOffset MonthStart(ReportMonth month) =>
        ToOperationInstant(new DateTime(month.Year, month.Month, 1));

    public static DateTimeOffset MonthEnd(ReportMonth month) =>
        ToOperationInstant(new DateTime(month.Year, month.Month, 1).AddMonths(1));

    private static DateTimeOffset ToOperationInstant(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        var offset = OperationTimeZone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(IanaTimeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SA Eastern Standard Time"); }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.FindSystemTimeZoneById("SA Eastern Standard Time"); }
    }
}