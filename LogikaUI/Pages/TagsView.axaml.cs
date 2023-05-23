using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LogikaUI.ViewModels;

namespace LogikaUI.Pages;

public partial class TagsView : UserControl
{
    public TagsView()
    {
        InitializeComponent();
        DataContext = new TagsViewModel();
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