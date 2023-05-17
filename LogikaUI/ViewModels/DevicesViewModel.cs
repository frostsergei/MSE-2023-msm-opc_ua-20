using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Npgsql;

namespace LogikaUI.ViewModels;
using Models;

public class DevicesViewModel : ViewModelBase
{
    private string connectionString = "Host=localhost;Username=skygrel19;Password=root;Database=logika";
    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public DevicesViewModel()
    {
        LoadDevices();
    }

    public async void LoadDevices()
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using var cmd = dataSource.CreateCommand("SELECT key, description FROM devices");
        await using var reader = await cmd.ExecuteReaderAsync();
        Devices.Clear();
        while (await reader.ReadAsync())
        {
            Devices.Add(new DeviceModel
            {
                Name =  reader.GetString(0),
                Description = reader.GetString(1)
            });
                
        }
    }
    
    
}