namespace LasAliasManager.Core;

/// <summary>
/// Основные базовые значения переменных
/// </summary>
public static class Constants
{
    /// <summary>
    /// Основные маркеры для выпадающего списка
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
    /// Базовое имя для экспорта игнорируемых
    /// </summary>
    public static class PrimaryNames
    {
        public const string Ignored = "No";
    }

    /// <summary>
    /// Идентификаторы секций в LAS файле
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

        public const string VersionPrefix = "~V";
        public const string WellPrefix = "~W";
        public const string CurvePrefix = "~C";
        public const string ParameterPrefix = "~P";
        public const string OtherPrefix = "~O";
        public const string AsciiPrefix = "~A";
    }

    /// <summary>
    /// Поля секции "Well"
    /// </summary>
    public static class WellFields
    {
        public const string Start = "STRT";
        public const string Stop = "STOP";
        public const string Step = "STEP";
        public const string Well = "WELL";
        public const string Null = "NULL";

    }

    /// <summary>
    /// Null значения в LAS файлах
    /// </summary>
    public static readonly string[] LasNullValues =
    {
        "-999.25", "-999.2500", "-9999.25", "null", "-999", "-9999"
    };

    /// <summary>
    /// Структура CSV файла
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


    /// <summary>
    /// Расширения файлов, используемые в приложении
    /// </summary>
    public static class FileExtensions
    {
        public const string Las = ".las";
        public const string LasUpper = ".LAS";
        public const string Csv = ".csv";
        public const string Txt = ".txt";

        // Шаблоны поиска файлов
        public const string LasPattern = "*.las";
        public const string LasPatternUpper = "*.LAS";
        public const string CsvPattern = "*.csv";
        public const string TxtPattern = "*.txt";
    }

    /// <summary>
    /// Строки пользовательского интерфейса
    /// </summary>
    public static class UiStrings
    {
        public const string StatusModified = "Изменен";
        public const string StatusUnknown = "Неизвестный";
        public const string StatusIgnored = "Игнорируется";
        public const string StatusExported = "Экспортирован";
        public const string StatusMapped = "Сопоставлен";

        // Сообщения статусной строки
        public const string Ready = "Готов. Загрузите БД и выберите папку.";
        public const string LoadingDatabase = "Загрузка БД...";
        public const string AnalyzingFiles = "Анализ LAS файлов...";
        public const string Saving = "Сохранение...";
        public const string ExportingCurves = "Экспорт выбранных кривых...";

        // Заголовки диалогов
        public const string ExportTitle = "Экспорт";
        public const string ExportSuccessTitle = "Успешный экспорт";
        public const string ExportErrorTitle = "Ошибка экспорта";
        public const string UnsavedChangesTitle = "Несохраненные изменения";

        // Сообщения диалогов
        public const string NoCurvesSelected = "Нет выбранных кривых для экспорта.\n Проставьте (✓) чтобы выбрать кривые.";
        public const string NoBaseMappings = "У выбранных кривых отсутсвуют базовые имена.\n Укажите базовые имена...";
        public const string UnsavedChangesPrompt = "Найдены несохраненные иземения. Сохранить их перед выходом?";
    }

    /// <summary>
    /// Константы форматирования чисел
    /// </summary>
    public static class Formatting
    {
        public const string DepthFormat = "F2";
        public const string StepFormat = "F4";
        public const string FileSizeFormat = "0.##";

        public static readonly string[] FileSizeUnits = { "Б", "КБ", "МБ", "ГБ" };

    }



    /// <summary>
    /// Константы парсера LAS файлов
    /// </summary>
    public static class LasParser
        {
            public const char CommentChar = '#';
            public const char SectionStartChar = '~';
            public const char ColonSeparator = ':';
            public const char DotSeparator = '.';
            public const string DefaultPlaceholder = "-";
        }


    

}
