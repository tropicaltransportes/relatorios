using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Relatorios.Infrastructure.Imports;

public sealed record TabularData(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);

public static class TabularReader
{
    public static TabularData Read(Stream input, string extension)
    {
        ArgumentNullException.ThrowIfNull(input);
        var snapshotPath = Path.Combine(Path.GetTempPath(), $"relatorios-{Guid.NewGuid():N}.snapshot");
        using var snapshot = new FileStream(snapshotPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            bufferSize: 81920, FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        input.CopyTo(snapshot);
        snapshot.Position = 0;
        return extension.Trim().ToLowerInvariant() switch
        {
            ".csv" => ReadCsv(snapshot),
            ".xlsx" => ReadXlsx(snapshot),
            _ => throw new NotSupportedException($"Formato de planilha não suportado: {extension}.")
        };
    }

    private static TabularData ReadCsv(Stream input)
    {
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true, 4096, leaveOpen: true);
        var rows = new List<IReadOnlyList<object?>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var justClosedQuote = false;
        var firstCharacter = true;
        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                if (quoted) throw new InvalidDataException("CSV terminou dentro de um campo entre aspas.");
                if (field.Length > 0 || row.Count > 0 || justClosedQuote) { row.Add(field.ToString()); rows.Add(row.Cast<object?>().ToArray()); }
                break;
            }

