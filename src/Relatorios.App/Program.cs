using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Relatorios.Core.Domain;
using Relatorios.Core.Sources;
using Relatorios.Infrastructure.Pcme;
using Relatorios.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddSingleton<WorkspaceStore>();
builder.Services.AddSingleton<ConnectionSettingsVault>();
builder.Services.AddSingleton(new ConnectionProbe(ConnectionProbe.ReadServerVersionAsync));
builder.Services.AddSingleton<PcmeReader>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'";
    await next();
});

app.MapGet("/", async (WorkspaceStore store, CancellationToken ct) =>
{
    var state = await store.LoadAsync(ct);
    var sourceRows = state.Sources.Count == 0
        ? "<p>Nenhuma fonte foi carregada. Configure a conexão do PCME para começar.</p>"
        : "<ul>" + string.Join("", state.Sources.Select(SourceRow)) + "</ul>";
    return Results.Content(Page("Workspace", $"""
        <h1>Relatórios de manutenção</h1>
        <p>Workspace local deste usuário Windows. Dados e rascunhos ficam em LocalAppData, fora da pasta compartilhada.</p>
        <form method="post" action="/workspace">
          <label>Contrato <input name="contract" required maxlength="120" value="{EncodeHtml(state.Contract)}"></label>
          <label>Competência <input name="month" type="month" required value="{EncodeHtml(state.Month ?? DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture))}"></label>
          <button>Salvar workspace</button>
        </form>
        <h2>Rascunho deste contrato e competência</h2>
        <form method="post" action="/workspace/notes">
          <input type="hidden" name="contract" value="{EncodeHtml(state.Contract)}">
          <input type="hidden" name="month" value="{EncodeHtml(state.Month)}">
          <input type="hidden" name="revision" value="{state.ActiveDraftRevision}">
          <label>Resumo e observações <textarea name="notes" maxlength="10000" rows="8">{EncodeHtml(state.ActiveDraftNotes)}</textarea></label>
          <button>Salvar rascunho</button>
        </form>
        <h2>Fontes e carregamentos desta seleção</h2>{sourceRows}
        <form method="post" action="/workspace/refresh"><button>Atualizar PCME</button></form>
        <p><a href="/settings">Configurar conexão Firebird</a></p>
        """), "text/html; charset=utf-8");
});

app.MapPost("/workspace/notes", async (HttpRequest request, WorkspaceStore store, CancellationToken ct) =>
{
    var form = await request.ReadFormAsync(ct);
    var notes = form["notes"].ToString();
    if (notes.Length > 10000 || !long.TryParse(form["revision"], NumberStyles.None,
        CultureInfo.InvariantCulture, out var expectedRevision))
        return Results.BadRequest(Page("Rascunho", "<p>O rascunho deve ter até 10.000 caracteres.</p><a href='/'>Voltar</a>"));
    var result = await store.SaveDraftNotesAsync(form["contract"].ToString(), form["month"].ToString(),
        notes, expectedRevision, ct);
    if (!result.Saved)
        return Results.Content(Page("Conflito no rascunho", "<p>Este rascunho mudou desde que foi aberto. Recarregue o workspace e reaplique suas alterações.</p><a href='/'>Recarregar</a>"),
            "text/html; charset=utf-8", statusCode: StatusCodes.Status409Conflict);
    return Results.Redirect("/");
});

app.MapPost("/workspace", async (HttpRequest request, WorkspaceStore store, CancellationToken ct) =>
{
    var form = await request.ReadFormAsync(ct);
    var contract = form["contract"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(contract) || contract.Length > 120 || !ReportMonth.TryParse(form["month"], out var month))
        return Results.BadRequest(Page("Dados inválidos", "<p>Informe um contrato e uma competência válidos.</p><a href='/'>Voltar</a>"));
    await store.SaveSelectionAsync(contract, $"{month.Year:D4}-{month.Month:D2}", ct);
    return Results.Redirect("/");
});

app.MapGet("/settings", async (ConnectionSettingsVault vault, CancellationToken ct) =>
{
    ConnectionOptions? saved;
    try { saved = await vault.LoadAsync(ct); }
    catch (InvalidDataException)
    {
        return Results.Content(Page("Conexão Firebird", "<p>Não foi possível ler a configuração protegida deste usuário Windows.</p><a href='/settings'>Tentar novamente</a>"), "text/html; charset=utf-8");
    }
    return Results.Content(Page("Conexão Firebird", $"""
        <h1>Conexão somente leitura ao PCME</h1>
        <p>O teste consulta a versão do servidor em uma transação Firebird somente leitura. A senha não é exibida nem registrada.</p>
        <form method="post" action="/settings/test">
          <label>Servidor <input name="server" required value="{EncodeHtml(saved?.Server)}"></label>
          <label>Porta <input name="port" type="number" min="1" max="65535" value="{saved?.Port ?? 3050}" required></label>
          <label>Base / alias no servidor <input name="database" required value="{EncodeHtml(saved?.Database)}"></label>
          <label>Usuário <input name="user" required value="{EncodeHtml(saved?.User)}"></label>
          <label>Senha <input name="password" type="password" autocomplete="new-password" required></label>
          <label>Charset <input name="charset" value="{EncodeHtml(saved?.Charset ?? "WIN1252")}" required></label>
          <label>Timeout (segundos) <input name="timeout" type="number" min="1" max="120" value="{saved?.TimeoutSeconds ?? 10}" required></label>
          <button>Testar e salvar configuração protegida</button>
        </form>
        <p><a href="/">Voltar ao workspace</a></p>
        """), "text/html; charset=utf-8");
});

