using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LogikaUI.ViewModels;
using Npgsql;

namespace LogikaUI.Pages;

public partial class L4TagsView : UserControl
{
    public L4TagsView()
    {
        InitializeComponent();
        DataContext = new L4TagsViewModel();
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