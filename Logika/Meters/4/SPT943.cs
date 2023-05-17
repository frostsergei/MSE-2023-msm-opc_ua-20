using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT943 : Logika4L
    {
        public override ushort IdentWord => 0x542B;
        public override bool IdentMatch(byte id0, byte id1, byte ver)
        {
            return base.IdentMatch(id0, id1, ver) && ver < 0x0A;
        }

        internal TSPT943() { }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ943"; } }
        public override int MaxChannels { get { return 6; } }
        public override int MaxGroups { get { return 2; } }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            { ImportantTag.Model,       "ОБЩ.model" },
            { ImportantTag.EngUnits,    "ОБЩ.ЕИ" },
            { ImportantTag.NetAddr,     "ОБЩ.NT" },
            { ImportantTag.Ident,       "ОБЩ.ИД" },
            //{ ImportantTag.IfConfig,    "ОБЩ.КИ" },
            { ImportantTag.RDay,        "ОБЩ.СР" },
            { ImportantTag.RHour,       "ОБЩ.ЧР" },
            //{ ImportantTag.ConsConfig, new string[] { "ТВ1.СП", "ТВ2.СП" } },
            };
        }

        protected override string[] getNsDescriptions() {            
                return new string[] {
           "Разряд батареи",        //00
           "Перегрузка по цепям питания датчиков объема",
           "Изменение сигнала на дискретном входе",
           "Параметр tхв вне диапазона",
           "Выход контролируемого параметра за границы диапазона",  //04
           "",
           "",
           "",
           "Параметр P1 вне диапазона",    //08
           "Параметр P2 вне диапазона",
           "Параметр t1 вне диапазона",
           "Параметр t2 вне диапазона",
           "Параметр t3 вне диапазона",
           "Расход через ВС1 выше верхнего предела",
           "Ненулевой расход через ВС1 ниже нижнего предела",
           "Расход через ВС2 выше верхнего предела",
           "Ненулевой расход через ВС2 ниже нижнего предела",
           "Расход через ВС3 выше верхнего предела",
           "Ненулевой расход через ВС3 ниже нижнего предела",
           "Отрицательное значение разности часовых масс теплоносителя, выходящее за допустимые пределы",
           "Отрицательное значение часового количества тепловой энергии",
           "Значение разности часовых масс (М1ч–М2ч) меньше нуля",  //21
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
            return new string((char)flashImage[0x30], 1);
        }

        public override ADSFlashRun[] getAdsFileLayout(bool all, string model)
        {
            if (all) {
                return new ADSFlashRun[] { new ADSFlashRun() { Start = 0x00000, Length = 0x3A980 } };

            } else {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0x8CC0 },
                    new ADSFlashRun() { Start = 0x1AEC0, Length = 0x6E00 },
                    new ADSFlashRun() { Start = 0x33B80, Length = 0x6E00 },
               };
            }
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            return SPT942.BuildEUDict(euTags);
        }

        /*
                public override ArchiveLayout[] getArchiveLayouts()
                {
                    return new ArchiveLayout[] {
                        new SyncArchiveLayout(1, ArchiveType.Hour, 0x433D, 0x4349, 0x9019, 1080),
                        new SyncArchiveLayout(1, ArchiveType.Day,  0x433F, 0x5429, 0x1AEF9, 365),
                        new SyncArchiveLayout(1, ArchiveType.Month, 0x4341, 0x59DD, 0x20FED, 100),
                        new EventArchiveLayout(1, ArchiveType.Errors, 0x4345, 0x643C),
                        new EventArchiveLayout(1, ArchiveType.ParamChanges, 0x4343, 0x5ADC),

                        new SyncArchiveLayout(2, ArchiveType.Hour, 0x686E, 0x687A, 0x21CAD, 1080),
                        new SyncArchiveLayout(2, ArchiveType.Day,  0x6870, 0x795A, 0x33B8D, 365),
                        new SyncArchiveLayout(2, ArchiveType.Month, 0x6872, 0x7F0E, 0x39C81, 100),
                        new EventArchiveLayout(2, ArchiveType.Errors, 0x6876, 0x896D),
                        new EventArchiveLayout(3, ArchiveType.ParamChanges, 0x6874, 0x800D),
                    };
                }
        */

        public override CalcFieldDef[] GetCalculatedFields()
        {
            ChannelDef cTV = Channels.Single(x => x.Kind == ChannelKind.TV);
            return new CalcFieldDef[] {
                new CalcFieldDef(cTV, 1, -1, "dt", StdVar.T, "dt ТВ1", typeof(Single), null, "0.00", "ТВ1_t2", "ТВ1_t1-ТВ1_t2", "°C"),
                new CalcFieldDef(cTV, 2, -1, "dt", StdVar.T, "dt ТВ2", typeof(Single), null, "0.00", "ТВ2_t2", "ТВ2_t1-ТВ2_t2", "°C"),
            };
        }
    }
}
