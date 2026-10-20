using System.Text.Json;
using Relatorios.Infrastructure.Storage;

namespace Relatorios.Tests;

public sealed class WorkspaceStoreTests
{
    [Fact]
    public async Task DraftNotesAreIsolatedByContractAndMonth()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WorkspaceStore(root);
            await store.SaveSelectionAsync("Contrato A", "2026-09");
            var aRevision = (await store.LoadAsync()).ActiveDraftRevision;
            var aSave = await store.SaveDraftNotesAsync("Contrato A", "2026-09", "Resumo do contrato A", aRevision);
            Assert.True(aSave.Saved);
            await store.SaveSelectionAsync("Contrato B", "2026-09");
            var delayedSave = await store.SaveDraftNotesAsync("Contrato A", "2026-09", "Rascunho atrasado de A", aSave.Revision);
            Assert.True(delayedSave.Saved);
            var bRevision = (await store.LoadAsync()).ActiveDraftRevision;
            await store.SaveDraftNotesAsync("Contrato B", "2026-09", "Resumo do contrato B", bRevision);
            await store.SaveSelectionAsync("Contrato A", "2026-09");

            var reopened = await new WorkspaceStore(root).LoadAsync();
            Assert.Equal("Rascunho atrasado de A", reopened.ActiveDraftNotes);

            await store.SaveSelectionAsync("Contrato B", "2026-09");
            Assert.Equal("Resumo do contrato B", (await store.LoadAsync()).ActiveDraftNotes);
            await store.SaveSelectionAsync("Contrato A", "2026-10");
            var newMonth = await store.LoadAsync();
            Assert.Equal(string.Empty, newMonth.ActiveDraftNotes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SourceRefreshAndFailureKeepDraftNotesAndExposeSnapshotState()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        var at = DateTimeOffset.Parse("2026-10-19T10:00:00Z");
        using var records = JsonDocument.Parse("[{\"id\":\"synthetic\"}]");
        var issues = new[] { new Relatorios.Core.Sources.PendingIssue("missing", "Vínculo pendente") };
        try
        {
            var store = new WorkspaceStore(root);
            await store.SaveSelectionAsync("Contrato A", "2026-10");
            var revision = (await store.LoadAsync()).ActiveDraftRevision;
            await store.SaveDraftNotesAsync("Contrato A", "2026-10", "Observação operacional", revision);
            var revisionAfterNotes = (await store.LoadAsync()).ActiveDraftRevision;
            await store.RecordSuccessAsync("PCME", records.RootElement, at, issues);
            await store.RecordFailureAsync("PCME");

            var reopened = await new WorkspaceStore(root).LoadAsync();
            var source = Assert.Single(reopened.Sources);
            Assert.Equal("Observação operacional", reopened.ActiveDraftNotes);
            Assert.Equal(revisionAfterNotes, reopened.ActiveDraftRevision);
            Assert.Equal(at, source.LastSuccessAt);
            Assert.Equal("Vínculo pendente", Assert.Single(source.Issues).Message);
            Assert.Equal("synthetic", source.Snapshot![0].GetProperty("id").GetString());
            Assert.NotNull(source.LastError);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentDraftSavesFromSeparateStoreInstancesDetectConflict()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var first = new WorkspaceStore(root);
            await first.SaveSelectionAsync("Contrato A", "2026-10");
            var revision = (await first.LoadAsync()).ActiveDraftRevision;
            var second = new WorkspaceStore(root);

            var results = await Task.WhenAll(
                first.SaveDraftNotesAsync("Contrato A", "2026-10", "Edição 1", revision),
                second.SaveDraftNotesAsync("Contrato A", "2026-10", "Edição 2", revision));

            Assert.Single(results, result => result.Saved);
            Assert.Single(results, result => !result.Saved);
            var persisted = await new WorkspaceStore(root).LoadAsync();
            Assert.Equal(1, persisted.ActiveDraftRevision);
            Assert.True(persisted.ActiveDraftNotes is "Edição 1" or "Edição 2");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AtomicReplacementSucceedsWhileWorkspaceSnapshotIsBeingRead()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WorkspaceStore(root);
            await store.SaveSelectionAsync("Contrato A", "2026-10");
            var statePath = Path.Combine(root, "workspace.json");
            await using var reader = new FileStream(statePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            await store.SaveSelectionAsync("Contrato B", "2026-10");

            Assert.Equal("Contrato B", (await new WorkspaceStore(root).LoadAsync()).Contract);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LateRefreshCannotWriteIntoAnotherSelectedWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        using var records = JsonDocument.Parse("[{\"contract\":\"A\"}]");
        try
        {
            var store = new WorkspaceStore(root);
            await store.SaveSelectionAsync("Contrato A", "2026-10");
            await store.SaveSelectionAsync("Contrato B", "2026-10");
            await store.RecordSuccessAsync("PCME", records.RootElement, DateTimeOffset.UtcNow,
                contract: "Contrato A", month: "2026-10");

            var state = await store.LoadAsync();
            Assert.Equal("Contrato B", state.Contract);
            Assert.Empty(state.Sources);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FailedRefreshKeepsLastSuccessfulSnapshotAndTimestampAfterReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        var at = DateTimeOffset.Parse("2026-10-19T10:00:00Z");
        using var records = JsonDocument.Parse("[{\"id\":\"synthetic\"}]");

        try
        {
            var store = new WorkspaceStore(root);
            await store.RecordSuccessAsync("PCME", records.RootElement, at, cancellationToken: CancellationToken.None);
            await store.RecordFailureAsync("PCME", CancellationToken.None);

            var reopened = await new WorkspaceStore(root).LoadAsync(CancellationToken.None);
            var source = Assert.Single(reopened.Sources);

            Assert.Equal(at, source.LastSuccessAt);
            Assert.Equal("Não foi possível atualizar esta fonte. Confira a configuração e tente novamente.", source.LastError);
            Assert.Equal("synthetic", source.Snapshot![0].GetProperty("id").GetString());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}