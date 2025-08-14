using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Reko.Extras.Interactive.ViewModels;
using System;
using System.Diagnostics;

namespace Reko.Extras.Interactive.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(Control.KeyDownEvent, KeyDownHandler, RoutingStrategies.Bubble);
    }

    public MainWindowViewModel? ViewModel => (MainWindowViewModel)DataContext;

    private void menu_FileExit(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }

    private void KeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10)
        {
            if (e.KeyModifiers == KeyModifiers.None)
            {
                e.Handled = true;
                ViewModel?.StepAcross();

            }
        }
        Debugger.Break();
    }
}