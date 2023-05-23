using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Logika.Meters;
using ReactiveUI;

namespace LogikaUI.ViewModels;
using Models;

public class TagsViewModel : ViewModelBase
{
    public ObservableCollection<DataTagDef> Tags { get; } = new(Meter.SupportedMeters[0].Tags.All);
    public string Header => "Tags";
    
    private int _selectedTagIndex;
    public int SelectedIndex
    {
        get => _selectedTagIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTagIndex, value);
            Tags.Clear();
            foreach (var tag in Items[value].Tags.All)
            {
                Tags.Add(tag);
            }
        }
    }

    private Meter[] _items = Meter.SupportedMeters;
    public Meter[] Items
    {
        get => _items;
        set => this.RaiseAndSetIfChanged(ref _items, value);
    }
}