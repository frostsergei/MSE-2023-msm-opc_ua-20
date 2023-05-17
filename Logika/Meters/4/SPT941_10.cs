using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT941_10 : Logika4L
    {
        public override ushort IdentWord => 0x9229;
        public override bool IdentMatch(byte id0, byte id1, byte ver)
        {
            return base.IdentMatch(id0, id1, ver) && ver < 0x80;
        }

        internal TSPT941_10() { }
        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            { ImportantTag.Model,       "ОБЩ.model" },
            { ImportantTag.EngUnits,    "ОБЩ.ЕИ" },
            //{ ImportantTag.ConsConfig,  "ОБЩ.СП" },
            { ImportantTag.RDay,        "ОБЩ.СР" },
            { ImportantTag.RHour,       "ОБЩ.ЧР" },
            { ImportantTag.NetAddr,     "ОБЩ.NT" },
            { ImportantTag.Ident,       "ОБЩ.ИД" },
            //{ ImportantTag.IfConfig,    "ОБЩ.КИ" },
        };
        }

        //public override MeterType Type { get { return MeterType.SPT941_10; } }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ941.10/11"; } }
        public override int MaxChannels { get { return 3; } }
        public override int MaxGroups { get { return 1; } }
        /*
                #region MDB maps

                public override string[] mdb_GetTotalsMap()
                {
                    return new string[] {                           
                        "P1.V", "P2.V", "P3.V",  
                        "P1.M", "P2.M", "P3.M", 
                        "C1.dW", "C1.Ti",             
                    };
                }

                #endregion
        */

        protected override string[] getNsDescriptions() {            
                return new string[] {
           "Разряд батареи",        //00
           "Перегрузка по цепям питания датчиков объема",
           "Изменение сигнала на дискретном входе",
           "",
           "Выход контролируемого параметра за границы диапазона",  //04
           "",
           "",
           "",
           "Параметр t1 вне диапазона",    //08
           "Параметр t2 вне диапазона",
           "Расход через ВС1 выше верхнего предела",
           "Ненулевой расход через ВС1 ниже нижнего предела",
           "Расход через ВС2 выше верхнего предела",
           "Ненулевой расход через ВС2 ниже нижнего предела",
           "Расход через ВС3 выше верхнего предела",
           "Ненулевой расход через ВС3 ниже нижнего предела",   //15
           "Отрицательное значение разности часовых масс теплоносителя, выходящее за допустимые пределы",
           "Отрицательное значение часового количества тепловой энергии",
           "Значение разности часовых масс (М1ч–М2ч) меньше нуля",  //18
        };            
        }

        public override bool SupportsBaudRateChangeRequests { get { return true; } }
        public override int MaxBaudRate
        {
            get { return 19200; }
        }
        public override TimeSpan SessionTimeout
        {
            get { return new TimeSpan(0, 2, 0); }
        }
        public override bool SupportsFastSessionInit
        {
            get { return true; }
        }

        public override string getModelFromImage(byte[] flashImage)
        {
            return "1" + new string((char)flashImage[0x30], 1);
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            return SPT942.BuildEUDict(euTags);
        }

        public override ADSFlashRun[] getAdsFileLayout(bool all, string model)
        {
            if (all) {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0x1200 },
                    new ADSFlashRun() { Start = 0x04000, Length = 0x12880 },
                };
            } else {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0x1200 },
                    new ADSFlashRun() { Start = 0x04000, Length = 0x27C0 },
                    new ADSFlashRun() { Start = 0x12140, Length = 0x4740 },
                };
            }
        }
        public override CalcFieldDef[] GetCalculatedFields()
        {
            return new CalcFieldDef[] {
                new CalcFieldDef(this.Channels[0], 0, -1, "dt", StdVar.T, "dt", typeof(Single), null, "0.00", "t2", "t1-t2", "°C")
            };
        }

    }
}