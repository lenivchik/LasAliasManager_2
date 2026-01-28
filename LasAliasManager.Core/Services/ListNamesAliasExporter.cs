using System.Text;
using static LasAliasManager.Core.Constants;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Экспорт в ListNamesAlias.txt
/// </summary>
public class ListNamesAliasExporter
{
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

        foreach (var kvp in aliases)
        {
            var primaryName = kvp.Key;
            foreach (var fieldName in kvp.Value)
            {
                entries.Add((primaryName, fieldName));
            }

            if (!kvp.Value.Contains(primaryName, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add((primaryName, primaryName));
            }
        }

        foreach (var ignored in ignoredNames)
        {
            entries.Add((PrimaryNames.Ignored, ignored));
        }

        var sorted = entries
            .OrderBy(e => e.PrimaryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var writer = new StreamWriter(filePath, false, Encoding.GetEncoding(1251));

        writer.WriteLine(TxtExport.Header); 

        foreach (var entry in sorted)
        {
            var line = $"{entry.PrimaryName,-TxtExport.PrimaryNameColumnWidth} {entry.FieldName}";
            writer.WriteLine(line);
        }

        writer.WriteLine(TxtExport.Footer);
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
            var entry = (PrimaryNames.Ignored, ignored);
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

        writer.WriteLine(TxtExport.Header);

        foreach (var entry in sorted)
        {
            var line = $"{entry.PrimaryName,-10} {entry.FieldName}";
            writer.WriteLine(line);
        }

        writer.WriteLine(TxtExport.Footer);
    }

    /// <summary>
    /// Loads existing entries from a ListNamesAlias.txt file
    /// </summary>
    private HashSet<(string PrimaryName, string FieldName)> LoadExistingEntries(string filePath)
    {
        var entries = new HashSet<(string, string)>();

        if (!File.Exists(filePath))
            return entries;

        var lines = FileEncoder.ReadFileLinesAnsi(filePath);
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

            if (trimmed.StartsWith("Основное") || trimmed.StartsWith("имя"))
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
                var colonIndex = restOfLine.IndexOf('.');
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

   
}
