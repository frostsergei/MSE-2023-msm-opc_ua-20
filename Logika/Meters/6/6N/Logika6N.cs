using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    public abstract class Logika6N : Logika6        // x6x.1, x6x.2
    {
        public override bool Outdated => false;

        public override int MaxChannels
        {
            get
            {
                return 12;
            }
        }
        public override int MaxGroups
        {
            get
            {
                return 6;
            }
        }
        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            var d = base.GetCommonTagDefs();
            d[ImportantTag.Model] = "099н00";
            d[ImportantTag.SerialNo] = "099н01";
            //d[ImportantTag.PipeConfig] = "031н00";
            //d[ImportantTag.ConsConfig] = "031н01";            
            return d;
        }

        public const string dfNS = "00000000";      //формат отображения поля НС
    }
}
