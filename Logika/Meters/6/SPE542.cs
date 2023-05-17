using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPE542 : Logika6
    {
        public override bool SupportedByProlog4
        {
            get { return true; }
        }

        internal TSPE542() { }
        
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.E; } }
        public override string Caption { get { return "СПЕ542"; } }
              
        public override int MaxChannels { get { return 128; } }        
        public override int MaxGroups { get { return 32; } }

        public const int ChannelsPerCluster = 16;  
        public const int GroupsPerCluster = 4;

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            var dt = base.GetCommonTagDefs();
            dt.Remove(ImportantTag.ParamsCSum);
            dt[ImportantTag.EngUnits] = "027н00";
            return dt;
        }
    }
}
