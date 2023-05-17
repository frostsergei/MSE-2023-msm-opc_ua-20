using Npgsql;
using ReactiveUI;

namespace LogikaUI.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    public string Header => "Connection";
    private NpgsqlDataSource dataSource;
    
    public ConnectionViewModel()
    {
        dataSource = DbConnection.GetDataSource().Result;
        
    }

    private bool _is_connected = false;
    public bool IsConnected {
        get => _is_connected;
        set => this.RaiseAndSetIfChanged(ref _is_connected, value);
    }

    private string _address = "";
    public string IpAddress {
        get => _address;
        set => this.RaiseAndSetIfChanged(ref _address, value);
    }
    
    private string _port = "";
    public string Port {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public void Connect()
    {
        IsConnected = true;
        
        
    }

}