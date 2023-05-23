using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT941_20 : Logika4M
    {
        public override ushort IdentWord => 0x9229;
        public override bool IdentMatch(byte id0, byte id1, byte ver)
        {
            return base.IdentMatch(id0, id1, ver) && ver >= 0x80;
        }
    
        internal TSPT941_20() { }

        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ941.20"; } }
        public override int MaxChannels { get { return 3; } }
        public override int MaxGroups { get { return 1; } }

        protected override string[] getNsDescriptions() {            
                return new string[] {
           "Разряд батареи",        //00
           "Отсутствие напряжения на разъеме X1 тепловычислителя",
           "Изменение сигнала на дискретном входе X4",
           "Изменение сигнала на дискретном входе X11",
           "Параметр tх вне диапазона 0..176 'C",  //04
           "Параметр t4 вне диапазона -50..176 'C",
           "Параметр Pх вне диапазона 0..1,03*ВП3",
           "Параметр P4 вне диапазона 0..1,03*ВП3",

           "Значение контролируемого параметра, определяемого КУ1 вне диапазона УН1..УВ1",    //08
           "Значение контролируемого параметра, определяемого КУ2 вне диапазона УН2..УВ2",
           "Значение контролируемого параметра, определяемого КУ3 вне диапазона УН3..УВ3",
           "Значение контролируемого параметра, определяемого КУ4 вне диапазона УН4..УВ4",
           "Значение контролируемого параметра, определяемого КУ5 вне диапазона УН5..УВ5",      //12
           "", "", "", "",  "", "", "", "",  "", "", "", "",  "", "", "", "",  "", "", "",
           "Параметр P1 вне диапазона 0..1,03*ВП1", //32
           "Параметр P2 вне диапазона 0..1,03*ВП2",
           "Параметр P3 вне диапазона 0..1,03*ВП3",
           "Параметр t1 вне диапазона 0..176 'C",
           "Параметр t2 вне диапазона 0..176 'C",
           "Параметр t3 вне диапазона 0..176 'C",

           "Расход через ВС1 выше верхнего предела диапазона измерений (G1>Gв1)", //38
           "Ненулевой расход через ВС1 ниже нижнего предела диапазона измерений (0<G1<Gн1)",
           "Ненулевой расход через ВС1 ниже значения отсечки самохода (0<G1<Gотс1)",
           "Расход через ВС2 выше верхнего предела диапазона измерений (G2>Gв2)",
           "Ненулевой расход через ВС2 ниже нижнего предела диапазона (0<G2<Gн2)",
           "Ненулевой расход через ВС2 ниже значения отсечки самохода (0<G2<Gотс2)",
           "Расход через ВС3 выше верхнего предела диапазона измерений (G3>Gв3)",
           "Ненулевой расход через ВС3 ниже нижнего предела диапазона (0<G3<Gн3)",
           "Ненулевой расход через ВС3 ниже значения отсечки самохода (0<G3<Gотс3)",
           "Диагностика отрицательного значения разности часовых масс теплоносителя (М1ч–М2ч), выходящего за допустимые пределы",
           "Значение разности часовых масс (М1ч–М2ч) находится в пределах (-НМ)*М1ч <(М1ч–М2ч)<0",
           "Значение разности часовых масс (М1ч–М2ч) находится в пределах 0<(М1ч–М2ч)< НМ*М1ч",
           "Отрицательное значение часового количества тепловой энергии (Qч<0)",
           "Некорректное задание температурного графика", //51
           "",
           "Текущее значение температуры по обратному трубопроводу выше чем значение температуры, вычисленное по заданному температурному графику", //53

        };            
        }
        
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
            { ImportantTag.EngUnits,   new string[] {"ОБЩ.ЕИ/P", "ОБЩ.ЕИ/Q" }},
            { ImportantTag.ParamsCSum, "ОБЩ.КСБД" },
        };
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)    //общее для M4 теплосчетчиков с ЕИ/P + ЕИ/Q
        {
            Dictionary<string, string> eus = new Dictionary<string, string>();
            if (euTags.Length != 2 || euTags[0].Value == null || euTags[1].Value == null)
                throw new Exception("incorrect EU tags supplied");
            
            string[] pua = { "кгс/см²", "МПа", "бар" };
            string[] qua = { "Гкал", "ГДж", "МВт*ч" };

            int pi = Convert.ToInt32(euTags[0].Value);
            if (pi > pua.Length - 1)
                pi = 0;
            eus.Add("[P]", pua[pi]);

            int qi = Convert.ToInt32(euTags[1].Value);
            if (qi > qua.Length - 1)
                qi = 0;
            eus.Add("[Q]", qua[qi]);

            return eus;
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
                new AdsTagBlock(0, 0, 0, 200), // БД (167 окр. до 200)
                new AdsTagBlock(3, new String[] {"8224", "1024", "1025"}),  //info T D
                new AdsTagBlock(3, 0, 2048, 32)    //тотальные (27 окр. до 32)
            };
        }
    }
}
