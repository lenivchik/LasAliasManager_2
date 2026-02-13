using LasAliasManager.Core.Models;
using static LasAliasManager.Core.Models.AliasDatabase;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Основной сервис для управления псевдонимами кривых
/// </summary>
public class AliasManager
{
    private readonly CsvAliasParser _csvParser;
    private readonly LasFileParser _lasParser;

    public AliasDatabase Database { get; private set; }

    /// <summary>
    /// Текущий путь к файлу базы данных (CSV или TXT)
    /// </summary>
    public string? DatabaseFilePath { get; private set; }
    public AliasManager()
    {
        _csvParser = new CsvAliasParser();
        _lasParser = new LasFileParser();
        Database = new AliasDatabase();
    }

    /// <summary>
    /// Загружает базу данных псевдонимов из CSV файла
    /// </summary>
    public void LoadFromCsv(string csvFilePath)
    {
        Database = new AliasDatabase();
        DatabaseFilePath = csvFilePath;
        _csvParser.LoadInto(csvFilePath, Database);

    }

    /// <summary>
    /// Сохраняет текущую базу данных в CSV файл
    /// </summary>
    public void SaveToCsv(string? csvFilePath = null)
    {
        csvFilePath ??= DatabaseFilePath;

        if (string.IsNullOrEmpty(csvFilePath))
        {
            throw new InvalidOperationException("Путь к CSV файлу не указан");
        }

        var aliases = Database.GetAllAliasesGrouped();
        var ignored = Database.GetIgnoredNamesSet();

        _csvParser.SaveCsvFile(csvFilePath, aliases, ignored);
        DatabaseFilePath = csvFilePath;
    }


    /// <summary>
    /// Анализирует один LAS файл на наличие неизвестных имён кривых
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
                var (classification, baseName) = Database.Classify(cleanName);  // Один поиск в словаре!

                switch (classification)
                {
                    case CurveClassification.Ignored:
                        result.IgnoredCurves.Add(cleanName);
                        break;

                    case CurveClassification.Mapped:
                        if (!result.MappedCurves.TryGetValue(baseName!, out var list))
                        {
                            list = new List<string>();
                            result.MappedCurves[baseName!] = list;
                        }
                        list.Add(cleanName);
                        break;

                    case CurveClassification.Unknown:
                        result.UnknownCurves.Add(cleanName);
                        break;
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
    /// Анализирует несколько LAS файлов в директории
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
    /// Добавляет неизвестную кривую как полевое имя к существующему базовому имени
    /// </summary>
    public bool AddAsAlias(string curveName, string baseName)
    {
        return Database.AddAliasToBase(baseName, curveName);
    }

    /// <summary>
    /// Добавляет неизвестную кривую как новое базовое имя
    /// </summary>
    public void AddAsNewBase(string curveName)
    {
        Database.AddBaseName(curveName, new[] { curveName });
    }

    /// <summary>
    /// Добавляет неизвестную кривую в список игнорируемых
    /// </summary>
    public bool AddAsIgnored(string curveName)
    {
        return Database.AddIgnored(curveName);
    }

    /// <summary>
    /// Очищает имя кривой от лишних пробелов
    /// </summary>
    private string CleanCurveName(string name)
    {
        return name.Trim();
    }
}

/// <summary>
/// Результат анализа LAS файла
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
    /// Кривые, сопоставленные с базовыми именами
    /// Ключ: базовое имя, Значение: список найденных полевых имен
    /// </summary>
    public Dictionary<string, List<string>> MappedCurves { get; set; } = new();
    
    /// <summary>
    /// Кривые из списка игнорируемых
    /// </summary>
    public List<string> IgnoredCurves { get; set; } = new();
    
    /// <summary>
    /// Кривые, не найденные ни в псевдонимах, ни в списке игнорируемых
    /// </summary>
    public List<string> UnknownCurves { get; set; } = new();
    
    /// <summary>
    /// Сообщение об ошибке при парсинге
    /// </summary>
    public string? Error { get; set; }

    public bool HasUnknown => UnknownCurves.Count > 0;
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Получает описание кривой по мнемонике
    /// </summary>
    public LasFileParser.CurveDefinition? GetCurveDefinition(string mnemonic)
    {
        return CurveDefinitions.FirstOrDefault(c =>
            c.Mnemonic.Equals(mnemonic, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString()
    {
        if (HasError)
            return $"{FileName}: ОШИБКА - {Error}";
        
        return $"{FileName}: {TotalCurves} кривых ({MappedCurves.Count} сопоставлено, {IgnoredCurves.Count} игнорируется, {UnknownCurves.Count} неизвестных)";
    }
}

/// <summary>
/// Представляет неизвестную кривую с информацией об исходном файле
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
        return $"{CurveName} (из: {SourceFilePath})";
    }
}
