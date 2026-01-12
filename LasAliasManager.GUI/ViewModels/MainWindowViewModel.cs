using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LasAliasManager.Core.Services;

namespace LasAliasManager.GUI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AliasManager _aliasManager;

    [ObservableProperty]
    private ObservableCollection<LasFileViewModel> _lasFiles = new();

    [ObservableProperty]
    private LasFileViewModel? _selectedFile;

    [ObservableProperty]
    private ObservableCollection<CurveRowViewModel> _filteredCurves = new();

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

    public string? AliasFilePath => _aliasManager.AliasFilePath;
    public string? IgnoredFilePath => _aliasManager.IgnoredFilePath;
    public DatabaseFormat CurrentFormat => _aliasManager.CurrentFormat;

    public MainWindowViewModel()
    {
        _aliasManager = new AliasManager();
        
        AvailablePrimaryNames.Add("");
        AvailablePrimaryNames.Add("[ИГНОРИРОВАТЬ]");
        AvailablePrimaryNames.Add("[НОВОЕ ИМЯ]");
    }

    partial void OnSelectedFileChanged(LasFileViewModel? value)
    {
        ApplyFilter();
    }

    partial void OnShowOnlyUnknownChanged(bool value) => ApplyFilter();
    partial void OnFilterTextChanged(string value) => ApplyFilter();

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
        ApplyFilter();
        UpdateHasUnsavedChanges();

        StatusMessage = $"Re-analyzed {LasFiles.Count} files, {TotalCurves} curves ({UnknownCurves} unknown)";
    }



    [RelayCommand]
    private async Task LoadTxtDatabaseAsync(string[]? paths)
    {
        if (paths == null || paths.Length < 2)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading TXT database...";

            await Task.Run(() =>
            {
                _aliasManager.LoadFromTxt(paths[0], paths[1]);
            });

            DatabaseFilePath = paths[0];
            RefreshAvailablePrimaryNames();

            var stats = _aliasManager.Database.GetStatistics();
            StatusMessage = $"TXT loaded: {stats.BaseCount} base names, {stats.TotalAliases} aliases, {stats.IgnoredCount} ignored";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading TXT: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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
            if (LasFiles.Count > 0)
            {
                SelectedFile = LasFiles[0];
            }

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
        };
        return row;
    }

    private void ApplyFilter()
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
            StatusMessage = "Saving changes...";

            var modifiedCurves = new List<CurveRowViewModel>();
            var newBaseNames = new List<string>();

            // Collect all modified curves from all files
            foreach (var file in LasFiles)
            {
                modifiedCurves.AddRange(file.Curves.Where(c => c.IsModified));
            }

            foreach (var curve in modifiedCurves)
            {
                if (curve.PrimaryName == "[ИГНОРИРОВАТЬ]")
                {
                    _aliasManager.AddAsIgnored(curve.CurveFieldName);
                }
                else if (curve.PrimaryName == "[НОВОЕ ИМЯ]")
                {
                    _aliasManager.AddAsNewBase(curve.CurveFieldName);
                    newBaseNames.Add(curve.CurveFieldName);
                }
                else if (!string.IsNullOrEmpty(curve.PrimaryName))
                {
                    _aliasManager.AddAsAlias(curve.CurveFieldName, curve.PrimaryName);
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

            // Update all modified curves
            foreach (var curve in modifiedCurves)
            {
                curve.OriginalPrimaryName = curve.PrimaryName;
                curve.IsModified = false;
                curve.IsUnknown = string.IsNullOrEmpty(curve.PrimaryName);
                curve.IsIgnored = curve.PrimaryName == "[ИГНОРИРОВАТь]";
            }

            // Refresh file statuses
            int totalUnknown = 0;
            foreach (var file in LasFiles)
            {
                file.RefreshStatus();
                totalUnknown += file.UnknownCount;
            }
            UnknownCurves = totalUnknown;

            HasUnsavedChanges = false;
            StatusMessage = $"Saved {modifiedCurves.Count} changes successfully";
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
        ApplyFilter();
        foreach (var file in LasFiles)
        {
            file.RefreshStatus();
        }
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
        ApplyFilter();
    }
}
