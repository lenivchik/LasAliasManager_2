using System.Text;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Определение кодировки и чтение файлов
/// </summary>
public static class FileEncoder
{
    /// <summary>
    /// Кодовая страница кириллицы (Windows-1251)
    /// </summary>
    public const int CyrillicCodePage = 1251;

    /// <summary>
    /// Читает все строки файла с автоматическим определением кодировки.
    /// Последовательность: определение через Ude → UTF-8 с BOM → UTF-8 → Latin1 (резервный вариант)
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <returns>Список строк с нормализованными переносами</returns>
    public static List<string> ReadFileLines(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        // Стратегия 1: Определение кодировки через Ude
        var udeResult = TryReadWithUdeDetection(filePath);
        if (udeResult != null)
            return udeResult;

        // Стратегия 2: Проверка UTF-8 BOM
        var bomResult = TryReadWithUtf8Bom(bytes);
        if (bomResult != null)
            return bomResult;

        // Стратегия 3: Строгая проверка UTF-8
        var utf8Result = TryReadAsUtf8(bytes);
        if (utf8Result != null)
            return utf8Result;

        // Стратегия 4: Резервный вариант — Latin1 (никогда не даёт ошибку)
        return ReadAsLatin1(bytes);
    }

    /// <summary>
    /// Читает все строки файла в кодировке ANSI (Windows-1251 для кириллицы).
    /// Используется для устаревших файлов в формате ANSI.
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <returns>Список строк с нормализованными переносами</returns>
    public static List<string> ReadFileLinesAnsi(string filePath)
    {
        var encoding = Encoding.GetEncoding(CyrillicCodePage);
        var text = File.ReadAllText(filePath, encoding);
        return SplitIntoLines(text);
    }

    /// <summary>
    /// Читает все строки файла в указанной кодировке
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <param name="encoding">Кодировка для чтения</param>
    /// <returns>Список строк с нормализованными переносами</returns>
    public static List<string> ReadFileLines(string filePath, Encoding encoding)
    {
        var text = File.ReadAllText(filePath, encoding);
        return SplitIntoLines(text);
    }

    /// <summary>
    /// Определяет кодировку файла с помощью библиотеки Ude
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <returns>Определённая кодировка или null, если определение не удалось</returns>
    public static Encoding? DetectEncoding(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var detector = new Ude.CharsetDetector();
            detector.Feed(fs);
            detector.DataEnd();

            if (!string.IsNullOrEmpty(detector.Charset))
            {
                return Encoding.GetEncoding(detector.Charset);
            }
        }
        catch
        {
            // Определение кодировки не удалось, возвращаем null
        }

        return null;
    }

    #region Вспомогательные методы

    /// <summary>
    /// Попытка чтения через определение кодировки Ude
    /// </summary>
    private static List<string>? TryReadWithUdeDetection(string filePath)
    {
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
                return SplitIntoLines(text);
            }
        }
        catch
        {
            // Определение через Ude не удалось, пробуем следующую стратегию
        }

        return null;
    }

    /// <summary>
    /// Попытка чтения с UTF-8 BOM
    /// </summary>
    private static List<string>? TryReadWithUtf8Bom(byte[] bytes)
    {
        // UTF-8 BOM: EF BB BF
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return SplitIntoLines(text);
        }

        return null;
    }

    /// <summary>
    /// Попытка чтения как строгий UTF-8
    /// </summary>
    private static List<string>? TryReadAsUtf8(byte[] bytes)
    {
        try
        {
            // Строгий UTF-8, вызывающий исключение при невалидных последовательностях
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var text = utf8Strict.GetString(bytes);
            return SplitIntoLines(text);
        }
        catch
        {
            // Не валидный UTF-8
            return null;
        }
    }

    /// <summary>
    /// Чтение как Latin1 (резервный вариант, никогда не даёт ошибку)
    /// </summary>
    private static List<string> ReadAsLatin1(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        return SplitIntoLines(text);
    }

    /// <summary>
    /// Разбивает текст на строки с нормализацией переносов
    /// </summary>
    private static List<string> SplitIntoLines(string text)
    {
        return text
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToList();
    }

    #endregion
}
