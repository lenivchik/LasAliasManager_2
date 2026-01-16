using LasAliasManager.Core.Models;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Main service for managing curve name aliases
/// </summary>
public class AliasManager
{
    private readonly CsvAliasParser _csvParser;
    private readonly LasFileParser _lasParser;

    public AliasDatabase Database { get; private set; }

    /// <summary>
    /// Current database file path (CSV or TXT)
    /// </summary>
    public string? DatabaseFilePath { get; private set; }
    public AliasManager()
    {
        _csvParser = new CsvAliasParser();
        _lasParser = new LasFileParser();
        Database = new AliasDatabase();
    }

    /// <summary>
    /// Loads alias database from a CSV file
    /// </summary>
    public void LoadFromCsv(string csvFilePath)
    {
        Database = new AliasDatabase();
        DatabaseFilePath = csvFilePath;
        var (aliases, ignored) = _csvParser.LoadCsvFile(csvFilePath);

        foreach (var kvp in aliases)
        {
            Database.AddBaseName(kvp.Key, kvp.Value);
        }

        foreach (var name in ignored)
        {
            Database.AddIgnored(name);
        }
    }

    /// <summary>
    /// Saves the current database to CSV file
    /// </summary>
    public void SaveToCsv(string? csvFilePath = null)
    {
        csvFilePath ??= DatabaseFilePath;

        if (string.IsNullOrEmpty(csvFilePath))
        {
            throw new InvalidOperationException("CSV file path not specified");
        }

        var aliases = Database.Aliases.ToDictionary(
            k => k.Key,
            v => v.Value.FieldNames
        );

        _csvParser.SaveCsvFile(csvFilePath, aliases, Database.IgnoredNames);
        DatabaseFilePath = csvFilePath;
    }

    /// <summary>
    /// Saves the current database (uses the format it was loaded with)
    /// </summary>
    public void SaveToFiles(string? filePath = null, string? ignoredFilePath = null)
    {
        if (string.IsNullOrEmpty(DatabaseFilePath))
        {
            throw new InvalidOperationException("No database loaded. Load a database first.");
        }
        SaveToCsv(DatabaseFilePath);
    }

    /// <summary>
    /// Parses a single LAS file and returns full content with well info
    /// </summary>
    public LasFileParser.LasFileContent ParseLasFile(string filePath)
    {
        return _lasParser.ParseLasFile(filePath);
    }

