namespace Relatorios.Core.Sources;

public sealed record SourceState(string Name, DateTimeOffset? LastSuccessAt, string? LastError);

public sealed record PendingIssue(string Code, string Message, string? RecordId = null,
    Relatorios.Core.Domain.VehicleId? Vehicle = null);

public sealed record SourceSnapshot<T>(
    IReadOnlyList<T> Records,
    SourceState State,
    IReadOnlyList<PendingIssue> Issues,
    bool Succeeded);