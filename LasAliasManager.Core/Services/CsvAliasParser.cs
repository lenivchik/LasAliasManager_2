using System.Text;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Parses and saves alias database in CSV format
/// </summary>
public class CsvAliasParser
{
    /// <summary>
    /// Represents a single record from the CSV file
    /// </summary>
    public class AliasRecord
    {
        public string FieldName { get; set; } = "";
        public string PrimaryName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Loads alias database from CSV file
    /// </summary>
    /// <returns>Tuple of (baseNames dictionary, ignoredNames set)</returns>
    public (Dictionary<string, List<string>> Aliases, HashSet<string> Ignored) LoadCsvFile(string filePath)
    {
        var aliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var records = ReadCsvRecords(filePath);

        foreach (var record in records)
        {
            switch (record.Status.ToLowerInvariant())
            {
                case "base":
                    // Add base name with itself as first alias
                    if (!aliases.ContainsKey(record.FieldName))
                    {
                        aliases[record.FieldName] = new List<string>();
                    }
                    break;

                case "alias":
                    // Add field name as alias to primary name
                    if (!string.IsNullOrWhiteSpace(record.PrimaryName))
                    {
                        if (!aliases.ContainsKey(record.PrimaryName))
                        {
                            aliases[record.PrimaryName] = new List<string>();
                        }
                        if (!aliases[record.PrimaryName].Contains(record.FieldName, StringComparer.OrdinalIgnoreCase))
                        {
                            aliases[record.PrimaryName].Add(record.FieldName);
                        }
                    }
                    break;

                case "ignore":
                    ignored.Add(record.FieldName);
                    break;
            }
        }

        return (aliases, ignored);
    }

    /// <summary>
    /// Saves alias database to CSV file
    /// </summary>
    public void SaveCsvFile(string filePath, Dictionary<string, List<string>> aliases, HashSet<string> ignored)
    {
        var records = new List<AliasRecord>();

        // Add base names
        foreach (var baseName in aliases.Keys.OrderBy(k => k))
        {
            records.Add(new AliasRecord
            {
                FieldName = baseName,
                PrimaryName = baseName,
                Status = "base",
                Description = ""
            });

            // Add aliases for this base name
            foreach (var alias in aliases[baseName].OrderBy(a => a))
            {
                // Skip if alias equals base name
                if (!alias.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    records.Add(new AliasRecord
                    {
                        FieldName = alias,
                        PrimaryName = baseName,
                        Status = "alias",
                        Description = ""
                    });
                }
            }
        }

        // Add ignored names
        foreach (var name in ignored.OrderBy(n => n))
        {
            records.Add(new AliasRecord
            {
                FieldName = name,
                PrimaryName = "",
                Status = "ignore",
                Description = ""
            });
        }

        WriteCsvFile(filePath, records);
    }

    /// <summary>
    /// Gets all records from CSV file (for advanced operations)
    /// </summary>
    public List<AliasRecord> ReadCsvRecords(string filePath)
    {
        var records = new List<AliasRecord>();
        var lines = ReadFileLines(filePath);

        bool isFirstLine = true;

        foreach (var line in lines)
        {
            // Skip header line
            if (isFirstLine)
            {
                isFirstLine = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Length >= 3)
            {
                records.Add(new AliasRecord
                {
                    FieldName = fields[0],
                    PrimaryName = fields.Length > 1 ? fields[1] : "",
                    Status = fields.Length > 2 ? fields[2] : "",
                    Description = fields.Length > 3 ? fields[3] : ""
                });
            }
        }

        return records;
    }

    private void WriteCsvFile(string filePath, List<AliasRecord> records)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));

        // Write header
        writer.WriteLine("FieldName,PrimaryName,Status,Description");

        foreach (var record in records)
        {
            writer.WriteLine($"{EscapeCsv(record.FieldName)},{EscapeCsv(record.PrimaryName)},{EscapeCsv(record.Status)},{EscapeCsv(record.Description)}");
        }
    }

    private string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        // Add last field
        fields.Add(currentField.ToString());

        return fields.ToArray();
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private List<string> ReadFileLines(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        // Try UTF-8 with BOM first
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        }

        // Try UTF-8
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var text = utf8.GetString(bytes);
            return text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        }
        catch { }

        // Fallback to Latin1
        var latin1 = Encoding.Latin1;
        var content = latin1.GetString(bytes);
        return content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    }
}
