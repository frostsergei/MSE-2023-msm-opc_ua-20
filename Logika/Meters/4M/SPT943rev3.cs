using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT943rev3 : Logika4M
    {
        public override ushort IdentWord => 0x542B;
        public override bool IdentMatch(byte id0, byte id1, byte ver)
        {
            return base.IdentMatch(id0, id1, ver) && (ver >= 0x0A && ver <= 0x1F);
        }

        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ943rev3"; } }
        public override int MaxChannels { get { return 6; } }
        public override int MaxGroups { get { return 2; } }

        protected override string[] getNsDescriptions() {            
                return new string[] {
           "Разряд батареи",        //00
           "Перегрузка по цепям питания преобразователей расхода",
           "Изменение сигнала на дискретном входе",
           "Параметр tхв вне диапазона 0..176°C",
           "Выход контролируемого параметра за границы диапазона УН..УВ",
           "Выход контролируемого параметра за границы диапазона УН2..УВ2",
           "",
           "Отсутствует внешнее питание",   //7

           "Параметр P1 по вводу вне диапазона 0..1,1*ВП1",    //08
           "Параметр P2 по вводу вне диапазона 0..1,1*ВП2",
           "Параметр t1 по вводу вне диапазона 0..176°C",
           "Параметр t2 по вводу вне диапазона 0..176°C",
           "Параметр t3 по вводу вне диапазона 0..176°C",   //12           
           "Расход через ВС1 выше верхнего предела измерений",
           "Ненулевой расход через ВС1 ниже нижнего предела измерений",
           "Расход через ВС2 выше верхнего предела измерений",
           "Ненулевой расход через ВС2 ниже нижнего предела измерений",
           "Расход через ВС3 выше верхнего предела измерений",
           "Ненулевой расход через ВС3 ниже нижнего предела измерений",  //18

           "Отрицательное значение разности часовых масс теплоносителя(М1ч–М2ч), выходит за допустимые пределы",
           "Отрицательное значение часового количества тепловой энергии (Qч<0)",
           "Значение разности часовых масс (М1ч–М2ч) меньше нуля",
           "Значение разности часовых масс (М1ч–М2ч) в пределах допустимого расхождения",
           "Значение разности температур (dt) ниже минимального нормированного значения",  //23
        };            
        }        

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            { ImportantTag.SerialNo,    "ОБЩ.serial" },
            { ImportantTag.Ident,       "ОБЩ.ИД" },
            { ImportantTag.NetAddr,     "ОБЩ.NT" },
            //{ ImportantTag.IfConfig,    "ОБЩ.КИ" },
            { ImportantTag.EngUnits,    "ОБЩ.ЕИ" },
            { ImportantTag.RDay,        "ОБЩ.СР" },
            { ImportantTag.RHour,       "ОБЩ.ЧР" },
            //{ ImportantTag.ConsConfig, new string[] { "ТВ1.СП", "ТВ2.СП" } },
            { ImportantTag.ParamsCSum, "ОБЩ.КСБД" },
            };
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            return SPT941_20.BuildEUDict(new DataTag[] { euTags[0], euTags[0] });   //имитируем раздельные ЕИ/P + ЕИ/Q
        }

        public override bool SupportsBaudRateChangeRequests { get { return true; } }
        public override int MaxBaudRate
        {
            get { return 19200; }
        }
        public override TimeSpan SessionTimeout
        {
            get { return new TimeSpan(0, 1, 0); }   //1 min
        }
        public override bool SupportsArchivePartitions
        {
            get { return false; }
        }

        public override bool SupportsFLZ
        {
            get { return false; }
        }
        public override AdsTagBlock[] getADSTagBlocks()
        {
            return new AdsTagBlock[] {
                new AdsTagBlock(0, 0, 0, 64),  //БД ch0
                new AdsTagBlock(100, 1, 0, 64),  //БД ch1
                new AdsTagBlock(200, 2, 0, 64),  //БД ch2
                new AdsTagBlock(3, new String[] { "8224", "1024", "1025", "0.2048" }), //info T D Qобщ                
                new AdsTagBlock(103, 1, 2048, 16),   //тот ТВ1
                new AdsTagBlock(203, 2, 2048, 16),   //тот ТВ2                                               
                 };
        }
    }
}
