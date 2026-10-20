using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using Relatorios.Core.Domain;
using Relatorios.Core.Sources;
using Relatorios.Infrastructure.Imports;

namespace Relatorios.Tests;

public sealed class SpreadsheetImportTests
{
    [Fact]
    public void CsvReaderPreservesIdentifiersAndQuotedMultilineDescriptions()
    {
        const string csv = "Veículo,Carimbo,Descrição\r\n01/150V,20/10/2026 08:15,\"Falha, elétrica\r\nrevisar ação\"\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var table = TabularReader.Read(stream, ".csv");

        Assert.Equal("01/150V", table.Rows[0][0]);
        Assert.Equal("20/10/2026 08:15", table.Rows[0][1]);
        Assert.Equal("Falha, elétrica\r\nrevisar ação", table.Rows[0][2]);
    }

    [Fact]
    public void XlsxReaderPreservesTextAndTypedDates()
    {
        using var stream = CreateWorkbook();

        var table = TabularReader.Read(stream, ".xlsx");

        Assert.Equal("01/150V", table.Rows[0][0]);
        Assert.IsType<DateTime>(table.Rows[0][1]);
        Assert.Equal(new DateTime(2026, 10, 20), table.Rows[0][1]);
    }

    [Fact]
    public void MissingVehicleHeaderRequiresExplicitMapping()
    {
        var table = Table(
            ["Nome", "Data", "Hodômetro"],
            ["Motorista A", "20/10/2026 08:15", "00150"]);

        var result = OperationImporter.Import(table, new ColumnMapping(new Dictionary<string, string>
        {
            ["at"] = "Data", ["odometer"] = "Hodômetro"
        }));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == "column_mapping_missing");
    }

    [Fact]
    public void OperationImportPreservesVehicleAndDoesNotAggregateOdometerReadings()
    {
        var table = Table(
            ["Hodômetro", "Contrato", "Momento", "Veículo"],
            ["00150", "C-1", "20/10/2026 08:15", "01/150V"],
            ["00180", "C-1", "20/10/2026 09:15", "01/150V"]);
        var mapping = new ColumnMapping(new Dictionary<string, string>
        {
            ["vehicle"] = "Veículo", ["at"] = "Momento", ["contract"] = "Contrato", ["odometer"] = "Hodômetro"
        });

        var result = OperationImporter.Import(table, mapping);

        Assert.True(result.Succeeded);
        Assert.Equal("01/150V", result.Records[0].Vehicle.Value);
        Assert.Equal(150m, result.Records[0].Odometer);
        Assert.Equal(180m, result.Records[1].Odometer);
    }

    [Fact]
    public void InvalidOperationImportPreservesPreviousSnapshot()
    {
        var previousAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-3));
        var previousRecord = new OperationRecord("op-1", new VehicleId("01/150V"), previousAt, "C-1", 150m);
        var previous = new SourceSnapshot<OperationRecord>([previousRecord], new SourceState("planilha de operação", previousAt, null), [], true);
        const string csv = "Veículo,Data,Contrato,Hodômetro\r\n01/150V,desconhecida,C-1,00150\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var mapping = new ColumnMapping(new Dictionary<string, string>
        {
            ["vehicle"] = "Veículo", ["at"] = "Data", ["contract"] = "Contrato", ["odometer"] = "Hodômetro"
        });

        var result = OperationImporter.Import(stream, ".csv", mapping, previous);

        Assert.False(result.Succeeded);
        Assert.Equal(previousAt, result.State.LastSuccessAt);
        Assert.Equal("op-1", Assert.Single(result.Records).SourceId);
    }

    [Fact]
    public void RefreshPreservesRequestIdentityWhenStatusOrRowOrderChanges()
    {
        var mapping = new ColumnMapping(new Dictionary<string, string>
        {
            ["vehicle"] = "Veículo", ["openedAt"] = "Carimbo", ["status"] = "Situação", ["description"] = "Descrição"
        });
        var original = Table(
            ["Veículo", "Carimbo", "Descrição", "Situação"],
            ["01/150V", "20/10/2026 08:15", "Motor não parte", "PENDENTE"],
            ["02/150V", "20/10/2026 09:00", "Pneu furado", "EM ANDAMENTO"]);
        var refreshed = Table(
            ["Veículo", "Carimbo", "Descrição", "Situação"],
            ["02/150V", "20/10/2026 09:00", "Pneu furado", "FINALIZADO"],
            ["01/150V", "20/10/2026 08:15", "Motor não parte, falha recorrente", "FINALIZADO"]);
        var duplicate = Table(
            ["Veículo", "Carimbo", "Descrição", "Situação"],
            ["01/150V", "20/10/2026 08:15", "Motor não parte", "PENDENTE"],
            ["01/150V", "20/10/2026 08:15", "Motor falha", "PENDENTE"]);

        var first = RequestImporter.Import(original, mapping, "https://sheets.example/pub");
        var second = RequestImporter.Import(refreshed, mapping, "https://sheets.example/pub");
        var resent = RequestImporter.Import(duplicate, mapping, "https://sheets.example/pub");

        Assert.Equal(first.Records.Select(r => r.SourceId).Order(), second.Records.Select(r => r.SourceId).Order());
        var oldRequest = first.Records.Single(r => r.Vehicle.Value == "01/150V");
        var refreshedRequest = second.Records.Single(r => r.Vehicle.Value == "01/150V");
        Assert.NotEqual(oldRequest.ContentRevision, refreshedRequest.ContentRevision);
        Assert.Equal(2, first.Records.Count);
        Assert.Equal(2, resent.Records.Count);
        Assert.All(resent.Records, record => Assert.True(record.PossibleResubmission));
        Assert.Contains(resent.Issues, issue => issue.Code == "possible_resubmission");
    }

    [Fact]
    public void SeveralProblemsRemainOneRequest()
    {
        var table = Table(["Veículo", "Carimbo", "Descrição", "Situação"],
            ["01/150V", "20/10/2026 08:15", "Falha elétrica, ruído no motor e vazamento", "PENDENTE"]);

        var result = RequestImporter.Import(table, RequestMapping(), "https://sheets.example/pub");

        Assert.True(result.Succeeded);
        Assert.Single(result.Records);
        Assert.Equal("Falha elétrica, ruído no motor e vazamento", result.Records[0].Description);
    }

    [Fact]
    public void SimilarResubmissionsRemainDistinctAndFlagged()
    {
        var table = Table(["Veículo", "Carimbo", "Descrição", "Situação"],
            ["01/150V", "20/10/2026 08:15", "Motor apresenta falha elétrica recorrente", "PENDENTE"],
            ["01/150V", "21/10/2026 08:15", "Falha elétrica recorrente no motor", "PENDENTE"]);

        var result = RequestImporter.Import(table, RequestMapping(), "https://sheets.example/pub");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(2, result.Records.Select(record => record.SourceId).Distinct().Count());
        Assert.All(result.Records, record => Assert.True(record.PossibleResubmission));
        Assert.Contains(result.Issues, issue => issue.Code == "possible_resubmission");
    }

    [Fact]
    public void SameTimestampVehicleCollisionRemainsPendingWithoutMergingOrReplacingSnapshot()
    {
        var table = Table(["Veículo", "Carimbo", "Descrição", "Situação"],
            ["01/150V", "20/10/2026 08:15", "Motor não parte", "PENDENTE"],
            ["01/150V", "20/10/2026 08:15", "Pneu furado", "PENDENTE"]);

        var oldOpenedAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-3));
        var previous = new SourceSnapshot<MaintenanceRequest>(
            [new MaintenanceRequest("previous", new VehicleId("03/150V"), oldOpenedAt, "PENDENTE", "Pedido anterior")],
            new SourceState("publicação", oldOpenedAt, null), [], true);
        var result = RequestImporter.Import(table, RequestMapping(), "https://sheets.example/pub", previous);

        Assert.False(result.Succeeded);
        Assert.Equal("previous", Assert.Single(result.Records).SourceId);
        Assert.Equal(oldOpenedAt, result.State.LastSuccessAt);
        Assert.Contains(result.Issues, issue => issue.Code == "request_identity_collision");
    }

    [Fact]
    public void UnknownRequestStatusIsPreservedAndReported()
    {
        var table = Table(["Veículo", "Carimbo", "Descrição", "Situação"],
            ["01/150V", "20/10/2026 08:15", "Reparo", "AGUARDANDO PEÇA"]);
        var mapping = new ColumnMapping(new Dictionary<string, string>
        {
            ["vehicle"] = "Veículo", ["openedAt"] = "Carimbo", ["status"] = "Situação", ["description"] = "Descrição"
        });

        var result = RequestImporter.Import(table, mapping, "source");

        Assert.Equal("AGUARDANDO PEÇA", result.Records[0].Status);
        Assert.Contains(result.Issues, issue => issue.Code == "unexpected_status");
    }

    [Fact]
    public async Task NetworkFailurePreservesDatedSnapshot()
    {
        var updatedAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-3));
        var request = new MaintenanceRequest("stable-id", new VehicleId("01/150V"), updatedAt, "PENDENTE", "Revisar freio");
        var previous = new SourceSnapshot<MaintenanceRequest>([request], new SourceState("publicação", updatedAt, null), [], true);
        using var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline")));
        var reader = new PublishedRequestsReader(client);

        var result = await reader.ReadAsync(new Uri("https://sheets.example/pubhtml"), RequestMapping(), default, previous);

        Assert.False(result.Succeeded);
        Assert.Equal(updatedAt, result.State.LastSuccessAt);
        Assert.Equal("stable-id", Assert.Single(result.Records).SourceId);
        Assert.Contains(result.Issues, issue => issue.Code == "network_failure");
    }

    [Fact]
    public async Task UnexpectedStatusProducesIssue()
    {
        var previous = new SourceSnapshot<MaintenanceRequest>(
            [new MaintenanceRequest("stable-id", new VehicleId("01/150V"), DateTimeOffset.Now, "PENDENTE", "Revisar freio")],
            new SourceState("publicação", DateTimeOffset.Now, null), [], true);
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var reader = new PublishedRequestsReader(client);

        var result = await reader.ReadAsync(new Uri("https://sheets.example/pubhtml"), RequestMapping(), default, previous);

        Assert.False(result.Succeeded);
        Assert.Single(result.Records);
        Assert.Contains(result.Issues, issue => issue.Code == "unexpected_status");
    }

    [Fact]
    public void PublicationUriTargetsIndividualResponsesTabAsCsv()
    {
        var uri = PublishedRequestsReader.BuildCsvUri(new Uri("https://docs.google.com/spreadsheets/d/example/pubhtml"));

        Assert.EndsWith("/pub", uri.AbsolutePath);
        Assert.Contains("gid=1115240100", uri.Query);
        Assert.Contains("output=csv", uri.Query);
    }

    private static TabularData Table(string[] headers, params string[][] rows) =>
        new(headers, rows.Select(row => (IReadOnlyList<object?>)row.Cast<object?>().ToArray()).ToArray());

    private static ColumnMapping RequestMapping() => new(new Dictionary<string, string>
    {
        ["vehicle"] = "Veículo", ["openedAt"] = "Carimbo", ["status"] = "Situação", ["description"] = "Descrição"
    });

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }

    private static MemoryStream CreateWorkbook()
    {
        var stream = new MemoryStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true);
        Add("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\"/></Relationships>");
        Add("xl/styles.xml", "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/></numFmts><cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"164\"/></cellXfs></styleSheet>");
        Add("xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Veículo</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Data</t></is></c></row><row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>01/150V</t></is></c><c r=\"B2\" s=\"1\"><v>46315</v></c></row></sheetData></worksheet>");
        stream.Position = 0;
        return stream;

        void Add(string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}