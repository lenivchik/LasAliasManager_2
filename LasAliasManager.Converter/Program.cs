using LasAliasManager.Core.Services;

namespace LasAliasManager.Converter;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("  TXT to CSV Alias Database Converter");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        string ignoredNamesFile;
        string primaryNamesFile;
        string aliasFile;
        string outputCsvFile;

        if (args.Length >= 4)
        {
            // Command line mode
            ignoredNamesFile = args[0];
            primaryNamesFile = args[1];
            aliasFile = args[2];
            outputCsvFile = args[3];
        }
        else
        {
            // Interactive mode
            Console.WriteLine("Enter the paths to the input files:");
            Console.WriteLine();

            Console.Write("File 1 - Ignored names file (e.g., ListNameAlias_NO.txt): ");
            ignoredNamesFile = Console.ReadLine()?.Trim() ?? "";

            Console.Write("File 2 - Primary names file (base names only): ");
            primaryNamesFile = Console.ReadLine()?.Trim() ?? "";

            Console.Write("File 3 - Alias file (primary names with field names): ");
            aliasFile = Console.ReadLine()?.Trim() ?? "";

            Console.Write("Output CSV file path: ");
            outputCsvFile = Console.ReadLine()?.Trim() ?? "";
        }

        // Validate files exist
        if (!File.Exists(ignoredNamesFile))
        {
            Console.WriteLine($"Error: Ignored names file not found: {ignoredNamesFile}");
            return;
        }

        if (!File.Exists(primaryNamesFile))
        {
            Console.WriteLine($"Error: Primary names file not found: {primaryNamesFile}");
            return;
        }

        if (!File.Exists(aliasFile))
        {
            Console.WriteLine($"Error: Alias file not found: {aliasFile}");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputCsvFile))
        {
            Console.WriteLine("Error: Output file path is required");
            return;
        }

        try
        {
            Console.WriteLine();
            Console.WriteLine("Converting...");

            var converter = new AliasFormatConverter();
            converter.ConvertToCSV(ignoredNamesFile, primaryNamesFile, aliasFile, outputCsvFile);

            Console.WriteLine();
            Console.WriteLine($"Success! CSV file created: {outputCsvFile}");
            Console.WriteLine();

            // Show summary
            var lines = File.ReadAllLines(outputCsvFile);
            var baseCount = lines.Count(l => l.EndsWith(",base,") || l.Contains(",base,"));
            var aliasCount = lines.Count(l => l.EndsWith(",alias,") || l.Contains(",alias,"));
            var ignoreCount = lines.Count(l => l.EndsWith(",ignore,") || l.Contains(",ignore,"));

            Console.WriteLine("Summary:");
            Console.WriteLine($"  Base names:    {baseCount}");
            Console.WriteLine($"  Aliases:       {aliasCount}");
            Console.WriteLine($"  Ignored:       {ignoreCount}");
            Console.WriteLine($"  Total records: {lines.Length - 1}"); // -1 for header
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during conversion: {ex.Message}");
        }
    }
}
