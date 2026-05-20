using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Models;

namespace OrigenateFunction.Excel;

public sealed class OpenXmlExcelReader
{
    private readonly ILogger<OpenXmlExcelReader> _log;
    public OpenXmlExcelReader(ILogger<OpenXmlExcelReader> log) => _log = log;

    public IEnumerable<OrigenateRow> StreamRows(string xlsxPath)
    {
        using var doc = SpreadsheetDocument.Open(xlsxPath, false);
        var wbPart = doc.WorkbookPart ?? throw new InvalidDataException("Workbook part missing.");
        var sheet = wbPart.Workbook.Descendants<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("No sheet found.");
        var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
        var sst = wbPart.SharedStringTablePart?.SharedStringTable;

        using var reader = OpenXmlReader.Create(wsPart);
        var headers = new Dictionary<int, string>();
        int rowNum = 0;

        while (reader.Read())
        {
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;

            rowNum++;
            var values = ReadRowValues(reader, sst);

            if (rowNum == 1)
            {
                foreach (var kv in values) headers[kv.Key] = (kv.Value ?? "").Trim();
                continue;
            }

            if (values.Count == 0 || values.All(v => string.IsNullOrWhiteSpace(v.Value))) continue;

            var row = new OrigenateRow { RowNumber = rowNum };
            foreach (var (colIdx, val) in values)
            {
                if (!headers.TryGetValue(colIdx, out var header) || string.IsNullOrWhiteSpace(header)) continue;
                if (!ColumnMap.ExcelHeaderToDataverse.TryGetValue(header, out var dvField)) continue;
                row.Fields[dvField] = val;
                if (string.Equals(dvField, ColumnMap.ApplicationNumberField, StringComparison.OrdinalIgnoreCase))
                    row.ApplicationNumber = val;
            }
            yield return row;
        }
        _log.LogInformation("Streamed {Rows} data rows from {Path}", rowNum - 1, xlsxPath);
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
