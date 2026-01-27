namespace LasAliasManager.Core;

/// <summary>
/// Основные базовые значения переменных
/// </summary>
public static class Constants
{
    /// <summary>
    /// Основные базовые имена
    /// </summary>
    public static class Markers
    {
        public const string Ignore = "[ИГНОРИРОВАТЬ]";
        public const string NewBase = "[НОВОЕ ИМЯ]";
        public const string Empty = "";
    }

    /// <summary>
    /// Статусы в csv файле
    /// </summary>
    public static class Status
    {
        public const string Base = "base";
        public const string Alias = "alias";
        public const string Ignore = "ignore";
    }

    /// <summary>
    /// Базовое имя для экспорта
    /// </summary>
    public static class PrimaryNames
    {
        public const string Ignored = "No";
    }

    /// <summary>
    /// Идеентификаторы в las файле
    /// </summary>
    public static class LasSections
    {
        public const string Version = "VERSION";
        public const string Well = "WELL";
        public const string Curve = "CURVE";
        public const string Parameter = "PARAMETER";
        public const string Other = "OTHER";
        public const string Ascii = "ASCII";
        public const string Unknown = "UNKNOWN";
    }

    /// <summary>
    /// Секция "Well"
    /// </summary>
    public static class WellFields
    {
        public const string Start = "STRT";
        public const string Stop = "STOP";
        public const string Step = "STEP";
        public const string Well = "WELL";
    }

    /// <summary>
    /// Null значения
    /// </summary>
    public static readonly string[] LasNullValues =
    {
        "-999.25", "-999.2500", "-9999.25", "null", "-999", "-9999"
    };

    /// <summary>
    ///  Струтура CSV файла
    /// </summary>
    public static class CsvHeaders
    {
        public const string Header = "FieldName,PrimaryName,Status,Description";
        public const int FieldNameIndex = 0;
        public const int PrimaryNameIndex = 1;
        public const int StatusIndex = 2;
        public const int DescriptionIndex = 3;
        public const int MinimumColumns = 3;
    }

    /// <summary>
    /// Структура выходного txt файла
    /// </summary>
    public static class TxtExport
    {
        public const string Header = @"Основное     Полевые
  имя         имена
===================================================================================================";
        public const string Footer = "**********************************************************************************************************";
        public const int PrimaryNameColumnWidth = 10;
    }

    public static class UiStrings
    {
        public const string StatusModified = "Изменен";
        public const string StatusUnknown = "Неизвестный";
        public const string StatusIgnored = "Игнорируется";
        public const string StatusExported = "Экпортирован";
        public const string StatusMapped = "Сопоставлен";
    }


}