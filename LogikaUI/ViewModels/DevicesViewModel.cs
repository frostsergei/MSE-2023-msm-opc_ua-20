using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Logika.Meters;

namespace LogikaUI.ViewModels;
using Models;

public class DevicesViewModel : ViewModelBase
{
    public ObservableCollection<Meter> Devices { get; } = new(Meter.SupportedMeters);
    public string Header => "Devices";
    
    
}