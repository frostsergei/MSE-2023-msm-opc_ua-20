using System;
using System.Text;
using Logika.Comms.Connections;
using Logika.Comms.Protocols;
using Logika.Comms.Protocols.M4;
using Logika.Comms.Protocols.SPBus;
using Logika.Meters;
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
        m4_protocol_test_request();

    }
    
    void m4_protocol_test_request() {//печатает значение одного из тэгов прибора
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TCPConnection first_level = new TCPConnection(30000, "91.209.59.238", 8002);
        M4Protocol second_level = new M4Protocol();
        second_level.connection = first_level;
        first_level.Open();
        byte[] connection_dump;
        string test_meter_model;
        string model;
        int Rate;
        Meter test_meter = Protocol.AutodetectSPT(first_level, BaudRate.Undefined, 30000, true, false, false, null, null, out connection_dump, out Rate, out model);
        Console.WriteLine(test_meter);
        DataTagDef def = test_meter.FindTag("ОБЩ", "Д");
        DataTag request = new DataTag(def, 0);
        second_level.UpdateTags(null, M4Protocol.BROADCAST, new DataTag[] { request });
        Console.WriteLine(request);
        first_level.Close();
    }

    void SPBus_protocol_test_request() { //печатает значение одного из тэгов прибора
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TCPConnection first_level = new TCPConnection(30000, "91.209.59.238", 8001);
        first_level.Open();
        SPBusProtocol second_level = new SPBusProtocol(false);
        second_level.connection = first_level;
        byte[] connection_dump;
        string test_meter_model;
        string model;
        int Rate;
        Meter test_meter = Protocol.AutodetectSPT(first_level, BaudRate.Undefined, 30000, false, true, false, 5, 0, out connection_dump, out Rate, out model);
        DataTagDef def = test_meter.FindTag("0","022н02");
        DataTag request = new DataTag(def, 0);
        second_level.UpdateTags(5, 0, new DataTag[] { request });
        Console.WriteLine(request);
        first_level.Close();
    }

}