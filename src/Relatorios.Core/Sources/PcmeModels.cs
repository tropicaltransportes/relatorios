namespace Relatorios.Core.Sources;

public sealed record PcmeWorkOrder(
    string Id,
    string Vehicle,
    string? Type,
    IReadOnlyDictionary<string, object?> OriginalFields,
    decimal? Odometer);

public sealed record PcmeVehicle(
    string Vehicle,
    string? Plate,
    string? Description,
    decimal? CurrentOdometer,
    string? Situation);

public sealed record MileageReading(
    string Vehicle,
    DateTime At,
    decimal Odometer,
    string? OriginalFlag);