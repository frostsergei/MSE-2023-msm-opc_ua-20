using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LogikaUI.ViewModels;
using Npgsql;

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
}