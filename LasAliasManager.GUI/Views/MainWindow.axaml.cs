using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LasAliasManager.GUI.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LasAliasManager.GUI.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;
    private bool _closingConfirmed = false;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        viewModel.ShowMessageDialog = ShowMessageDialogAsync;
        DataContext = viewModel;
    }
    /// <summary>
    /// Shows a message dialog with appropriate icon based on message type
    /// </summary>
    /// 
    private async void ShowMessageDialogAsync(string title, string message, MessageType type)
    {
        if (type == MessageType.Success)
        {
            // Custom success dialog with green checkmark
            var dialog = new SuccessDialog(title, message);
            await dialog.ShowDialog(this);
        }
        else
        {
            // Standard message box for other types
            var icon = type switch
            {
                MessageType.Warning => MsBox.Avalonia.Enums.Icon.Warning,
                MessageType.Error => MsBox.Avalonia.Enums.Icon.Error,
                _ => MsBox.Avalonia.Enums.Icon.Info
            };

            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, icon);
            await box.ShowWindowDialogAsync(this);
        }
    }

    private async Task<IStorageFolder?> GetAppDirectoryAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        if (Directory.Exists(appDir))
        {
            return await topLevel.StorageProvider.TryGetFolderFromPathAsync(appDir);
        }
        return null;
    }
    private async void LoadDatabase_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        var startLocation = await GetAppDirectoryAsync();

        // Let user choose CSV or TXT file
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл с БД (CSV)",
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } }
            }
        });

        if (files.Count == 0) return;

        var selectedFile = files[0].Path.LocalPath;
        await ViewModel.LoadCsvDatabaseCommand.ExecuteAsync(selectedFile);
    }

    private async void LoadFolder_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с Las файлами",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        var folderPath = folders[0].Path.LocalPath;
        await ViewModel.LoadFolderCommand.ExecuteAsync(folderPath);
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            "О программе",
            "LAS Curve Alias Manager v1.0\n\n" + 
            "Формат базы данных:\n" +
            "• CSV - единичный файл\n" +
            "Возможности:\n" +
            "• Загрузка и анализ LAS файлов из папок/директорий\n" +
            "• Связывание полевых и основных имен\n" +
            "• Выгрузка в TXT файл\n" +
            "• Сохранение иземенеий в БД",
            ButtonEnum.Ok,
            MsBox.Avalonia.Enums.Icon.Info);
        
        await box.ShowAsync();
    }

    private async void ExportListNamesAlias_Click(object? sender, RoutedEventArgs e)
    {
        // Check if there are user-defined mappings before opening file picker
        if (ViewModel.UserDefinedCount == 0)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Export",
                "No user-defined mappings to export.\nSave changes first to track mappings.",
                ButtonEnum.Ok,
                 MsBox.Avalonia.Enums.Icon.Warning);
            await box.ShowWindowDialogAsync(this);
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        // Get application directory as starting location
        var startLocation = await GetAppDirectoryAsync();

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export User-Defined to ListNamesAlias.txt",
            DefaultExtension = "txt",
            SuggestedFileName = "ListNamesAlias.txt",
            SuggestedStartLocation = startLocation,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
            }
        });

        if (file == null) return;

        await ViewModel.ExportListNamesAliasCommand.ExecuteAsync(file.Path.LocalPath);
    }

    private async void ExportSelectedCurves_Click(object? sender, RoutedEventArgs e)
    {
        // Check if curves are selected before opening file picker
        if (ViewModel.SelectedForExportCount == 0)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Экспорт кривых",
                "Нет выбранных кривых для экспорта.\n Проставьте (✓) чтобы выбрать кривые.",
                ButtonEnum.Ok,
                 MsBox.Avalonia.Enums.Icon.Warning);
            await box.ShowWindowDialogAsync(this);
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        // Get application directory as starting location
        var startLocation = await GetAppDirectoryAsync();

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт выбранных кривых",
            DefaultExtension = "txt",
            SuggestedFileName = "ListNameAlias.txt",
            SuggestedStartLocation = startLocation,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
            }
        });

        if (file == null) return;

        await ViewModel.ExportSelectedCurvesCommand.ExecuteAsync(file.Path.LocalPath);
    }



    //private async void ExportListNamesAlias_Click(object? sender, RoutedEventArgs e)
    //{
    //    var topLevel = GetTopLevel(this);
    //    if (topLevel == null) return;

    //    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    //    {
    //        Title = "Export User-Defined to ListNamesAlias.txt",
    //        DefaultExtension = "txt",
    //        SuggestedFileName = "ListNamesAlias.txt",
    //        FileTypeChoices = new[]
    //        {
    //            new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
    //        }
    //    });

    //    if (file == null) return;

    //    await ViewModel.ExportListNamesAliasCommand.ExecuteAsync(file.Path.LocalPath);
    //}



    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        // Если уже подтверждали — просто закрываем
        if (_closingConfirmed)
        {
            base.OnClosing(e);
            return;
        }

        if (!ViewModel.HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }

        // иначе показываем диалог
        e.Cancel = true;

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Несохраненные изменения",
            "Найдены несохраненные иземения. Сохранить их перед выходом?",
            ButtonEnum.YesNoCancel,
            MsBox.Avalonia.Enums.Icon.Warning);

        var result = await box.ShowAsync();

        if (result == ButtonResult.Yes)
        {
            _closingConfirmed = true;
            await ViewModel.SaveChangesCommand.ExecuteAsync(null);
            Close(); // запускает OnClosing второй раз, но флаг блокирует диалог
        }
        else if (result == ButtonResult.No)
        {
            _closingConfirmed = true;
            Close(); // закрыть без сохранения
        }
        // Cancel — ничего не делаем
    }
}
