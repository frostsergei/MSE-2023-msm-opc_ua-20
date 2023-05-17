namespace Logika.Comms.Protocols.M4
{
    public class TagWriteData
    {
        public int channel;
        public int ordinal;
        public object value;
        public bool? oper; //non-null value indicates that tag's 'operative' flag should be set to given value            
    }
}
