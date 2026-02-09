using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using static LasAliasManager.Core.Constants;


namespace LasAliasManager.GUI.ViewModels;

/// <summary>
/// Represents a single row in the curve table
/// </summary>
public partial class CurveRowViewModel : ObservableObject
{
    /// <summary>
    /// Flag to prevent cascading updates between PrimaryName and SearchText
    /// </summary>
    private bool _suppressSideEffects;

    /// <summary>
    /// Reference to available primary names for the ComboBox
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string>? _availablePrimaryNames;

    /// <summary>
    /// Filtered list based on current search text
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string>? _filteredPrimaryNames;

    /// <summary>
    /// Search text for filtering primary names
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Whether the ComboBox dropdown is open
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

        // Open dropdown when user starts typing
        if (!string.IsNullOrEmpty(value))
        {
            IsComboBoxOpen = true;
        }
    }

    partial void OnPrimaryNameChanging(string? oldValue, string? newValue)
    {
        // Notify undo system before the value changes
        OnBeforePrimaryNameChanged?.Invoke(this, oldValue);
    }

    partial void OnPrimaryNameChanged(string? value)
    {
        IsModified = value != OriginalPrimaryName;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnModificationChanged?.Invoke();

        // Update search text to match selected value without triggering side effects
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
    /// Refreshes the filtered list based on search text.
    /// When search text matches the current PrimaryName, show all items (not filtered).
    /// </summary>
    public void RefreshFilteredPrimaryNames()
    {
        if (AvailablePrimaryNames == null)
        {
            FilteredPrimaryNames = new ObservableCollection<string>();
            return;
        }

        // If search text is empty OR matches the current PrimaryName, show all items
        if (string.IsNullOrWhiteSpace(SearchText) ||
            SearchText.Equals(PrimaryName, StringComparison.OrdinalIgnoreCase))
        {
            FilteredPrimaryNames = new ObservableCollection<string>(AvailablePrimaryNames);
        }
        else
        {
            // Filter items that contain the search text (case-insensitive)
            var filtered = AvailablePrimaryNames
                .Where(name => name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredPrimaryNames = new ObservableCollection<string>(filtered);
        }
    }

    /// <summary>
    /// The original curve field name from the LAS file
    /// </summary>
    [ObservableProperty]
    private string _curveFieldName = string.Empty;

    /// <summary>
    /// Description/comment of the curve from the LAS file
    /// </summary>
    [ObservableProperty]
    private string _curveDescription = string.Empty;

    /// <summary>
    /// Units of the curve from the LAS file
    /// </summary>
    [ObservableProperty]
    private string _curveUnits = string.Empty;

    /// <summary>
    /// The selected primary (base) name from the dropdown
    /// </summary>
    [ObservableProperty]
    private string? _primaryName;

    /// <summary>
    /// The LAS file name this curve came from
    /// </summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>
    /// Full path to the LAS file
    /// </summary>
    [ObservableProperty]
    private string _filePath = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    [ObservableProperty]
    private long _fileSize;

    /// <summary>
    /// Formatted file size string (KB, MB, etc.)
    /// </summary>
    public string FileSizeFormatted => FormatFileSize(FileSize);

    /// <summary>
    /// STRT (Top/Start depth) from well information
    /// </summary>
    [ObservableProperty]
    private double? _top;

    /// <summary>
    /// STOP (Bottom/End depth) from well information
    /// </summary>
    [ObservableProperty]
    private double? _bottom;

    /// <summary>
    /// STEP from well information
    /// </summary>
    [ObservableProperty]
    private double? _step;

    /// <summary>
    /// Formatted Top value
    /// </summary>
    public string TopFormatted => Top.HasValue ? Top.Value.ToString("F2") : "-";

    /// <summary>
    /// Formatted Bottom value
    /// </summary>
    public string BottomFormatted => Bottom.HasValue ? Bottom.Value.ToString("F2") : "-";

    /// <summary>
    /// Formatted Step value
    /// </summary>
    public string StepFormatted => Step.HasValue ? Step.Value.ToString("F4") : "-";

    /// <summary>
    /// Unit for depth values
    /// </summary>
    [ObservableProperty]
    private string _depthUnit = string.Empty;

    /// <summary>
    /// Whether this curve mapping has been modified
    /// </summary>
    [ObservableProperty]
    private bool _isModified;

    /// <summary>
    /// Whether this is an unknown curve (no mapping found)
    /// </summary>
    [ObservableProperty]
    private bool _isUnknown;

    /// <summary>
    /// Whether this curve is in the ignored list
    /// </summary>
    [ObservableProperty]
    private bool _isIgnored;

    /// <summary>
    /// Whether this curve is selected for export (checkbox)
    /// </summary>
    [ObservableProperty]
    private bool _isSelectedForExport;

    /// <summary>
    /// Whether this curve has been exported to TXT file
    /// </summary>
    [ObservableProperty]
    private bool _isExported;

    /// <summary>
    /// Status text for display
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
    /// Original primary name before any changes
    /// </summary>
    public string? OriginalPrimaryName { get; set; }

    /// <summary>
    /// Callback when modification status changes
    /// </summary>
    public Action? OnModificationChanged { get; set; }

    /// <summary>
    /// Callback when PrimaryName is about to change — provides (curve, oldValue) for undo tracking.
    /// </summary>
    public Action<CurveRowViewModel, string?>? OnBeforePrimaryNameChanged { get; set; }

    /// <summary>
    /// Callback when selection for export changes
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

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
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