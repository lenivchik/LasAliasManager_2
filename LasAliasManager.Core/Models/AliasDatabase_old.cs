namespace LasAliasManager.Core.Models;

/// <summary>
/// Database containing all curve aliases and ignored names
/// </summary>
public class AliasDatabase_old
{
    /// <summary>
    /// Dictionary mapping base names to their CurveAlias objects
    /// </summary>
    public Dictionary<string, CurveAlias> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set of curve names that should be ignored during analysis
    /// </summary>
    public HashSet<string> IgnoredNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the base name for a given field name
    /// </summary>
    /// <param name="fieldName">The field name to look up</param>
    /// <returns>The base name if found, null otherwise</returns>
    public string? FindBaseName(string fieldName)
    {
        var trimmed = fieldName.Trim();

        // Check if it's a base name itself

        if (Aliases.TryGetValue(trimmed, out var alias))
            return alias.BaseName;  // Returns "DTp" (the actual base name)
        // Search through all aliases
        foreach (var kvp in Aliases)
        {
            if (kvp.Value.IsAlias(trimmed))
                return kvp.Key;
        }

        return null;
    }

    /// <summary>
    /// Checks if a name should be ignored
    /// </summary>
    public bool IsIgnored(string name)
    {
        return IgnoredNames.Contains(name.Trim());
    }

    /// <summary>
    /// Checks if a name is known (either as alias or ignored)
    /// </summary>
    public bool IsKnown(string name)
    {
        var trimmed = name.Trim();
        return IsIgnored(trimmed) || FindBaseName(trimmed) != null;
    }

    /// <summary>
    /// Adds a new alias to an existing base name
    /// </summary>
    public bool AddAliasToBase(string baseName, string fieldName)
    {
        var trimmedFieldName = fieldName.Trim();

        // Remove from ignored list if present (prevents duplicate records)
        IgnoredNames.Remove(trimmedFieldName);

        // Remove from any other base name's aliases (in case it was mapped elsewhere)
        RemoveFieldNameFromAllAliases(trimmedFieldName);

        if (!Aliases.TryGetValue(baseName, out var curveAlias))
        {
            curveAlias = new CurveAlias(baseName);
            Aliases[baseName] = curveAlias;
        }
        return curveAlias.AddAlias(trimmedFieldName);
    }

    /// <summary>
    /// Adds a new base name with optional aliases
    /// </summary>
    public void AddBaseName(string baseName, IEnumerable<string>? aliases = null)
    {
        if (!Aliases.ContainsKey(baseName))
        {
            Aliases[baseName] = new CurveAlias(baseName, aliases ?? Enumerable.Empty<string>());
        }
        else if (aliases != null)
        {
            foreach (var alias in aliases)
            {
                Aliases[baseName].AddAlias(alias);
            }
        }
    }

    /// <summary>
    /// Adds a name to the ignored list
    /// </summary>
    public bool AddIgnored(string name)
    {
        var trimmed = name.Trim();

        // Remove from all aliases (prevents duplicate records)
        RemoveFieldNameFromAllAliases(trimmed);

        return IgnoredNames.Add(trimmed);
    }
    /// <summary>
    /// Removes a field name from all alias lists
    /// </summary>
    private void RemoveFieldNameFromAllAliases(string fieldName)
    {
        foreach (var kvp in Aliases)
        {
            kvp.Value.RemoveAlias(fieldName);
        }
    }
    /// <summary>
    /// Gets all base names
    /// </summary>
    public IEnumerable<string> GetAllBaseNames()
    {
        return Aliases.Keys.OrderBy(k => k);
    }

    /// <summary>
    /// Gets statistics about the database
    /// </summary>
    public (int BaseCount, int TotalAliases, int IgnoredCount) GetStatistics()
    {
        int totalAliases = Aliases.Values.Sum(a => a.FieldNames.Count);
        return (Aliases.Count, totalAliases, IgnoredNames.Count);
    }
}
