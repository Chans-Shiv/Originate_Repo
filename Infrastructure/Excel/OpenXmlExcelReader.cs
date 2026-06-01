using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using Fhn.Originate.FtbanknewSync.Domain.Entities;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Excel;

public sealed class OpenXmlExcelReader
{
    private readonly ILogger<OpenXmlExcelReader> _log;
    public OpenXmlExcelReader(ILogger<OpenXmlExcelReader> log) => _log = log;

    public IEnumerable<OrigenateRow> StreamRows(string xlsxPath)
    {
        _log.LogInformation("Excel ▶ opening {Path}", xlsxPath);
        using var doc = SpreadsheetDocument.Open(xlsxPath, false);
        var wbPart = doc.WorkbookPart ?? throw new InvalidDataException("Workbook part missing.");
        var sheet = wbPart.Workbook.Descendants<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("No sheet found.");
        var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
        var sst = wbPart.SharedStringTablePart?.SharedStringTable;

        using var reader = OpenXmlReader.Create(wsPart);
        var headers = new Dictionary<int, string>();
        var unmappedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int rowNum = 0;
        int dataRows = 0, skippedEmpty = 0;

        while (reader.Read())
        {
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;

            rowNum++;
            var values = ReadRowValues(reader, sst);

            if (rowNum == 1)
            {
                foreach (var kv in values) headers[kv.Key] = (kv.Value ?? "").Trim();
                var mapped = headers.Values.Count(h => !string.IsNullOrWhiteSpace(h) && ColumnMap.StgExcelHeaderToDataverse.ContainsKey(h));
                var ignored = headers.Values
                    .Where(h => !string.IsNullOrWhiteSpace(h) && !ColumnMap.StgExcelHeaderToDataverse.ContainsKey(h))
                    .ToArray();
                _log.LogInformation("Excel header row: {Total} columns, {Mapped} mapped to Dataverse, {Ignored} ignored",
                    headers.Count, mapped, ignored.Length);
                if (ignored.Length > 0)
                    _log.LogWarning("Excel headers NOT in ColumnMap (skipped): {Ignored}",
                        string.Join(", ", ignored));
                continue;
            }

            if (values.Count == 0 || values.All(v => string.IsNullOrWhiteSpace(v.Value)))
            {
                skippedEmpty++;
                continue;
            }

            var row = new OrigenateRow { RowNumber = rowNum };
            foreach (var (colIdx, val) in values)
            {
                if (!headers.TryGetValue(colIdx, out var header) || string.IsNullOrWhiteSpace(header)) continue;
                if (!ColumnMap.StgExcelHeaderToDataverse.TryGetValue(header, out var dvField))
                {
                    unmappedHeaders.Add(header);
                    continue;
                }
                row.Fields[dvField] = val;
                if (string.Equals(dvField, ColumnMap.ApplicationNumberField, StringComparison.OrdinalIgnoreCase))
                    row.ApplicationNumber = val;
            }
            dataRows++;
            yield return row;
        }
        _log.LogInformation("Excel ✓ streamed {Rows} data rows ({Skipped} empty rows skipped) from {Path}",
            dataRows, skippedEmpty, xlsxPath);
    }

    private static Dictionary<int, string?> ReadRowValues(OpenXmlReader reader, SharedStringTable? sst)
    {
        var result = new Dictionary<int, string?>();
        while (reader.Read())
        {
            if (reader.ElementType == typeof(Row) && reader.IsEndElement) break;
            if (reader.ElementType != typeof(Cell) || !reader.IsStartElement) continue;

            var cell = (Cell)reader.LoadCurrentElement()!;
            if (cell.CellReference?.Value is null) continue;
            var colIdx = ColRefToIndex(cell.CellReference!.Value!);

            string? value = cell.CellValue?.InnerText;
            if (cell.DataType?.Value == CellValues.SharedString && sst is not null
                && int.TryParse(value, out var sIdx) && sIdx < sst.ChildElements.Count)
            {
                value = sst.ChildElements[sIdx].InnerText;
            }
            else if (cell.DataType?.Value == CellValues.InlineString)
            {
                value = cell.InlineString?.InnerText;
            }
            else if (cell.DataType?.Value == CellValues.Boolean)
            {
                value = value == "1" ? "true" : "false";
            }

            result[colIdx] = value;
        }
        return result;
    }

    private static int ColRefToIndex(string cellRef)
    {
        int idx = 0;
        foreach (var c in cellRef)
        {
            if (c < 'A' || c > 'Z') break;
            idx = idx * 26 + (c - 'A' + 1);
        }
        return idx;
    }
}
