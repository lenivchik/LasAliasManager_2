using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace LasAliasManager.GUI.ViewModels;

/// <summary>
/// Represents a single row in the curve table
/// </summary>
public partial class CurveRowViewModel : ObservableObject
{
    /// <summary>
    /// Reference to available primary names for the ComboBox
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string>? _availablePrimaryNames;

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
    /// Status text for display
    /// </summary>
    public string StatusText
    {
        get
        {
            if (IsModified) return "Modified";
            if (IsUnknown) return "Unknown";
            if (IsIgnored) return "Ignored";
            return "Mapped";
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
    /// Callback when selection for export changes
    /// </summary>
    public Action? OnSelectionForExportChanged { get; set; }

    partial void OnPrimaryNameChanged(string? value)
    {
        IsModified = value != OriginalPrimaryName;
        OnPropertyChanged(nameof(StatusText));
        OnModificationChanged?.Invoke();
    }

    partial void OnIsSelectedForExportChanged(bool value)
    {
        OnSelectionForExportChanged?.Invoke();
    }

    partial void OnIsModifiedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnIsUnknownChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnIsIgnoredChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
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
