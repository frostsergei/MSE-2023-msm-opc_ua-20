using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TLGK410 : Logika4M
    {
        public override bool SupportedByProlog4 { get { return true; } }

        internal TLGK410() { }
        
        //public override MeterType Type { get { return MeterType.LGK410; } }
        public override MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "ЛГК410"; } }
        public override int MaxChannels { get { return 1; } }
        public override int MaxGroups { get { return 0; } }

        public override int MaxBaudRate
        {
            get { return 57600; }
        }

        public override bool SupportsBaudRateChangeRequests {
            get {
                return false;   //на запрос смены скорости 410 отвечает пакетом не в формате протокола M4 (неправильная CSUM, нет конца кадра)
            }
        }
        public override TimeSpan SessionTimeout
        {
            get { return TimeSpan.MaxValue; }   //no session timeout
        }

        protected override string[] getNsDescriptions()
        {
            return new string[0];
        }

        public override bool SupportsFLZ
        {
            get { return false; }
        }
        public override bool SupportsArchivePartitions
        {
            get { return false; }
        }

        public override ushort IdentWord => 0x460A;                    

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            return new Dictionary<string, string>();        //у 410 фиксированные единицы измерений -> возвращаем пустой словарь
        }
        
        public override AdsTagBlock[] getADSTagBlocks()
        {
            return new AdsTagBlock[0];
        }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            { ImportantTag.SerialNo,   "ОБЩ.serial"  },            
            { ImportantTag.NetAddr,    "ОБЩ.NT" },
            { ImportantTag.Ident,      "ОБЩ.ИД" },
            //{ ImportantTag.IfConfig,   new string[] {"ОБЩ.КИ"} },            
            { ImportantTag.ParamsCSum, "ОБЩ.КСБД" },
            };
        }
    }
}
