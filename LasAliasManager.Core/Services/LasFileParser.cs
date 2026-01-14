using System.Text;
using System.Text.RegularExpressions;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Parses LAS (Log ASCII Standard) files to extract curve information
/// </summary>
public class LasFileParser
{
    /// <summary>
    /// Represents a curve definition from a LAS file
    /// </summary>
    public class CurveDefinition
    {
        public string Mnemonic { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Mnemonic}.{Units} {Data}: {Description}";
        }
    }

    /// <summary>
    /// Represents well information from a LAS file
    /// </summary>
    public class WellInfo
    {
        public double? Strt { get; set; }
        public double? Stop { get; set; }
        public double? Step { get; set; }
        public Dictionary<string, string> OtherInfo { get; set; } = new();
    }

    /// <summary>
    /// Represents the parsed content of a LAS file
    /// </summary>
    public class LasFileContent
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public long FileSize { get; set; }
        public WellInfo WellInfo { get; set; } = new();
        public List<CurveDefinition> Curves { get; set; } = new();
        public List<string> CurveNames => Curves.Select(c => c.Mnemonic).ToList();
    }

    /// <summary>
    /// Parses a LAS file and extracts curve section information
    /// </summary>
    public LasFileContent ParseLasFile(string filePath)
    {
        var result = new LasFileContent 
        { 
            FilePath = filePath,
            FileSize = new FileInfo(filePath).Length
        };
        var lines = ReadFileLines(filePath);

        string currentSection = string.Empty;
        var wellInfoDict = new Dictionary<string, (string Value, string Unit)>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            // Check for section headers
            if (trimmed.StartsWith("~"))
            {
                currentSection = GetSectionType(trimmed);
                continue;
            }

            // Process lines based on current section
            switch (currentSection)
            {
                case "VERSION":
                    break;
                case "WELL":
                    ParseWellInfoLine(trimmed, wellInfoDict);
                    break;
                case "CURVE":
                    var curve = ParseCurveLine(trimmed);
                    if (curve != null)
                    {
                        result.Curves.Add(curve);
                    }
                    break;
                case "ASCII":
                    // Stop parsing when we reach the data section
                    ProcessWellInfo(wellInfoDict, result.WellInfo);
                    return result;
            }
        }

        ProcessWellInfo(wellInfoDict, result.WellInfo);
        return result;
    }

    /// <summary>
    /// Extracts only curve names from a LAS file (faster than full parse)
    /// </summary>
    public List<string> ExtractCurveNames(string filePath)
    {
        var curveNames = new List<string>();
        var lines = ReadFileLines(filePath);

        bool inCurveSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            if (trimmed.StartsWith("~"))
            {
                var sectionType = GetSectionType(trimmed);
                inCurveSection = sectionType == "CURVE";
                
                // Stop if we've passed the curve section
                if (!inCurveSection && curveNames.Count > 0)
                    break;
                    
                continue;
            }

            if (inCurveSection)
            {
                var curve = ParseCurveLine(trimmed);
                if (curve != null)
                {
                    curveNames.Add(curve.Mnemonic);
                }
            }
        }

        return curveNames;
    }

    /// <summary>
    /// Scans a directory for LAS files
    /// </summary>
    public IEnumerable<string> FindLasFiles(string directory, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(directory, "*.las", searchOption)
                       .Concat(Directory.GetFiles(directory, "*.LAS", searchOption))
                       .Distinct();
    }

    private void ProcessWellInfo(Dictionary<string, (string Value, string Unit)> wellInfoDict, WellInfo wellInfo)
    {
        if (wellInfoDict.TryGetValue("STRT", out var strt))
        {
            wellInfo.Strt = ParseDouble(strt.Value);
        }
        if (wellInfoDict.TryGetValue("STOP", out var stop))
        {
            wellInfo.Stop = ParseDouble(stop.Value);
        }
        if (wellInfoDict.TryGetValue("STEP", out var step))
        {
            wellInfo.Step = ParseDouble(step.Value);
        }

        foreach (var kvp in wellInfoDict)
        {
            if (!new[] { "STRT", "STOP", "STEP", "WELL" }.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                wellInfo.OtherInfo[kvp.Key] = kvp.Value.Value;
            }
        }
    }

    private double? ParseDouble(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        
        // Handle null values commonly used in LAS files
        var nullValues = new[] { "-999.25", "-999.2500", "-9999.25", "null", "-999", "-9999" };
        if (nullValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            return null;

        if (double.TryParse(value.Replace(',', '.'), 
            System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, 
            out var result))
        {
            return result;
        }
        return null;
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

    private string GetSectionType(string sectionHeader)
    {
        var header = sectionHeader.ToUpperInvariant();
        
        if (header.Contains("VERSION") || header.StartsWith("~V"))
            return "VERSION";
        if (header.Contains("WELL") || header.StartsWith("~W"))
            return "WELL";
        if (header.Contains("CURVE") || header.StartsWith("~C"))
            return "CURVE";
        if (header.Contains("PARAMETER") || header.StartsWith("~P"))
            return "PARAMETER";
        if (header.Contains("OTHER") || header.StartsWith("~O"))
            return "OTHER";
        if (header.Contains("ASCII") || header.StartsWith("~A"))
            return "ASCII";

        return "UNKNOWN";
    }

    private void ParseInfoLine(string line, Dictionary<string, string> dict)
    {
        // LAS format: MNEMONIC.UNIT VALUE:DESCRIPTION
        var match = Regex.Match(line, @"^(\w+)\s*\.([^:]*):(.*)$");
        if (match.Success)
        {
            var mnemonic = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();
            dict[mnemonic] = value;
        }
    }

    private void ParseWellInfoLine(string line, Dictionary<string, (string Value, string Unit)> dict)
    {
        // LAS format: MNEMONIC.UNIT VALUE:DESCRIPTION
        // Example: STRT.M 1500.0000 : START DEPTH
        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0) return;

        var beforeColon = line.Substring(0, colonIndex);
        var dotIndex = beforeColon.IndexOf('.');
        
        if (dotIndex > 0)
        {
            var mnemonic = beforeColon.Substring(0, dotIndex).Trim();
            var afterDot = beforeColon.Substring(dotIndex + 1).Trim();
            
            // Split afterDot into unit and value
            var parts = afterDot.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var unit = parts.Length > 0 ? parts[0] : string.Empty;
            var value = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : (parts.Length > 0 ? parts[0] : string.Empty);
            
            // If unit looks like a number, it's actually the value
            if (double.TryParse(unit.Replace(',', '.'), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                value = unit;
                unit = string.Empty;
            }

            dict[mnemonic] = (value, unit);
        }
        else
        {
            var parts = beforeColon.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var mnemonic = parts[0];
                var value = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
                dict[mnemonic] = (value, string.Empty);
            }
        }
    }

    private CurveDefinition? ParseCurveLine(string line)
    {
        // LAS format: MNEMONIC.UNIT DATA:DESCRIPTION
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
        {
            var beforeColon = line.Substring(0, colonIndex);
            var description = line.Substring(colonIndex + 1).Trim();

            var dotIndex = beforeColon.IndexOf('.');
            if (dotIndex > 0)
            {
                var mnemonic = beforeColon.Substring(0, dotIndex).Trim();
                var afterDot = beforeColon.Substring(dotIndex + 1).Trim();
                
                var parts = afterDot.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var units = parts.Length > 0 ? parts[0] : string.Empty;
                var data = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;

                return new CurveDefinition
                {
                    Mnemonic = mnemonic,
                    Units = units,
                    Data = data,
                    Description = description
                };
            }
            else
            {
                var mnemonic = beforeColon.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(mnemonic))
                {
                    return new CurveDefinition
                    {
                        Mnemonic = mnemonic,
                        Description = description
                    };
                }
            }
        }
        else
        {
            var parts = line.Split(new[] { ' ', '\t', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                return new CurveDefinition
                {
                    Mnemonic = parts[0]
                };
            }
        }

        return null;
    }
}
