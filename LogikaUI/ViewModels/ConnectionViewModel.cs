using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Logika.Comms.Connections;
using Logika.Comms.Protocols;
using Logika.Comms.Protocols.M4;
using Logika.Comms.Protocols.SPBus;
using Logika.Meters;
using LogikaUI.Models;
using ReactiveUI;

namespace LogikaUI.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    public string Header => "Connection";
    private string _meterName = "<meter name>";
    public string MeterName {
        get => _meterName;
        set => this.RaiseAndSetIfChanged(ref _meterName, value);
    }
    public ObservableCollection<DeviceTagModel> Tags { get; } = new();

    private string _buttonText = "Connect";
    public string ButtonText {
        get => _buttonText;
        set => this.RaiseAndSetIfChanged(ref _buttonText, value);
    }

    private bool _is_connected = false;
    public bool IsConnected {
        get => _is_connected;
        set => this.RaiseAndSetIfChanged(ref _is_connected, value);
    }

    private string _address = "91.209.59.238";
    public string IpAddress {
        get => _address;
        set => this.RaiseAndSetIfChanged(ref _address, value);
    }
    
    private string _port = "8002";
    public string Port {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public void Connect()
    {
        IsConnected = true;
        ButtonText = "Update";
        Tags.Clear();

        m4_protocol_test_request();
    }
    
    void m4_protocol_test_request() {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        DataTag request;
        using TCPConnection firstLevel = new TCPConnection(30000, IpAddress, ushort.Parse(Port));
        M4Protocol secondLevel = new M4Protocol
        {
            connection = firstLevel
        };
        firstLevel.Open();
        Meter testMeter = Protocol.AutodetectSPT(firstLevel, BaudRate.Undefined, 30000, true, false, false, null, null, out _, out _, out _) ?? throw new ArgumentNullException("Protocol.AutodetectSPT(first_level, BaudRate.Undefined, 30000, true, false, false, null, null, out _, out _, out _)");
        MeterName = testMeter.Caption;
        DataTagDef def = testMeter.FindTag("ОБЩ", "Д");
        request = new DataTag(def, 0);
        secondLevel.UpdateTags(null, M4Protocol.BROADCAST, new DataTag[] { request });
        Tags.Add(new DeviceTagModel {Id = request.Name, Value = request.Value.ToString()});
        
        
        firstLevel.Close();
    }

}