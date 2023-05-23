using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Reflection;
using System.ComponentModel;

namespace Logika.Meters
{
    /*
    struct nsdef
    {
        int number;
        string caption;        
    }
    */
    public abstract class Logika4 : Meter
    {
        public const string dfPressure = "0.000";
        public const string dfMass = "0.000";
        public const string dfVolume = "0.000";
        public const string dfFlow = "0.000";

        public const string dfEnergy = "0.000";
        public const string dfTimeInt = "0.00";
        public const string df0000 = "0.0000";
       
        string[] nsDescs;
        public string GetNSDescription(int nsNumber)
        {
            if (nsDescs == null)
                nsDescs = getNsDescriptions();
            
            if(nsNumber > nsDescs.Length - 1)
                return "";
            return nsDescs[nsNumber];
        }

        public static string DisplayNS(object value)
        {
            const string separator = ",";

            StringBuilder sb = new StringBuilder();
            if (value == null || value == DBNull.Value)
                return "";
            else if (value is byte[]) {
                byte[] bns = (byte[])value;
                for (int i = 0; i < bns.Length * 8; i++) {
                    if ((bns[i / 8] & (1UL << i % 8)) > 0) {
                        sb.Append(i);
                        sb.Append(separator);
                    }
                }
                if (sb.Length > 0)
                    sb.Remove(sb.Length - separator.Length, separator.Length);
                else
                    sb.Append("-");
            } else
                return "?";

            return sb.ToString();
        }


        public string GetNSDescription(string nsString) //"НСxx+ / ДСxx-"
        {
            string evtDesc = null;
            if (nsString.EndsWith("+")) {
                int si = nsString.IndexOf("С");  //ДС, НС
                if (si > 0)
                    nsString = nsString.Substring(si + 1, nsString.Length - si - 2);   //trim leading 'ДС'/'НС' and trailing '+' or '-'
                int nsNo = Convert.ToInt32(nsString);
                evtDesc = GetNSDescription(nsNo);
            }
            return evtDesc;
        }

        protected abstract string[] getNsDescriptions();
        
        internal Logika4()
        {              
        }

        public override string GetDisplayFormat(TagDef fi)
        {
            if (!string.IsNullOrEmpty(fi.DisplayFormat))
                return fi.DisplayFormat;
            switch (fi.StdVar) {
                case StdVar.G: return dfFlow;
                case StdVar.M: return dfMass;
                case StdVar.P: return dfPressure;
                case StdVar.T: return dfTemperature;
                case StdVar.ti: return dfTimeInt;
                case StdVar.V: return dfVolume;
                case StdVar.W: return dfEnergy;
                default:
                    return null;
            };
        }

        public override BusProtocolType BusType
        {
            get { return BusProtocolType.RSbus; }
        }

        //static Dictionary<ArchiveType, string> archiveTagsDict = new Dictionary<ArchiveType, string>() {
        //    { ArchiveType.Hour, "арх_Ч" },
        //    { ArchiveType.Day, "арх_С" },
        //    { ArchiveType.Month, "арх_М" },
        //    { ArchiveType.Control, "арх_К" },

        //    { ArchiveType.Errors, "НСа" },
        //    { ArchiveType.ParamChanges, "ИЗМ" },
        //};


        //string[] getArchiveKeys(ArchiveType archiveType)
        //{
        //    string st;
        //    if (this.MaxConsumers == 1) {                
        //        if (archiveTagsDict.TryGetValue(archiveType, out st))
        //            return new string[] { "ОБЩ." + st };
        //        else
        //            return new string[0];
        //    } else {
        //        List<string> ls = new List<string>();
        //        for (int i = 1; i <= this.MaxConsumers; i++) {
        //            if (archiveTagsDict.TryGetValue(archiveType, out st))
        //                ls.Add(string.Format("ТВ{0}.{1}", i, st));
        //        }
        //        return ls.ToArray();
        //    }
        //}

        //public override string[] GetArchiveOPCKeys(ArchiveType archiveType)
        //{
        //    return getArchiveKeys(archiveType);
        //}


        public override string GetEventPrefixForTV(int TVnum)
        {
            if (this.MaxGroups == 1)
                return "";
            else
                return string.Format("ТВ{0} ", TVnum.ToString());
        }


