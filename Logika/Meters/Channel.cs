using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    public enum ChannelKind
    {
        Undefined,

        Common,  //record header

        Channel, // measuring channel / pipe. СПТ(Г): "т", СПЕ: "к"
        Group, //group of channels / consumer.  СПТ(Г): "п", СПЕ: "г"

        TV,
    }

    public class ChannelDef
    {
        public readonly Meter Meter;
        public ChannelKind Kind { get; }
        public readonly string Prefix;
        public readonly int Start;
        public readonly int Count;       
        public readonly string Description;

        public ChannelDef(Meter Meter, string Prefix, int Start, int Count, string Description)
        {
            this.Meter = Meter;
            this.Kind = Meter.getChannelKind(Start, Count, Prefix);
            this.Prefix = Prefix;
            this.Start = Start;
            this.Count = Count;
            this.Description = Description;
        }
        public ChannelDef(ChannelDef a)
        {
            Meter = a.Meter;
            Kind = a.Kind;
            Prefix = a.Prefix;
            Start = a.Start;
            Count = a.Count;
            Description = a.Description;
        }

        public override string ToString()
        {
            return Prefix + " (" + Description + ")";
        }
    }

    public class Channel : ChannelDef
    {        
        public int No { get; }
        public string Name { get; }

        public Channel(ChannelDef cdef, int channelNo)
            :base(cdef)
        {    
            No = channelNo;
            Name = cdef.Prefix + (channelNo > 0 ? channelNo.ToString() : "");
        }
       
        public override string ToString()
        {
            return Name + " (" + Description + ")";
        }
    }

}
