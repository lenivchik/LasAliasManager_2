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
        viewModel.ShowInputDialog = ShowInputDialogAsync;
        DataContext = viewModel;
        Opened += MainWindow_Opened;

    }


    private async Task<string?> ShowInputDialogAsync(string title, string prompt)
    {
        var inputDialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        string? result = null;
        var textBox = new TextBox { Margin = new Avalonia.Thickness(10), Watermark = prompt };
        var okButton = new Button { Content = "ОК", Width = 80, Margin = new Avalonia.Thickness(5), IsDefault = true };
        var cancelButton = new Button { Content = "Отмена", Width = 80, Margin = new Avalonia.Thickness(5), IsCancel = true };

        okButton.Click += (_, _) => { result = textBox.Text; inputDialog.Close(); };
        cancelButton.Click += (_, _) => { inputDialog.Close(); };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(10, 0, 10, 10),
            Children = { okButton, cancelButton }
        };

        inputDialog.Content = new StackPanel
        {
            Children =
        {
            new TextBlock
            {
                Text = prompt,
                Margin = new Avalonia.Thickness(10, 10, 10, 0)
            },
            textBox,
            buttonPanel
        }
        };

        await inputDialog.ShowDialog(this);
        return result;
    }



    /// <summary>
    /// Обработчик открытия окна — загружает начальные данные из аргументов командной строки
    /// </summary>
    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        Opened -= MainWindow_Opened;

        // Первый аргумент = директория с LAS файлами
        if (Program.Args.Length > 0 && Directory.Exists(Program.Args[0]))
        {
            var folderPath = Program.Args[0];
            ViewModel.CurrentFolderPath = folderPath;
            await ViewModel.LoadFolderCommand.ExecuteAsync(folderPath);
        }

    }

    /// <summary>
    /// Показывает диалог сообщения с соответствующей иконкой в зависимости от типа
    /// </summary>
    private async void ShowMessageDialogAsync(string title, string message, MessageType type)
    {
        if (type == MessageType.Success)
        {
            // Пользовательский диалог успеха с зелёной галочкой
            var dialog = new SuccessDialog(title, message);
            await dialog.ShowDialog(this);
        }
        else
        {
            // Стандартное окно сообщения для остальных типов
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

    /// <summary>
    /// Получает директорию приложения для диалогов выбора файлов
    /// </summary>
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

    /// <summary>
    /// Обработчик кнопки загрузки базы данных
    /// </summary>
    private async void LoadDatabase_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        var startLocation = await GetAppDirectoryAsync();

        // Пользователь выбирает CSV файл
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл с БД (CSV)",
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV файлы") { Patterns = new[] { "*.csv" } }
            }
        });

        if (files.Count == 0) return;

        var selectedFile = files[0].Path.LocalPath;
        await ViewModel.LoadCsvDatabaseCommand.ExecuteAsync(selectedFile);
    }

    /// <summary>
    /// Обработчик кнопки выбора папки
    /// </summary>
    private async void LoadFolder_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с LAS файлами",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        var folderPath = folders[0].Path.LocalPath;
        await ViewModel.LoadFolderCommand.ExecuteAsync(folderPath);
    }

    /// <summary>
    /// Обработчик кнопки выхода
    /// </summary>
    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Обработчик кнопки «О программе»
    /// </summary>
    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            "О программе",
            "Менеджер псевдонимов кривых LAS v1.0\n\n" +
            "Формат базы данных:\n" +
            "• CSV — единичный файл\n" +
            "Возможности:\n" +
            "• Загрузка и анализ LAS файлов из папок/директорий\n" +
            "• Связывание полевых и основных имен\n" +
            "• Выгрузка в TXT файл\n" +
            "• Сохранение изменений в БД",
            ButtonEnum.Ok,
            MsBox.Avalonia.Enums.Icon.Info);

        await box.ShowAsync();
    }

    /// <summary>
    /// Обработчик экспорта пользовательских сопоставлений в ListNamesAlias
    /// </summary>
    private async void ExportListNamesAlias_Click(object? sender, RoutedEventArgs e)
    {
        // Проверяем наличие пользовательских сопоставлений перед открытием диалога
        if (ViewModel.UserDefinedCount == 0)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Экспорт",
                "Нет пользовательских сопоставлений для экспорта.\nСначала сохраните изменения для отслеживания сопоставлений.",
                ButtonEnum.Ok,
                 MsBox.Avalonia.Enums.Icon.Warning);
            await box.ShowWindowDialogAsync(this);
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        // Получаем директорию приложения как начальное расположение
        var startLocation = await GetAppDirectoryAsync();

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт пользовательских сопоставлений в ListNamesAlias.txt",
            DefaultExtension = "txt",
            SuggestedFileName = "ListNamesAlias.txt",
            SuggestedStartLocation = startLocation,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Текстовые файлы") { Patterns = new[] { "*.txt" } }
            }
        });

        if (file == null) return;

        await ViewModel.ExportListNamesAliasCommand.ExecuteAsync(file.Path.LocalPath);
    }

    /// <summary>
    /// Обработчик экспорта выбранных кривых
    /// </summary>
    private async void ExportSelectedCurves_Click(object? sender, RoutedEventArgs e)
    {
        // Проверяем наличие выбранных кривых перед открытием диалога
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

        // Получаем директорию приложения как начальное расположение
        var startLocation = await GetAppDirectoryAsync();

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт выбранных кривых",
            DefaultExtension = "txt",
            SuggestedFileName = "ListNameAlias.txt",
            SuggestedStartLocation = startLocation,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Текстовые файлы") { Patterns = new[] { "*.txt" } }
            }
        });

        if (file == null) return;

        await ViewModel.ExportSelectedCurvesCommand.ExecuteAsync(file.Path.LocalPath);
    }

    /// <summary>
    /// Обработчик получения фокуса полем ввода основного имени — открывает выпадающий список
    /// </summary>
    private void PrimaryNameTextBox_GotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is CurveRowViewModel viewModel)
        {
            // Обновляем полный список, затем открываем выпадающий список
            viewModel.RefreshFilteredPrimaryNames();
            if (viewModel.FilteredPrimaryNames?.Count > 0)
            {
                viewModel.IsComboBoxOpen = true;
            }

            // Выделяем весь текст, чтобы пользователь мог сразу начать ввод для фильтрации
            textBox.SelectAll();
        }
    }

    /// <summary>
    /// Обработчик потери фокуса полем ввода основного имени — фиксирует выбранное значение
    /// </summary>
    private void PrimaryNameTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is CurveRowViewModel viewModel)
        {
            // Capture the current viewModel reference at focus-loss time.
            // When DataGrid recycles rows during file switch, DataContext changes
            // before the posted callback runs — we must detect and skip that case.
            var capturedViewModel = viewModel;
            var capturedTextBox = textBox;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // CRITICAL: verify DataContext hasn't been recycled to a different curve
                if (capturedTextBox.DataContext != capturedViewModel)
                    return;

                // Close dropdown
                capturedViewModel.IsComboBoxOpen = false;

                // Commit: update PrimaryName with the search text
                if (!string.IsNullOrWhiteSpace(capturedViewModel.SearchText))
                {
                    var match = capturedViewModel.AvailablePrimaryNames?.FirstOrDefault(
                        name => name.Equals(capturedViewModel.SearchText, StringComparison.OrdinalIgnoreCase));

                    capturedViewModel.PrimaryName = match ?? capturedViewModel.SearchText;
                }
                else
                {
                    // User cleared the text — set PrimaryName to empty
                    capturedViewModel.PrimaryName = string.Empty;
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Обработчик кнопки раскрытия выпадающего списка базовых имен
    /// </summary>
    private void ShowPrimaryNamesDropdown_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CurveRowViewModel viewModel)
        {
            if (viewModel.IsComboBoxOpen)
            {
                // Просто закрываем
                viewModel.IsComboBoxOpen = false;
            }
            else
            {
                // Показываем полный нефильтрованный список без очистки отображаемого текста
                viewModel.RefreshFilteredPrimaryNames();
                viewModel.IsComboBoxOpen = true;
            }
        }
    }

    /// <summary>
    /// Обработчик выбора элемента из выпадающего списка базовых имен
    /// </summary>
    private void PrimaryNameListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox &&
            listBox.SelectedItem is string selectedName &&
            listBox.DataContext is CurveRowViewModel viewModel)
        {
            viewModel.PrimaryName = selectedName;
            viewModel.IsComboBoxOpen = false;

            // Сбрасываем выбор, чтобы тот же элемент можно было выбрать снова
            listBox.SelectedItem = null;
        }
    }

    /// <summary>
    /// Обработчик закрытия окна — проверяет несохранённые изменения и неэкспортированные кривые
    /// </summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        // Если уже подтверждали — просто закрываем
        if (_closingConfirmed)
        {
            base.OnClosing(e);
            return;
        }

        // Сначала проверяем несохранённые изменения
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
                // После сохранения проверяем неэкспортированные кривые
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
            else if (result == "Нет")
            {
                // Пользователь не хочет сохранять, но проверяем неэкспортированные кривые
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
            // Отмена — ничего не делаем
            return;
        }

        // Нет несохранённых изменений, проверяем неэкспортированные кривые
        if (ViewModel.HasUnexportedSelectedCurves)
        {
            await CheckUnexportedCurvesAsync(e);
            return;
        }

        // Предупреждения не нужны, закрываем нормально
        base.OnClosing(e);
    }

    /// <summary>
    /// Проверяет наличие неэкспортированных кривых и показывает предупреждение
    /// </summary>
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
        // Нет — ничего не делаем, пользователь вернётся к приложению
    }

    private async void AddCustomName_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.AddCustomNameCommand.ExecuteAsync(null);
    }

    private async void RemoveBaseName_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is string baseName)
        {
            await ViewModel.RemoveBaseNameCommand.ExecuteAsync(baseName);
        }
    }
}