        /*
        public override void PrepareIntervalData(ArchiveType archiveType, string []headers, HistoricalSeries[] dataSeries, System.Data.DataSet container)
        {
            DataTable rt = container.Tables[this.MeasureKind.ToString()];
            DataTable pt = container.Tables[this.MeasureKind.ToString()+"Pipe"];
            DataTable ct = container.Tables[this.MeasureKind.ToString()+"Cons"];

            rt.BeginLoadData();
            pt.BeginLoadData();
            ct.BeginLoadData();

            MeterFieldInfo [] smap = this.GetArchiveMap();            
            int FIELDS_PER_TV = smap.Length/this.MaxConsumers;

            int[] fptrs = new int[smap.Length]; //индексы колонок в соответствующих таблицах

            for (int i = 0; i < smap.Length; i++) {
                MeterFieldInfo fi = smap[i];
                DataTable dt = null;
                switch (fi.ChannelKind) {
                    case ChannelKind.Rec: dt = rt; break;
                    case ChannelKind.Pipe: dt = pt; break;
                    case ChannelKind.Cons: dt = ct; break;
                }
                fptrs[i] = dt.Columns.IndexOf(fi.StdVar.ToString());
            }
            const int SUBST_MTRID = -1;
            
            for (int p = 0; p < dataSeries.Length; p++) {
                //HistoricalSeries tvData = dataSeries[arPart];
                List<VQT> arPart = dataSeries[p].Data;

                for (int d=0; d<arPart.Count; d++) {
                    VQT rec = arPart[d];

                    object[][] pRows = new object[this.MaxPipes + 1][];   //+1 = empty [0] element
                    object[][] cRows = new object[this.MaxConsumers + 1][];
                    
                    DataRow rRow = rt.Rows.Find(new object[] { SUBST_MTRID, (int)archiveType, rec.Timestamp });
                    if (rRow==null)
                        rRow = rt.Rows.Add(new object[] { null, SUBST_MTRID, (int)archiveType, rec.Timestamp });
                    int recID = (int)rRow[0];
                    object [] slice = rec.Value as object[];
                    
                    for (int i = 0; i < slice.Length; i++) {
                        int fpIdx = p * FIELDS_PER_TV + i;
                        MeterFieldInfo fi = smap[fpIdx];
                        int columnIndex = fptrs[fpIdx];
                        
                        switch (fi.ChannelKind) {
                            case ChannelKind.Rec:
                                rRow[columnIndex] = slice[i];
                                break;
                            
                            case ChannelKind.Pipe:                                 
                                object []pRow;
                                if (pRows[fi.ChannelNo] == null) {
                                    pRow = pRows[fi.ChannelNo] = new object[pt.Columns.Count];
                                    pRow[0] = recID;
                                    pRow[1] = fi.ChannelNo;
                                } else
                                    pRow = pRows[fi.ChannelNo];
                                //int pIdx = pt.Columns.IndexOf(fi.StdVar.ToString());
                                pRow[columnIndex] = slice[i];
                                break;

                            case ChannelKind.Cons: ;
                                //int сidx = ct.Columns.IndexOf(fi.StdVar.ToString());
                                object []cRow;
                                if (cRows[fi.ChannelNo] == null) {
                                    cRow = cRows[fi.ChannelNo] = new object[ct.Columns.Count];
                                    cRow[0] = recID;
                                    cRow[1] = fi.ChannelNo;
                                } else
                                    cRow = cRows[fi.ChannelNo];
                                //int сIdx = ct.Columns.IndexOf(fi.StdVar.ToString());
                                cRow[columnIndex] = slice[i];
                                break;
                        }
                    }
                    foreach (object[] oa in pRows)
                        if (oa != null)
                            pt.Rows.Add(oa);

                    foreach (object[] oa in cRows)
                        if (oa != null)
                            ct.Rows.Add(oa);
                }
            }
#if DEBUG
           // container.WriteXml(string.Format("d:\\temp\\test_{0}.xml", archiveType));
#endif
            rt.EndLoadData();
            pt.EndLoadData();
            ct.EndLoadData();
        }
        */

        public override DateTime advanceReadPtr(ArchiveType archiveType, DateTime time)
        {
            if (archiveType == ArchiveType.Hour)
                return time.AddDays(1);

            else if (archiveType == ArchiveType.Day || archiveType == ArchiveType.Control)
                return time.AddMonths(1);

            else if (archiveType == ArchiveType.Month)
                return time.AddYears(1);

            else if (archiveType.IsServiceArchive)   //сервисные архивы
                return time.AddDays(7);

            else
                throw new Exception("unsupported archive");
        }
        //public override int PipeCluster { get { return MaxPipes; } }
        //public override int ConsCluster { get { return MaxConsumers; } }

        /*
        public override ColumnInfo[] getArchiveTableDefinition(ArchiveType ar)
        {
            string tName = this.GetType().Name;//makeTableName(ar);

            List<ColumnInfo> lc = new List<ColumnInfo>();
            lc.AddRange(getArchiveKeyColumns());

            ArchiveField[] mf = GetArchiveFields(ar);

            int o = lc.Count;
            foreach (var f in mf)
                lc.Add(new ColumnInfo() { index = o++, name = f.Name, dataType = f.Def.DbType, nullable = true });
            ColumnInfo ci = new ColumnInfo() { index = colIndex++, name = af.Name, dataType = af.Def.DbType, nullable = true };

            return lc.ToArray();
        }
        */

