using System.Text;
using LasAliasManager.Core.Models;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Parses alias and ignored name files
/// </summary>
public class AliasFileParser
{
    /// <summary>
    /// Loads alias definitions from the ListNameAlias file
    /// </summary>
    public Dictionary<string, List<string>> LoadAliasFile(string filePath)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var lines = ReadFileLines(filePath);

        bool headerPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("===") || trimmed.StartsWith("***"))
            {
                headerPassed = true;
                continue;
            }

            if (!headerPassed)
                continue;

            var parsed = ParseAliasLine(trimmed);
            if (parsed.HasValue)
            {
                var (baseName, fieldName) = parsed.Value;
                
                if (baseName.Equals("No", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!result.ContainsKey(baseName))
                {
                    result[baseName] = new List<string>();
                }

                var cleanFieldName = CleanFieldName(fieldName);
                if (!string.IsNullOrWhiteSpace(cleanFieldName) && 
                    !result[baseName].Contains(cleanFieldName, StringComparer.OrdinalIgnoreCase))
                {
                    result[baseName].Add(cleanFieldName);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Loads ignored names from the ListNameAlias_NO file
    /// </summary>
    public HashSet<string> LoadIgnoredFile(string filePath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = ReadFileLines(filePath);

        bool headerPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("==="))
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

    /// <summary>
    /// Saves alias definitions to file
    /// </summary>
    public void SaveAliasFile(string filePath, Dictionary<string, List<string>> aliases, HashSet<string> ignored)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        
        writer.WriteLine("BaseName     FieldNames");
        writer.WriteLine("===================================================================================================");

        foreach (var kvp in aliases.OrderBy(k => k.Key))
        {
            foreach (var fieldName in kvp.Value.OrderBy(f => f))
            {
                writer.WriteLine($"{kvp.Key,-10} {fieldName}");
            }
        }

        foreach (var name in ignored.OrderBy(n => n))
        {
            writer.WriteLine($"{"No",-10} {name}");
        }

        writer.WriteLine("*****************************************");
    }

    /// <summary>
    /// Saves ignored names to file
    /// </summary>
    public void SaveIgnoredFile(string filePath, HashSet<string> ignored)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        
        writer.WriteLine("Ignored Field Names");
        writer.WriteLine("===========================");

        foreach (var name in ignored.OrderBy(n => n))
        {
            writer.WriteLine(name);
        }
    }

    private List<string> ReadFileLines(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var text = utf8.GetString(bytes);
            return text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        }
        catch { }
        
        var latin1 = Encoding.Latin1;
        var content = latin1.GetString(bytes);
        return content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    }

    private (string BaseName, string FieldName)? ParseAliasLine(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
            return null;

        var baseName = parts[0].Trim();
        var fieldName = string.Join(" ", parts.Skip(1)).Trim();

        return (baseName, fieldName);
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

        var dotIndex = clean.IndexOf('.');
        if (dotIndex > 0)
        {
            clean = clean.Substring(0, dotIndex).Trim();
        }

        return clean;
    }
}
