using Avalonia.Styling;

namespace LogikaUI.Models;


public class L4TagModel
{
    public int Id { get; set; }
    public string Device { get; set; }
    public string Channel { get; set; }
    public int Ordinal { get; set; }
    public string Kind { get; set; }
    public int Basic { get; set; }
    public string DataType { get; set; }
    public string Description { get; set; }
}