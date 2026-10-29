using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.GremlinStudio.Core.Export;

/// <summary>
/// Flattened, tabular representation of Gremlin query results (same shape as the app's table view).
/// </summary>
/// <param name="Columns">Ordered column names.</param>
/// <param name="ColumnIsProperty">True for columns that come from the element's <c>properties</c> bag.</param>
/// <param name="Rows">Row values keyed by column name. Missing cells are absent.</param>
public sealed record ResultTableData(
    IReadOnlyList<string> Columns,
    IReadOnlyDictionary<string, bool> ColumnIsProperty,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

/// <summary>
/// Builds the result table from Gremlin JSON and writes it as CSV or XLSX.
/// Shared by the Gremlin Studio results pane and the MCP server.
/// </summary>
public static class ResultTableExporter
{
    // Excel rejects cell text longer than this.
    private const int MaxExcelCellLength = 32767;

    /// <summary>
    /// Builds a table from a Gremlin result JSON array.
    /// </summary>
    public static ResultTableData BuildTable(string resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return BuildTable(Array.Empty<object>());
        }

        var token = JToken.Parse(resultJson);
        IEnumerable<object> items = token is JArray array ? array.Cast<object>() : new object[] { token };
        return BuildTable(items);
    }

    /// <summary>
    /// Builds a table from deserialized Gremlin results. Non-object items are skipped.
    /// Columns are ordered: id, name, pk, then alphabetically.
    /// </summary>
    public static ResultTableData BuildTable(IEnumerable<object> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var objects = results.OfType<JObject>().ToList();
        var columnIsProperty = new Dictionary<string, bool>();

        foreach (var jObj in objects)
        {
            foreach (var prop in jObj.Properties())
            {
                if (prop.Name == "properties" && prop.Value is JObject propsObj)
                {
                    foreach (var nestedProp in propsObj.Properties())
                    {
                        columnIsProperty.TryAdd(nestedProp.Name, true);
                    }
                }
                else
                {
                    columnIsProperty.TryAdd(prop.Name, false);
                }
            }
        }

        var columns = columnIsProperty.Keys
            .OrderBy(k => k.Equals("id", StringComparison.OrdinalIgnoreCase) ? 0
                        : k.Equals("name", StringComparison.OrdinalIgnoreCase) ? 1
                        : k.Equals("pk", StringComparison.OrdinalIgnoreCase) ? 2
                        : 3)
            .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<IReadOnlyDictionary<string, string>>(objects.Count);
        foreach (var jObj in objects)
        {
            var values = new Dictionary<string, string>();
            foreach (var prop in jObj.Properties())
            {
                if (prop.Name == "properties" && prop.Value is JObject propsObj)
                {
                    foreach (var nestedProp in propsObj.Properties())
                    {
                        values[nestedProp.Name] = ExtractPropertyValue(nestedProp.Value) ?? string.Empty;
                    }
                }
                else
                {
                    values[prop.Name] = ExtractSimpleValue(prop.Value) ?? string.Empty;
                }
            }
            rows.Add(values);
        }

        return new ResultTableData(columns, columnIsProperty, rows);
    }

    /// <summary>
    /// Serializes rows to RFC 4180 CSV with a header row.
    /// </summary>
    public static string ToCsv(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", columns.Select(EscapeCsvField)));
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
        }
        return csv.ToString();
    }

    /// <summary>
    /// Serializes a <see cref="ResultTableData"/> to CSV.
    /// </summary>
    public static string ToCsv(ResultTableData table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return ToCsv(table.Columns, ToRowArrays(table));
    }

    /// <summary>
    /// Writes rows to an XLSX workbook with a filterable header row.
    /// </summary>
    /// <param name="stream">Writable, seekable output stream.</param>
    /// <param name="columns">Column headers in output order.</param>
    /// <param name="rows">Row values in the same order as <paramref name="columns"/>.</param>
    /// <param name="convertEpochDates">Convert epoch values in date-like columns to Excel dates.</param>
    public static void WriteXlsx(Stream stream, IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows, bool convertEpochDates = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        // Style index 1 = date format "yyyy-mm-dd hh:mm:ss".
        uint dateStyleIndex = 0;
        if (convertEpochDates)
        {
            dateStyleIndex = 1;
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateDateStylesheet();
            stylesPart.Stylesheet.Save();
        }

        var dateColumnIndexes = new HashSet<int>();
        if (convertEpochDates)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (IsDateColumnName(columns[i]))
                {
                    dateColumnIndexes.Add(i);
                }
            }
        }

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Query Results"
        });

        var headerRow = new Row();
        foreach (var col in columns)
        {
            headerRow.Append(TextCell(col));
        }
        sheetData.Append(headerRow);

        var rowCount = 0;
        foreach (var values in rows)
        {
            var dataRow = new Row();
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i] ?? string.Empty;
                if (dateColumnIndexes.Contains(i) && TryConvertEpochToOADate(value, out var oaDate))
                {
                    dataRow.Append(new Cell
                    {
                        CellValue = new CellValue(oaDate.ToString(CultureInfo.InvariantCulture)),
                        StyleIndex = dateStyleIndex
                    });
                }
                else
                {
                    dataRow.Append(TextCell(value));
                }
            }
            sheetData.Append(dataRow);
            rowCount++;
        }

        if (columns.Count > 0)
        {
            worksheetPart.Worksheet.InsertAfter(new AutoFilter
            {
                Reference = $"A1:{GetExcelColumnLetter(columns.Count)}{rowCount + 1}"
            }, sheetData);
        }
    }

    /// <summary>
    /// Writes a <see cref="ResultTableData"/> to an XLSX workbook.
    /// </summary>
    public static void WriteXlsx(Stream stream, ResultTableData table, bool convertEpochDates = false)
    {
        ArgumentNullException.ThrowIfNull(table);
        WriteXlsx(stream, table.Columns, ToRowArrays(table), convertEpochDates);
    }

    private static IEnumerable<IReadOnlyList<string>> ToRowArrays(ResultTableData table) =>
        table.Rows.Select(r => (IReadOnlyList<string>)table.Columns
            .Select(c => r.TryGetValue(c, out var v) ? v : string.Empty)
            .ToArray());

    private static Cell TextCell(string value) => new()
    {
        DataType = CellValues.String,
        CellValue = new CellValue(SanitizeForExcel(value))
    };

    /// <summary>
    /// Removes characters that are invalid in XML and truncates to Excel's cell limit,
    /// so arbitrary graph data can never produce a corrupt workbook.
    /// </summary>
    private static string SanitizeForExcel(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder? sb = null;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            var valid = XmlCharIsValid(ch) || (char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]));
            if (char.IsLowSurrogate(ch) && i > 0 && char.IsHighSurrogate(value[i - 1]))
            {
                valid = true;
            }

            if (!valid)
            {
                sb ??= new StringBuilder(value, 0, i, value.Length);
                continue;
            }
            sb?.Append(ch);
        }

        var result = sb?.ToString() ?? value;
        return result.Length > MaxExcelCellLength ? result[..MaxExcelCellLength] : result;
    }

    private static bool XmlCharIsValid(char ch) =>
        ch == '\t' || ch == '\n' || ch == '\r' || (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD);

    private static string EscapeCsvField(string? field)
    {
        field ??= string.Empty;
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static Stylesheet CreateDateStylesheet()
    {
        const uint customFormatId = 164; // First user-defined format id
        return new Stylesheet
        {
            NumberingFormats = new NumberingFormats(
                new NumberingFormat { NumberFormatId = customFormatId, FormatCode = "yyyy\\-mm\\-dd\\ hh:mm:ss" })
            { Count = 1 },
            Fonts = new Fonts(new Font()) { Count = 1 },
            Fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
            { Count = 2 },
            Borders = new Borders(new Border()) { Count = 1 },
            CellFormats = new CellFormats(
                new CellFormat(),
                new CellFormat { NumberFormatId = customFormatId, ApplyNumberFormat = true })
            { Count = 2 }
        };
    }

    /// <summary>
    /// Determines whether a column name suggests it holds epoch timestamp values.
    /// </summary>
    public static bool IsDateColumnName(string columnName)
    {
        var lower = columnName.ToLowerInvariant();
        return lower.Contains("date")
            || lower.Contains("time")
            || lower.Contains("created")
            || lower.Contains("modified")
            || lower.Contains("updated")
            || lower.Contains("timestamp")
            || lower.Contains("expir")
            || lower.EndsWith("_at")
            || lower.EndsWith("at");
    }

    private static bool TryConvertEpochToOADate(string value, out double oaDate)
    {
        oaDate = 0;
        if (!long.TryParse(value, out var epoch) || epoch <= 0)
            return false;

        try
        {
            // Heuristic: values above year 2100 in seconds are treated as milliseconds.
            var dto = epoch > 4_102_444_800
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
            oaDate = dto.UtcDateTime.ToOADate();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Not a representable date; keep the raw value as text.
            return false;
        }
    }

    private static string GetExcelColumnLetter(int columnNumber)
    {
        var result = string.Empty;
        while (columnNumber > 0)
        {
            columnNumber--;
            result = (char)('A' + columnNumber % 26) + result;
            columnNumber /= 26;
        }
        return result;
    }

    /// <summary>
    /// Extracts the display value of a Gremlin vertex/edge property (first value of a multi-property).
    /// </summary>
    public static string? ExtractPropertyValue(JToken? token)
    {
        if (token is JArray arr && arr.Count > 0)
        {
            var first = arr[0];
            if (first is JObject obj && obj["value"] != null)
            {
                return obj["value"]?.ToString();
            }
            return first?.ToString();
        }
        return token?.ToString();
    }

    private static string? ExtractSimpleValue(JToken? token)
    {
        if (token is JValue val)
        {
            return val.Value?.ToString();
        }
        if (token is JArray arr)
        {
            return arr.Count <= 3 ? string.Join(", ", arr.Select(x => x.ToString())) : $"[{arr.Count} items]";
        }
        if (token is JObject obj)
        {
            return obj.ToString(Formatting.None);
        }
        return token?.ToString();
    }
}
