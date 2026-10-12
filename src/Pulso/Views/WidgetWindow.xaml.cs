using System.Windows;
using System.Windows.Input;
using Pulso.Models;
using Pulso.ViewModels;

namespace Pulso.Views;

public partial class WidgetWindow : Window
{
    private readonly WidgetViewModel _viewModel;

    public WidgetWindow(WidgetViewModel viewModel, WidgetSettings settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        RestorePosition(settings);
        Closing += (_, _) => _viewModel.SavePosition(Left, Top);
    }

    private void RestorePosition(WidgetSettings settings)
    {
        // Se o monitor onde o widget estava não existe mais, volta para o canto da tela principal.
        if (settings is { Left: { } left, Top: { } top }
            && left >= SystemParameters.VirtualScreenLeft
            && top >= SystemParameters.VirtualScreenTop
            && left + Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top + 40 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            Left = left;
            Top = top;
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Top + 24;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        // DragMove bloqueia até o botão ser solto; depois salva a nova posição.
        DragMove();
        _viewModel.SavePosition(Left, Top);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
