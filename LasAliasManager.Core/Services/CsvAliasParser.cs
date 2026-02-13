using LasAliasManager.Core.Models;
using System.Text;
using System.Threading.Channels;
using static LasAliasManager.Core.Constants;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Парсинг и сохранение CSV файла (БД)
/// </summary>
public class CsvAliasParser
{
    /// <summary>
    /// Элемент БД
    /// </summary>
    public class AliasRecord
    {
        public string FieldName { get; set; } = "";
        public string PrimaryName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Загружает псевдонимы из CSV файла непосредственно в AliasDatabase
    /// </summary>
    public void LoadInto(string filePath, AliasDatabase database)
    {
        var records = ReadCsvRecords(filePath);

        foreach (var record in records)
        {
            switch (record.Status.ToLowerInvariant())
            {
                case Status.Base:
                    database.AddBaseName(record.FieldName);
                    break;

                case Status.Alias:
                    if (!string.IsNullOrWhiteSpace(record.PrimaryName))
                    {
                        if (!database.IsBaseName(record.PrimaryName))
                            database.AddBaseName(record.PrimaryName);

                        database.AddAliasToBase(record.PrimaryName, record.FieldName);
                    }
                    break;

                case Status.Ignore:
                    database.AddIgnored(record.FieldName);
                    break;
            }
        }
    }

    /// <summary>
    /// Сохранение в CSV файл
    /// </summary>
    public void SaveCsvFile(string filePath, Dictionary<string, List<string>> aliases, HashSet<string> ignored)
    {
        var records = new List<AliasRecord>();

        // Базовые имена
        foreach (var baseName in aliases.Keys.OrderBy(k => k))
        {
            records.Add(new AliasRecord
            {
                FieldName = baseName,
                PrimaryName = baseName,
                Status = Status.Base,
                Description = string.Empty
            });

            // Полевые имена
            foreach (var alias in aliases[baseName].OrderBy(a => a))
            {
                if (!alias.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    records.Add(new AliasRecord
                    {
                        FieldName = alias,
                        PrimaryName = baseName,
                        Status = Status.Alias,
                        Description = string.Empty
                    });
                }
            }
        }

        // Игнорируемые
        foreach (var name in ignored.OrderBy(n => n))
        {
            records.Add(new AliasRecord
            {
                FieldName = name,
                PrimaryName = string.Empty,
                Status = Status.Ignore,
                Description = string.Empty
            });
        }

        WriteCsvFile(filePath, records);
    }

    /// <summary>
    /// Чтение записей CSV
    /// </summary>
    public List<AliasRecord> ReadCsvRecords(string filePath)
    {
        var records = new List<AliasRecord>();
        var lines = FileEncoder.ReadFileLines(filePath);

        bool isFirstLine = true;

        foreach (var line in lines)
        {
            // Пропускаем заголовок
            if (isFirstLine)
            {
                isFirstLine = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            // Проверяем количество полей
            if (fields.Length >= CsvHeaders.MinimumColumns)
            {
                records.Add(new AliasRecord
                {
                    FieldName = fields[0],
                    PrimaryName = fields.Length > 1 ? fields[1] : "",
                    Status = fields.Length > 2 ? fields[2] : "",
                    Description = fields.Length > 3 ? fields[3] : ""
                });
            }
        }

        return records;
    }

    /// <summary>
    /// Запись в CSV файл
    /// </summary>
    private void WriteCsvFile(string filePath, List<AliasRecord> records)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));

        // Заголовок
        writer.WriteLine(CsvHeaders.Header);

        foreach (var record in records)
        {
            writer.WriteLine($"{EscapeCsv(record.FieldName)},{EscapeCsv(record.PrimaryName)},{EscapeCsv(record.Status)},{EscapeCsv(record.Description)}");
        }
    }

    /// <summary>
    /// Разбирает строку CSV с учётом кавычек
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Проверяем экранированную кавычку
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // Пропускаем следующую кавычку
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        // Добавляем последнее поле
        fields.Add(currentField.ToString());

        return fields.ToArray();
    }

    /// <summary>
    /// Экранирует значение для CSV (оборачивает в кавычки при необходимости)
    /// </summary>
    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

}
