using System.Text;
using static LasAliasManager.Core.Constants;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Экспорт в ListNamesAlias.txt
/// </summary>
public class ListNamesAliasExporter
{
    /// <summary>
    /// Экспортирует базу данных в формат ListNamesAlias.txt
    /// Сортировка по базовому имени, затем по полевому имени внутри каждой группы
    /// </summary>
    /// <param name="filePath">Путь к выходному файлу</param>
    /// <param name="aliases">Словарь базовых имен с их полевыми именами</param>
    /// <param name="ignoredNames">Набор игнорируемых имен (будет использовано "No" как базовое имя)</param>
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
    /// Дополняет существующий файл ListNamesAlias.txt новыми записями
    /// После добавления пересортировывает весь файл
    /// </summary>
    /// <param name="filePath">Путь к существующему файлу</param>
    /// <param name="newAliases">Новые псевдонимы для добавления</param>
    /// <param name="newIgnored">Новые игнорируемые имена для добавления</param>
    public void AppendAndSort(string filePath, Dictionary<string, List<string>> newAliases, HashSet<string> newIgnored)
    {
        // Загружаем существующие записи
        var existingEntries = LoadExistingEntries(filePath);

        // Добавляем новые псевдонимы
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

        // Добавляем новые игнорируемые имена
        foreach (var ignored in newIgnored)
        {
            var entry = (PrimaryNames.Ignored, ignored);
            if (!existingEntries.Contains(entry))
            {
                existingEntries.Add(entry);
            }
        }

        // Сортируем и записываем
        var sorted = existingEntries
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
    /// Загружает существующие записи из файла ListNamesAlias.txt
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

            // Пропускаем заголовок и подвал
            if (trimmed.StartsWith("===") || trimmed.StartsWith("***"))
            {
                headerPassed = true;
                continue;
            }

            if (trimmed.StartsWith("Основное") || trimmed.StartsWith("имя"))
                continue;

            if (!headerPassed)
                continue;

            // Парсим запись: БазовоеИмя ПолевоеИмя [необязательно :формула]
            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var primaryName = parts[0];

                // Полевое имя — всё после базового имени, но до "." если присутствует
                var restOfLine = trimmed.Substring(primaryName.Length).Trim();
                var colonIndex = restOfLine.IndexOf('.');
                var fieldName = colonIndex > 0
                    ? restOfLine.Substring(0, colonIndex).Trim()
                    : restOfLine.Trim();

                // Очищаем полевое имя от завершающих точек и пробелов
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
