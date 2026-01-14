using System.Text;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Exports alias database to ListNamesAlias.txt format (system file)
/// </summary>
public class ListNamesAliasExporter
{
    private const string Header = @"Основное     Полевые
  имя         имена
===================================================================================================";

    private const string Footer = "**********************************************************************************************************";

    /// <summary>
    /// Exports the database to ListNamesAlias.txt format
    /// Sorted by PrimaryName, then by FieldName within each primary name
    /// </summary>
    /// <param name="filePath">Output file path</param>
    /// <param name="aliases">Dictionary of base names to their field names</param>
    /// <param name="ignoredNames">Set of ignored names (will use "No" as primary name)</param>
    public void Export(string filePath, Dictionary<string, List<string>> aliases, HashSet<string> ignoredNames)
    {
        var entries = new List<(string PrimaryName, string FieldName)>();

        // Add all aliases
        foreach (var kvp in aliases)
        {
            var primaryName = kvp.Key;
            foreach (var fieldName in kvp.Value)
            {
                entries.Add((primaryName, fieldName));
            }

            // Also add the base name itself as an entry
            if (!kvp.Value.Contains(primaryName, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add((primaryName, primaryName));
            }
        }

        // Add ignored names with "No" as primary name
        foreach (var ignored in ignoredNames)
        {
            entries.Add(("No", ignored));
        }

        // Sort by PrimaryName, then by FieldName
        var sorted = entries
            .OrderBy(e => e.PrimaryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Write file
        using var writer = new StreamWriter(filePath, false, Encoding.GetEncoding(1251)); // Windows-1251 for Cyrillic

        writer.WriteLine(Header);

        foreach (var entry in sorted)
        {
            // Format: PrimaryName (left-padded to ~10 chars) followed by FieldName
            var line = $"{entry.PrimaryName,-10} {entry.FieldName}";
            writer.WriteLine(line);
        }

        writer.WriteLine(Footer);
    }

    /// <summary>
    /// Appends new entries to an existing ListNamesAlias.txt file
    /// Re-sorts the entire file after adding
    /// </summary>
    /// <param name="filePath">Path to existing file</param>
    /// <param name="newAliases">New aliases to add</param>
    /// <param name="newIgnored">New ignored names to add</param>
    public void AppendAndSort(string filePath, Dictionary<string, List<string>> newAliases, HashSet<string> newIgnored)
    {
        // Load existing entries
        var existingEntries = LoadExistingEntries(filePath);

        // Add new aliases
        foreach (var kvp in newAliases)
        {
            var primaryName = kvp.Key;
            foreach (var fieldName in kvp.Value)
            {
                var entry = (primaryName, fieldName);
                if (!existingEntries.Contains(entry))
                {
                    existingEntries.Add(entry);
                }
            }
        }

        // Add new ignored names
        foreach (var ignored in newIgnored)
        {
            var entry = ("No", ignored);
            if (!existingEntries.Contains(entry))
            {
                existingEntries.Add(entry);
            }
        }

        // Sort and write
        var sorted = existingEntries
            .OrderBy(e => e.PrimaryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var writer = new StreamWriter(filePath, false, Encoding.GetEncoding(1251));

        writer.WriteLine(Header);

        foreach (var entry in sorted)
        {
            var line = $"{entry.PrimaryName,-10} {entry.FieldName}";
            writer.WriteLine(line);
        }

        writer.WriteLine(Footer);
    }

    /// <summary>
    /// Loads existing entries from a ListNamesAlias.txt file
    /// </summary>
    private HashSet<(string PrimaryName, string FieldName)> LoadExistingEntries(string filePath)
    {
        var entries = new HashSet<(string, string)>();

        if (!File.Exists(filePath))
            return entries;

        var lines = ReadFileLines(filePath);
        bool headerPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Skip header and footer
            if (trimmed.StartsWith("===") || trimmed.StartsWith("***"))
            {
                headerPassed = true;
                continue;
            }

            if (trimmed.StartsWith("Основное") || trimmed.StartsWith("имя") || trimmed.StartsWith("Îñíîâíîå"))
                continue;

            if (!headerPassed)
                continue;

            // Parse entry: PrimaryName FieldName [optional :formula]
            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var primaryName = parts[0];

                // Field name is everything after primary name, but before ":" if present
                var restOfLine = trimmed.Substring(primaryName.Length).Trim();
                var colonIndex = restOfLine.IndexOf(':');
                var fieldName = colonIndex > 0
                    ? restOfLine.Substring(0, colonIndex).Trim()
                    : restOfLine.Trim();

                // Clean up field name (remove trailing dots and spaces)
                fieldName = fieldName.TrimEnd('.', ' ');

                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    entries.Add((primaryName, fieldName));
                }
            }
        }

        return entries;
    }

    private List<string> ReadFileLines(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        // Try to detect encoding using Ude
        try
        {
            using var fs = File.OpenRead(filePath);
            var detector = new Ude.CharsetDetector();
            detector.Feed(fs);
            detector.DataEnd();

            if (!string.IsNullOrEmpty(detector.Charset))
            {
                var encoding = Encoding.GetEncoding(detector.Charset);
                var text = File.ReadAllText(filePath, encoding);
                return text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            }
        }
        catch { }

        // Fallback to UTF-8 with fallback to Latin1
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var text = utf8.GetString(bytes);
            return text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        }
        catch { }

        // Final fallback
        var latin1 = Encoding.Latin1;
        var content = latin1.GetString(bytes);
        return content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

    }
}
