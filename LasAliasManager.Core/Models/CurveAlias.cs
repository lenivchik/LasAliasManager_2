namespace LasAliasManager.Core.Models;

/// <summary>
/// Represents a base curve name with all its field name aliases
/// </summary>
public class CurveAlias
{
    /// <summary>
    /// The standardized base name (e.g., "NPHI", "GR", "ILD")
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    /// <summary>
    /// List of all field names (aliases) that correspond to this base name
    /// </summary>
    public List<string> FieldNames { get; set; } = new();

    public CurveAlias() { }

    public CurveAlias(string baseName)
    {
        BaseName = baseName;
    }

    public CurveAlias(string baseName, IEnumerable<string> fieldNames)
    {
        BaseName = baseName;
        FieldNames = fieldNames.ToList();
    }

    /// <summary>
    /// Adds a new field name alias if it doesn't already exist
    /// </summary>
    public bool AddAlias(string fieldName)
    {
        var trimmed = fieldName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (!FieldNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            FieldNames.Add(trimmed);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a field name is an alias of this base name
    /// </summary>
    public bool IsAlias(string fieldName)
    {
        return FieldNames.Contains(fieldName.Trim(), StringComparer.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Removes a field name alias if it exists
    /// </summary>
    public bool RemoveAlias(string fieldName)
    {
        var trimmed = fieldName.Trim();
        var index = FieldNames.FindIndex(f => f.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            FieldNames.RemoveAt(index);
            return true;
        }
        return false;
    }

    public override string ToString()
    {
        return $"{BaseName}: [{string.Join(", ", FieldNames)}]";
    }
}
