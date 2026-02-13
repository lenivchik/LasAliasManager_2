using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;


namespace LasAliasManager.GUI.Views;

/// <summary>
/// Диалог успешного завершения операции с зелёной галочкой
/// </summary>
public partial class SuccessDialog : Window
{
    public SuccessDialog()
    {
        InitializeComponent();
    }

    public SuccessDialog(string title, string message) : this()
    {
        var titleText = this.FindControl<TextBlock>("TitleText");
        var messageText = this.FindControl<TextBlock>("MessageText");

        if (titleText != null) titleText.Text = title;
        if (messageText != null) messageText.Text = message;

        Title = title;
    }

    /// <summary>
    /// Обработчик кнопки «Хорошо» — закрывает диалог
    /// </summary>
    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
