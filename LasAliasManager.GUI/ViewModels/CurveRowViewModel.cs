using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using static LasAliasManager.Core.Constants;


namespace LasAliasManager.GUI.ViewModels;

/// <summary>
/// Представляет одну строку в таблице кривых
/// </summary>
public partial class CurveRowViewModel : ObservableObject
{
    /// <summary>
    /// Флаг для предотвращения каскадных обновлений между PrimaryName и SearchText
    /// </summary>
    private bool _suppressSideEffects;

    /// <summary>
    /// Ссылка на доступные базовые имена для выпадающего списка
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string>? _availablePrimaryNames;

    /// <summary>
    /// Отфильтрованный список на основе текущего текста поиска
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string>? _filteredPrimaryNames;

    /// <summary>
    /// Текст поиска для фильтрации базовых имен
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Открыт ли выпадающий список
    /// </summary>
    [ObservableProperty]
    private bool _isComboBoxOpen;

    partial void OnAvailablePrimaryNamesChanged(ObservableCollection<string>? value)
    {
        RefreshFilteredPrimaryNames();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_suppressSideEffects)
            return;

        RefreshFilteredPrimaryNames();

        // Открываем выпадающий список при начале ввода
        if (!string.IsNullOrEmpty(value))
        {
            IsComboBoxOpen = true;
        }
    }

    partial void OnPrimaryNameChanging(string? oldValue, string? newValue)
    {
        // Уведомляем систему отмены перед изменением значения
        OnBeforePrimaryNameChanged?.Invoke(this, oldValue);
    }

    partial void OnPrimaryNameChanged(string? value)
    {
        IsModified = value != OriginalPrimaryName;

        // Реактивно обновляем классификацию на основе назначенного имени
        if (string.IsNullOrEmpty(value))
        {
            IsUnknown = true;
            IsIgnored = false;
        }
        else if (value == Markers.Ignore)
        {
            IsUnknown = false;
            IsIgnored = true;
        }
        else
        {
            IsUnknown = false;
            IsIgnored = false;
        }

        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnModificationChanged?.Invoke();

        // Обновляем текст поиска для соответствия выбранному значению без побочных эффектов
        if (value != null && value != SearchText)
        {
            _suppressSideEffects = true;
            try
            {
                SearchText = value;
            }
            finally
            {
                _suppressSideEffects = false;
            }
        }
    }



    /// <summary>
    /// Обновляет отфильтрованный список на основе текста поиска.
    /// Когда текст поиска совпадает с текущим PrimaryName, показывает все элементы (без фильтрации).
    /// </summary>
    public void RefreshFilteredPrimaryNames()
    {
        if (AvailablePrimaryNames == null)
        {
            FilteredPrimaryNames = new ObservableCollection<string>();
            return;
        }

        // Если текст поиска пуст ИЛИ совпадает с текущим PrimaryName, показываем все элементы
        if (string.IsNullOrWhiteSpace(SearchText) ||
            SearchText.Equals(PrimaryName, StringComparison.OrdinalIgnoreCase))
        {
            FilteredPrimaryNames = new ObservableCollection<string>(AvailablePrimaryNames);
        }
        else
        {
            // Фильтруем элементы, содержащие текст поиска (без учёта регистра)
            var filtered = AvailablePrimaryNames
                .Where(name => name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredPrimaryNames = new ObservableCollection<string>(filtered);
        }
    }

    /// <summary>
    /// Исходное полевое имя кривой из LAS файла
    /// </summary>
    [ObservableProperty]
    private string _curveFieldName = string.Empty;

    /// <summary>
    /// Описание/комментарий кривой из LAS файла
    /// </summary>
    [ObservableProperty]
    private string _curveDescription = string.Empty;

    /// <summary>
    /// Единицы измерения кривой из LAS файла
    /// </summary>
    [ObservableProperty]
    private string _curveUnits = string.Empty;

    /// <summary>
    /// Выбранное базовое (основное) имя из выпадающего списка
    /// </summary>
    [ObservableProperty]
    private string? _primaryName;

    /// <summary>
    /// Имя LAS файла, из которого получена кривая
    /// </summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>
    /// Полный путь к LAS файлу
    /// </summary>
    [ObservableProperty]
    private string _filePath = string.Empty;

    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    [ObservableProperty]
    private long _fileSize;

    /// <summary>
    /// Отформатированный размер файла (КБ, МБ и т.д.)
    /// </summary>
    public string FileSizeFormatted => FormatFileSize(FileSize);

    /// <summary>
    /// STRT (Кровля/Начальная глубина) из информации о скважине
    /// </summary>
    [ObservableProperty]
    private double? _top;

    /// <summary>
    /// STOP (Подошва/Конечная глубина) из информации о скважине
    /// </summary>
    [ObservableProperty]
    private double? _bottom;

    /// <summary>
    /// STEP из информации о скважине
    /// </summary>
    [ObservableProperty]
    private double? _step;

    /// <summary>
    /// Отформатированное значение кровли
    /// </summary>
    public string TopFormatted => Top.HasValue ? Top.Value.ToString("F2") : "-";

    /// <summary>
    /// Отформатированное значение подошвы
    /// </summary>
    public string BottomFormatted => Bottom.HasValue ? Bottom.Value.ToString("F2") : "-";

    /// <summary>
    /// Отформатированное значение шага
    /// </summary>
    public string StepFormatted => Step.HasValue ? Step.Value.ToString("F4") : "-";

    /// <summary>
    /// Единица измерения глубины
    /// </summary>
    [ObservableProperty]
    private string _depthUnit = string.Empty;

    /// <summary>
    /// Изменено ли сопоставление этой кривой
    /// </summary>
    [ObservableProperty]
    private bool _isModified;

    /// <summary>
    /// Является ли кривая неизвестной (сопоставление не найдено)
    /// </summary>
    [ObservableProperty]
    private bool _isUnknown;

    /// <summary>
    /// Находится ли кривая в списке игнорируемых
    /// </summary>
    [ObservableProperty]
    private bool _isIgnored;

    /// <summary>
    /// Выбрана ли кривая для экспорта (флажок)
    /// </summary>
    [ObservableProperty]
    private bool _isSelectedForExport;

    /// <summary>
    /// Экспортирована ли кривая в TXT файл
    /// </summary>
    [ObservableProperty]
    private bool _isExported;

    /// <summary>
    /// Текст статуса для отображения
    /// </summary>
    public string StatusText
    {
        get
        {
            if (IsModified) return UiStrings.StatusModified;
            if (IsUnknown) return UiStrings.StatusUnknown;
            if (IsIgnored) return UiStrings.StatusIgnored;
            if (IsExported) return UiStrings.StatusExported;

            return UiStrings.StatusMapped;
        }
    }

    /// <summary>
    /// Цвет статуса для отображения
    /// </summary>
    public Avalonia.Media.IBrush StatusColor
    {
        get
        {
            if (IsModified) return Avalonia.Media.Brushes.Blue;
            if (IsUnknown) return Avalonia.Media.Brushes.Orange;
            if (IsIgnored) return Avalonia.Media.Brushes.Gray;
            if (IsExported) return Avalonia.Media.Brushes.Purple;
            return Avalonia.Media.Brushes.Green;
        }
    }

    /// <summary>
    /// Исходное базовое имя до внесения изменений
    /// </summary>
    public string? OriginalPrimaryName { get; set; }

    /// <summary>
    /// Обратный вызов при изменении статуса модификации
    /// </summary>
    public Action? OnModificationChanged { get; set; }

    /// <summary>
    /// Обратный вызов перед изменением PrimaryName — передаёт (кривая, старое значение) для отслеживания отмены
    /// </summary>
    public Action<CurveRowViewModel, string?>? OnBeforePrimaryNameChanged { get; set; }

    /// <summary>
    /// Обратный вызов при изменении выбора для экспорта
    /// </summary>
    public Action? OnSelectionForExportChanged { get; set; }

    partial void OnIsSelectedForExportChanged(bool value)
    {
        OnSelectionForExportChanged?.Invoke();
    }

    partial void OnIsModifiedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnIsUnknownChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnIsIgnoredChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnIsExportedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnTopChanged(double? value)
    {
        OnPropertyChanged(nameof(TopFormatted));
    }

    partial void OnBottomChanged(double? value)
    {
        OnPropertyChanged(nameof(BottomFormatted));
    }

    partial void OnStepChanged(double? value)
    {
        OnPropertyChanged(nameof(StepFormatted));
    }

    partial void OnFileSizeChanged(long value)
    {
        OnPropertyChanged(nameof(FileSizeFormatted));
    }

    /// <summary>
    /// Форматирует размер файла в удобочитаемый вид
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
