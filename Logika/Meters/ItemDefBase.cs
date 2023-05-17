using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    public abstract class ItemDefBase
    {
        public ChannelDef ChannelDef { get; }
        public Meter Meter {
            get { return ChannelDef.Meter; }
        }
        public virtual int Ordinal { get; }
        public string Name { get; protected set; }        //name taken from internal tags database
        public string Description { get; }
        public Type ElementType { get; }

        public ItemDefBase(ChannelDef ChannelDef, int Ordinal, string Name, string Description, Type ElementType)
        {            
            this.ChannelDef = ChannelDef;
            this.Ordinal = Ordinal;
            this.Name = Name;
            this.Description = Description;
            this.ElementType = ElementType;
        }
    }
}
