using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LogikaUI.ViewModels;

namespace LogikaUI.Pages;

public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
        DataContext = new ConnectionViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void DataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Console.WriteLine("Selection changed!");
    }
}