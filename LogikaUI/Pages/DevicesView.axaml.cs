using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Logika.Meters;
using LogikaUI.ViewModels;
using LogikaUI.Views;

namespace LogikaUI.Pages;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
        DataContext = new DevicesViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void MainTable_OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid) return;
        if (dataGrid.SelectedItem is not Meter meter) return;
        var index = dataGrid.SelectedIndex;
        var mainView = this.GetLogicalParent().GetLogicalParent().GetLogicalParent();
        var tagsView_tab = mainView.Find<TagsView>("TagsView_tab");
        (tagsView_tab.DataContext as TagsViewModel).SelectedIndex = index;
        
        //go to tags tab
        var tabControl = mainView.Find<HamburgerMenu.HamburgerMenu>("Sidebar");
        tabControl.SelectedIndex = 2;
    }
}