    /// <summary>
    /// Analyzes a single LAS file for unknown curve names
    /// </summary>
    public LasAnalysisResult AnalyzeLasFile(string filePath)
    {
        var result = new LasAnalysisResult { FilePath = filePath };

        try
        {
            var content = _lasParser.ParseLasFile(filePath);
            result.TotalCurves = content.Curves.Count;
            result.WellInfo = content.WellInfo;
            result.FileSize = content.FileSize;
            result.CurveDefinitions = content.Curves;

            foreach (var curve in content.Curves)
            {
                var cleanName = CleanCurveName(curve.Mnemonic);
                
                if (Database.IsIgnored(cleanName))
                {
                    result.IgnoredCurves.Add(cleanName);
                }
                else
                {
                    var baseName = Database.FindBaseName(cleanName);
                    if (baseName != null)
                    {
                        if (!result.MappedCurves.ContainsKey(baseName))
                        {
                            result.MappedCurves[baseName] = new List<string>();
                        }
                        result.MappedCurves[baseName].Add(cleanName);
                    }
                    else
                    {
                        result.UnknownCurves.Add(cleanName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Analyzes multiple LAS files in a directory
    /// </summary>
    public List<LasAnalysisResult> AnalyzeDirectory(string directory, bool recursive = true)
    {
        var results = new List<LasAnalysisResult>();
        var files = _lasParser.FindLasFiles(directory, recursive);

        foreach (var file in files)
        {
            results.Add(AnalyzeLasFile(file));
        }

        return results;
    }

    /// <summary>
    /// Gets all unique unknown curve names from multiple analysis results
    /// </summary>
    public HashSet<string> GetAllUnknownCurves(IEnumerable<LasAnalysisResult> results)
    {
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            foreach (var curve in result.UnknownCurves)
            {
                unknown.Add(curve);
            }
        }
        return unknown;
    }

    /// <summary>
    /// Gets all unknown curves with their source file information
    /// </summary>
    public List<UnknownCurveInfo> GetAllUnknownCurvesWithFiles(IEnumerable<LasAnalysisResult> results)
    {
        var unknownList = new List<UnknownCurveInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var result in results)
        {
            foreach (var curve in result.UnknownCurves)
            {
                if (!seen.Contains(curve))
                {
                    seen.Add(curve);
                    unknownList.Add(new UnknownCurveInfo(curve, result.FilePath));
                }
            }
        }
        return unknownList;
    }

    /// <summary>
    /// Gets unknown curves grouped by curve name with all source files
    /// </summary>
    public Dictionary<string, List<string>> GetUnknownCurvesGrouped(IEnumerable<LasAnalysisResult> results)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var result in results)
        {
            foreach (var curve in result.UnknownCurves)
            {
                if (!grouped.ContainsKey(curve))
                {
                    grouped[curve] = new List<string>();
                }
                if (!grouped[curve].Contains(result.FilePath))
                {
                    grouped[curve].Add(result.FilePath);
                }
            }
        }
        return grouped;
    }

    /// <summary>
    /// Adds an unknown curve as an alias to an existing base name
    /// </summary>
    public bool AddAsAlias(string curveName, string baseName)
    {
        if (!Database.Aliases.ContainsKey(baseName))
        {
            return false;
        }

        return Database.AddAliasToBase(baseName, curveName);
    }

    /// <summary>
    /// Adds an unknown curve as a new base name
    /// </summary>
    public void AddAsNewBase(string curveName)
    {
        Database.AddBaseName(curveName, new[] { curveName });
    }

    /// <summary>
    /// Adds an unknown curve to the ignored list
    /// </summary>
    public bool AddAsIgnored(string curveName)
    {
        return Database.AddIgnored(curveName);
    }

    private string CleanCurveName(string name)
    {
        return name.Trim();
    }
}

/// <summary>
/// Result of analyzing a LAS file
/// </summary>
public class LasAnalysisResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public long FileSize { get; set; }
    public int TotalCurves { get; set; }
    public LasFileParser.WellInfo WellInfo { get; set; } = new();

    public List<LasFileParser.CurveDefinition> CurveDefinitions { get; set; } = new();

    /// <summary>
    /// Curves that were mapped to base names
    /// Key: base name, Value: list of field names found
    /// </summary>
    public Dictionary<string, List<string>> MappedCurves { get; set; } = new();
    
    /// <summary>
    /// Curves that were in the ignored list
    /// </summary>
    public List<string> IgnoredCurves { get; set; } = new();
    
    /// <summary>
    /// Curves that were not found in aliases or ignored list
    /// </summary>
    public List<string> UnknownCurves { get; set; } = new();
    
    /// <summary>
    /// Error message if parsing failed
    /// </summary>
    public string? Error { get; set; }

    public bool HasUnknown => UnknownCurves.Count > 0;
    public bool HasError => !string.IsNullOrEmpty(Error);

    public LasFileParser.CurveDefinition? GetCurveDefinition(string mnemonic)
    {
        return CurveDefinitions.FirstOrDefault(c =>
            c.Mnemonic.Equals(mnemonic, StringComparison.OrdinalIgnoreCase));
    }
    public override string ToString()
    {
        if (HasError)
            return $"{FileName}: ERROR - {Error}";
        
        return $"{FileName}: {TotalCurves} curves ({MappedCurves.Count} mapped, {IgnoredCurves.Count} ignored, {UnknownCurves.Count} unknown)";
    }
}

/// <summary>
/// Represents an unknown curve with its source file information
/// </summary>
public class UnknownCurveInfo
{
    public string CurveName { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public string SourceFileName => Path.GetFileName(SourceFilePath);
    
    public UnknownCurveInfo() { }
    
    public UnknownCurveInfo(string curveName, string sourceFilePath)
    {
        CurveName = curveName;
        SourceFilePath = sourceFilePath;
    }
    
    public override string ToString()
    {
        return $"{CurveName} (from: {SourceFilePath})";
    }
}
