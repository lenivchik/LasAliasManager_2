using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LasAliasManager.GUI.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;

namespace LasAliasManager.GUI.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;
    private bool _closingConfirmed = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private async void LoadDatabase_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        // Let user choose CSV or TXT file
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл с БД (CSV)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                //new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                //new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count == 0) return;

        var selectedFile = files[0].Path.LocalPath;

        // Check if CSV or TXT
        if (selectedFile.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            // CSV - single file
            await ViewModel.LoadCsvDatabaseCommand.ExecuteAsync(selectedFile);
        }
        else
        {
            // TXT - need second file (ignored names)
            var ignoredFiles = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Ignored Names File (ListNameAlias_NO.txt)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (ignoredFiles.Count == 0) return;

            var ignoredFile = ignoredFiles[0].Path.LocalPath;
            await ViewModel.LoadTxtDatabaseCommand.ExecuteAsync(new[] { selectedFile, ignoredFile });
        }
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
            "About LAS Curve Alias Manager",
            "LAS Curve Alias Manager v1.0\n\n" +
            "A tool for managing curve name aliases in LAS files.\n\n" +
            "Supported database formats:\n" +
            "• CSV (recommended) - single file\n" +
            "• TXT (legacy) - two files\n\n" +
            "Features:\n" +
            "• Load and analyze LAS files from folders\n" +
            "• Map unknown curve names to standardized base names\n" +
            "• View well information (STRT, STOP, STEP)\n" +
            "• Save changes to alias database",
            ButtonEnum.Ok,
            MsBox.Avalonia.Enums.Icon.Info);
        
        await box.ShowAsync();
    }

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
            "Unsaved Changes",
            "You have unsaved changes. Do you want to save before closing?",
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
