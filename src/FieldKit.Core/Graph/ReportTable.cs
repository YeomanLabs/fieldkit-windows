using System.Globalization;
using System.Text.Json;

namespace FieldKit.Core.Graph;

/// <summary>
/// An Intune report response: <c>{"TotalRowCount":n,"Schema":[{"Column","PropertyType"}],"Values":[[...]]}</c>.
/// Columns are found by name: their order isn't the one asked for, and Intune adds
/// <c>_loc</c> display-text columns. Graph sends these as text/plain or octet-stream, not JSON.
/// </summary>
public sealed class ReportTable
{
    private readonly Dictionary<string, int> _columns;
    private readonly List<JsonElement[]> _rows;

    private ReportTable(Dictionary<string, int> columns, List<JsonElement[]> rows, int totalRowCount)
    {
        _columns = columns;
        _rows = rows;
        TotalRowCount = totalRowCount;
    }

    /// <summary>All the rows the filter matched; <see cref="Count"/> is this page.</summary>
    public int TotalRowCount { get; }

    public int Count => _rows.Count;

    public bool HasColumn(string column) => _columns.ContainsKey(column);

    public static ReportTable Parse(byte[] data)
    {
        if (data.Length == 0) throw GraphException.Decoding($"{GraphException.EmptyResponseDetail} from an Intune report");
        try
        {
            using var document = JsonDocument.Parse(GraphJson.Normalized(data));
            var root = document.RootElement;
            if (!root.TryGetProperty("Schema", out var schema) || !root.TryGetProperty("Values", out var values))
            {
                throw GraphException.Decoding("Intune report without Schema or Values");
            }
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var column in schema.EnumerateArray())
            {
                if (column.TryGetProperty("Column", out var name) && name.GetString() is { } text) columns[text] = index;
                index++;
            }
            var rows = values.EnumerateArray().Select(row => row.EnumerateArray().Select(cell => cell.Clone()).ToArray()).ToList();
            var total = root.TryGetProperty("TotalRowCount", out var count) && count.TryGetInt32(out var n) ? n : rows.Count;
            return new ReportTable(columns, rows, total);
        }
        catch (JsonException error)
        {
            throw GraphException.Decoding(GraphJson.Describe(error));
        }
    }

    /// <summary>The cell as text; empty when the column is missing or null.</summary>
    public string Text(int row, string column)
    {
        if (Cell(row, column) is not { } cell) return "";
        return cell.ValueKind switch
        {
            JsonValueKind.String => cell.GetString() ?? "",
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => cell.GetRawText(),
            _ => "",
        };
    }

    public int? Int(int row, string column)
    {
        if (Cell(row, column) is not { } cell) return null;
        if (cell.ValueKind == JsonValueKind.Number && cell.TryGetInt32(out var number)) return number;
        return cell.ValueKind == JsonValueKind.String && int.TryParse(cell.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Report times have no time zone and come either as ISO ("2026-10-11T10:44:27") or as
    /// US-formatted text ("10/11/2026 11:04:37 AM"); both are UTC.
    /// </summary>
    public DateTimeOffset? Date(int row, string column)
    {
        var text = Text(row, column);
        if (text.Length == 0) return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? new DateTimeOffset(date, TimeSpan.Zero)
            : null;
    }

    private JsonElement? Cell(int row, string column) =>
        _columns.TryGetValue(column, out var index) && index < _rows[row].Length && _rows[row][index].ValueKind != JsonValueKind.Null
            ? _rows[row][index]
            : null;
}
