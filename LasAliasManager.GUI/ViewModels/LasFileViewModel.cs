using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using static LasAliasManager.Core.Constants;


namespace LasAliasManager.GUI.ViewModels;

/// <summary>
/// Представляет один LAS файл в списке файлов
/// </summary>
public partial class LasFileViewModel : ObservableObject
{
	[ObservableProperty]
	private string _filePath = string.Empty;

	[ObservableProperty]
	private string _fileName = string.Empty;

	[ObservableProperty]
	private long _fileSize;

	[ObservableProperty]
	private double? _top;

	[ObservableProperty]
	private double? _bottom;

	[ObservableProperty]
	private double? _step;

	[ObservableProperty]
	private int _curveCount;

	[ObservableProperty]
	private int _unknownCount;

	[ObservableProperty]
	private int _mappedCount;

	[ObservableProperty]
	private int _ignoredCount;

	[ObservableProperty]
	private ObservableCollection<CurveRowViewModel> _curves = new();

	public string FileSizeFormatted => FormatFileSize(FileSize);
	public string TopFormatted => Top.HasValue ? Top.Value.ToString() : "-";
	public string BottomFormatted => Bottom.HasValue ? Bottom.Value.ToString() : "-";
	public string StepFormatted => Step.HasValue ? Step.Value.ToString() : "-";

	/// <summary>
	/// Есть ли неизвестные кривые
	/// </summary>
	public bool HasUnknown => UnknownCount > 0;

	/// <summary>
	/// Есть ли изменённые кривые
	/// </summary>
	public bool HasModified => Curves.Any(c => c.IsModified);

	/// <summary>
	/// Цвет фона в зависимости от статуса файла
	/// </summary>
	public IBrush BackgroundColor
	{
		get
		{
			if (HasModified) return new SolidColorBrush(Color.FromRgb(184, 218, 255)); // Светло-голубой
			if (HasUnknown) return new SolidColorBrush(Color.FromRgb(255, 238, 186)); // Светло-жёлтый
			return Brushes.Transparent;
		}
	}

	partial void OnUnknownCountChanged(int value)
	{
		OnPropertyChanged(nameof(HasUnknown));
		OnPropertyChanged(nameof(BackgroundColor));
	}

	partial void OnFileSizeChanged(long value)
	{
		OnPropertyChanged(nameof(FileSizeFormatted));
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

	/// <summary>
	/// Обновляет счётчики статуса кривых файла
	/// </summary>
	public void RefreshStatus()
	{
		UnknownCount = Curves.Count(c => c.IsUnknown);
		MappedCount = Curves.Count(c => !c.IsUnknown && !c.IsIgnored);
		IgnoredCount = Curves.Count(c => c.IsIgnored);
		OnPropertyChanged(nameof(HasModified));
		OnPropertyChanged(nameof(BackgroundColor));
	}

	/// <summary>
	/// Форматирует размер файла в удобочитаемый вид
	/// </summary>
	private static string FormatFileSize(long bytes)
	{
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < Core.Constants.Formatting.FileSizeUnits.Length - 1)
		{
			order++;
			len /= 1024;
		}
		return $"{len:0.##} {Core.Constants.Formatting.FileSizeUnits[order]}";
	}
}
