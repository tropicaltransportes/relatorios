using System.Net;
using System.Text;
using Relatorios.Core.Domain;
using Relatorios.Core.Sources;

namespace Relatorios.Infrastructure.Imports;

public sealed class PublishedRequestsReader(HttpClient httpClient)
{
    public const int ResponsesSheetGid = 1115240100;

    public async Task<SourceSnapshot<MaintenanceRequest>> ReadAsync(Uri publication, ColumnMapping mapping,
        CancellationToken cancellationToken, SourceSnapshot<MaintenanceRequest>? previous = null)
    {
        var publicationUri = BuildCsvUri(publication);
        var source = publication.GetLeftPart(UriPartial.Path);
        try
        {
            using var response = await httpClient.GetAsync(publicationUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
                return Failed(previous, source, "unexpected_status", $"A publicação respondeu HTTP {(int)response.StatusCode}.");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var prefix = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if ((mediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) ?? false) ||
                prefix.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                return Failed(previous, source, "published_response_html", "A publicação retornou HTML ou uma tela de autenticação em vez do CSV da aba de respostas.");

            using var stream = new MemoryStream(bytes, writable: false);
            var table = TabularReader.Read(stream, ".csv");
            var imported = RequestImporter.Import(table, mapping, source, previous);
            if (!imported.Succeeded)
            {
                if (imported.Issues.Any(issue => issue.Code == "column_mapping_missing"))
                    return Failed(previous, source, "published_headers_mismatch", "Os cabeçalhos não correspondem ao mapeamento da aba de respostas; nenhuma atualização foi aplicada.");
                return new SourceSnapshot<MaintenanceRequest>(previous?.Records ?? [],
                    new SourceState("publicação de solicitações", previous?.State.LastSuccessAt, imported.State.LastError),
                    imported.Issues, false);
            }
            return imported with { State = imported.State with { Name = "publicação de solicitações" } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (HttpRequestException)
        {
            return Failed(previous, source, "network_failure", "Não foi possível acessar a publicação de solicitações.");
        }
        catch (IOException)
        {
            return Failed(previous, source, "publication_read_failure", "A resposta da publicação não pôde ser lida integralmente.");
        }
        catch (InvalidDataException)
        {
            return Failed(previous, source, "publication_read_failure", "A resposta não contém uma planilha CSV válida.");
        }
    }

    public static Uri BuildCsvUri(Uri publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (publication.Scheme is not ("http" or "https")) throw new ArgumentException("A publicação precisa usar HTTP ou HTTPS.", nameof(publication));
        var builder = new UriBuilder(publication) { Query = $"gid={ResponsesSheetGid}&single=true&output=csv", Fragment = string.Empty };
        if (builder.Path.EndsWith("pubhtml", StringComparison.OrdinalIgnoreCase))
            builder.Path = builder.Path[..^"pubhtml".Length] + "pub";
        else if (!builder.Path.EndsWith("/pub", StringComparison.OrdinalIgnoreCase))
            builder.Path = builder.Path.TrimEnd('/') + "/pub";
        return builder.Uri;
    }

    private static SourceSnapshot<MaintenanceRequest> Failed(SourceSnapshot<MaintenanceRequest>? previous, string source,
        string code, string message) =>
        ImportParsing.Failure("publicação de solicitações", code, message, previous?.State.LastSuccessAt, message,
            previous?.Records ?? []);
}