using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Npgsql;

namespace LogikaUI.ViewModels;
using Models;

public class DevicesViewModel : ViewModelBase
{
    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public string Header => "Devices";
    private NpgsqlDataSource dataSource;
    public DevicesViewModel()
    {
        dataSource = DbConnection.GetDataSource().Result;
        Devices.Add(new()
        {
            Id = -1,
            Name = "Connection to db failed"

        });
        LoadDevices();
        
    }

    public async void LoadDevices()
    {

        await using var cmd = dataSource.CreateCommand("SELECT id, key, description, m4 FROM devices");
        await using var reader = await cmd.ExecuteReaderAsync();
        Devices.Clear();
        while (await reader.ReadAsync())
        {
            Devices.Add(new DeviceModel
            {
                Id = reader.GetInt32(0),
                Name =  reader.GetString(1),
                Description = reader.GetString(2),
                Type = (reader.IsDBNull(3) ? DeviceType.X6 : (reader.GetBoolean(3) ? DeviceType.M4 : DeviceType.L4))
            });
                
        }
    }
    
    
}