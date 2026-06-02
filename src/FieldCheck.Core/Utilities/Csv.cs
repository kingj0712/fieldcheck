using System.Text;

namespace FieldCheck.Core.Utilities;

/// <summary>
/// A small, dependency-free CSV reader/writer that follows RFC&#160;4180. I wrote this by hand
/// rather than take a NuGet dependency so the published app stays lean. It correctly handles
/// quoted fields containing commas, embedded newlines, and escaped quotes ("").
/// </summary>
public static class Csv
{
    private const char Bom = '﻿';

    /// <summary>
    /// Parses CSV text into a list of rows, each a list of fields. Accepts CRLF, LF, or CR
    /// line endings and strips a leading UTF-8 BOM. A trailing newline does not produce an
    /// extra empty row, but genuine blank lines are preserved as a single empty field so the
    /// caller can decide whether to skip them.
    /// </summary>
    public static List<List<string>> Parse(string? text)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(text))
            return rows;

        // Drop a leading UTF-8 BOM if the file was saved with one.
        if (text[0] == Bom)
            text = text.Substring(1);

        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;
        int i = 0;
        int n = text.Length;

        while (i < n)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // A doubled quote is a literal quote; otherwise it ends the quoted run.
                    if (i + 1 < n && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    field.Append(c);
                    i++;
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    i++;
                    break;
                case '\r':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    i += (i + 1 < n && text[i + 1] == '\n') ? 2 : 1; // swallow CRLF as one break
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    i++;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        // Flush the final field/row only when there is pending content, so a file that ends
        // with a newline does not yield a spurious trailing row.
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Escapes a single field, quoting it only when it contains a delimiter, quote, or newline.</summary>
    public static string Escape(string? field)
    {
        field ??= string.Empty;
        bool mustQuote = field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!mustQuote)
            return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Builds a single CSV line from fields, escaping each as needed.</summary>
    public static string Row(IEnumerable<string?> fields) => string.Join(",", fields.Select(Escape));

    /// <summary>Builds a full CSV document (CRLF separated) from a sequence of rows.</summary>
    public static string Build(IEnumerable<IEnumerable<string?>> rows) =>
        string.Join("\r\n", rows.Select(Row));
}
