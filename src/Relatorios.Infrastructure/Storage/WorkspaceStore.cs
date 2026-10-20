using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Relatorios.Infrastructure.Storage;

public sealed record WorkspaceSource(
    string Name,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    JsonElement[]? Snapshot,
    IReadOnlyList<Relatorios.Core.Sources.PendingIssue> Issues);

public sealed record WorkspaceDraft(string Contract, string Month, IReadOnlyList<WorkspaceSource> Sources,
    string Notes = "", long Revision = 0)
{
    public bool Matches(string? contract, string? month) =>
        contract is not null && month is not null &&
        string.Equals(Contract.Trim(), contract.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Month, month, StringComparison.Ordinal);
}

public sealed record DraftSaveResult(bool Saved, long Revision);

public sealed record WorkspaceState(
    int SchemaVersion,
    string? Contract,
    string? Month,
    IReadOnlyList<WorkspaceSource> Sources,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkspaceDraft>? Drafts = null)
{
    public static WorkspaceState Empty => new(2, null, null, [], DateTimeOffset.UtcNow, []);

    [JsonIgnore]
    public string ActiveDraftNotes => Drafts?.FirstOrDefault(draft =>
        draft.Matches(Contract, Month))?.Notes ?? string.Empty;

    [JsonIgnore]
    public long ActiveDraftRevision => Drafts?.FirstOrDefault(draft => draft.Matches(Contract, Month))?.Revision ?? 0;
}

