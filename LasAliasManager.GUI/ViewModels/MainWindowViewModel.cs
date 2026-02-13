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
/// Типы сообщений для уведомлений пользователя
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
    /// Представляет одно изменение кривой для системы отмены
    /// </summary>
    private record UndoEntry(CurveRowViewModel Curve, string? PreviousPrimaryName);

    private readonly AliasManager _aliasManager;

    /// <summary>
    /// Отслеживает сопоставления, определённые пользователем в текущей сессии (ПолевоеИмя -> БазовоеИмя)
    /// </summary>
    private readonly Dictionary<string, string> _userDefinedMappings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Отслеживает имена, отмеченные пользователем как игнорируемые в текущей сессии
    /// </summary>
    private readonly HashSet<string> _userDefinedIgnored = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Стек отмены — каждый элемент содержит группу изменений (одиночное или пакетное из «Применить ко всем»)
    /// </summary>
    private readonly Stack<List<UndoEntry>> _undoStack = new();

    /// <summary>
    /// Флаг для подавления записи отмены при операциях отмены/пакетных/загрузки
    /// </summary>
    private bool _suppressUndoRecording;

    /// <summary>
    /// Накопитель записей пакетной отмены (используется при «Применить ко всем»)
    /// </summary>
    private List<UndoEntry>? _batchUndoEntries;

    /// <summary>
    /// Действие для показа диалога сообщения (устанавливается представлением)
    /// </summary>
    public Action<string, string, MessageType>? ShowMessageDialog { get; set; }

    /// <summary>
    /// Путь экспорта по умолчанию, переданный из командной строки (второй аргумент).
    /// Используется как предложенный путь в диалоге выбора файла экспорта.
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

    private bool _suppressStatusUpdates;

    /// <summary>
    /// Есть ли кривые, выбранные для экспорта, но ещё не экспортированные
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
    /// Количество пользовательских сопоставлений, доступных для экспорта
    /// </summary>
    public int UserDefinedCount => _userDefinedMappings.Count + _userDefinedIgnored.Count;

    /// <summary>
    /// Количество экспортированных кривых
    /// </summary>
    public int ExportedCount => LasFiles.SelectMany(f => f.Curves).Count(c => c.IsExported);

    /// <summary>
    /// Загружена ли база данных
    /// </summary>
    public bool HasDatabase => !string.IsNullOrEmpty(_aliasManager.DatabaseFilePath);

    /// <summary>
    /// Есть ли изменения, которые можно отменить
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Описание действия отмены для подсказки кнопки
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

    /// <summary>
    /// Загружает базу данных из CSV файла
    /// </summary>
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

            // Очищаем устаревшие данные сессии от предыдущей базы данных
            _userDefinedMappings.Clear();
            _userDefinedIgnored.Clear();
            OnPropertyChanged(nameof(UserDefinedCount));

            DatabaseFilePath = csvPath;
            RefreshAvailablePrimaryNames();

            var stats = _aliasManager.Database.GetStatistics();
            StatusMessage = $"База данных загружена: {stats.BaseCount} Основных имен, {stats.TotalAliases} Полевых имен, {stats.IgnoredCount} Игнорируются";

            // Пере-анализируем уже загруженные файлы, если есть
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

    /// <summary>
    /// Пере-анализирует уже загруженные файлы после смены базы данных
    /// </summary>
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

                    // Обновляем статус каждой кривой
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

            // Обновляем итоги
            TotalCurves = LasFiles.Sum(f => f.CurveCount);
            UnknownCurves = LasFiles.Sum(f => f.UnknownCount);

            // Обновляем текущее представление
            ApplyFileFilter();
            ApplyCurveFilter();
            UpdateHasUnsavedChanges();

            StatusMessage = $"Пере-анализировано {LasFiles.Count} файлов, сопоставлено {TotalCurves} кривых ({UnknownCurves} неизвестных)";
        }
        finally
        {
            _suppressUndoRecording = false;
        }

        _undoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoDescription));
    }

    /// <summary>
    /// Обновляет список доступных базовых имен из базы данных
    /// </summary>
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

    /// <summary>
    /// Загружает и анализирует LAS файлы из указанной папки
    /// </summary>
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

            // Очищаем обратные вызовы предыдущих файлов
            foreach (var file in LasFiles)
            {
                CleanupFileCallbacks(file);
            }
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

                // Добавляем сопоставленные кривые
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

                // Добавляем игнорируемые кривые
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

                // Добавляем неизвестные кривые
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

            // Выбираем первый файл, если доступен
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
            StatusMessage = $"Ошибка загрузки папки: {ex.Message}";
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

    /// <summary>
    /// Создаёт строку кривой с привязкой обратных вызовов
    /// </summary>
    private CurveRowViewModel CreateCurveRow(string curveName, LasFileViewModel parentFile)
    {
        var row = new CurveRowViewModel
        {
            AvailablePrimaryNames = AvailablePrimaryNames,
            CurveFieldName = curveName
        };

        // Инициализируем отфильтрованный список
        row.RefreshFilteredPrimaryNames();

        // Подключаем запись отмены
        row.OnBeforePrimaryNameChanged = RecordUndoChange;

        // Подключаем обратный вызов модификации
        row.OnModificationChanged = () =>
        {
            if (_suppressStatusUpdates) return;  // пропускаем при пакетной обработке

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                parentFile.RefreshStatus();
                UpdateHasUnsavedChanges();
                UnknownCurves = LasFiles.Sum(f => f.UnknownCount);
            }, Avalonia.Threading.DispatcherPriority.Background);

            // Подключаем обратный вызов выбора для экспорта
            row.OnSelectionForExportChanged = () =>
        {
            UpdateSelectedForExportCount();
        };
        };

        return row;
    }

    /// <summary>
    /// Обновляет количество выбранных для экспорта кривых
    /// </summary>
    private void UpdateSelectedForExportCount()
    {
        SelectedForExportCount = LasFiles.Sum(f => f.Curves.Count(c => c.IsSelectedForExport));
    }

    /// <summary>
    /// Применяет фильтр к списку файлов
    /// </summary>
    private void ApplyFileFilter()
    {
        var filtered = LasFiles.AsEnumerable();

        if (ShowOnlyUnknown)
        {
            filtered = filtered.Where(f => f.HasUnknown || f.HasModified);
        }

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var filter = FilterText.ToLowerInvariant();
            filtered = filtered.Where(f =>
                f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.Curves.Any(c => c.CurveFieldName.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }

        UpdateCollectionInPlace(FilteredFiles, filtered.ToList());

        if (SelectedFile != null && !FilteredFiles.Contains(SelectedFile))
        {
            SelectedFile = FilteredFiles.FirstOrDefault();
        }
    }

    /// <summary>
    /// Применяет фильтр к списку кривых выбранного файла
    /// </summary>
    private void ApplyCurveFilter()
    {
        if (SelectedFile == null)
        {
            FilteredCurves.Clear();
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

        var desiredItems = filtered.ToList();

        // Синхронизация «на месте»: удаляем лишние элементы, добавляем недостающие
        UpdateCollectionInPlace(FilteredCurves, desiredItems);

        UpdateHasUnsavedChanges();
    }

    /// <summary>
    /// Синхронизирует ObservableCollection с целевым списком
    /// без пересоздания, сохраняя позицию прокрутки DataGrid.
    /// </summary>
    private static void UpdateCollectionInPlace<T>(
        ObservableCollection<T> collection,
        List<T> desired)
    {
        // Удаляем элементы, отсутствующие в целевом наборе
        var desiredSet = new HashSet<T>(desired);
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (!desiredSet.Contains(collection[i]))
            {
                collection.RemoveAt(i);
            }
        }

        // Добавляем/переупорядочиваем для соответствия целевому списку
        var existingSet = new HashSet<T>(collection);
        int insertIndex = 0;
        foreach (var item in desired)
        {
            if (insertIndex < collection.Count &&
                EqualityComparer<T>.Default.Equals(collection[insertIndex], item))
            {
                // Уже на правильной позиции
                insertIndex++;
            }
            else if (existingSet.Contains(item))
            {
                // Элемент существует, но на неправильной позиции — перемещаем
                var currentIndex = collection.IndexOf(item);
                if (currentIndex != insertIndex)
                {
                    collection.Move(currentIndex, insertIndex);
                }
                insertIndex++;
            }
            else
            {
                // Новый элемент — вставляем на правильную позицию
                collection.Insert(insertIndex, item);
                existingSet.Add(item);
                insertIndex++;
            }
        }
    }

    /// <summary>
    /// Сохраняет все изменения в базу данных
    /// </summary>
    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if ((!HasDatabase))
        {
            StatusMessage = "Ошибка: база данных не загружена";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Сохранение...";

            var modifiedCurves = new List<CurveRowViewModel>();
            var newBaseNames = new List<string>();

            // Собираем все изменённые кривые из всех файлов
            foreach (var file in LasFiles)
            {
                modifiedCurves.AddRange(file.Curves.Where(c => c.IsModified));
            }

            // Словарь новых сопоставлений для быстрого поиска
            var newMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newIgnored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Получаем текущие базовые имена для проверки, является ли введённое имя новым
            var existingBaseNames = new HashSet<string>(_aliasManager.Database.GetAllBaseNames(), StringComparer.OrdinalIgnoreCase);
            foreach (var curve in modifiedCurves)
            {
                if (curve.PrimaryName == Markers.Ignore)
                {
                    _aliasManager.AddAsIgnored(curve.CurveFieldName);
                    newIgnored.Add(curve.CurveFieldName);

                    // Отслеживаем для экспорта
                    _userDefinedIgnored.Add(curve.CurveFieldName);
                    _userDefinedMappings.Remove(curve.CurveFieldName);
                }
                else if (curve.PrimaryName == Markers.NewBase)
                {
                    _aliasManager.AddAsNewBase(curve.CurveFieldName);
                    newBaseNames.Add(curve.CurveFieldName);
                    newMappings[curve.CurveFieldName] = curve.CurveFieldName;
                    existingBaseNames.Add(curve.CurveFieldName);

                    // Отслеживаем для экспорта
                    _userDefinedMappings[curve.CurveFieldName] = curve.CurveFieldName;
                    _userDefinedIgnored.Remove(curve.CurveFieldName);
                }
                else if (!string.IsNullOrEmpty(curve.PrimaryName))
                {
                    var enteredName = curve.PrimaryName.Trim();

                    // Проверяем, является ли введённое имя новым базовым (отсутствует в существующем списке)
                    if (!existingBaseNames.Contains(enteredName) &&
                        enteredName != Markers.Ignore &&
                        enteredName != Markers.NewBase)
                    {
                        // Создаём новое базовое имя с введённым пользователем именем
                        _aliasManager.Database.AddBaseName(enteredName, new[] { curve.CurveFieldName });
                        newBaseNames.Add(enteredName);
                        newMappings[curve.CurveFieldName] = enteredName;
                        existingBaseNames.Add(enteredName);

                        // Отслеживаем для экспорта
                        _userDefinedMappings[curve.CurveFieldName] = enteredName;
                        _userDefinedIgnored.Remove(curve.CurveFieldName);
                    }
                    else
                    {
                        // Добавляем как полевое имя к существующему базовому
                        _aliasManager.AddAsAlias(curve.CurveFieldName, enteredName);
                        newMappings[curve.CurveFieldName] = enteredName;

                        // Отслеживаем для экспорта
                        _userDefinedMappings[curve.CurveFieldName] = enteredName;
                        _userDefinedIgnored.Remove(curve.CurveFieldName);
                    }
                }
            }

            await Task.Run(() =>
            {
                _aliasManager.SaveToCsv();
            });

            // Добавляем новые базовые имена в список доступных
            foreach (var newBase in newBaseNames)
            {
                if (!AvailablePrimaryNames.Contains(newBase))
                {
                    AvailablePrimaryNames.Add(newBase);
                }
            }

            // Обновляем ВСЕ кривые во ВСЕХ файлах, соответствующие новым сопоставлениям
            _suppressUndoRecording = true;
            try
            {
                foreach (var file in LasFiles)
                {
                    foreach (var curve in file.Curves)
                    {
                        var fieldName = curve.CurveFieldName;

                        // Проверяем, была ли кривая только что сопоставлена
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
                            // Это изменённая кривая, которую мы уже обработали
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

            // Очищаем стек отмены — исходное состояние изменилось
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));

            // Обновляем итоги
            UnknownCurves = LasFiles.Sum(f => f.UnknownCount);

            HasUnsavedChanges = false;
            ApplyFileFilter();
            ApplyCurveFilter();
            StatusMessage = $"Сохранено {modifiedCurves.Count} изменений";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Обновляет представление
    /// </summary>
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
    /// Записывает одно изменение кривой для отмены. Вызывается из CurveRowViewModel перед изменением PrimaryName.
    /// </summary>
    private void RecordUndoChange(CurveRowViewModel curve, string? oldValue)
    {
        if (_suppressUndoRecording) return;

        var entry = new UndoEntry(curve, oldValue);

        if (_batchUndoEntries != null)
        {
            // Мы в пакетной операции («Применить ко всем») — накапливаем
            _batchUndoEntries.Add(entry);
        }
        else
        {
            // Одиночное изменение — помещаем как отдельную группу
            _undoStack.Push(new List<UndoEntry> { entry });
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));
        }
    }

    /// <summary>
    /// Отменяет последнее изменение (один шаг назад)
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

        // Обновляем интерфейс
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
    /// Применяет все назначенные базовые имена из кривых текущего файла к совпадающим кривым во всех остальных файлах
    /// </summary>
    [RelayCommand]
    private void ApplyToAll()
    {
        if (SelectedFile == null)
        {
            StatusMessage = "Нет выбранного файла";
            return;
        }

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

        var curvesToUpdate = LasFiles
            .Where(f => f != SelectedFile)
            .SelectMany(f => f.Curves)
            .Where(c => sourceMappings.TryGetValue(c.CurveFieldName, out var targetName)
                        && c.PrimaryName != targetName)
            .ToList();  // материализуем перед изменением

        if (curvesToUpdate.Count == 0)
        {
            StatusMessage = "Нет кривых в других файлах для применения";
            return;
        }

        // Подавляем отложенные обновления интерфейса при пакетной обработке
        _suppressUndoRecording = false; // отмена всё ещё нужна
        _batchUndoEntries = new List<UndoEntry>();
        _suppressStatusUpdates = true;

        try
        {
            foreach (var curve in curvesToUpdate)
            {
                curve.PrimaryName = sourceMappings[curve.CurveFieldName];
            }
        }
        finally
        {
            _suppressStatusUpdates = false;

            if (_batchUndoEntries.Count > 0)
            {
                _undoStack.Push(_batchUndoEntries);
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(UndoDescription));
            }
            _batchUndoEntries = null;
        }

        // Одно синхронное обновление — без устаревших данных
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

    /// <summary>
    /// Обновляет флаг наличия несохранённых изменений
    /// </summary>
    private void UpdateHasUnsavedChanges()
    {
        HasUnsavedChanges = LasFiles.Any(f => f.Curves.Any(c => c.IsModified));
    }

    /// <summary>
    /// Сбрасывает все изменения к исходным значениям
    /// </summary>
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

        // Очищаем стек отмены, т.к. всё сброшено
        _undoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoDescription));

        UpdateHasUnsavedChanges();
        ApplyFileFilter();
        ApplyCurveFilter();
    }

    /// <summary>
    /// Очищает историю экспорта
    /// </summary>
    [RelayCommand]
    private void ClearExportHistory()
    {
        _userDefinedMappings.Clear();
        _userDefinedIgnored.Clear();
        OnPropertyChanged(nameof(UserDefinedCount));
        StatusMessage = "История экспорта очищена";
    }

    /// <summary>
    /// Экспортирует пользовательские сопоставления в файл ListNamesAlias.txt
    /// </summary>
    [RelayCommand]
    private async Task ExportListNamesAliasAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        if (_userDefinedMappings.Count == 0 && _userDefinedIgnored.Count == 0)
        {
            StatusMessage = "Нет пользовательских сопоставлений для экспорта. Сначала сохраните изменения.";
            ShowMessageDialog?.Invoke("Экспорт", "Нет пользовательских сопоставлений для экспорта.\nСначала сохраните изменения для отслеживания сопоставлений.", MessageType.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Экспорт в ListNamesAlias.txt...";

            // Отслеживаем экспортированные полевые имена
            var exportedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                var exporter = new ListNamesAliasExporter();

                // Преобразуем пользовательские сопоставления в формат, ожидаемый экспортёром
                // Группируем по базовому имени
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

                // Добавляем игнорируемые имена в набор экспортированных
                foreach (var name in _userDefinedIgnored)
                {
                    exportedFieldNames.Add(name);
                }

                // Если файл существует, дополняем и сортируем; иначе создаём новый
                if (File.Exists(filePath))
                {
                    exporter.AppendAndSort(filePath, userAliases, _userDefinedIgnored);
                }
                else
                {
                    exporter.Export(filePath, userAliases, _userDefinedIgnored);
                }
            });

            // Отмечаем экспортированные кривые в интерфейсе
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

            // Обновляем счётчик экспортированных
            OnPropertyChanged(nameof(ExportedCount));

            var count = _userDefinedMappings.Count + _userDefinedIgnored.Count;
            StatusMessage = $"Экспортировано {count} пользовательских записей в {Path.GetFileName(filePath)}";
            ShowMessageDialog?.Invoke("Успешный экспорт", $"Экспортировано {count} пользовательских записей в:\n{filePath}", MessageType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
            ShowMessageDialog?.Invoke("Ошибка экспорта", $"Не удалось экспортировать:\n{ex.Message}", MessageType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Экспортирует выбранные кривые в файл
    /// </summary>
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
            StatusMessage = "У выбранных кривых отсутствуют базовые имена.";
            ShowMessageDialog?.Invoke("Экспорт", "У выбранных кривых отсутствуют базовые имена.\n Укажите базовые имена...", MessageType.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Экспорт выбранных кривых...";

            // Формируем списки для отображения сообщения
            var exportedMappingsList = new List<string>();
            var exportedIgnoredList = new List<string>();

            await Task.Run(() =>
            {
                var exporter = new ListNamesAliasExporter();

                // Группируем выбранные кривые по базовому имени
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

                // Собираем имена игнорируемых кривых
                var ignored = new HashSet<string>(
                    ignoredCurves.Select(c => c.CurveFieldName),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var curve in ignoredCurves)
                {
                    exportedIgnoredList.Add(curve.CurveFieldName);
                }

                // Если файл существует, дополняем и сортируем; иначе создаём новый
                if (File.Exists(filePath))
                {
                    exporter.AppendAndSort(filePath, aliases, ignored);
                }
                else
                {
                    exporter.Export(filePath, aliases, ignored);
                }
            });

            // Отмечаем все выбранные кривые как экспортированные
            foreach (var curve in selectedCurves)
            {
                curve.IsExported = true;
            }
            foreach (var curve in ignoredCurves)
            {
                curve.IsExported = true;
            }

            // Обновляем счётчик экспортированных
            OnPropertyChanged(nameof(ExportedCount));

            // Формируем подробное сообщение
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
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
            ShowMessageDialog?.Invoke("Ошибка экспорта", $"Не удалось экспортировать:\n{ex.Message}", MessageType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Выбирает все кривые текущего файла для экспорта
    /// </summary>
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

    /// <summary>
    /// Снимает выбор со всех кривых для экспорта
    /// </summary>
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

    /// <summary>
    /// Очищает обратные вызовы кривых файла для предотвращения утечек памяти
    /// </summary>
    private void CleanupFileCallbacks(LasFileViewModel file)
    {
        foreach (var curve in file.Curves)
        {
            curve.OnModificationChanged = null;
            curve.OnBeforePrimaryNameChanged = null;
            curve.OnSelectionForExportChanged = null;
        }
    }
}
