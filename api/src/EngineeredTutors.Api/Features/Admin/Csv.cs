using System.Text;

namespace EngineeredTutors.Api.Features.Admin;

/// <summary>
/// Minimal RFC 4180 CSV reader and writer. Respects quotes, doubled quotes and line breaks inside quoted fields,
/// which question stems need (section 5.2).
/// </summary>
public static class Csv
{
    public sealed record Record(int RowNumber, string[] Fields);

    /// <summary>Parses all records. RowNumber counts records from 1 (the header), as a spreadsheet would show them.</summary>
    public static List<Record> Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;
        var row = 1;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            fieldStarted = false;
        }

        void EndRecord()
        {
            EndField();
            if (!(fields.Count == 1 && fields[0].Length == 0)) records.Add(new Record(row++, fields.ToArray()));
            fields.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"' when !fieldStarted && field.Length == 0:
                    inQuotes = true;
                    fieldStarted = true;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    EndRecord();
                    break;
                case '\n':
                    EndRecord();
                    break;
                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        if (inQuotes) throw new FormatException("The file ends inside a quoted field. Check for a missing closing quote.");
        if (field.Length > 0 || fields.Count > 0) EndRecord();
        return records;
    }

    /// <summary>Quotes every field. Values starting with =, +, - or @ get a leading apostrophe so spreadsheets
    /// don't run them as formulas.</summary>
    public static string Line(IEnumerable<string?> values) =>
        string.Join(',', values.Select(v =>
        {
            var s = v ?? "";
            if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(s, out _)) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        })) + "\r\n";
}
