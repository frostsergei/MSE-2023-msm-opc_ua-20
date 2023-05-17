using Avalonia.Styling;

namespace LogikaUI.Models;


public class X6TagModel
{
    public int Id { get; set; }
    public string Device { get; set; }
    public string Channel { get; set; }
    public int Ordinal { get; set; }
    public int Index { get; set; }
    public string Type { get; set; }
    public string Kind { get; set; }
    public bool Basic { get; set; }
}