public sealed class WorkspaceStore
{
    private const int CurrentSchemaVersion = 2;
    private readonly string statePath;
    private readonly string processGateName;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public WorkspaceStore(string? root = null)
    {
        var dataRoot = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tropical", "Relatorios");
        statePath = Path.Combine(dataRoot, "workspace.json");
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(statePath).ToUpperInvariant())))[..24];
        processGateName = $"Local\\Tropical.Relatorios.Workspace.{identity}";
    }

    public async Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadFromDiskAsync(cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task SaveSelectionAsync(string contract, string month, CancellationToken cancellationToken = default)
    {
        await WithWriteLockAsync(async () =>
        {
            var state = await LoadFromDiskAsync(cancellationToken);
            var drafts = UpsertDraft(state.Drafts ?? [], state);
            var existing = drafts.FirstOrDefault(draft => DraftKeyEquals(draft.Contract, draft.Month, contract, month));
            var selected = existing ?? new WorkspaceDraft(contract.Trim(), month, []);
            drafts = UpsertDraft(drafts, selected);
            await WriteAtomicallyAsync(state with
            {
                SchemaVersion = CurrentSchemaVersion,
                Contract = selected.Contract,
                Month = selected.Month,
                Sources = selected.Sources,
                Drafts = drafts,
                UpdatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<DraftSaveResult> SaveDraftNotesAsync(string contract, string month, string notes,
        long expectedRevision, CancellationToken cancellationToken = default)
    {
        return await WithWriteLockAsync(async () =>
        {
            var state = await LoadFromDiskAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(contract) || string.IsNullOrWhiteSpace(month))
                return new DraftSaveResult(false, 0);

            var drafts = state.Drafts ?? [];
            var current = drafts.FirstOrDefault(draft => DraftKeyEquals(draft.Contract, draft.Month, contract, month));
            var revision = current?.Revision ?? 0;
            if (revision != expectedRevision) return new DraftSaveResult(false, revision);

            current ??= new WorkspaceDraft(contract.Trim(), month, []);
            var updatedDraft = current with { Notes = notes, Revision = revision + 1 };
            await WriteAtomicallyAsync(state with
            {
                SchemaVersion = CurrentSchemaVersion,
                Drafts = UpsertDraft(drafts, updatedDraft),
                UpdatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
            return new DraftSaveResult(true, updatedDraft.Revision);
        }, cancellationToken);
    }

    public async Task RecordSuccessAsync(string sourceName, JsonElement snapshot, DateTimeOffset at,
        IReadOnlyList<Relatorios.Core.Sources.PendingIssue>? issues = null,
        CancellationToken cancellationToken = default, string? contract = null, string? month = null)
    {
        var records = snapshot.ValueKind == JsonValueKind.Array
            ? snapshot.EnumerateArray().Select(item => item.Clone()).ToArray()
            : [snapshot.Clone()];
        await UpdateAsync(state =>
        {
            if (!MatchesActiveSelection(state, contract, month)) return state;
            var prior = state.Sources.FirstOrDefault(source => source.Name == sourceName);
            var current = new WorkspaceSource(sourceName, at, null, records, issues ?? []);
            return state with { Sources = ReplaceOrAdd(state.Sources, prior, current) };
        }, cancellationToken);
    }

    public async Task RecordFailureAsync(string sourceName, CancellationToken cancellationToken = default,
        string? contract = null, string? month = null)
    {
        const string safeError = "Não foi possível atualizar esta fonte. Confira a configuração e tente novamente.";
        await UpdateAsync(state =>
        {
            if (!MatchesActiveSelection(state, contract, month)) return state;
            var prior = state.Sources.FirstOrDefault(source => source.Name == sourceName);
            var current = prior is null
                ? new WorkspaceSource(sourceName, null, safeError, null, [])
                : prior with { LastError = safeError };
            return state with { Sources = ReplaceOrAdd(state.Sources, prior, current) };
        }, cancellationToken);
    }

    private async Task UpdateAsync(Func<WorkspaceState, WorkspaceState> update, CancellationToken cancellationToken)
    {
        await WithWriteLockAsync(async () =>
        {
            var state = await LoadFromDiskAsync(cancellationToken);
            var updated = update(state with { SchemaVersion = CurrentSchemaVersion, UpdatedAt = DateTimeOffset.UtcNow });
            updated = updated with { Drafts = UpsertDraft(updated.Drafts ?? [], updated) };
            await WriteAtomicallyAsync(updated, cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<T> WithWriteLockAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        using var processGate = new Semaphore(1, 1, processGateName);
        var processGateHeld = false;
        try
        {
            WaitForProcessGate(processGate, cancellationToken);
            processGateHeld = true;
            return await operation();
        }
        finally
        {
            if (processGateHeld) processGate.Release();
            gate.Release();
        }
    }

    private static void WaitForProcessGate(Semaphore processGate, CancellationToken cancellationToken)
    {
        var selected = WaitHandle.WaitAny([processGate, cancellationToken.WaitHandle]);
        if (selected == 1) throw new OperationCanceledException(cancellationToken);
    }

    private async Task<WorkspaceState> LoadFromDiskAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath)) return WorkspaceState.Empty;
        await using var stream = new FileStream(statePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var state = await JsonSerializer.DeserializeAsync<WorkspaceState>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("O arquivo local do workspace está vazio.");
        if (state.SchemaVersion == 1)
        {
            var legacySources = state.Sources.Select(source => source with { Issues = source.Issues ?? [] }).ToArray();
            var drafts = state.Contract is not null && state.Month is not null
                ? new[] { new WorkspaceDraft(state.Contract, state.Month, legacySources) }
                : [];
            state = state with { SchemaVersion = CurrentSchemaVersion, Sources = legacySources, Drafts = drafts };
        }
        if (state.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException("A versão do workspace não é compatível; o arquivo foi preservado.");
        var active = state.Drafts?.FirstOrDefault(draft => DraftKeyEquals(draft.Contract, draft.Month, state.Contract, state.Month));
        return active is null ? state : state with { Sources = active.Sources };
    }

    private static IReadOnlyList<WorkspaceDraft> UpsertDraft(IReadOnlyList<WorkspaceDraft> drafts, WorkspaceState state)
    {
        if (string.IsNullOrWhiteSpace(state.Contract) || string.IsNullOrWhiteSpace(state.Month)) return drafts;
        var prior = drafts.FirstOrDefault(draft => DraftKeyEquals(draft.Contract, draft.Month, state.Contract, state.Month));
        return UpsertDraft(drafts, new WorkspaceDraft(state.Contract, state.Month, state.Sources,
            prior?.Notes ?? string.Empty, prior?.Revision ?? 0));
    }

    private static IReadOnlyList<WorkspaceDraft> UpsertDraft(IReadOnlyList<WorkspaceDraft> drafts, WorkspaceDraft current)
    {
        var prior = drafts.FirstOrDefault(draft => DraftKeyEquals(draft.Contract, draft.Month, current.Contract, current.Month));
        return prior is null
            ? drafts.Append(current).ToArray()
            : drafts.Select(draft => draft == prior ? current : draft).ToArray();
    }

    private static bool DraftKeyEquals(string leftContract, string leftMonth, string? rightContract, string? rightMonth) =>
        new WorkspaceDraft(leftContract, leftMonth, []).Matches(rightContract, rightMonth);

    private static bool MatchesActiveSelection(WorkspaceState state, string? contract, string? month) =>
        contract is null || month is null || DraftKeyEquals(state.Contract ?? string.Empty, state.Month ?? string.Empty, contract, month);

    private async Task WriteAtomicallyAsync(WorkspaceState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(statePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"workspace.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static IReadOnlyList<WorkspaceSource> ReplaceOrAdd(
        IReadOnlyList<WorkspaceSource> sources, WorkspaceSource? prior, WorkspaceSource current) =>
        prior is null ? sources.Append(current).ToArray() : sources.Select(source => source == prior ? current : source).ToArray();
}