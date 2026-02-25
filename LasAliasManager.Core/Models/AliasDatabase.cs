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
        Unknown,   // Не найдено в словаре
        Ignored,   // В словаре со значением null
        Mapped     // В словаре с базовым именем
    }


    /// <summary>
    /// Словарь:
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
    /// <returns>Базовое имя, если найдено или null, если неизвестно/игнорируется</returns>
    public string? FindBaseName(string fieldName)
    {
        var trimmed = fieldName.Trim();
        return _fieldMappings.TryGetValue(trimmed, out var baseName) ? baseName : null;
    }

    /// <summary>
    /// Проверка: игнорируется? (есть в списке && значение null)
    /// </summary>
    public bool IsIgnored(string name)
    {
        var trimmed = name.Trim();
        return _fieldMappings.TryGetValue(trimmed, out var baseName) && baseName == null;
    }

    /// <summary>
    /// Наличие имени в БД
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
    /// Добавление базового имени и соответствующих полевых
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
    /// Добавление нового полевого имени
    /// </summary>
    /// <param name="baseName">Базовое имя</param>
    /// <param name="fieldName">Полевое имя</param>
    /// <returns>True — успешно, false — нет базового имени</returns>
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
    /// Добавляет имя в список игнорируемых
    /// </summary>
    public bool AddIgnored(string name)
    {
        var trimmed = name.Trim();

        // Базовые имена нельзя игнорировать
        if (_baseNames.Contains(trimmed))
        {
            return false;
        }

        // null означает "игнорируется"
        _fieldMappings[trimmed] = null;
        return true;
    }

    /// <summary>
    /// Удаляет полевое имя из всех сопоставлений
    /// </summary>
    public bool RemoveFieldName(string fieldName)
    {
        var trimmed = fieldName.Trim();

        // Базовые имена удалять нельзя
        if (_baseNames.Contains(trimmed))
        {
            return false;
        }

        return _fieldMappings.Remove(trimmed);
    }




    /// <summary>
    /// Возвращает все базовые имена (для выпадающего списка)
    /// </summary>
    public IEnumerable<string> GetAllBaseNames()
    {
        return _baseNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Возвращает все игнорируемые имена
    /// </summary>
    public IEnumerable<string> GetAllIgnoredNames()
    {
        return _fieldMappings
            .Where(kvp => kvp.Value == null)
            .Select(kvp => kvp.Key)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Возвращает все полевые имена для заданного базового имени
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
    /// Возвращает все сопоставления, сгруппированные по базовому имени (для экспорта)
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
    /// Возвращает все игнорируемые имена в виде HashSet (для совместимости при экспорте)
    /// </summary>
    public HashSet<string> GetIgnoredNamesSet()
    {
        return new HashSet<string>(GetAllIgnoredNames(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Возвращает статистику по базе данных
    /// </summary>
    public (int BaseCount, int TotalAliases, int IgnoredCount) GetStatistics()
    {
        int baseCount = _baseNames.Count;
        int ignoredCount = _fieldMappings.Count(kvp => kvp.Value == null);
        int totalMappings = _fieldMappings.Count(kvp => kvp.Value != null);
        int aliasCount = totalMappings - baseCount; // Вычитаем базовые имена, сопоставленные сами с собой

        return (baseCount, aliasCount, ignoredCount);
    }



    /// <summary>
    /// Очищает все данные из базы
    /// </summary>
    public void Clear()
    {
        _fieldMappings.Clear();
        _baseNames.Clear();
    }


    /// <summary>
    /// Классифицирует полевое имя и возвращает базовое имя
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

    /// <summary>
    /// Удаляет базовое имя и все связанные полевые имена из базы данных.
    /// </summary>
    /// <param name="baseName">Базовое имя для удаления</param>
    /// <returns>True если имя найдено и удалено, false если не найдено</returns>
    public bool RemoveBaseName(string baseName)
    {
        var trimmed = baseName.Trim();

        if (!_baseNames.Contains(trimmed))
            return false;

        // Find all field names that map to this base name
        var aliasesToRemove = _fieldMappings
            .Where(kvp => trimmed.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        // Remove all aliases (including the base name's own self-mapping)
        foreach (var alias in aliasesToRemove)
        {
            _fieldMappings.Remove(alias);
        }

        // Remove from base names set
        _baseNames.Remove(trimmed);

        return true;
    }


}
