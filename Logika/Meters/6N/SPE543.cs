using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPE543 : Logika6N
    {
        public override bool SupportedByProlog4
        {
            get { return true; }
        }        

        internal TSPE543() { }

        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.E; } }
        public override string Caption { get { return "СПЕ543"; } }
        
        //для СПЕ: Pipe = канал, Consumer = группа
        public override int MaxChannels { get { return 128; } }        
        public override int MaxGroups { get { return 32; } }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            var d = base.GetCommonTagDefs();
            d.Remove(ImportantTag.EngUnits);    //no configurable EUs
            d.Remove(ImportantTag.ParamsCSum);
            return d;
        }
                               
    }
}
