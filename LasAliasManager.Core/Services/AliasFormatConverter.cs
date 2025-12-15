using System.Text;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Converts legacy TXT alias files to the new CSV format
/// </summary>
public class AliasFormatConverter
{
    /// <summary>
    /// Converts three TXT files to a single CSV file
    /// </summary>
    /// <param name="ignoredNamesFile">File 1: List of ignored names</param>
    /// <param name="primaryNamesFile">File 2: List of primary (base) names only</param>
    /// <param name="aliasFile">File 3: Primary names with their field names (aliases)</param>
    /// <param name="outputCsvFile">Output CSV file path</param>
    public void ConvertToCSV(string ignoredNamesFile, string primaryNamesFile, string aliasFile, string outputCsvFile)
    {
        var records = new List<AliasRecord>();
        var addedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Step 1: Load primary names (base names) from File 2
        var primaryNames = LoadPrimaryNames(primaryNamesFile);
        foreach (var primaryName in primaryNames)
        {
            if (!addedFieldNames.Contains(primaryName))
            {
                records.Add(new AliasRecord
                {
                    FieldName = primaryName,
                    PrimaryName = primaryName,
                    Status = "base",
                    Description = ""
                });
                addedFieldNames.Add(primaryName);
            }
        }

        // Step 2: Load aliases from File 3
        var aliases = LoadAliasFile(aliasFile);
        foreach (var (fieldName, primaryName) in aliases)
        {
            if (addedFieldNames.Contains(fieldName))
                continue;

            if (primaryName.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                // "No" means ignored
                records.Add(new AliasRecord
                {
                    FieldName = fieldName,
                    PrimaryName = "",
                    Status = "ignore",
                    Description = ""
                });
            }
            else
            {
                records.Add(new AliasRecord
                {
                    FieldName = fieldName,
                    PrimaryName = primaryName,
                    Status = "alias",
                    Description = ""
                });
            }
            addedFieldNames.Add(fieldName);
        }

        // Step 3: Load ignored names from File 1
        var ignoredNames = LoadIgnoredNames(ignoredNamesFile);
        foreach (var name in ignoredNames)
        {
            if (!addedFieldNames.Contains(name))
            {
                records.Add(new AliasRecord
                {
                    FieldName = name,
                    PrimaryName = "",
                    Status = "ignore",
                    Description = ""
                });
                addedFieldNames.Add(name);
            }
        }

        // Step 4: Write CSV
        WriteCsv(outputCsvFile, records);
    }

    private HashSet<string> LoadPrimaryNames(string filePath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = ReadFileLines(filePath);

        bool headerPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Skip header separators
            if (trimmed.StartsWith("===") || trimmed.StartsWith("***") || trimmed.StartsWith("---"))
            {
                headerPassed = true;
                continue;
            }

            if (!headerPassed)
                continue;

            // Each line is a primary name
            var cleanName = CleanFieldName(trimmed);
            if (!string.IsNullOrWhiteSpace(cleanName))
            {
                result.Add(cleanName);
            }
        }

        return result;
    }

    private HashSet<string> LoadIgnoredNames(string filePath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = ReadFileLines(filePath);

        bool headerPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("===") || trimmed.StartsWith("***") || trimmed.StartsWith("---"))
            {
                headerPassed = true;
                continue;
            }

            if (!headerPassed)
                continue;

            var cleanName = CleanFieldName(trimmed);
            if (!string.IsNullOrWhiteSpace(cleanName))
            {
                result.Add(cleanName);
            }
        }

        return result;
    }

    private List<(string FieldName, string PrimaryName)> LoadAliasFile(string filePath)
    {
        var result = new List<(string, string)>();
        var lines = ReadFileLines(filePath);

        bool headerPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("===") || trimmed.StartsWith("***") || trimmed.StartsWith("---"))
            {
                headerPassed = true;
                continue;
            }

            if (!headerPassed)
                continue;

            // Parse: PrimaryName    FieldName
            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                continue;

            var primaryName = parts[0].Trim();
            var fieldName = CleanFieldName(string.Join(" ", parts.Skip(1)).Trim());

            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                result.Add((fieldName, primaryName));
            }
        }

        return result;
    }

    private void WriteCsv(string filePath, List<AliasRecord> records)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));

        // Write header
        writer.WriteLine("FieldName,PrimaryName,Status,Description");

        // Sort records: base first, then alias, then ignore
        var sorted = records
            .OrderBy(r => r.Status switch
            {
                "base" => 0,
                "alias" => 1,
                "ignore" => 2,
                _ => 3
            })
            .ThenBy(r => r.PrimaryName)
            .ThenBy(r => r.FieldName);

        foreach (var record in sorted)
        {
            writer.WriteLine($"{EscapeCsv(record.FieldName)},{EscapeCsv(record.PrimaryName)},{EscapeCsv(record.Status)},{EscapeCsv(record.Description)}");
        }
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // If value contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private List<string> ReadFileLines(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        // Try UTF-8 first
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

    private string CleanFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return string.Empty;

        var clean = fieldName.Trim();

        while (clean.EndsWith(".") || clean.EndsWith(" "))
        {
            clean = clean.TrimEnd('.', ' ');
        }

        // Extract just the curve name (before any dots)
        var dotIndex = clean.IndexOf('.');
        if (dotIndex > 0)
        {
            clean = clean.Substring(0, dotIndex).Trim();
        }

        return clean;
    }

    private class AliasRecord
    {
        public string FieldName { get; set; } = "";
        public string PrimaryName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
