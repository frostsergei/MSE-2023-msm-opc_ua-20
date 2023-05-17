using Logika.Meters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    [DebuggerDisplay("{ToString()}")]
    public class ArchiveField : Tag, IArchivingElement
    {
        public string Caption { get; set; } //field name received from device

        public int archiveOrd; //для СПЕ с их multipart-архивами, номер архива содержащего переменную

        public ArchiveField(ArchiveFieldDef rt, int channelNo)
            : base(rt, channelNo)
        {
        }

        public ArchiveField(ArchiveField vt)
            :base(vt)
        {            
            this.Caption = vt.Caption;
            this.EU = vt.EU;
        }
        
        public ArchiveType ArchiveType => (def as ArchiveFieldDef).ArchiveType;
        public string DisplayFormat { get { return def.DisplayFormat; } }

        public override string Address
        {
            get {
                return def is ArchiveFieldDef6 ? ((ArchiveFieldDef6)def).Address : ((ArchiveFieldDef4)def).Ordinal.ToString();
            }
        }

        public override string ToString()
        {
            string sChNum = Channel.No == 0 ? "" : Channel.No.ToString();
            string euStr = string.IsNullOrWhiteSpace(EU) ? "" : "[" + EU.Trim() + "]";
            return string.Format("{0} {1} {2}", Channel.Name, def.Name, euStr);
        }
    }
   

}