app.MapPost("/settings/test", async (HttpRequest request, ConnectionProbe probe,
    ConnectionSettingsVault vault, CancellationToken ct) =>
{
    var form = await request.ReadFormAsync(ct);
    if (!int.TryParse(form["port"], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
        !int.TryParse(form["timeout"], NumberStyles.None, CultureInfo.InvariantCulture, out var timeout))
        return Results.BadRequest(Page("Conexão Firebird", "<p>Porta ou timeout inválido.</p><a href='/settings'>Voltar</a>"));

    var options = new ConnectionOptions(form["server"].ToString(), port, form["database"].ToString(),
        form["user"].ToString(), form["password"].ToString(), form["charset"].ToString(), timeout);
    var result = await probe.ProbeAsync(options, ct);
    if (result.Success)
        await vault.SaveAsync(options, ct);
    var detail = result.Success
        ? $"<p>Conexão e compatibilidade básica confirmadas. Firebird: <strong>{EncodeHtml(result.ServerVersion)}</strong>; provider: <strong>{EncodeHtml(result.ProviderVersion)}</strong>. Configuração protegida para este usuário Windows.</p>"
        : $"<p>{EncodeHtml(result.Error)}</p>";
    return Results.Content(Page("Conexão Firebird", detail + "<p><a href='/settings'>Voltar às configurações</a></p>"), "text/html; charset=utf-8");
});

app.MapPost("/workspace/refresh", async (WorkspaceStore store, ConnectionSettingsVault vault,
    PcmeReader reader, CancellationToken ct) =>
{
    var state = await store.LoadAsync(ct);
    if (string.IsNullOrWhiteSpace(state.Contract) || !ReportMonth.TryParse(state.Month, out var month))
        return Results.Content(Page("Workspace", "<p>Salve primeiro o contrato e a competência.</p><a href='/'>Voltar</a>"), "text/html; charset=utf-8");

    ConnectionOptions? options;
    try { options = await vault.LoadAsync(ct); }
    catch (InvalidDataException) { options = null; }
    if (options is null)
        return Results.Content(Page("Workspace", "<p>Configure e teste a conexão Firebird antes de atualizar.</p><a href='/settings'>Configurar conexão</a>"), "text/html; charset=utf-8");

    await Refresh("PCME - ordens de serviço", reader.ReadWorkOrdersAsync(options, month, ct));
    await Refresh("PCME - veículos", reader.ReadVehiclesAsync(options, ct));
    await Refresh("PCME - leituras de hodômetro", reader.ReadMileageReadingsAsync(options, month, ct));
    return Results.Redirect("/");

    async Task Refresh<T>(string name, Task<SourceSnapshot<T>> loading)
    {
        var snapshot = await loading;
        if (snapshot.Succeeded)
        {
            var json = JsonSerializer.SerializeToElement(snapshot.Records);
            await store.RecordSuccessAsync(name, json, snapshot.State.LastSuccessAt ?? DateTimeOffset.UtcNow,
                snapshot.Issues, ct, contract: state.Contract, month: state.Month);
        }
        else
        {
            await store.RecordFailureAsync(name, ct, contract: state.Contract, month: state.Month);
        }
    }
});

await app.StartAsync();
if (!args.Contains("--no-browser", StringComparer.OrdinalIgnoreCase))
{
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
    var address = addresses?.FirstOrDefault();
    if (address is not null)
    {
        try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
        catch (InvalidOperationException) { /* Browser opening is optional in locked-down Windows profiles. */ }
    }
}
await app.WaitForShutdownAsync();

static string SourceRow(WorkspaceSource source)
{
    var date = source.LastSuccessAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"))
        ?? "sem carregamento válido";
    var error = source.LastError is null ? string.Empty : $"<p role='status'>{EncodeHtml(source.LastError)}</p>";
    var count = source.Snapshot?.Length ?? 0;
    var issues = source.Issues.Count == 0 ? "<p>Nenhuma pendência nesta fonte.</p>" : "<p>Pendências:</p><ul>" + string.Join("", source.Issues.Select(issue =>
        $"<li>{EncodeHtml(issue.Code)}: {EncodeHtml(issue.Message)}</li>")) + "</ul>";
    return $"<li><strong>{EncodeHtml(source.Name)}</strong>: último sucesso {EncodeHtml(date)}; {count} registros preservados.{error}{issues}</li>";
}

static string Page(string title, string body) => $"<!doctype html><html lang='pt-BR'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>{EncodeHtml(title)}</title><style>body{{font:16px system-ui;max-width:820px;margin:3rem auto;padding:0 1rem;color:#202734}}label{{display:block;margin:1rem 0}}input,textarea{{display:block;box-sizing:border-box;width:min(100%,34rem);padding:.6rem;margin-top:.3rem;font:inherit}}button{{padding:.65rem 1rem;cursor:pointer}}a{{color:#0759a5}}li{{margin:1rem 0}}form{{margin:1rem 0 2rem}}small{{color:#4c596b}}</style></head><body>{body}</body></html>";

static string EncodeHtml(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);