        public static string getGasPressureUnits(int euParamValue)
        {
            switch (euParamValue) {
                default:    //в некоторые приборы можно ввести значение > 3, прибор при этом использует 0
                case 0: return "кПа";
                case 1: return "МПа";
                case 2: return "кг/см²";
                case 3: return "кг/м²";
            }
        }

        public abstract bool SupportsBaudRateChangeRequests { get; } 

        [Browsable(false)]
        public abstract int MaxBaudRate { get; }


        public abstract TimeSpan SessionTimeout { get; }  //младшие приборы автоматически перестают прослушивать порт по истечении некоторого таймаута при отсутствии обмена.
        public abstract bool SupportsFastSessionInit { get; }  //выдерживать паузы между байтами FF и после FF-ов в стартовой последовательности или нет        

        public abstract ushort IdentWord { get; }
        public virtual bool IdentMatch(byte id0, byte id1, byte ver)
        {
            ushort devID = (ushort)(id0 << 8 | id1);
            return devID == IdentWord;                
        }

        public static Logika4 MeterTypeFromResponse(byte id0, byte id1, byte ver)
        {            
            var m4devs = Meter.SupportedMeters.Where(x => x is Logika4).Cast<Logika4>();
            foreach (Logika4 dev in m4devs)
                if (dev.IdentMatch(id0, id1, ver))
                    return dev;
                                
            throw new Exception(string.Format("неподдерживаемый прибор {0:X2} {1:X2} {2:X2}", id0, id1, ver));
        }

        //----------------------------------------------------------------------------------------------
        public static int[] BitNumbers(ulong val, int nBits, int nOffset)
        {
            List<int> bitNumbers = new List<int>(nBits);
            for (int ib = 0; ib < nBits; ib++)
                if ((val & (1UL << ib)) > 0)
                    bitNumbers.Add((byte)(ib + nOffset));
            return bitNumbers.ToArray();
        }

        public static DateTime CombineDateTime(string dateTag, string timeTag)
        {
            string[] tt = timeTag.ToString().Split('-', ':');
            string[] dt = dateTag.ToString().Split('-', '.');

            return new DateTime(2000 + Convert.ToByte(dt[2]), Convert.ToByte(dt[1]), Convert.ToByte(dt[0]), Convert.ToByte(tt[0]), Convert.ToByte(tt[1]), Convert.ToByte(tt[2]), DateTimeKind.Local);
        }

        //----------------------------------------------------------------------------------------------
        public static int[] BitNumbers(byte[] array, int offset, int nBits)
        {
            List<int> bitNumbers = new List<int>(nBits);
            for (int i = 0; i < nBits; i++)
                if ((array[offset + i / 8] & 1 << i % 8) > 0)
                    bitNumbers.Add((byte)i);
            return bitNumbers.ToArray();
        }
        //----------------------------------------------------------------------------------------------


        public override byte? GetNTFromTag(string tagValue)
        {
            byte NT;
            if (byte.TryParse(tagValue, out NT))
                return NT;
            return null;
        }

        //---------------------------------------------------------------------------------------------
        public static byte Checksum8(byte[] Buf, int Start, int Len)
        {
            byte a = 0xFF;
            for (int i = 0; i < Len; i++)
                a -= Buf[Start + i];

            return a;
        }

        internal override ChannelKind getChannelKind(int channelStart, int channelCount, string channelName)
        {
            switch (channelStart) {
                case 0:
                    return ChannelKind.Common;
                default:
                    return ChannelKind.TV;
            }
        }

        public static string getEU(Dictionary<string, string> euDict, string euDef)  //can be [P], [Q] etc. which should be translated using euParams
        {
            if (euDict!=null && euDict.TryGetValue(euDef, out string translatedEU))
                return translatedEU;
            else
                return euDef;
        }

        public abstract Dictionary<string, string> BuildEUDict(DataTag[] euTags);

        public class CalcFieldDef : TagDef
        {
            public readonly int channelNo;
            public readonly string insertAfter;
            public readonly string expression;
            public readonly string eu;

            //public string name;
            //public Type dataType = typeof(float);
            public CalcFieldDef(ChannelDef channel, int channelNo, 
                int ordinal, string name, StdVar stdVar, string desc, Type dataType, string dbType, string displayFormat, 
                string insertAfter, string expression, string eu)
            :base(channel, ordinal, name, stdVar, desc, dataType, dbType, displayFormat)
            {
                this.insertAfter = insertAfter;
                this.expression = expression;
                this.channelNo = channelNo;
                this.eu = eu;
            }

            public override string Key {
                get {
                    throw new NotImplementedException();
                }
            }
        }

        public virtual CalcFieldDef[] GetCalculatedFields()
        {
            return new CalcFieldDef[0];
        }


    }

}
