using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LasAliasManager.Core.Services;

namespace LasAliasManager.GUI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AliasManager _aliasManager;

    /// <summary>
    /// Tracks mappings defined by the user during this session (FieldName -> PrimaryName)
    /// </summary>
    private readonly Dictionary<string, string> _userDefinedMappings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tracks names the user marked as ignored during this session
    /// </summary>
    private readonly HashSet<string> _userDefinedIgnored = new(StringComparer.OrdinalIgnoreCase);



    [ObservableProperty]
    private ObservableCollection<LasFileViewModel> _lasFiles = new();

    [ObservableProperty]
    private ObservableCollection<LasFileViewModel> _filteredFiles = new();

    [ObservableProperty]
    private LasFileViewModel? _selectedFile;

    [ObservableProperty]
    private ObservableCollection<CurveRowViewModel> _filteredCurves = new();

    [ObservableProperty]
    private int _selectedForExportCount;

    [ObservableProperty]
    private ObservableCollection<string> _availablePrimaryNames = new();

    [ObservableProperty]
    private string _statusMessage = "Готов. Загрузите БД и выберите папку.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _databaseFilePath = string.Empty;

    [ObservableProperty]
    private string _currentFolderPath = string.Empty;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _totalCurves;

    [ObservableProperty]
    private int _unknownCurves;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _showOnlyUnknown;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private CurveRowViewModel? _selectedCurveRow;

    [ObservableProperty]
    private string _selectedCurveInfo = string.Empty;

    /// <summary>
    /// Number of user-defined mappings available for export
    /// </summary>
    public int UserDefinedCount => _userDefinedMappings.Count + _userDefinedIgnored.Count;
    public string? AliasFilePath => _aliasManager.AliasFilePath;
    public string? IgnoredFilePath => _aliasManager.IgnoredFilePath;
    public DatabaseFormat CurrentFormat => _aliasManager.CurrentFormat;

    partial void OnSelectedCurveRowChanged(CurveRowViewModel? value)
    {
        if (value != null)
        {
            var info = $"Кривая: {value.CurveFieldName}";
            if (!string.IsNullOrWhiteSpace(value.CurveUnits))
                info += $"  |  Единицы измерения: {value.CurveUnits}";
            if (!string.IsNullOrWhiteSpace(value.CurveDescription))
                info += $"  |  Описание: {value.CurveDescription}";
            SelectedCurveInfo = info;
        }
        else
        {
            SelectedCurveInfo = string.Empty;
        }
    }

    public MainWindowViewModel()
    {
        _aliasManager = new AliasManager();
        
        AvailablePrimaryNames.Add("");
        AvailablePrimaryNames.Add("[ИГНОРИРОВАТЬ]");
        AvailablePrimaryNames.Add("[НОВОЕ ИМЯ]");
    }

    partial void OnSelectedFileChanged(LasFileViewModel? value)
    {
        ApplyCurveFilter();
    }

    partial void OnShowOnlyUnknownChanged(bool value)
    {
        ApplyFileFilter();
        ApplyCurveFilter();
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFileFilter();
        ApplyCurveFilter();
    }

    [RelayCommand]
    private async Task LoadCsvDatabaseAsync(string? csvPath)
    {
        if (string.IsNullOrEmpty(csvPath))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Загрузка БД...";

            await Task.Run(() =>
            {
                _aliasManager.LoadFromCsv(csvPath);
            });

            DatabaseFilePath = csvPath;
            RefreshAvailablePrimaryNames();

            var stats = _aliasManager.Database.GetStatistics();
            StatusMessage = $"База данных загружена: {stats.BaseCount} Основных имен, {stats.TotalAliases} Полевых имен, {stats.IgnoredCount} Игнорируются";
            if (!string.IsNullOrEmpty(CurrentFolderPath))
            {
                await ReanalyzeLoadedFilesAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при загрузке CSV: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ReanalyzeLoadedFilesAsync()
    {
        StatusMessage = "Анализ файлов...";

        await Task.Run(() =>
        {
            foreach (var fileVm in LasFiles)
            {
                var result = _aliasManager.AnalyzeLasFile(fileVm.FilePath);
                if (result.HasError)
                    continue;

                // Update each curve's status
                foreach (var curve in fileVm.Curves)
                {
                    var fieldName = curve.CurveFieldName;

                    if (_aliasManager.Database.IsIgnored(fieldName))
                    {
                        curve.OriginalPrimaryName = "[ИГНОРИРОВАТЬ]";
                        curve.PrimaryName = "[ИГНОРИРОВАТЬ]";
                        curve.IsUnknown = false;
                        curve.IsIgnored = true;
                    }
                    else
                    {
                        var baseName = _aliasManager.Database.FindBaseName(fieldName);
                        if (baseName != null)
                        {
                            curve.OriginalPrimaryName = baseName;
                            curve.PrimaryName = baseName;
                            curve.IsUnknown = false;
                            curve.IsIgnored = false;
                        }
                        else
                        {
                            curve.OriginalPrimaryName = "";
                            curve.PrimaryName = "";
                            curve.IsUnknown = true;
                            curve.IsIgnored = false;
                        }
                    }
                    curve.IsModified = false;
                }

                fileVm.RefreshStatus();
            }
        });

        // Update totals
        TotalCurves = LasFiles.Sum(f => f.CurveCount);
        UnknownCurves = LasFiles.Sum(f => f.UnknownCount);

        // Refresh current view
        ApplyFileFilter();
        ApplyCurveFilter();
        UpdateHasUnsavedChanges();

        StatusMessage = $"Re-analyzed {LasFiles.Count} files, {TotalCurves} curves ({UnknownCurves} unknown)";
    }



  

    private void RefreshAvailablePrimaryNames()
    {
        AvailablePrimaryNames.Clear();
        AvailablePrimaryNames.Add("");
        AvailablePrimaryNames.Add("[ИГНОРИРОВАТЬ]");
        AvailablePrimaryNames.Add("[НОВОЕ ИМЯ]");

        foreach (var baseName in _aliasManager.Database.GetAllBaseNames())
        {
            AvailablePrimaryNames.Add(baseName);
        }
    }

    [RelayCommand]
    private async Task LoadFolderAsync(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Анализ LAS файлов...";
            CurrentFolderPath = folderPath;

            var results = await Task.Run(() => 
                _aliasManager.AnalyzeDirectory(folderPath, IncludeSubfolders));

            LasFiles.Clear();
            int totalUnknown = 0;
            int totalCurvesCount = 0;

            foreach (var result in results)
            {
                if (result.HasError)
                    continue;

                var fileVm = new LasFileViewModel
                {
                    FilePath = result.FilePath,
                    FileName = result.FileName,
                    FileSize = result.FileSize,
                    Top = result.WellInfo.Strt,
                    Bottom = result.WellInfo.Stop,
                    Step = result.WellInfo.Step,
                    CurveCount = result.TotalCurves
                };

                // Add mapped curves
                foreach (var mapped in result.MappedCurves)
                {
                    foreach (var fieldName in mapped.Value)
                    {
                        var row = CreateCurveRow(fieldName, fileVm);
                        var curveDef = result.GetCurveDefinition(fieldName);
                        if (curveDef != null)
                        {
                            row.CurveDescription = curveDef.Description;
                            row.CurveUnits = curveDef.Units;
                        }
                        row.OriginalPrimaryName = mapped.Key;
                        row.PrimaryName = mapped.Key;
                        row.IsUnknown = false;
                        row.IsIgnored = false;
                        fileVm.Curves.Add(row);
                    }
                }

                // Add ignored curves
                foreach (var ignored in result.IgnoredCurves)
                {
                    var row = CreateCurveRow(ignored, fileVm);
                    var curveDef = result.GetCurveDefinition(ignored);
                    if (curveDef != null)
                    {
                        row.CurveDescription = curveDef.Description;
                        row.CurveUnits = curveDef.Units;
                    }
                    row.OriginalPrimaryName = "[ИГНОРИРОВАТЬ]";
                    row.PrimaryName = "[ИГНОРИРОВАТЬ]";
                    row.IsUnknown = false;
                    row.IsIgnored = true;
                    fileVm.Curves.Add(row);
                }

                // Add unknown curves
                foreach (var unknown in result.UnknownCurves)
                {
                    var row = CreateCurveRow(unknown, fileVm);
                    var curveDef = result.GetCurveDefinition(unknown);
                    if (curveDef != null)
                    {
                        row.CurveDescription = curveDef.Description;
                        row.CurveUnits = curveDef.Units;
                    }
                    row.OriginalPrimaryName = "";
                    row.PrimaryName = "";
                    row.IsUnknown = true;
                    row.IsIgnored = false;
                    fileVm.Curves.Add(row);
                }

                fileVm.RefreshStatus();
                totalUnknown += fileVm.UnknownCount;
                totalCurvesCount += fileVm.CurveCount;

                LasFiles.Add(fileVm);
            }

            TotalFiles = LasFiles.Count;
            TotalCurves = totalCurvesCount;
            UnknownCurves = totalUnknown;

            // Select first file if available
            ApplyFileFilter();
            if (FilteredFiles.Count > 0)
            {
                SelectedFile = FilteredFiles[0];
            }
            ApplyCurveFilter();

            StatusMessage = $"Загружено: {TotalFiles} файлов, {TotalCurves} кривых ({UnknownCurves} неизвестных)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading folder: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }


    private CurveRowViewModel CreateCurveRow(string curveName, LasFileViewModel parentFile)
    {
        var row = new CurveRowViewModel
        {
            AvailablePrimaryNames = AvailablePrimaryNames,
            CurveFieldName = curveName
        };

        // Wire up modification callback
        row.OnModificationChanged = () =>
        {
            parentFile.RefreshStatus();
            UpdateHasUnsavedChanges();

            // Update total unknown count
            UnknownCurves = LasFiles.Sum(f => f.UnknownCount);

            // Update file filter if "Show Only Unknown" is active
            if (ShowOnlyUnknown)
            {
                ApplyFileFilter();
            }
        };

        // Wire up selection for export callback
        row.OnSelectionForExportChanged = () =>
        {
            UpdateSelectedForExportCount();
        };

        return row;
    }


    private void UpdateSelectedForExportCount()
    {
        SelectedForExportCount = LasFiles.Sum(f => f.Curves.Count(c => c.IsSelectedForExport));
    }

    private void ApplyFileFilter()
    {
        var filtered = LasFiles.AsEnumerable();

        if (ShowOnlyUnknown)
        {
            // Only show files that have unknown or modified curves
            filtered = filtered.Where(f => f.HasUnknown || f.HasModified);
        }

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var filter = FilterText.ToLowerInvariant();
            filtered = filtered.Where(f =>
                f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.Curves.Any(c => c.CurveFieldName.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredFiles = new ObservableCollection<LasFileViewModel>(filtered);

        // If currently selected file is no longer in filtered list, select first available
        if (SelectedFile != null && !FilteredFiles.Contains(SelectedFile))
        {
            SelectedFile = FilteredFiles.FirstOrDefault();
        }
    }

    private void ApplyCurveFilter()
    {
        if (SelectedFile == null)
        {
            FilteredCurves = new ObservableCollection<CurveRowViewModel>();
            return;
        }

        var filtered = SelectedFile.Curves.AsEnumerable();

        if (ShowOnlyUnknown)
        {
            filtered = filtered.Where(c => c.IsUnknown || c.IsModified);
        }

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var filter = FilterText.ToLowerInvariant();
            filtered = filtered.Where(c =>
                c.CurveFieldName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (c.PrimaryName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredCurves = new ObservableCollection<CurveRowViewModel>(filtered);
        UpdateHasUnsavedChanges();
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (_aliasManager.CurrentFormat == DatabaseFormat.None)
        {
            StatusMessage = "Error: No database loaded";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Сохранение...";

            var modifiedCurves = new List<CurveRowViewModel>();
            var newBaseNames = new List<string>();

            // Collect all modified curves from all files
            foreach (var file in LasFiles)
            {
                modifiedCurves.AddRange(file.Curves.Where(c => c.IsModified));
            }

            // Build a dictionary of new mappings for quick lookup
            var newMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newIgnored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var curve in modifiedCurves)
            {
                if (curve.PrimaryName == "[ИГНОРИРОВАТЬ]")
                {
                    _aliasManager.AddAsIgnored(curve.CurveFieldName);
                    newIgnored.Add(curve.CurveFieldName);
                }
                else if (curve.PrimaryName == "[НОВОЕ ИМЯ]")
                {
                    _aliasManager.AddAsNewBase(curve.CurveFieldName);
                    newBaseNames.Add(curve.CurveFieldName);
                    newMappings[curve.CurveFieldName] = curve.CurveFieldName;
                }
                else if (!string.IsNullOrEmpty(curve.PrimaryName))
                {
                    _aliasManager.AddAsAlias(curve.CurveFieldName, curve.PrimaryName);
                    newMappings[curve.CurveFieldName] = curve.PrimaryName;
                }
            }

            await Task.Run(() =>
            {
                _aliasManager.SaveToFiles();
            });

            // Add new base names to available list
            foreach (var newBase in newBaseNames)
            {
                if (!AvailablePrimaryNames.Contains(newBase))
                {
                    AvailablePrimaryNames.Add(newBase);
                }
            }

            // Update ALL curves across ALL files that match the new mappings
            foreach (var file in LasFiles)
            {
                foreach (var curve in file.Curves)
                {
                    var fieldName = curve.CurveFieldName;

                    // Check if this curve was just mapped
                    if (newMappings.TryGetValue(fieldName, out var primaryName))
                    {
                        curve.OriginalPrimaryName = primaryName;
                        curve.PrimaryName = primaryName;
                        curve.IsModified = false;
                        curve.IsUnknown = false;
                        curve.IsIgnored = false;
                    }
                    else if (newIgnored.Contains(fieldName))
                    {
                        curve.OriginalPrimaryName = "[ИГНОРИРОВАТЬ]";
                        curve.PrimaryName = "[ИГНОРИРОВАТЬ]";
                        curve.IsModified = false;
                        curve.IsUnknown = false;
                        curve.IsIgnored = true;
                    }
                    else if (curve.IsModified)
                    {
                        // This was a modified curve that we already processed
                        curve.OriginalPrimaryName = curve.PrimaryName;
                        curve.IsModified = false;
                        curve.IsUnknown = string.IsNullOrEmpty(curve.PrimaryName);
                        curve.IsIgnored = curve.PrimaryName == "[ИГНОРИРОВАТЬ]";
                    }
                }

                file.RefreshStatus();
            }

            // Update totals
            UnknownCurves = LasFiles.Sum(f => f.UnknownCount);

            HasUnsavedChanges = false;
            ApplyFileFilter();
            ApplyCurveFilter();
            StatusMessage = $"Сохранено {modifiedCurves.Count} изменений";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
   [RelayCommand]
    private void RefreshView()
    {
        foreach (var file in LasFiles)
        {
            file.RefreshStatus();
        }
        ApplyFileFilter();
        ApplyCurveFilter();
    }

    private void UpdateHasUnsavedChanges()
    {
        HasUnsavedChanges = LasFiles.Any(f => f.Curves.Any(c => c.IsModified));
    }

    [RelayCommand]
    private void ClearAllChanges()
    {
        foreach (var file in LasFiles)
        {
            foreach (var curve in file.Curves.Where(c => c.IsModified))
            {
                curve.PrimaryName = curve.OriginalPrimaryName;
            }
            file.RefreshStatus();
        }

        UpdateHasUnsavedChanges();
        ApplyFileFilter();
        ApplyCurveFilter();
    }

    [RelayCommand]
    private void ClearExportHistory()
    {
        _userDefinedMappings.Clear();
        _userDefinedIgnored.Clear();
        OnPropertyChanged(nameof(UserDefinedCount));
        StatusMessage = "Export history cleared";
    }

    [RelayCommand]
    private async Task ExportListNamesAliasAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        if (_userDefinedMappings.Count == 0 && _userDefinedIgnored.Count == 0)
        {
            StatusMessage = "No user-defined mappings to export. Save changes first.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Exporting to ListNamesAlias.txt...";

            await Task.Run(() =>
            {
                var exporter = new ListNamesAliasExporter();

                // Convert user-defined mappings to the format expected by exporter
                // Group by PrimaryName
                var userAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in _userDefinedMappings)
                {
                    var fieldName = kvp.Key;
                    var primaryName = kvp.Value;

                    if (!userAliases.ContainsKey(primaryName))
                    {
                        userAliases[primaryName] = new List<string>();
                    }

                    if (!userAliases[primaryName].Contains(fieldName, StringComparer.OrdinalIgnoreCase))
                    {
                        userAliases[primaryName].Add(fieldName);
                    }
                }

                // If file exists, append and sort; otherwise create new
                if (File.Exists(filePath))
                {
                    exporter.AppendAndSort(filePath, userAliases, _userDefinedIgnored);
                }
                else
                {
                    exporter.Export(filePath, userAliases, _userDefinedIgnored);
                }
            });

            var count = _userDefinedMappings.Count + _userDefinedIgnored.Count;
            StatusMessage = $"Exported {count} user-defined entries to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportSelectedCurvesAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        var selectedCurves = LasFiles
            .SelectMany(f => f.Curves)
            .Where(c => c.IsSelectedForExport && !string.IsNullOrEmpty(c.PrimaryName) && c.PrimaryName != "[IGNORE]")
            .ToList();

        var ignoredCurves = LasFiles
            .SelectMany(f => f.Curves)
            .Where(c => c.IsSelectedForExport && c.PrimaryName == "[IGNORE]")
            .ToList();

        if (selectedCurves.Count == 0 && ignoredCurves.Count == 0)
        {
            StatusMessage = "Нет выбранных кривых для экспорта.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Экспорт...";

            await Task.Run(() =>
            {
                var exporter = new ListNamesAliasExporter();

                // Group selected curves by PrimaryName
                var aliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var curve in selectedCurves)
                {
                    var primaryName = curve.PrimaryName!;
                    var fieldName = curve.CurveFieldName;

                    if (!aliases.ContainsKey(primaryName))
                    {
                        aliases[primaryName] = new List<string>();
                    }

                    if (!aliases[primaryName].Contains(fieldName, StringComparer.OrdinalIgnoreCase))
                    {
                        aliases[primaryName].Add(fieldName);
                    }
                }

                // Collect ignored curve names
                var ignored = new HashSet<string>(
                    ignoredCurves.Select(c => c.CurveFieldName),
                    StringComparer.OrdinalIgnoreCase);

                // If file exists, append and sort; otherwise create new
                if (File.Exists(filePath))
                {
                    exporter.AppendAndSort(filePath, aliases, ignored);
                }
                else
                {
                    exporter.Export(filePath, aliases, ignored);
                }
            });

            var totalCount = selectedCurves.Count + ignoredCurves.Count;
            StatusMessage = $"Экспортировано {totalCount} кривых в {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectAllCurves()
    {
        if (SelectedFile == null) return;

        foreach (var curve in SelectedFile.Curves)
        {
            curve.IsSelectedForExport = true;
        }
        UpdateSelectedForExportCount();
    }

    [RelayCommand]
    private void DeselectAllCurves()
    {
        foreach (var file in LasFiles)
        {
            foreach (var curve in file.Curves)
            {
                curve.IsSelectedForExport = false;
            }
        }
        UpdateSelectedForExportCount();
    }
}
