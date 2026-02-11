using System.Text;
using System.Text.RegularExpressions;
using static LasAliasManager.Core.Constants;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Парсинг LAS файла для извлечения информации о нем
/// </summary>
public class LasFileParser
{
    /// <summary>
    /// Описание кривой
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
    /// well information
    /// </summary>
    public class WellInfo
    {
        public double? Strt { get; set; }
        public double? Stop { get; set; }
        public double? Step { get; set; }
        public Dictionary<string, string> OtherInfo { get; set; } = new();
    }

    /// <summary>
    ///parsed content of LAS file
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
    /// Parses a LAS file and extracts curve information
    /// </summary>
    public LasFileContent ParseLasFile(string filePath)
    {
        var result = new LasFileContent 
        { 
            FilePath = filePath,
            FileSize = new FileInfo(filePath).Length
        };
        //var lines = ReadFileLines(filePath);
        var lines = FileEncoder.ReadFileLines(filePath);
        string currentSection = string.Empty;
        var wellInfoDict = new Dictionary<string, (string Value, string Unit)>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            // Начало секции
            if (trimmed.StartsWith("~"))
            {
                currentSection = GetSectionType(trimmed);
                continue;
            }

            // Проходим по секциям, смотрим только на WELL и CURVE
            switch (currentSection)
            { 
                case LasSections.Version:
                    break;
                case LasSections.Well:
                    ParseWellInfoLine(trimmed, wellInfoDict);
                    break;
                case LasSections.Curve:
                    var curve = ParseCurveLine(trimmed);
                    if (curve != null)
                    {
                        result.Curves.Add(curve);
                    }
                    break;
                case LasSections.Ascii:  
                    ProcessWellInfo(wellInfoDict, result.WellInfo);
                    return result;
            }
        }

        ProcessWellInfo(wellInfoDict, result.WellInfo);
        return result;
    }

    /// <summary>
    /// Scans a directory for LAS files
    /// </summary>
    public IEnumerable<string> FindLasFiles(string directory, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(directory, "*.las", searchOption)
                       .Concat(Directory.GetFiles(directory, "*.LAS", searchOption))
                       .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void ProcessWellInfo(Dictionary<string, (string Value, string Unit)> wellInfoDict, WellInfo wellInfo)
    {
        if (wellInfoDict.TryGetValue(WellFields.Start, out var strt))
        {
            wellInfo.Strt = ParseDouble(strt.Value);
        }
        if (wellInfoDict.TryGetValue(WellFields.Stop, out var stop))
        {
            wellInfo.Stop = ParseDouble(stop.Value);
        }
        if (wellInfoDict.TryGetValue(WellFields.Step, out var step))
        {
            wellInfo.Step = ParseDouble(step.Value);
        }
    }

    private double? ParseDouble(string value)
    {        
        if (string.IsNullOrWhiteSpace(value))
            return null;
        
        if (LasNullValues.Contains(value, StringComparer.OrdinalIgnoreCase))
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


    private string GetSectionType(string sectionHeader)
    {
        var header = sectionHeader.ToUpperInvariant();

        if (header.Contains(LasSections.Version) || header.StartsWith(LasSections.VersionPrefix))
            return LasSections.Version;
        if (header.Contains(LasSections.Well) || header.StartsWith(LasSections.WellPrefix))
            return LasSections.Well;
        if (header.Contains(LasSections.Curve) || header.StartsWith(LasSections.CurvePrefix))
            return LasSections.Curve;
        if (header.Contains(LasSections.Parameter) || header.StartsWith(LasSections.ParameterPrefix))
            return LasSections.Parameter;
        if (header.Contains(LasSections.Other) || header.StartsWith(LasSections.OtherPrefix))
            return LasSections.Other;
        if (header.Contains(LasSections.Ascii) || header.StartsWith(LasSections.AsciiPrefix))
            return LasSections.Ascii;

        return LasSections.Unknown;
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
