using Avalonia.Styling;

namespace LogikaUI.Models;

public enum DeviceType
{
    L4,
    M4,
    X6
}

public class DeviceModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    
    public string Bus { get; set; }

    public DeviceType Type {
        get;
        set;
    }
}