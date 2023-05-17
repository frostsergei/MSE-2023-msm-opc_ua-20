using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT942 : Logika4L
    {
        internal TSPT942() { }

        public override ushort IdentWord => 0x542A;
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ942"; } }
        public override int MaxChannels { get { return 6; } }
        public override int MaxGroups { get { return 2; } }
        
        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            { ImportantTag.Model,       "ОБЩ.model" },
            { ImportantTag.EngUnits,    "ОБЩ.ЕИ" },
            { ImportantTag.NetAddr,     "ОБЩ.NT" },
            { ImportantTag.Ident,       "ОБЩ.ИД" },
            { ImportantTag.RDay,        "ОБЩ.СР" },
            { ImportantTag.RHour,       "ОБЩ.ЧР" },
            //{ ImportantTag.ConsConfig, new string[] { "ТВ1.СП", "ТВ2.СП" } },
            };
        }

        protected override string[] getNsDescriptions() {
            
                return new string[] {
           "Разряд батареи",        //00
           "Перегрузка по цепям питания датчиков объема и давления",   //01
           "",
           "",
           "",
           "",
           "",
           "",
           "Параметр P1 вне диапазона",    //8
           "Параметр P2 вне диапазона",
           "Параметр t1 вне диапазона",
           "Параметр t2 вне диапазона",
           "Расход через ВС1 выше верхнего предела",
           "Ненулевой расход через ВС1 ниже нижнего предела",
           "Расход через ВС2 выше верхнего предела",
           "Ненулевой расход через ВС2 ниже нижнего предела",
           "Расход через ВС3 выше верхнего предела",
           "Ненулевой расход через ВС3 ниже нижнего предела",
           "Абсолютное значение отрицательной часовой массы М3ч больше, чем 4 % часовой массы М1ч",
           "Отрицательное значение часового количества тепловой энергии",   //19
        };            
        }

        public override bool SupportsBaudRateChangeRequests { get { return true; } }
        public override int MaxBaudRate
        {
            get { return 9600; }
        }
        public override TimeSpan SessionTimeout
        {
            get { return new TimeSpan(0, 30, 0); }
        }
        public override bool SupportsFastSessionInit
        {
            get { return false; }
        }

        public override string getModelFromImage(byte[] flashImage)
        {
            return new string((char)flashImage[0x30], 1);
        }

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            Dictionary<string, string> eus = new Dictionary<string, string>();
            if (euTags.Length != 1 || euTags[0].Name != "ЕИ" || euTags[0].Value == null)
                throw new Exception("incorrect EU tag supplied");

            string sEU = Convert.ToString(euTags[0].Value);
            string euP;
            string euQ;
            switch (sEU) {
                case "0":
                default: //в прибор можно ввести значение и больше 2, прибор будет использовать в этом случае значение по умолчанию - 0 (Фомин)
                    euP = "кг/см²";
                    euQ = "Гкал";
                    break;
                case "1":
                    euP = "МПа";
                    euQ = "ГДж";
                    break;
                case "2":
                    euP = "бар";
                    euQ = "MWh";
                    break;
            }
            eus.Add("[P]", euP);
            eus.Add("[Q]", euQ);

            return eus;
        }

        public override ADSFlashRun[] getAdsFileLayout(bool all, string model)
        {

            List<ADSFlashRun> lfr = new List<ADSFlashRun>();

            bool bothTVs;
            if (model == "1" || model == "2" || model == "3" || model == "5")
                bothTVs = true;
            else if (model == "4" || model == "6")
                bothTVs = false;
            else
                throw new ArgumentException("неподдерживаемая модель СПТ942: '" + Convert.ToString(model) +"'"); 
            
            if (all) {  //AL              
                if (bothTVs)
                    lfr.Add(new ADSFlashRun() { Start = 0x00000, Length = 0x2A800 });
                else
                    lfr.Add(new ADSFlashRun() { Start = 0x00000, Length = 0x19D00 });

            } else {    //MH

                lfr.Add(new ADSFlashRun() { Start = 0x00000, Length = 0x7300 });
                lfr.Add(new ADSFlashRun() { Start = 0x151C0, Length = 0x4B40 });
                if (bothTVs)   //у моделей 4 6 нет архива по второму вводу
                    lfr.Add(new ADSFlashRun() { Start = 0x27840, Length = 0x2FC0 });

            }
            return lfr.ToArray();
        }
        //public const int MOD46_TV2_BASE = 100;
        //public const int MOD46_TV2_ID = MOD46_TV2_BASE + 2;  //искусственный ид канала ТВ2 для одноканального СПТ942 (мод. 4/6)

        protected override ArchiveDef[] readArchiveDefs(DataRow[] rows)
        {
            List<ArchiveDef> la = new List<ArchiveDef>(base.readArchiveDefs(rows));

            ArchiveDef4L ah = (ArchiveDef4L)la.FirstOrDefault(x => x.ArchiveType == ArchiveType.Hour);
            ArchiveDef4L ad = (ArchiveDef4L)la.FirstOrDefault(x => x.ArchiveType == ArchiveType.Day);
            ArchiveDef4L am = (ArchiveDef4L)la.FirstOrDefault(x => x.ArchiveType == ArchiveType.Month);

            ChannelDef m46tv2 = this.Channels.FirstOrDefault(x => x.Start == 1 && x.Count==1);

            if (m46tv2 == null) {
                m46tv2 = new ChannelDef(this, ah.ChannelDef.Prefix, 2, 1, "канал ТВ2 в одноканальных СПТ942 (мод. 4/6)");
                List<ChannelDef> lc = new List<ChannelDef>(Channels);
                lc.Add(m46tv2);
                _channels = lc.ToArray();
            }

            //архивы для моделей 4, 6 (одноканальные, ТВ1 отсутствует, по ТВ2 индекс и заголовки на своём месте, данные находятся на месте данных ТВ1)
            la.Add(new ArchiveDef4L(m46tv2, ArchiveType.Hour, ah.ElementType, ah.Capacity, ah.Name, ah.Description, ah.RecordSize, -1, null, -1, ah.IndexAddr2.Value, ah.HeadersAddr2, ah.RecordsAddr, true));
            la.Add(new ArchiveDef4L(m46tv2, ArchiveType.Day, ad.ElementType, ad.Capacity, ad.Name, ad.Description, ad.RecordSize, -1, null, -1, ad.IndexAddr2.Value, ad.HeadersAddr2, ad.RecordsAddr, true));
            la.Add(new ArchiveDef4L(m46tv2, ArchiveType.Month, am.ElementType, am.Capacity, am.Name, am.Description, am.RecordSize, -1, null, -1, am.IndexAddr2.Value, am.HeadersAddr2, am.RecordsAddr, true));

            return la.ToArray();
        }

        public override CalcFieldDef[] GetCalculatedFields()
        {
            ChannelDef cTV = Channels.Single(x=>x.Kind==ChannelKind.TV && x.Start==1);
            
            return new CalcFieldDef[] {
                new CalcFieldDef(cTV, 1, -1, "dt", StdVar.T, "dt ТВ1", typeof(Single), null, "0.00", "ТВ1_t2", "ТВ1_t1-ТВ1_t2", "°C"),
                new CalcFieldDef(cTV, 2, -1, "dt", StdVar.T, "dt ТВ2", typeof(Single), null, "0.00", "ТВ2_t2", "ТВ2_t1-ТВ2_t2", "°C"),
            };
        }
    }
}
