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
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;

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
        Opened += MainWindow_Opened;

    }
    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        Opened -= MainWindow_Opened;

        // First arg = directory with LAS files
        if (Program.Args.Length > 0 && Directory.Exists(Program.Args[0]))
        {
            var folderPath = Program.Args[0];
            ViewModel.CurrentFolderPath = folderPath;
            await ViewModel.LoadFolderCommand.ExecuteAsync(folderPath);
        }

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
            // Ensure full list is available, then open dropdown
            viewModel.RefreshFilteredPrimaryNames();
            if (viewModel.FilteredPrimaryNames?.Count > 0)
            {
                viewModel.IsComboBoxOpen = true;
            }

            // Select all text so user can start typing to filter immediately
            textBox.SelectAll();
        }
    }

    private void PrimaryNameTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is CurveRowViewModel viewModel)
        {
            // Small delay to allow click on list item to register first
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // If dropdown is still open, the user clicked somewhere else — close it
                viewModel.IsComboBoxOpen = false;

                // Commit: update PrimaryName with the search text
                if (!string.IsNullOrWhiteSpace(viewModel.SearchText))
                {
                    var match = viewModel.AvailablePrimaryNames?.FirstOrDefault(
                        name => name.Equals(viewModel.SearchText, StringComparison.OrdinalIgnoreCase));

                    viewModel.PrimaryName = match ?? viewModel.SearchText;
                }
                else
                {
                    // User cleared the text — set PrimaryName to empty
                    viewModel.PrimaryName = string.Empty;
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void ShowPrimaryNamesDropdown_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CurveRowViewModel viewModel)
        {
            if (viewModel.IsComboBoxOpen)
            {
                // Just close it
                viewModel.IsComboBoxOpen = false;
            }
            else
            {
                // Show full unfiltered list without clearing the displayed text
                viewModel.RefreshFilteredPrimaryNames();
                viewModel.IsComboBoxOpen = true;
            }
        }
    }

    private void PrimaryNameListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox &&
            listBox.SelectedItem is string selectedName &&
            listBox.DataContext is CurveRowViewModel viewModel)
        {
            viewModel.PrimaryName = selectedName;
            viewModel.IsComboBoxOpen = false;

            // Reset the listbox selection so the same item can be re-selected later
            listBox.SelectedItem = null;
        }
    }

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

            var box = MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
            {
                ContentTitle = "Несохраненные изменения",
                ContentMessage = "Найдены несохраненные изменения. Сохранить их перед выходом?",
                Icon = MsBox.Avalonia.Enums.Icon.Warning,
                ButtonDefinitions = new[]
                    {
                        new ButtonDefinition { Name = "Да", IsDefault = true },
                        new ButtonDefinition { Name = "Нет" },
                        new ButtonDefinition { Name = "Отмена", IsCancel = true }
                    },
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            });

            var result = await box.ShowWindowDialogAsync(this);

            if (result == "Да")
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
            else if (result == "Нет")
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



        var box = MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
        {
            ContentTitle = "Неэкспортированные кривые",
            ContentMessage = $"Имеются {unexportedCount} кривые, отмеченные для экспорта, но не экспортированные.\n\n" +
            "Вы хотите закрыть приложение без экспорта?",
            Icon = MsBox.Avalonia.Enums.Icon.Warning,
            ButtonDefinitions = new[]
    {
        new ButtonDefinition { Name = "Да", IsDefault = true },
        new ButtonDefinition { Name = "Нет" }
    },
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        });

        var result = await box.ShowWindowDialogAsync(this);

        if (result == "Да") { 

            _closingConfirmed = true;
            Close();
        }
        // No — ничего не делаем, пользователь вернется к приложению
    }
}