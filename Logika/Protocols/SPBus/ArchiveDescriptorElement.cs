using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.SPBus
{
    public struct ArchiveDescriptorElement
    {
        public int archiveOrd;      //номер (части)архива, содержащего данную переменную (актуально для СПЕ, у которых архивы срезов состоят из нескольких частей)
        public int channel;
        public int ordinal;
        public string name;
        public string eu;

        public override string ToString()
        {
            string desc = string.Format("{0}-{1} {2}", channel, ordinal, name);
            if (!string.IsNullOrWhiteSpace(eu))
                desc += " (" + eu + ")";
            return desc;
        }
    };

}