            var ch = (char)next;
            if (firstCharacter) { firstCharacter = false; if (ch == '\uFEFF') continue; }
            if (quoted)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else { quoted = false; justClosedQuote = true; }
                }
                else field.Append(ch);
                continue;
            }

            if (justClosedQuote && ch is not ',' and not '\r' and not '\n' and not ' ' and not '\t')
                throw new InvalidDataException("CSV contém caracteres após o fechamento de aspas.");
            if (ch == '"' && field.Length == 0 && !justClosedQuote) { quoted = true; continue; }
            if (ch == ',') { row.Add(field.ToString()); field.Clear(); justClosedQuote = false; continue; }
            if (ch is '\r' or '\n')
            {
                if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                row.Add(field.ToString()); rows.Add(row.Cast<object?>().ToArray());
                row = []; field.Clear(); justClosedQuote = false; continue;
            }
            field.Append(ch);
        }

        if (rows.Count == 0) throw new InvalidDataException("CSV não contém cabeçalho.");
        var headers = rows[0].Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        return new TabularData(headers, rows.Skip(1).Where(r => r.Any(v => !string.IsNullOrWhiteSpace(Convert.ToString(v, CultureInfo.InvariantCulture)))).ToArray());
    }

    private static TabularData ReadXlsx(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var dateStyleIds = ReadDateStyleIds(archive);
        var sheetPath = ResolveFirstWorksheet(archive);
        var entry = archive.GetEntry(sheetPath) ?? throw new InvalidDataException("A planilha XLSX não contém a primeira aba.");
        var rows = new List<IReadOnlyList<object?>>();
        using var sheetStream = entry.Open();
        using var xml = XmlReader.Create(sheetStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true });
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "row") continue;
            using var subtree = xml.ReadSubtree();
            rows.Add(ReadXlsxRow(subtree, sharedStrings, dateStyleIds));
        }
        if (rows.Count == 0) throw new InvalidDataException("XLSX não contém cabeçalho.");
        var headers = rows[0].Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        return new TabularData(headers, rows.Skip(1).Where(r => r.Any(v => v is not null && !string.IsNullOrWhiteSpace(Convert.ToString(v, CultureInfo.InvariantCulture)))).ToArray());
    }

    private static IReadOnlyList<object?> ReadXlsxRow(XmlReader xml, IReadOnlyList<string> sharedStrings, ISet<int> dateStyles)
    {
        var cells = new SortedDictionary<int, object?>();
        var maxColumn = -1;
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "c") continue;
            var cellRef = xml.GetAttribute("r") ?? "A1";
            var type = xml.GetAttribute("t");
            var styleText = xml.GetAttribute("s");
            var style = int.TryParse(styleText, out var s) ? s : 0;
            var col = ColumnIndex(cellRef);
            using var cell = xml.ReadSubtree();
            string? value = null;
            string? inline = null;
            while (cell.Read())
            {
                if (cell.NodeType != XmlNodeType.Element) continue;
                if (cell.LocalName == "v") value = cell.ReadElementContentAsString();
                else if (cell.LocalName == "t") inline = (inline ?? string.Empty) + cell.ReadElementContentAsString();
            }
            object? parsed = type switch
            {
                "s" when int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count => sharedStrings[index],
                "inlineStr" => inline ?? string.Empty,
                "b" => value == "1",
                "str" => value ?? string.Empty,
                _ when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && dateStyles.Contains(style) => DateTime.FromOADate(number),
                _ when value is not null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) => numeric,
                _ => value
            };
            cells[col] = parsed;
            maxColumn = Math.Max(maxColumn, col);
        }
        var result = new object?[maxColumn + 1];
        foreach (var cell in cells) result[cell.Key] = cell.Value;
        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        using var xml = XmlReader.Create(stream, SafeXmlSettings());
        var strings = new List<string>();
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "si") continue;
            using var item = xml.ReadSubtree();
            var value = new StringBuilder();
            while (item.Read()) if (item.NodeType == XmlNodeType.Element && item.LocalName == "t") value.Append(item.ReadElementContentAsString());
            strings.Add(value.ToString());
        }
        return strings;
    }

    private static HashSet<int> ReadDateStyleIds(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/styles.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        var doc = XDocument.Load(XmlReader.Create(stream, SafeXmlSettings()));
        var custom = doc.Descendants().Where(e => e.Name.LocalName == "numFmt")
            .Where(e => IsDateFormat((string?)e.Attribute("formatCode")))
            .Select(e => (int?)e.Attribute("numFmtId")).Where(i => i.HasValue).Select(i => i!.Value).ToHashSet();
        var builtIn = new HashSet<int>(Enumerable.Range(14, 23).Concat(Enumerable.Range(45, 3)));
        return doc.Descendants().Where(e => e.Name.LocalName == "cellXfs").Elements()
            .Select((e, index) => (e, index)).Where(x =>
            {
                var id = (int?)x.e.Attribute("numFmtId") ?? 0;
                return custom.Contains(id) || builtIn.Contains(id);
            }).Select(x => x.index).ToHashSet();
    }

    private static bool IsDateFormat(string? format) => format is not null &&
        format.ToLowerInvariant().Any(c => c is 'y' or 'd') && format.ToLowerInvariant().Any(c => c == 'm');

    private static string ResolveFirstWorksheet(ZipArchive archive)
    {
        var workbook = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("Arquivo XLSX inválido: workbook.xml ausente.");
        using var workbookStream = workbook.Open();
        var doc = XDocument.Load(XmlReader.Create(workbookStream, SafeXmlSettings()));
        var relationId = (string?)doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "sheet")?.Attributes().FirstOrDefault(a => a.Name.LocalName == "id");
        if (string.IsNullOrWhiteSpace(relationId)) throw new InvalidDataException("Arquivo XLSX não contém aba de dados.");
        var relEntry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("Arquivo XLSX inválido: relacionamentos ausentes.");
        using var relStream = relEntry.Open();
        var rels = XDocument.Load(XmlReader.Create(relStream, SafeXmlSettings()));
        var target = (string?)rels.Descendants().FirstOrDefault(e => (string?)e.Attribute("Id") == relationId)?.Attribute("Target");
        if (target is null) throw new InvalidDataException("Não foi possível localizar a primeira aba XLSX.");
        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.Replace("../", string.Empty, StringComparison.Ordinal);
    }

    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var ch in cellReference.TakeWhile(char.IsLetter)) index = (index * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        return Math.Max(0, index - 1);
    }

    private static XmlReaderSettings SafeXmlSettings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
}