using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPG740 : Logika4M
    {
        internal TSPG740() { }
        public override ushort IdentWord => 0x4728;
        //public override MeterType Type { get { return MeterType.SPT941_20; } }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.G; } }
        public override string Caption { get { return "СПГ740"; } }
        public override int MaxChannels { get { return 3; } }
        public override int MaxGroups { get { return 1; } }

        protected override string[] getNsDescriptions()
        {
            return new string[] {
           "Разряд батареи (Uб < 3,2 В)",        //00
           "Изменение сигнала на дискретном входе",
           "Ненулевой рабочий расход Qр1 ниже Qотс1",
           "Ненулевой рабочий расход Qр2 ниже Qотс2",
           "Рабочий расход Qр1 ниже нижнего предела, но выше Qотс1",
           "Рабочий расход Qр2 ниже нижнего предела, но выше Qотс2",
           "Рабочий расход Qр1 выше верхнего предела",
           "Рабочий расход Qр2 выше верхнего предела",
           "Измеренное значение давления датчика P1 вышло за пределы измерений датчика",
           "Измеренное значение давления датчика P2 вышло за пределы измерений датчика",
           "Измеренное значение перепада давления ΔP1 вне пределов диапазона измерений датчика",
           "Сигнал \"длительное состояние замкнуто\" входа V1",
           "Сигнал \"длительное состояние замкнуто\" входа V2",
           "",
           "",
           "Измеренное значение бар. давления Pб вне пределов диапазона измерений датчика",  //15
           "Измеренное значение температуры t1 вне пределов диапазона -52..107 °С",
           "Измеренное значение температуры t2 вне пределов диапазона -52..107 °С",
           "Значение контролируемого параметра, определяемого КУ1 вне диапазона УН1..УВ1",
           "Значение контролируемого параметра, определяемого КУ2 вне диапазона УН2..УВ2",
           "",  //20
           "Частота входного сигнала на входе V1 превышает 150 Гц",
           "Частота входного сигнала на входе V2 превышает 150 Гц",
           "Отсутствие напряжения на разъеме X1 корректора",
           "",
           "Объем выше нормы поставки", //25
           "Некорректные вычисления по первому трубопроводу",
           "Некорректные вычисления по второму трубопроводу",  //27
        };
        }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            { ImportantTag.SerialNo,   "ОБЩ.serial" },
            //{ ImportantTag.ConsConfig, "ОБЩ.СП" },
            { ImportantTag.NetAddr,    "ОБЩ.NT" },
            { ImportantTag.Ident,      "ОБЩ.ИД" },
            //{ ImportantTag.IfConfig,   new string[] { "ОБЩ.КИ" }},
            { ImportantTag.RDay,       "ОБЩ.СР" },
            { ImportantTag.RHour,      "ОБЩ.ЧР" },
            { ImportantTag.EngUnits,   new string[] {"ОБЩ.[Pб]", "ОБЩ.[P1]", "ОБЩ.[dP1]", "ОБЩ.[P2]" }},
            { ImportantTag.ParamsCSum, "ОБЩ.КСБД" },
            };
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {            
            return SPG742.BuildEUDict(euTags);
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
                new AdsTagBlock(0, 0, 0, 55), // БД ОБЩ
                new AdsTagBlock(1, 1, 0, 25), // БД канал 1
                new AdsTagBlock(2, 2, 0, 25), // БД канал 2
                new AdsTagBlock(3, new String[] {
                    "8224", "1024", "1025",         //info T D
                    "0.2048", "0.2049", "0.2050",   //тотальные ОБЩ
                    "1.2048", "1.2049",             //тотальные ch1
                    "2.2048", "2.2049",             //тотальные ch2
                }),
            };         
        }
    }
}
