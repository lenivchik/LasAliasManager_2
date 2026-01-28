using System.Text;

namespace LasAliasManager.Core.Services;

/// <summary>
/// Определение кодировки
/// </summary>
public static class FileEncoder
{
    /// <summary>
    /// Known Windows codepage for Cyrillic (Windows-1251)
    /// </summary>
    public const int CyrillicCodePage = 1251;

    /// <summary>
    /// Reads all lines from a file with automatic encoding detection.
    /// Tries: Ude charset detection → UTF-8 with BOM → UTF-8 → Latin1 fallback
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>List of lines with line endings normalized</returns>
    public static List<string> ReadFileLines(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        // Strategy 1: Try Ude charset detection
        var udeResult = TryReadWithUdeDetection(filePath);
        if (udeResult != null)
            return udeResult;

        // Strategy 2: Check for UTF-8 BOM
        var bomResult = TryReadWithUtf8Bom(bytes);
        if (bomResult != null)
            return bomResult;

        // Strategy 3: Try strict UTF-8
        var utf8Result = TryReadAsUtf8(bytes);
        if (utf8Result != null)
            return utf8Result;

        // Strategy 4: Fallback to Latin1 (never fails)
        return ReadAsLatin1(bytes);
    }

    /// <summary>
    /// Reads all lines from a file using ANSI encoding (Windows-1251 for Cyrillic).
    /// Use this for legacy files that are known to be in ANSI format.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>List of lines with line endings normalized</returns>
    public static List<string> ReadFileLinesAnsi(string filePath)
    {
        var encoding = Encoding.GetEncoding(CyrillicCodePage);
        var text = File.ReadAllText(filePath, encoding);
        return SplitIntoLines(text);
    }

    /// <summary>
    /// Reads all lines from a file using a specific encoding.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="encoding">Encoding to use</param>
    /// <returns>List of lines with line endings normalized</returns>
    public static List<string> ReadFileLines(string filePath, Encoding encoding)
    {
        var text = File.ReadAllText(filePath, encoding);
        return SplitIntoLines(text);
    }

    /// <summary>
    /// Detects the encoding of a file using Ude library.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Detected encoding or null if detection failed</returns>
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
            // Encoding detection failed, return null
        }

        return null;
    }

    #region Private Helper Methods

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
            // Ude detection failed, try next strategy
        }

        return null;
    }

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

    private static List<string>? TryReadAsUtf8(byte[] bytes)
    {
        try
        {
            // Use strict UTF-8 that throws on invalid sequences
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var text = utf8Strict.GetString(bytes);
            return SplitIntoLines(text);
        }
        catch
        {
            // Not valid UTF-8
            return null;
        }
    }

    private static List<string> ReadAsLatin1(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        return SplitIntoLines(text);
    }

    private static List<string> SplitIntoLines(string text)
    {
        return text
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToList();
    }

    #endregion
}