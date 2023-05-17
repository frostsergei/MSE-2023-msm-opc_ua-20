using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Npgsql;

namespace LogikaUI.ViewModels;
using Models;

public class X6TagsViewModel : ViewModelBase
{
    public ObservableCollection<X6TagModel> Tags { get; } = new();
    public string Header => "X6 Tags";
    private NpgsqlDataSource dataSource;
    
    public X6TagsViewModel()
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

        await using var cmd = dataSource.CreateCommand("SELECT id, device, channel, ordinal, index, type, kind, basic FROM x6_tags");
        await using var reader = await cmd.ExecuteReaderAsync();
        Tags.Clear();
        while (await reader.ReadAsync())
        {
            Tags.Add(new X6TagModel
            {
                Id = reader.GetInt32(0),
                Device = reader.GetString(1),
                Channel = reader.GetString(2),
                Ordinal = reader.GetInt32(3),
                Index = reader.IsDBNull(4) ? -1 : reader.GetInt32(4),
                Type = reader.GetString(5),
                Kind = reader.IsDBNull(4) ? "null" : reader.GetString(6),
                Basic = reader.GetBoolean(7)
            });
                
        }
    }
    
    
}