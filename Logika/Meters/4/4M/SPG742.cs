using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPG742 : Logika4M
    {
        public override ushort IdentWord => 0x472A;
        internal TSPG742() { }

        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.G; } }
        public override string Caption { get { return "СПГ742"; } }
        public override int MaxChannels { get { return 4; } }
        public override int MaxGroups { get { return 1; } }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {            
            return new Dictionary<ImportantTag, object>() { 
            { ImportantTag.SerialNo,   "ОБЩ.serial" },
            //{ ImportantTag.ConsConfig, "ОБЩ.СП" },
            { ImportantTag.NetAddr,    "ОБЩ.NT" },                
            { ImportantTag.Ident,      "ОБЩ.ИД" },
            //{ ImportantTag.IfConfig,   new string[] { "ОБЩ.КИ1", "ОБЩ.КИ2" }},
            { ImportantTag.RDay,       "ОБЩ.СР" },
            { ImportantTag.RHour,      "ОБЩ.ЧР" },
            { ImportantTag.EngUnits,   new string[] {"ОБЩ.[P1]", "ОБЩ.[dP1]", "ОБЩ.[P2]", "ОБЩ.[dP2]", "ОБЩ.[P3]", "ОБЩ.[dP3]", "ОБЩ.[dP4]", "ОБЩ.[Pб]" }},                
            };
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            Dictionary<string, string> eus = new Dictionary<string, string>();
                       
            foreach (DataTag t in euTags) {                
                int iEU = Convert.ToInt32(t.Value);   
                eus.Add(t.Name, Logika4.getGasPressureUnits(iEU));
            }
            return eus;            
        }

        protected override string[] getNsDescriptions() {
            
                return new string[] {
           "Разряд батареи",        //00
           "Частота входного сигнала на разъеме Х7 превышает 1,5 кГц",
           "Частота входного сигнала на разъеме Х8 превышает 1,5 кГц",
           "Изменение сигнала на дискретном входе",                 //03
           "Рабочий расход Qр1 ниже нижнего предела",
           "Рабочий расход Qр2 ниже нижнего предела",
           "Рабочий расход Qр1 выше верхнего предела",
           "Рабочий расход Qр2 выше верхнего предела",  //7
           "Давление P1 вне диапазона",
           "Давление P2 вне диапазона",
           "Перепад давления dР1 вне диапазона",
           "Перепад давления dР2 вне диапазона",
           "Давление P3 вне диапазона",
           "Перепад давления dР3 вне диапазона",
           "Перепад давления dР4 вне диапазона",
           "Давление Pб вне диапазона",
           "Температура t1 вне диапазона",
           "Температура t2 вне диапазона",
           "Значение параметра по КУ1 вне диапазона", //18
           "Значение параметра по КУ2 вне диапазона",
           "Значение параметра по КУ3 вне диапазона",
           "Значение параметра по КУ4 вне диапазона",
           "Значение параметра по КУ5 вне диапазона",
           "Значение параметра по КУ2 вне диапазона",
           "",
           "Объем выше нормы поставки", //25
           "Некорректные вычисления по первому трубопроводу",
           "Некорректные вычисления по второму трубопроводу",
           "Измеренное значение перепада давления dP1 превышает вычисленное предельное значение, при этом Qр1>НП/Qр1",
           "Измеренное значение перепада давления dP2 превышает вычисленное предельное значение, при этом Qр2>НП/Qр2",  //29
        };            
        }

        public override bool SupportsBaudRateChangeRequests { get { return true; } }

        public override int MaxBaudRate
        {
            get { return 57600; }    
        }

        public override TimeSpan SessionTimeout
        {
            get { return new TimeSpan(0, 1, 0); }   //1 min
        }

        public override bool SupportsFastSessionInit
        {
            get { return true; }
        }

        public override bool SupportsArchivePartitions
        {
            get { return true; }
        }

        public override bool SupportsFLZ
        {
            get { return false; }
        }

        public override AdsTagBlock[] getADSTagBlocks()
        {
            return new AdsTagBlock[] {
                new AdsTagBlock(0, 0, 0, 64),  //БД ch0
                new AdsTagBlock(1, 1, 0, 64),  //БД ch1
                new AdsTagBlock(2, 2, 0, 64),  //БД ch2
                new AdsTagBlock(3, new String[] {  //текущие
                        "8224", "1024", "1025",  //info T D
                        "1032", "1033", "1034",         //vch vpch tich
                        "0.2048", "0.2049", "0.2050",   //v vp ti
                        "1.1029", "1.1030", "1.2048", "1.2049", //vr1ch v1ch vr1 v1
                        "2.1029", "2.1030", "2.2048", "2.2049"  //vr2ch v2ch vr2 v2
                } ) };
        }
    }
}
