using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LasAliasManager.Core.Models;
/// <summary>
/// Внутреннее представление БД, 2 списка:
/// 1. список полевое имя -> базовое имя
/// 2. список базовых имен
/// </summary>

public class AliasDatabase
{

    public enum CurveClassification
    {
        Unknown,   // Not in dictionary
        Ignored,   // In dictionary with null value
        Mapped     // In dictionary with base name
    }


    /// <summary>
    ///Словарь:
    /// - Ключ: полевое имя
    /// - Значение: базовое значение или null, если игнорируется
    /// </summary>
    private readonly Dictionary<string, string?> _fieldMappings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Отдельный список базовых имен (для выпадающего списка)
    /// </summary>
    private readonly HashSet<string> _baseNames = new(StringComparer.OrdinalIgnoreCase);



    /// <summary>
    /// Находит базовое имя для заданного полевого имени
    /// </summary>
    /// <returns> Базовое имя, если найдено или null, если неизвестно/игнорируется</returns>
    public string? FindBaseName(string fieldName)
    {
        var trimmed = fieldName.Trim();
        return _fieldMappings.TryGetValue(trimmed, out var baseName) ? baseName : null;
    }

    /// <summary>
    /// Проверка: игнориуется? (есть в списке && значение null) .
    /// </summary>
    public bool IsIgnored(string name)
    {
        var trimmed = name.Trim();
        return _fieldMappings.TryGetValue(trimmed, out var baseName) && baseName == null;
    }

    /// <summary>
    /// Наличие имени в БД.
    /// </summary>
    public bool IsKnown(string name)
    {
        return _fieldMappings.ContainsKey(name.Trim());
    }

    /// <summary>
    /// Проверка на базовое имя
    /// </summary>
    public bool IsBaseName(string name)
    {
        return _baseNames.Contains(name.Trim());
    }



    /// <summary>
    /// Добавление базового имени и соответвующих полевых 
    /// </summary>
    public void AddBaseName(string baseName, IEnumerable<string>? aliases = null)
    {
        var trimmedBase = baseName.Trim();

        _baseNames.Add(trimmedBase);

        _fieldMappings[trimmedBase] = trimmedBase;

        if (aliases != null)
        {
            foreach (var alias in aliases)
            {
                var trimmedAlias = alias.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedAlias))
                {
                    _fieldMappings[trimmedAlias] = trimmedBase;
                }
            }
        }
    }

    /// <summary>
    /// Добавление нового полевого имени.
    /// </summary>
    /// <param name="baseName"> Базовое имя </param>
    /// <param name="fieldName"> Полевое имя </param>
    /// <returns>True successfull, false нет базового имени</returns>
    public bool AddAliasToBase(string baseName, string fieldName)
    {
        var trimmedBase = baseName.Trim();
        if (!_baseNames.Contains(trimmedBase))
        {
            return false;
        }
        _fieldMappings[fieldName.Trim()] = trimmedBase;
        return true;
    }

    /// <summary>
    /// Adds a name to the ignored list.
    /// </summary>
    public bool AddIgnored(string name)
    {
        var trimmed = name.Trim();

        // Don't ignore base names
        if (_baseNames.Contains(trimmed))
        {
            return false;
        }

        // null value indicates ignored
        _fieldMappings[trimmed] = null;
        return true;
    }

    /// <summary>
    /// Removes a field name from all mappings.
    /// </summary>
    public bool RemoveFieldName(string fieldName)
    {
        var trimmed = fieldName.Trim();

        // Don't remove base names
        if (_baseNames.Contains(trimmed))
        {
            return false;
        }

        return _fieldMappings.Remove(trimmed);
    }




    /// <summary>
    /// Gets all base names (for dropdown population).
    /// </summary>
    public IEnumerable<string> GetAllBaseNames()
    {
        return _baseNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all ignored names.
    /// </summary>
    public IEnumerable<string> GetAllIgnoredNames()
    {
        return _fieldMappings
            .Where(kvp => kvp.Value == null)
            .Select(kvp => kvp.Key)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all aliases for a specific base name.
    /// </summary>
    public IEnumerable<string> GetAliasesForBase(string baseName)
    {
        var trimmedBase = baseName.Trim();
        return _fieldMappings
            .Where(kvp => kvp.Value == trimmedBase && kvp.Key != trimmedBase)
            .Select(kvp => kvp.Key)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all mappings grouped by base name (for export).
    /// </summary>
    public Dictionary<string, List<string>> GetAllAliasesGrouped()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var baseName in _baseNames)
        {
            result[baseName] = GetAliasesForBase(baseName).ToList();
        }

        return result;
    }

    /// <summary>
    /// Gets all ignored names as a HashSet (for export compatibility).
    /// </summary>
    public HashSet<string> GetIgnoredNamesSet()
    {
        return new HashSet<string>(GetAllIgnoredNames(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets statistics about the database.
    /// </summary>
    public (int BaseCount, int TotalAliases, int IgnoredCount) GetStatistics()
    {
        int baseCount = _baseNames.Count;
        int ignoredCount = _fieldMappings.Count(kvp => kvp.Value == null);
        int totalMappings = _fieldMappings.Count(kvp => kvp.Value != null);
        int aliasCount = totalMappings - baseCount; // Subtract base names mapped to themselves

        return (baseCount, aliasCount, ignoredCount);
    }



    /// <summary>
    /// Clears all data from the database.
    /// </summary>
    public void Clear()
    {
        _fieldMappings.Clear();
        _baseNames.Clear();
    }


    /// <summary>
    /// Classifies a field name and returns base name
    /// </summary>
    public (CurveClassification Classification, string? BaseName) Classify(string fieldName)
    {
        var trimmed = fieldName.Trim();

        if (_fieldMappings.TryGetValue(trimmed, out var baseName))
        {
            return baseName == null
                ? (CurveClassification.Ignored, null)
                : (CurveClassification.Mapped, baseName);
        }

        return (CurveClassification.Unknown, null);
    }
}



