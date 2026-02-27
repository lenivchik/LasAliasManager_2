using Avalonia.Controls;
using Avalonia.Interactivity;
using LasAliasManager.GUI.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace LasAliasManager.GUI.Views;

/// <summary>
/// Окно управления основными именами: добавление, удаление, переименование
/// </summary>
public partial class BaseNameManagerWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    /// <summary>
    /// Полный список имён (без служебных маркеров)
    /// </summary>
    private readonly ObservableCollection<string> _allNames = new();

    /// <summary>
    /// Отфильтрованный список для отображения
    /// </summary>
    private readonly ObservableCollection<string> _filteredNames = new();

    /// <summary>
    /// Флаг: были ли внесены изменения (для обновления родительского окна)
    /// </summary>
    public bool HasChanges { get; private set; }

    public BaseNameManagerWindow()
    {
        InitializeComponent();
        _viewModel = null!;
    }

    public BaseNameManagerWindow(MainWindowViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        NamesListBox.ItemsSource = _filteredNames;
        LoadNames();
        UpdateStatus();
    }

    /// <summary>
    /// Загружает имена из AvailablePrimaryNames, исключая служебные маркеры
    /// </summary>
    private void LoadNames()
    {
        _allNames.Clear();

        foreach (var name in _viewModel.AvailablePrimaryNames)
        {
            // Пропускаем служебные маркеры: пустую строку, [ИГНОРИРОВАТЬ], [НОВОЕ ИМЯ]
            if (string.IsNullOrEmpty(name) ||
                name == Core.Constants.Markers.Ignore ||
                name == Core.Constants.Markers.NewBase)
                continue;

            _allNames.Add(name);
        }

        ApplyFilter();
    }

    /// <summary>
    /// Применяет текстовый фильтр к списку имён
    /// </summary>
    private void ApplyFilter()
    {
        var filterText = FilterTextBox?.Text?.Trim() ?? string.Empty;

        _filteredNames.Clear();

        var source = string.IsNullOrWhiteSpace(filterText)
            ? _allNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            : _allNames
                .Where(n => n.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var name in source)
        {
            _filteredNames.Add(name);
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (StatusText == null) return;

        var total = _allNames.Count;
        var shown = _filteredNames.Count;

        StatusText.Text = total == shown
            ? $"Всего имён: {total}"
            : $"Показано: {shown} из {total}";
    }

    private void FilterTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Добавление нового основного имени
    /// </summary>
    private async void AddName_Click(object? sender, RoutedEventArgs e)
    {
        var newName = await ShowInputAsync("Добавить основное имя", "Введите новое основное имя:");
        if (string.IsNullOrWhiteSpace(newName))
            return;

        newName = newName.Trim();

        // Проверка на дубликат
        if (_allNames.Contains(newName, StringComparer.OrdinalIgnoreCase))
        {
            await ShowWarningAsync($"Имя '{newName}' уже существует в списке.");
            return;
        }

        // Добавляем через ViewModel (в БД + AvailablePrimaryNames + CSV)
        await _viewModel.AddBaseNameDirectAsync(newName);

        _allNames.Add(newName);
        ApplyFilter();
        HasChanges = true;

        // Выделяем добавленный элемент
        NamesListBox.SelectedItem = newName;
        NamesListBox.ScrollIntoView(newName);

        StatusText.Text = $"Добавлено: '{newName}'";
    }

    /// <summary>
    /// Удаление выбранного основного имени
    /// </summary>
    private async void DeleteName_Click(object? sender, RoutedEventArgs e)
    {
        if (NamesListBox.SelectedItem is not string selectedName)
        {
            await ShowWarningAsync("Выберите имя для удаления.");
            return;
        }

        // Проверяем использование кривыми
        var usageCount = _viewModel.LasFiles
            .SelectMany(f => f.Curves)
            .Count(c => selectedName.Equals(c.PrimaryName, StringComparison.OrdinalIgnoreCase)
                     || selectedName.Equals(c.OriginalPrimaryName, StringComparison.OrdinalIgnoreCase));

        if (usageCount > 0)
        {
            await ShowWarningAsync(
                $"Невозможно удалить '{selectedName}'.\n" +
                $"Оно используется {usageCount} кривыми.\n" +
                $"Сначала измените сопоставление этих кривых.");
            return;
        }

        // Подтверждение
        var box = MessageBoxManager.GetMessageBoxStandard(
            "Подтверждение удаления",
            $"Удалить основное имя '{selectedName}' из базы данных?\n\n" +
            "Все связанные полевые имена (алиасы) также будут удалены.",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);

        var result = await box.ShowWindowDialogAsync(this);
        if (result != ButtonResult.Yes)
            return;

        // Удаляем через ViewModel (из БД + AvailablePrimaryNames + CSV)
        await _viewModel.RemoveBaseNameCommand.ExecuteAsync(selectedName);

        // Обновляем локальный список
        _allNames.Remove(selectedName);
        ApplyFilter();
        HasChanges = true;

        StatusText.Text = $"Удалено: '{selectedName}'";
    }

    /// <summary>
    /// Переименование выбранного основного имени
    /// </summary>
    private async void RenameName_Click(object? sender, RoutedEventArgs e)
    {
        if (NamesListBox.SelectedItem is not string selectedName)
        {
            await ShowWarningAsync("Выберите имя для переименования.");
            return;
        }

        var newName = await ShowInputAsync(
            "Переименовать основное имя",
            $"Текущее имя: {selectedName}\nВведите новое имя:");

        if (string.IsNullOrWhiteSpace(newName))
            return;

        newName = newName.Trim();

        if (newName.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
            return;

        // Проверка на дубликат
        if (_allNames.Contains(newName, StringComparer.OrdinalIgnoreCase))
        {
            await ShowWarningAsync($"Имя '{newName}' уже существует в списке.");
            return;
        }

        // Проверяем использование кривыми — нужно обновить их
        var usageCount = _viewModel.LasFiles
            .SelectMany(f => f.Curves)
            .Count(c => selectedName.Equals(c.PrimaryName, StringComparison.OrdinalIgnoreCase)
                     || selectedName.Equals(c.OriginalPrimaryName, StringComparison.OrdinalIgnoreCase));

        if (usageCount > 0)
        {
            var confirmBox = MessageBoxManager.GetMessageBoxStandard(
                "Переименование",
                $"Имя '{selectedName}' используется {usageCount} кривыми.\n" +
                $"Переименовать на '{newName}' и обновить все сопоставления?",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Warning);

            var confirmResult = await confirmBox.ShowWindowDialogAsync(this);
            if (confirmResult != ButtonResult.Yes)
                return;
        }

        // Переименовываем через ViewModel (БД + кривые + CSV)
        await _viewModel.RenameBaseNameAsync(selectedName, newName);

        // Обновляем локальный список
        var idx = _allNames.IndexOf(selectedName);
        if (idx >= 0)
            _allNames[idx] = newName;
        else
            _allNames.Add(newName);

        ApplyFilter();
        HasChanges = true;

        NamesListBox.SelectedItem = newName;
        StatusText.Text = $"Переименовано: '{selectedName}' → '{newName}'";
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #region Вспомогательные методы для диалогов

    private async Task<string?> ShowInputAsync(string title, string prompt)
    {
        var inputDialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        string? result = null;
        var textBox = new TextBox
        {
            Margin = new Avalonia.Thickness(12),
            Watermark = "Введите имя..."
        };

        var okButton = new Button
        {
            Content = "ОК",
            Width = 80,
            Margin = new Avalonia.Thickness(5),
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = "Отмена",
            Width = 80,
            Margin = new Avalonia.Thickness(5),
            IsCancel = true
        };

        okButton.Click += (_, _) => { result = textBox.Text; inputDialog.Close(); };
        cancelButton.Click += (_, _) => { inputDialog.Close(); };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(12, 0, 12, 12),
            Spacing = 8,
            Children = { okButton, cancelButton }
        };

        inputDialog.Content = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = prompt,
                    Margin = new Avalonia.Thickness(12, 12, 12, 0),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                textBox,
                buttonPanel
            }
        };

        await inputDialog.ShowDialog(this);
        return result;
    }

    private async Task ShowWarningAsync(string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            "Предупреждение", message, ButtonEnum.Ok,
            MsBox.Avalonia.Enums.Icon.Warning);
        await box.ShowWindowDialogAsync(this);
    }

    #endregion
}
