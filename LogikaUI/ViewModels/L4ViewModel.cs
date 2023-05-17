using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Npgsql;

namespace LogikaUI.ViewModels;
using Models;

public class L4TagsViewModel : ViewModelBase
{
    public ObservableCollection<L4TagModel> Tags { get; } = new();
    public string Header => "L4 Tags";
    private NpgsqlDataSource dataSource;
    
    public L4TagsViewModel()
    {
        dataSource = DbConnection.GetDataSource().Result;
        Tags.Add(new()
        {
            Id = -1,
            Device = "Connection to db failed"
        });
        LoadTags();
        
    }

    public async void LoadTags() {
        dataSource = DbConnection.GetDataSource().Result;

        await using var cmd = dataSource.CreateCommand("SELECT id, device, channel, ordinal, kind, basic, data_type, description FROM l4_tags");
        await using var reader = await cmd.ExecuteReaderAsync();
        Tags.Clear();
        while (await reader.ReadAsync())
        {
            Tags.Add(new L4TagModel
            {
                Id = reader.GetInt32(0),
                Device = reader.GetString(1),
                Channel = reader.GetString(2),
                Ordinal = reader.GetInt32(3),
                Kind = reader.IsDBNull(4) ? "null" : reader.GetString(6),
                Basic = reader.GetInt32(5),
                DataType = reader.IsDBNull(6) ? "null" : reader.GetString(6),
                Description = reader.IsDBNull(7) ? "null" : reader.GetString(7)
            });
                
        }
    }
    
    
}