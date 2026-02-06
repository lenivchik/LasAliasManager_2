using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LasAliasManager.GUI.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.IO;
using System.Linq;
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

    private void PrimaryNameTextBox_GotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is CurveRowViewModel viewModel)
        {
            // Show dropdown when textbox gets focus
            if (viewModel.FilteredPrimaryNames?.Count > 0)
            {
                viewModel.IsComboBoxOpen = true;
            }
        }
    }

    private void PrimaryNameTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is CurveRowViewModel viewModel)
        {
            // Small delay to allow click on list item
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Update PrimaryName with the search text if it matches an item
                if (!string.IsNullOrWhiteSpace(viewModel.SearchText))
                {
                    var match = viewModel.AvailablePrimaryNames?.FirstOrDefault(
                        name => name.Equals(viewModel.SearchText, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        viewModel.PrimaryName = match;
                    }
                    else
                    {
                        // User typed something that doesn't match - treat as new value
                        viewModel.PrimaryName = viewModel.SearchText;
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void ShowPrimaryNamesDropdown_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CurveRowViewModel viewModel)
        {
            // Clear search to show all items
            viewModel.SearchText = string.Empty;
            viewModel.IsComboBoxOpen = !viewModel.IsComboBoxOpen;
        }
    }

    private void PrimaryNameListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox &&
            listBox.SelectedItem is string selectedName &&
            listBox.DataContext is CurveRowViewModel viewModel)
        {
            viewModel.PrimaryName = selectedName;
            viewModel.SearchText = selectedName;
            viewModel.IsComboBoxOpen = false;
        }
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

        // Check for unsaved changes first
        if (ViewModel.HasUnsavedChanges)
        {
            e.Cancel = true;

            var box = MessageBoxManager.GetMessageBoxStandard(
                "Несохраненные изменения",
                "Найдены несохраненные изменения. Сохранить их перед выходом?",
                ButtonEnum.YesNoCancel,
                MsBox.Avalonia.Enums.Icon.Warning);

            var result = await box.ShowAsync();

            if (result == ButtonResult.Yes)
            {
                await ViewModel.SaveChangesCommand.ExecuteAsync(null);
                // After saving, check for unexported curves
                if (ViewModel.HasUnexportedSelectedCurves)
                {
                    // Reset flag to show the export warning
                    await CheckUnexportedCurvesAsync(e);
                }
                else
                {
                    _closingConfirmed = true;
                    Close();
                }
            }
            else if (result == ButtonResult.No)
            {
                // User doesn't want to save changes, but check for unexported curves
                if (ViewModel.HasUnexportedSelectedCurves)
                {
                    await CheckUnexportedCurvesAsync(e);
                }
                else
                {
                    _closingConfirmed = true;
                    Close();
                }
            }
            // Cancel — ничего не делаем
            return;
        }

        // No unsaved changes, check for unexported curves
        if (ViewModel.HasUnexportedSelectedCurves)
        {
            await CheckUnexportedCurvesAsync(e);
            return;
        }

        // No warnings needed, close normally
        base.OnClosing(e);
    }

    private async Task CheckUnexportedCurvesAsync(WindowClosingEventArgs e)
    {
        e.Cancel = true;

        var unexportedCount = ViewModel.LasFiles
            .SelectMany(f => f.Curves)
            .Count(c => c.IsSelectedForExport && !c.IsExported);

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Неэкспортированные кривые",
            $"Имеются {unexportedCount} кривые, отмеченные для экспорта, но не экспортированные.\n\n" +
            "Вы хотите закрыть приложение без экспорта?",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);

        var result = await box.ShowAsync();

        if (result == ButtonResult.Yes)
        {
            _closingConfirmed = true;
            Close();
        }
        // No — ничего не делаем, пользователь вернется к приложению
    }
}
