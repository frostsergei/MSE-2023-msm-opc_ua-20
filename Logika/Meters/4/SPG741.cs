using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPG741 : Logika4L
    {
        internal TSPG741() { }
        public override ushort IdentWord => 0x4729;
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.G; } }
        public override string Caption { get { return "СПГ741"; } }
        public override int MaxChannels { get { return 2; } }
        public override int MaxGroups { get { return 1; } }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
                { ImportantTag.EngUnits,   new string[] { "ОБЩ.[P1]", "ОБЩ.[dP1]", "ОБЩ.[P2]", "ОБЩ.[dP2]", "ОБЩ.[dP3]", "ОБЩ.[Pб]", "ОБЩ.[P3]", "ОБЩ.[P4]" } },
                //{ ImportantTag.ConsConfig, "ОБЩ.СП" },
                { ImportantTag.NetAddr,    "ОБЩ.NT" },
                { ImportantTag.Ident,      "ОБЩ.ИД" },
                { ImportantTag.RDay,       "ОБЩ.СР" },
                { ImportantTag.RHour,      "ОБЩ.ЧР" },
            };
        }

        protected override string[] getNsDescriptions()
        {
            return new string[] {
           "Разряд батареи",        //00
           "",
           "Перегрузка по цепям питания датчиков объема",   //02
           "Активен входной сигнал ВС",                 //03
           "Рабочий расход Qр1 ниже нижнего предела",
           "Рабочий расход Qр2 ниже нижнего предела",
           "Рабочий расход Qр1 выше верхнего предела",
           "Рабочий расход Qр2 выше верхнего предела",  //7
           "",
           "Входной сигнал по цепи Х12 вне диапазона",    //9
           "Входной сигнал по цепи Х13 вне диапазона",
           "Входной сигнал по цепи Х14 вне диапазона",
           "Входной сигнал по цепи Х15 вне диапазона",
           "Входной сигнал по цепи Х16 вне диапазона",    //13
           "Температура t1 вне диапазона",
           "Температура t2 вне диапазона",
           "Давление Р1 за пределами уставок",      //16
           "Перепад давления dР1 за пределами уставок",
           "Рабочий расход Qр1 за пределами уставок",
           "Давление Р2 за пределами уставок",
           "Перепад давления dР2 за пределами уставок",
           "Рабочий расход Qр2 за пределами уставок",
           "Перепад давления dР3 за пределами уставок",
           "Давление Р3 за пределами уставок",
           "Давление Р4 за пределами уставок",
           "Объем выше нормы поставки", //25
           "Некорректные вычисления по первому трубопроводу",
           "Некорректные вычисления по второму трубопроводу",
        };
        }

        public override bool SupportsBaudRateChangeRequests { get { return false; } }
        public override int MaxBaudRate
        {
            get { return 2400; }
        }
        public override TimeSpan SessionTimeout
        {
            get { return new TimeSpan(0, 30, 0); }
        }
        public override bool SupportsFastSessionInit
        {
            get { return false; }
        }


        #region parameters remapping
        /*
        internal class SPName
        {
            internal int SPBits;
            internal string FieldName;
            internal string TagName;
            public SPName(int sb, string field, string tag)
            {
                SPBits = sb;
                FieldName = field;
                TagName = tag;
            }
        };

        const int SP0 = 0x01;
        const int SP1 = 0x02;
        const int SP2 = 0x04;
        const int SP3 = 0x08;
        const int SP4 = 0x10;
        const int SP5 = 0x20;
        const int SP6 = 0x40;
        const int SP_ANY = 0x7F;
        static SPName[] SPNameMap = {
                new SPName( SP_ANY, "/P1", "_ПД1" ),                
                new SPName( SP_ANY, "/Qр1", "_СГ1" ),                
                new SPName( SP_ANY, "/t1", "_ТС1" ),                
                new SPName( SP0|SP5|SP6, "/dP1", "_ПД3" ),
                new SPName( SP2|SP4, "/dP1", "_ПД4" ),
                new SPName( SP3, "/dP1", "_ПД5" ),               
                new SPName( SP0|SP1|SP2|SP3|SP4, "/Qр2", "_СГ2" ),
                new SPName( SP1|SP2|SP3|SP4, "/P2", "_ПД3" ),
                new SPName( SP2, "/dP2", "_ПД5" ),
                new SPName( SP3|SP4, "/dP2", "_ПД2" ),
                new SPName( SP0|SP1|SP2|SP3|SP4, "/t2", "_ТС2" ),
                new SPName( SP0|SP1|SP4|SP5, "/P3", "_ПД5"),
                new SPName( SP6, "/P3", "_ПД4"),
                new SPName( SP6, "/P4", "_ПД5"),
                new SPName( SP0|SP1|SP2|SP5|SP6, "/dP3", "_ПД2"),
                new SPName( SP0|SP1|SP3|SP5, "/Pб", "_ПД4"),
                new SPName( SP5|SP6, "/t3", "_ТС2"),
        };

        public static string ParamNameToTagName(int SP, string ParamName)   //преобразуем имя настроечного параметра от показываемого устройством к внутреннему
        {
            string tmp = ParamName;
            bool found = false;
            foreach (SPName spn in SPNameMap) {
                if (tmp.Contains(spn.FieldName)) {
                    found = true;
                    if ((spn.SPBits & 1 << SP) > 0) {
                        tmp = tmp.Substring(0, tmp.Length - spn.FieldName.Length);
                        return tmp + spn.TagName;
                    }
                }

            }
            if (found)
                return null;    //found in some of SPs, but not in given SP

            return tmp;
        }

        public static string TagNameToParamName(int SP, string TagName)
        {
            string tmp = TagName;
            bool found = false;
            foreach (SPName spn in SPNameMap) {
                if (tmp.Contains(spn.TagName)) {
                    found = true;
                    if ((spn.SPBits & 1 << SP) > 0) {
                        tmp = tmp.Substring(0, tmp.Length - spn.TagName.Length);
                        return tmp + spn.FieldName;
                    }
                }

            }
            if (found)
                return null;    //found in some of SPs, but not in given SP

            return tmp;
        }
        */


        static string[] sensors =  {
                              "ПД1", "ПД2", "ПД3", "ПД4", "ПД5", "ТС1", "ТС2", "СГ1", "СГ2" };

        static string[][] spParamMap = {
                new string[] {"P1",  "dP3", "dP1", "Pб",  "P3",  "t1",  "t2",  "Qр1", "Qр2"},     //СП=0
                new string[] {"P1",  "dP3", "P2",  "Pб",  "P3",  "t1",  "t2",  "Qр1", "Qр2"},     //СП=1
                new string[] {"P1",  "dP3", "P2",  "dP1", "dP2", "t1",  "t2",  "Qр1", "Qр2"},     //СП=2
                new string[] {"P1",  "dP2", "P2",  "Pб",  "dP1", "t1",  "t2",  "Qр1", "Qр2"},     //СП=3
                new string[] {"P1",  "dP2", "P2",  "dP1", "P3",  "t1",  "t2",  "Qр1", "Qр2"},     //СП=4
                new string[] {"P1",  "dP3", "dP1", "Pб",  "P3",  "t1",  "t3",  "Qр1",  ""  },     //СП=5
                new string[] {"P1",  "dP3", "dP1", "P3",  "P4",  "t1",  "t3",  "Qр1",  ""  }      //СП=6
            };

        static string[] sensorVars = { "ВД", "ТД", "ВП", "НП", "ЦИ", "КС", "КВ", "КН", "УВ", "УН", "Vн" };

        public static int GetMappedDBParamAddr(string paramName, byte sp)
        {
            const int DB_FLASH_START = 0x200;
            const int PARAM_SIZE = 16;

            int? paramOrd = GetMappedDBParamOrdinal(paramName, sp);
            if (!paramOrd.HasValue)
                paramOrd = 103; // ПД1/НП, несуществующий, в приборе должен быть должен быть всегда пустым
            int addr = DB_FLASH_START + paramOrd.Value * PARAM_SIZE;

            return addr;
        }

        //возвращает номер параметра по описанию протокола прибора (100-198). По этим номерам в частности идет запись параметров
        public static int? GetMappedDBParamOrdinal(string paramName, byte sp)
        {
            string[] pn = paramName.Split('/');
            if (pn.Length != 2)
                throw new Exception("недопустимое имя параметра СПГ741: " + paramName);
            string[] map = spParamMap[sp];
            int sensIdx = Array.IndexOf(map, pn[1]);
            if (sensIdx < 0)
                return null;
            int varIdx = Array.IndexOf(sensorVars, pn[0]);
            if (varIdx < 0)
                return null;
            const int MAPPED_PARAMS_START_NO = 100;
            const int PARAMS_PER_SENSOR = 11;
            return MAPPED_PARAMS_START_NO + sensIdx * PARAMS_PER_SENSOR + varIdx;
        }

        #endregion

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            Dictionary<string, string> eus = new Dictionary<string, string>();
            if (euTags.Length != 8)     //"ОБЩ.[Р1]", "ОБЩ.[dР1]", "ОБЩ.[Р2]", "ОБЩ.[dР2]", "ОБЩ.[dР3]", "ОБЩ.[Pб]", "ОБЩ.[P3]", "ОБЩ.[P4]"
                throw new Exception("incorrect EU tags supplied");

            //NB: у 741 единицы измерения хранятся в двоичной(!) части параметров БД, в 2х младших битах                                    
            for (int i = 0; i < euTags.Length; i++) {
                DataTag t = euTags[i];

                if (int.TryParse(Convert.ToString(t.Value), out int iEU)) {
                    iEU &= 0x03;   //битовая маска обязательна, тк в старших битах встречается что угодно (!)                                
                    eus.Add(t.Name, Logika4.getGasPressureUnits(iEU));
                } else {
                    eus.Add(t.Name, "");
                }
            }
            return eus;
        }

        public override ADSFlashRun[] getAdsFileLayout(bool all, string model)
        {
            if (all) {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0x17C80 },
                };

            } else {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0x4840 },
                    new ADSFlashRun() { Start = 0x13440, Length = 0x4840 },
                };
            }
        }

        public override string getModelFromImage(byte[] flashImage)
        {
            return "";
        }
    }
}
