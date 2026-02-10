using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LasAliasManager.Core.Services;
using static LasAliasManager.Core.Constants;


namespace LasAliasManager.GUI.ViewModels;

/// <summary>
/// Types of messages for user notifications
/// </summary>
public enum MessageType
{
    Info,
    Success,
    Warning,
    Error
}

public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>
    /// Represents a single curve change for undo purposes
    /// </summary>
    private record UndoEntry(CurveRowViewModel Curve, string? PreviousPrimaryName);

    private readonly AliasManager _aliasManager;

    /// <summary>
    /// Tracks mappings defined by the user during this session (FieldName -> PrimaryName)
    /// </summary>
    private readonly Dictionary<string, string> _userDefinedMappings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tracks names the user marked as ignored during this session
    /// </summary>
    private readonly HashSet<string> _userDefinedIgnored = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Undo stack — each entry is a group of changes (single change or batch from Apply to All)
    /// </summary>
    private readonly Stack<List<UndoEntry>> _undoStack = new();

    /// <summary>
    /// Flag to suppress undo recording during undo/batch/load operations
    /// </summary>
    private bool _suppressUndoRecording;

    /// <summary>
    /// Accumulator for batch undo entries (used during Apply to All)
    /// </summary>
    private List<UndoEntry>? _batchUndoEntries;

    /// <summary>
    /// Action to show message dialog (set by View)
    /// </summary>
    public Action<string, string, MessageType>? ShowMessageDialog { get; set; }

    /// <summary>
    /// Default export file path passed from command-line (second argument).
    /// Used as suggested path in the export file picker dialog.
    /// </summary>
    [ObservableProperty]
    private string? _defaultExportPath;

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
    private string _statusMessage = UiStrings.Ready;

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
    /// Whether there are curves selected for export that haven't been exported
    /// </summary>
    public bool HasUnexportedSelectedCurves
    {
        get
        {
            return LasFiles
                .SelectMany(f => f.Curves)
                .Any(c => c.IsSelectedForExport && !c.IsExported);
        }
    }

    /// <summary>
    /// Number of user-defined mappings available for export
    /// </summary>
    public int UserDefinedCount => _userDefinedMappings.Count + _userDefinedIgnored.Count;

    /// <summary>
    /// Number of curves that have been exported
    /// </summary>
    public int ExportedCount => LasFiles.SelectMany(f => f.Curves).Count(c => c.IsExported);

    public bool HasDatabase => !string.IsNullOrEmpty(_aliasManager.DatabaseFilePath);

    /// <summary>
    /// Whether there are changes that can be undone
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Description of the undo action count for button tooltip
    /// </summary>
    public string UndoDescription => _undoStack.Count > 0
        ? $"Отменить ({_undoStack.Count})"
        : "Нечего отменять";


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
        AvailablePrimaryNames.Add(Markers.Empty);
        AvailablePrimaryNames.Add(Markers.Ignore);
        AvailablePrimaryNames.Add(Markers.NewBase);
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

            // Re-analyze already loaded files if any
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

        _suppressUndoRecording = true;
        try
        {
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
                            curve.OriginalPrimaryName = Markers.Ignore;
                            curve.PrimaryName = Markers.Ignore;
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

            StatusMessage = $"Пере-анализировано {LasFiles.Count} файлов, сопоставлено {TotalCurves} кривых ({UnknownCurves} неизвестных )";
        }
        finally
        {
            _suppressUndoRecording = false;
        }

        _undoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoDescription));
    }

    private void RefreshAvailablePrimaryNames()
    {
        AvailablePrimaryNames.Clear();
        AvailablePrimaryNames.Add("");
        AvailablePrimaryNames.Add(Markers.Ignore);
        AvailablePrimaryNames.Add(Markers.NewBase);

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

            _suppressUndoRecording = true;

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
                    row.OriginalPrimaryName = Markers.Ignore;
                    row.PrimaryName = Markers.Ignore;
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
            _suppressUndoRecording = false;
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));
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

        // Initialize filtered list
        row.RefreshFilteredPrimaryNames();

        // Wire up undo recording
        row.OnBeforePrimaryNameChanged = RecordUndoChange;

        // Wire up modification callback
        row.OnModificationChanged = () =>
        {
            // Defer status updates to avoid interfering with current UI operation
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                parentFile.RefreshStatus();
                UpdateHasUnsavedChanges();

                // Update total unknown count
                UnknownCurves = LasFiles.Sum(f => f.UnknownCount);
            }, Avalonia.Threading.DispatcherPriority.Background);
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
        if ((!HasDatabase))
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
            // Get current base names for checking if entered name is new
            var existingBaseNames = new HashSet<string>(_aliasManager.Database.GetAllBaseNames(), StringComparer.OrdinalIgnoreCase);
            foreach (var curve in modifiedCurves)
            {
                if (curve.PrimaryName == Markers.Ignore)
                {
                    _aliasManager.AddAsIgnored(curve.CurveFieldName);
                    newIgnored.Add(curve.CurveFieldName);

                    // Track for export
                    _userDefinedIgnored.Add(curve.CurveFieldName);
                    _userDefinedMappings.Remove(curve.CurveFieldName);
                }
                else if (curve.PrimaryName == Markers.NewBase)
                {
                    _aliasManager.AddAsNewBase(curve.CurveFieldName);
                    newBaseNames.Add(curve.CurveFieldName);
                    newMappings[curve.CurveFieldName] = curve.CurveFieldName;
                    existingBaseNames.Add(curve.CurveFieldName);

                    // Track for export
                    _userDefinedMappings[curve.CurveFieldName] = curve.CurveFieldName;
                    _userDefinedIgnored.Remove(curve.CurveFieldName);
                }
                else if (!string.IsNullOrEmpty(curve.PrimaryName))
                {
                    var enteredName = curve.PrimaryName.Trim();

                    // Check if the entered name is a new base name (not in existing list)
                    if (!existingBaseNames.Contains(enteredName) &&
                        enteredName != Markers.Ignore &&
                        enteredName != Markers.NewBase)
                    {
                        // Create new base name with the user-entered name
                        _aliasManager.Database.AddBaseName(enteredName, new[] { curve.CurveFieldName });
                        newBaseNames.Add(enteredName);
                        newMappings[curve.CurveFieldName] = enteredName;
                        existingBaseNames.Add(enteredName);

                        // Track for export
                        _userDefinedMappings[curve.CurveFieldName] = enteredName;
                        _userDefinedIgnored.Remove(curve.CurveFieldName);
                    }
                    else
                    {
                        // Add as alias to existing base name
                        _aliasManager.AddAsAlias(curve.CurveFieldName, enteredName);
                        newMappings[curve.CurveFieldName] = enteredName;

                        // Track for export
                        _userDefinedMappings[curve.CurveFieldName] = enteredName;
                        _userDefinedIgnored.Remove(curve.CurveFieldName);
                    }
                }
            }

            await Task.Run(() =>
            {
                _aliasManager.SaveToCsv();
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
            _suppressUndoRecording = true;
            try
            {
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
                            curve.OriginalPrimaryName = Markers.Ignore;
                            curve.PrimaryName = Markers.Ignore;
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
                            curve.IsIgnored = curve.PrimaryName == Markers.Ignore;
                        }
                    }

                    file.RefreshStatus();
                }
            }
            finally
            {
                _suppressUndoRecording = false;
            }

            // Clear undo stack — original state has changed
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));

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

    /// <summary>
    /// Records a single curve change for undo. Called by CurveRowViewModel before PrimaryName changes.
    /// </summary>
    private void RecordUndoChange(CurveRowViewModel curve, string? oldValue)
    {
        if (_suppressUndoRecording) return;

        var entry = new UndoEntry(curve, oldValue);

        if (_batchUndoEntries != null)
        {
            // We're in a batch operation (Apply to All) — accumulate
            _batchUndoEntries.Add(entry);
        }
        else
        {
            // Single change — push as its own group
            _undoStack.Push(new List<UndoEntry> { entry });
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));
        }
    }

    /// <summary>
    /// Undo the last change (single step back)
    /// </summary>
    [RelayCommand]
    private void UndoLastChange()
    {
        if (_undoStack.Count == 0) return;

        var entries = _undoStack.Pop();

        _suppressUndoRecording = true;
        try
        {
            foreach (var entry in entries)
            {
                entry.Curve.PrimaryName = entry.PreviousPrimaryName;
            }
        }
        finally
        {
            _suppressUndoRecording = false;
        }

        // Refresh UI
        foreach (var file in LasFiles)
        {
            file.RefreshStatus();
        }
        UnknownCurves = LasFiles.Sum(f => f.UnknownCount);
        UpdateHasUnsavedChanges();
        ApplyFileFilter();
        ApplyCurveFilter();

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoDescription));

        StatusMessage = entries.Count == 1
            ? $"Отменено изменение для {entries[0].Curve.CurveFieldName}"
            : $"Отменено {entries.Count} изменений";
    }

    /// <summary>
    /// Apply all assigned PrimaryNames from the current file's curves to matching curves across all other files
    /// </summary>
    [RelayCommand]
    private void ApplyToAll()
    {
        if (SelectedFile == null)
        {
            StatusMessage = "Нет выбранного файла";
            return;
        }

        // Collect all curves in the current file that have a PrimaryName assigned
        var sourceMappings = SelectedFile.Curves
            .Where(c => !string.IsNullOrEmpty(c.PrimaryName))
            .GroupBy(c => c.CurveFieldName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().PrimaryName!,
                StringComparer.OrdinalIgnoreCase);

        if (sourceMappings.Count == 0)
        {
            StatusMessage = "В текущем файле нет кривых с назначенными основными именами";
            return;
        }

        // Find all curves in OTHER files that match by field name and need updating
        var curvesToUpdate = LasFiles
            .Where(f => f != SelectedFile)
            .SelectMany(f => f.Curves)
            .Where(c => sourceMappings.TryGetValue(c.CurveFieldName, out var targetName)
                        && c.PrimaryName != targetName)
            .ToList();

        if (curvesToUpdate.Count == 0)
        {
            StatusMessage = "Нет кривых в других файлах для применения";
            return;
        }

        // Start batch undo recording
        _batchUndoEntries = new List<UndoEntry>();

        try
        {
            foreach (var curve in curvesToUpdate)
            {
                curve.PrimaryName = sourceMappings[curve.CurveFieldName];
            }
        }
        finally
        {
            // Commit batch to undo stack as single undoable action
            if (_batchUndoEntries.Count > 0)
            {
                _undoStack.Push(_batchUndoEntries);
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(UndoDescription));
            }
            _batchUndoEntries = null;
        }

        // Refresh UI
        foreach (var file in LasFiles)
        {
            file.RefreshStatus();
        }
        UnknownCurves = LasFiles.Sum(f => f.UnknownCount);
        UpdateHasUnsavedChanges();
        ApplyFileFilter();
        ApplyCurveFilter();

        StatusMessage = $"Применено {curvesToUpdate.Count} изменений из '{SelectedFile.FileName}' к другим файлам";
    }

    private void UpdateHasUnsavedChanges()
    {
        HasUnsavedChanges = LasFiles.Any(f => f.Curves.Any(c => c.IsModified));
    }

    [RelayCommand]
    private void ClearAllChanges()
    {
        _suppressUndoRecording = true;
        try
        {
            foreach (var file in LasFiles)
            {
                foreach (var curve in file.Curves.Where(c => c.IsModified))
                {
                    curve.PrimaryName = curve.OriginalPrimaryName;
                }
                file.RefreshStatus();
            }
        }
        finally
        {
            _suppressUndoRecording = false;
        }

        // Clear undo stack since we've reset everything
        _undoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoDescription));

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
            ShowMessageDialog?.Invoke("Export", "No user-defined mappings to export.\nSave changes first to track mappings.", MessageType.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Exporting to ListNamesAlias.txt...";

            // Keep track of exported field names
            var exportedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                    exportedFieldNames.Add(fieldName);
                }

                // Add ignored names to exported set
                foreach (var name in _userDefinedIgnored)
                {
                    exportedFieldNames.Add(name);
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

            // Mark exported curves in UI
            foreach (var file in LasFiles)
            {
                foreach (var curve in file.Curves)
                {
                    if (exportedFieldNames.Contains(curve.CurveFieldName))
                    {
                        curve.IsExported = true;
                    }
                }
            }

            // Update exported count
            OnPropertyChanged(nameof(ExportedCount));

            var count = _userDefinedMappings.Count + _userDefinedIgnored.Count;
            StatusMessage = $"Exported {count} user-defined entries to {Path.GetFileName(filePath)}";
            ShowMessageDialog?.Invoke("Export Successful", $"Exported {count} user-defined entries to:\n{filePath}", MessageType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting: {ex.Message}";
            ShowMessageDialog?.Invoke("Export Error", $"Failed to export:\n{ex.Message}", MessageType.Error);
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
            .Where(c => c.IsSelectedForExport && !string.IsNullOrEmpty(c.PrimaryName) && c.PrimaryName != Markers.Ignore)
            .ToList();

        var ignoredCurves = LasFiles
            .SelectMany(f => f.Curves)
            .Where(c => c.IsSelectedForExport && c.PrimaryName == Markers.Ignore)
            .ToList();

        if (selectedCurves.Count == 0 && ignoredCurves.Count == 0)
        {
            StatusMessage = "У выбранных кривых отсутсвуют базовые имена.";
            ShowMessageDialog?.Invoke("Экспорт", "У выбранных кривых отсутсвуют базовые имена.\n Укажите базовые имена...", MessageType.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Экпорт выбранных кривых...";

            // Build lists for display message
            var exportedMappingsList = new List<string>();
            var exportedIgnoredList = new List<string>();

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

                    exportedMappingsList.Add($"{fieldName} → {primaryName}");
                }

                // Collect ignored curve names
                var ignored = new HashSet<string>(
                    ignoredCurves.Select(c => c.CurveFieldName),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var curve in ignoredCurves)
                {
                    exportedIgnoredList.Add(curve.CurveFieldName);
                }

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

            // Mark all selected curves as exported
            foreach (var curve in selectedCurves)
            {
                curve.IsExported = true;
            }
            foreach (var curve in ignoredCurves)
            {
                curve.IsExported = true;
            }

            // Update exported count
            OnPropertyChanged(nameof(ExportedCount));

            // Build detailed message
            var totalCount = selectedCurves.Count + ignoredCurves.Count;
            var messageLines = new List<string>
            {
                $"Экспортировано {totalCount} кривых в:",
                filePath,
                ""
            };

            if (exportedMappingsList.Count > 0)
            {
                messageLines.Add("Сопоставленные кривые:");
                messageLines.AddRange(exportedMappingsList);
            }

            if (exportedIgnoredList.Count > 0)
            {
                messageLines.Add("");
                messageLines.Add("Сопоставлены как игнорируемые:");
                messageLines.AddRange(exportedIgnoredList);
            }

            StatusMessage = $"Экспортировано {totalCount} кривых в {Path.GetFileName(filePath)}";
            ShowMessageDialog?.Invoke("Успешный экспорт", string.Join("\n", messageLines), MessageType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting: {ex.Message}";
            ShowMessageDialog?.Invoke("Export Error", $"Failed to export:\n{ex.Message}", MessageType.Error);
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