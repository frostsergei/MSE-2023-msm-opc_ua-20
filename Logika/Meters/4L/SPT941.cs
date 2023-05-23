using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT941 : Logika4L
    {
        internal TSPT941() { }
        //public override MeterType Type { get { return MeterType.SPT941; } }
        public override ushort IdentWord => 0x5429;
        
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ941"; } }
        public override int MaxChannels { get { return 3; } }
        public override int MaxGroups { get { return 1; } }

        public override bool SupportedByProlog4 => false;

        public override Dictionary<string, string> BuildEUDict(DataTag[] euTags)
        {
            throw new NotImplementedException("not supported");
        }
        public override string getModelFromImage(byte[] flashImage)
        {
            return "";
        }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            return new Dictionary<ImportantTag, object>() {
            //{ ImportantTag.ConsConfig,  "ОБЩ.СП" },
            { ImportantTag.NetAddr,     "ОБЩ.NT" },
            { ImportantTag.Ident,       "ОБЩ.ИД" },
        };
        }
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


#region archive fields map
                static MeterFieldInfo[] syncMap = new MeterFieldInfo[] 
                { 
                    new MeterFieldInfo("СП", 0, ChannelKind.Cons, Stv.SP, 1, DbType.Byte),
                    new MeterFieldInfo("НС", 1, ChannelKind.Cons, Stv.NS, 1, DbType.Int32, -1, Logika4.DisplayNS),

                    new MeterFieldInfo("t1", 2, ChannelKind.Pipe, Stv.t, 1, DbType.Single),
                    new MeterFieldInfo("t2", 3, ChannelKind.Pipe, Stv.t, 2, DbType.Single),
                    new CalculatedFieldDisplayInfo("dt", Stv.dt, 1, "P1.t-P2.t"),

                    new MeterFieldInfo("V1", 4, ChannelKind.Pipe, Stv.V, 1, DbType.Single),
                    new MeterFieldInfo("V2", 5, ChannelKind.Pipe, Stv.V, 2, DbType.Single),
                    new MeterFieldInfo("V3", 6, ChannelKind.Pipe, Stv.V, 3, DbType.Single),

                    new MeterFieldInfo("M1", 7, ChannelKind.Pipe, Stv.M, 1, DbType.Single),
                    new MeterFieldInfo("M2", 8, ChannelKind.Pipe, Stv.M, 2, DbType.Single),
                    new MeterFieldInfo("M3", 9, ChannelKind.Pipe, Stv.M, 3, DbType.Single),

                    new MeterFieldInfo("Q",  10, ChannelKind.Cons, Stv.dW, DbType.Single),
                    new MeterFieldInfo("Tи", 11, ChannelKind.Cons, Stv.Ti, DbType.Single),

                };

                static MeterFieldInfo[] totalsMap = new MeterFieldInfo[] 
                {             
                    new MeterFieldInfo("V1", 4,  ChannelKind.Pipe, Stv.V, 1, DbType.Double),
                    new MeterFieldInfo("V2", 5,  ChannelKind.Pipe, Stv.V, 2, DbType.Double),
                    new MeterFieldInfo("V3", 6,  ChannelKind.Pipe, Stv.V, 3, DbType.Double),

                    new MeterFieldInfo("M1", 7,  ChannelKind.Pipe, Stv.M, 1, DbType.Double),
                    new MeterFieldInfo("M2", 8,  ChannelKind.Pipe, Stv.M, 2, DbType.Double),
                    new MeterFieldInfo("M3", 9,  ChannelKind.Pipe, Stv.M, 3, DbType.Double),

                    new MeterFieldInfo("Q",  10, ChannelKind.Cons, Stv.dW, 1, DbType.Double),
                    new MeterFieldInfo("Tи", 11, ChannelKind.Cons, Stv.Ti, 1, DbType.Double),
                };

                public override MeterFieldInfo[] GetTotalFields()
                {
                    return totalsMap;
                }

#endregion
                */

        protected override string[] getNsDescriptions() {
            
                return new string[] {
            "Разряд батареи",        //00
            "Выход температуры на ТС1 за диапазон",
            "Выход температуры на ТС2 за диапазон",
            "Перегрузка цепи питания ВС",
            "Масса М3 (ГВС) меньше минус 0,04 М1",
            "Отрицательная тепловая энергия",   //05
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

        public static object[] ExpandHourRecord(object[] hourRec)
        {
            object[] normRec = new object[12];
            Array.Copy(hourRec, normRec, 4);  //SP NS T1 T2 are in place already

            byte sp = (byte)hourRec[0];
            float v12 = (float)hourRec[4];
            float v23 = (float)hourRec[5];
            float m12 = (float)hourRec[6];
            float m23 = (float)hourRec[7];
            float q = (float)hourRec[8];

            //Variant &t2 = result[3];
            //Variant &v1 = Record[4];
            //Variant &v2 = result[5];
            //Variant &v3 = result[6];
            //Variant &m1 = result[7];
            //Variant &m2 = result[8];
            //Variant &m3 = result[9];

            switch (sp) {
                case 0:
                    normRec[4] = v12;
                    normRec[5] = v23;
                    normRec[6] = null;
                    normRec[7] = m12;    //m1
                    normRec[8] = m23;    //m2
                    normRec[9] = m12 - m23;
                    break;
                case 1:
                    normRec[4] = v12;
                    normRec[5] = null;
                    normRec[6] = v23;
                    normRec[7] = m12;
                    normRec[8] = m23;
                    normRec[9] = m12 - m23;
                    break;
                case 2:
                    normRec[4] = null;
                    normRec[5] = v12;
                    normRec[6] = v23;
                    normRec[7] = m12;
                    normRec[8] = m23;
                    normRec[9] = m12 - m23;
                    break;
                case 3:
                case 4:
                    normRec[4] = v12;
                    normRec[5] = v23;
                    normRec[6] = null;
                    normRec[7] = m12;
                    normRec[8] = m23;
                    normRec[9] = null;
                    break;
                case 5:
                    normRec[4] = v12;
                    normRec[5] = null;
                    normRec[6] = v23;
                    normRec[7] = m12;
                    normRec[8] = null;
                    normRec[9] = m23;
                    break;
                case 6:
                    normRec[4] = null;
                    normRec[5] = v12;
                    normRec[6] = v23;
                    normRec[7] = null;
                    normRec[8] = m12;
                    normRec[9] = m23;
                    break;
                case 7:
                    normRec[3] = null;    //t2
                    normRec[4] = v12;
                    normRec[5] = null;
                    normRec[6] = null;
                    normRec[7] = m12;
                    normRec[8] = null;
                    normRec[9] = null;
                    break;
                case 8:
                    normRec[4] = v12;
                    normRec[5] = v23;
                    normRec[6] = null;
                    normRec[7] = m12;
                    normRec[8] = null;
                    normRec[9] = null;
                    break;
                case 9:
                    normRec[4] = v12;
                    normRec[5] = v23;
                    normRec[6] = null;
                    normRec[7] = null;
                    normRec[8] = null;
                    normRec[9] = null;
                    break;
            }
            normRec[10] = q;
            normRec[11] = null;          //have no Ti

            return normRec;
        }
        //public override string getModel(byte[] flash)
        //{
        //    return "";
        //}
        
        public override ADSFlashRun[] getAdsFileLayout(bool all, string model)
        {
            if (all) {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0xD880 },
                };

            } else {
                return new ADSFlashRun[] {
                    new ADSFlashRun() { Start = 0x00000, Length = 0x1700 },
                    new ADSFlashRun() { Start = 0x09E00, Length = 0x3A80 },
                };
            }
        }
    }
}
