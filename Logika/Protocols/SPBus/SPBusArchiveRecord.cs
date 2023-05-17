
using System;

namespace Logika.Comms.Protocols.SPBus
{
    public class ArchiveRecord
    {
        public DateTime time;
        public string value;
        public string eu;   //or event info (for service archives)

        public override string ToString()
        {
            string sEU = string.IsNullOrEmpty(eu) ? "" : $"({eu})";
            return $"{time}: {value} {sEU}";
        }
    }

}