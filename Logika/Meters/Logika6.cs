using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Reflection;

namespace Logika.Meters
{
    public abstract partial class Logika6 : Meter     //старые x6x
    {
        public override bool Outdated => true;

        public override string GetDisplayFormat(TagDef fi)
        {
            switch ((StdVar)fi.StdVar) {
                case StdVar.ti:
                case StdVar.T:
                    return "0.##";
                case StdVar.P:
                case StdVar.dP:
                case StdVar.G:
                case StdVar.M:
                case StdVar.V:
                case StdVar.W:
                case StdVar.AVG:
                    return "0.###";
                default:
                    return null;
            };
        }

        public override BusProtocolType BusType {
            get { return BusProtocolType.SPbus; }
        }


        public override string GetEventPrefixForTV(int TV)
        {
            return "";
        }

        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            var ctDict = new Dictionary<ImportantTag, object>() {
                //{ImportantTag.IfConfig,   "003" },                
                {ImportantTag.NetAddr, "003" },    //--отдельного тега для NT нет - поэтому ImportantTag.NetAddr тоже не будет
                { ImportantTag.Ident, "008" },
                { ImportantTag.RHour, "024" },
                { ImportantTag.RDay, "025" },
                { ImportantTag.EngUnits, "030н00" },            
                //{ImportantTag.ConsConfig, "031" },
                { ImportantTag.Model, "099" },
            };
            if (this.MeasureKind==MeasureKind.T) //054 is structure only in SPTs, not SPgs (old non-dot devs)
                ctDict.Add(ImportantTag.ParamsCSum, "054н06");
            return ctDict;

        }

        public override byte? GetNTFromTag(string tagValue)
        {
            if (string.IsNullOrEmpty(tagValue) || tagValue.Length < 7)
                return null;

            string sNT = tagValue.Substring(5, 2);
            if (byte.TryParse(sNT, out byte NT))
                return NT;

            return null;
        }

        public override DateTime advanceReadPtr(ArchiveType archiveType, DateTime startTime)
        {
            if (archiveType == ArchiveType.Hour)
                return startTime.AddHours(1);
            else if (archiveType == ArchiveType.Day)
                return startTime.AddDays(1);
            else if (archiveType == ArchiveType.Month)
                return startTime.AddMonths(1);
            else if (archiveType == ArchiveType.ErrorsLog)
                return startTime.AddDays(7);
            else if (archiveType == ArchiveType.ParamsLog || archiveType == ArchiveType.PowerLog)
                return startTime.AddMonths(1);
            else
                throw new Exception("unsupported archive");
        }

        int[] getMdbMap(char kind)
        {
            string fname = string.Format("mdb_{0}_ords", kind);
            FieldInfo fi = this.GetType().GetField(fname, BindingFlags.Static | BindingFlags.NonPublic);

            if (fi == null)
                throw new Exception(fname + " not found for " + this.GetType().Name);
            return fi.GetValue(this) as int[];


        }
        public int[] getMdbMap()
        {
            int[] r_ords = getMdbMap('R');
            int[] p_ords = getMdbMap('P');
            int[] c_ords = getMdbMap('C');
            List<int> map = new List<int>();
            for (
                int i = 0; i < Math.Max(MaxChannels, MaxGroups); i++) {
                if (i == 0)
                    map.AddRange(r_ords);
                if (i < MaxChannels)
                    map.AddRange(p_ords);
                if (i < MaxGroups)
                    map.AddRange(c_ords);
            }
            return map.ToArray();
        }


        static char[] channelSuffixes = { 'т', 'п', 'г', 'к' };
        /// <summary>
        /// 
        /// </summary>
        /// <param name="compositeName">full var name with channel type and channel (i.e. 'Wт01')</param>
        /// <param name="channelType"></param>
        /// <param name="channelNo"></param>
        /// <returns></returns>
        public static void SplitVarCaption(string compositeName, out string caption, out string channelType, out int channelNo)
        {
            string sCh = "";
            for (int z = compositeName.Length - 1; z > 0; z--) {
                if (Char.IsDigit(compositeName[z]))
                    sCh = compositeName[z] + sCh;
                else if (channelSuffixes.Contains(compositeName[z]) && sCh.Length > 0) {
                    channelType = new string(compositeName[z], 1);
                    channelNo = Convert.ToInt32(sCh);
                    caption = compositeName.Substring(0, compositeName.Length - sCh.Length - 1);
                    return;
                } else
                    break;
            }
            channelType = "0";
            channelNo = 0;
            caption = compositeName;
        }
        /*
        public virtual int NonSparsePipes   //количество трубопроводов хранящихся в БД без признака sparse
        {
            get {
                return 6;
            }
        }

        public virtual int NonSparseConsumers   //количество потребителей хранящихся в БД без признака sparse
        {
            get {
                return 3;
            }
        }
        */

        //-------------------------------------------------------------------------------------------------------
        public static ushort NormalizeYear(ushort year)
        {
            if (year < 95) //if year in range 0..94, take as 21st century
                year += 2000;
            else if (year >= 95 && year < 100) //if in range 95..99 take as 20th century
                year += 1900;
            return year;
        }

        public static DateTime TimeStringToDateTime(string SPT_DateTime)
        {
            //converts SPTx6x datetime string in format dd-mm-yy/hh:nn:ss to System::DateTime
            string[] dt = SPT_DateTime.Split("/".ToCharArray(), 2); //(__gc new Char[] {'/'});
            string[] df = dt[0].Split("-".ToCharArray(), 3);
            string[] tf = dt[1].Split(":".ToCharArray(), 3);

            int year = NormalizeYear(Convert.ToUInt16(df[2])); //if year > 99 - do nothing

            return new DateTime(year, Convert.ToInt32(df[1]), Convert.ToInt32(df[0]), Convert.ToInt32(tf[0]), Convert.ToInt32(tf[1]), Convert.ToInt32(tf[2]), DateTimeKind.Local);
        }
        //-------------------------------------------------------------------------------------------------------

        public static string getChannelKind(int ordinal)
        {
            if (ordinal < 100)
                return "0";
            else if (ordinal < 300)
                return "т";
            else
                return "п";
        }

        public override string FamilyName { get { return "X6"; } }
        protected override string tagsSort { get { return "Device, Ordinal, Index"; } }
        protected override string archiveFieldsSort { get { return "Device, ArchiveType, Ordinal"; } }

        internal override DataTagDef readTagDef(DataRow r)
        {
            readCommonDef(r, out string chKey, out string name, out int ordinal, out TagKind kind, out bool isBasicParam, out int updRate, out Type dataType, out StdVar stv, out string desc, out string descriptionEx, out string range);

            Tag6NodeType type = (Tag6NodeType)Enum.Parse(typeof(Tag6NodeType), r["Type"].ToString(), true);

            ChannelDef ch = this.Channels.FirstOrDefault(x => x.Prefix == chKey);

            int? index = null;
            int? count = r["Count"] != DBNull.Value ? (int?)r["Count"] : null;

            if (type == Tag6NodeType.Tag || type == Tag6NodeType.Array) {
                kind = (TagKind)Enum.Parse(typeof(TagKind), r["Kind"].ToString());
                isBasicParam = Convert.ToBoolean(r["Basic"]);

                index = r["Index"] != DBNull.Value ? (int?)r["Index"] : null;
            } else {
                kind = TagKind.Undefined;
                isBasicParam = false;
            }

            return new DataTagDef6(ch, type, name, stv, kind, isBasicParam, updRate, ordinal, desc, dataType, null, index, count, descriptionEx, range);

        }

        protected override ArchiveDef[] readArchiveDefs(DataRow[] rows)
        {
            List<ArchiveDef> ra = new List<ArchiveDef>();
            ChannelDef ch = Channels.FirstOrDefault(x => x.Prefix == "0");

            foreach (DataRow r in rows) {
                ArchiveType art = ArchiveType.FromString(r["ArchiveType"].ToString());
                string sRecType = "System." + r["RecordType"].ToString();
                Type recType = Type.GetType(sRecType, true);
                string name = r["Name"].ToString();
                string desc = r["Description"].ToString();
                string[] sOrds = r["Ordinal"].ToString().Split(' ');
                int capacity = (int)r["Capacity"];
                ArchiveDef a;
                if (sOrds.Length == 1)
                    a = new ArchiveDef6(ch, art, recType, capacity, name, desc, Convert.ToInt32(sOrds[0]));
                else {
                    List<int> ords = new List<int>();
                    foreach (string s in sOrds)
                        ords.Add(Convert.ToInt32(s));

                    a = new MultipartArchiveDef6(ch, art, recType, capacity, name, desc, ords.ToArray());
                }

                ra.Add(a);
            }
            return ra.ToArray();
        }

        internal override ArchiveFieldDef readArchiveFieldDef(DataRow r)
        {
            //RefArchive ra = Archives.FirstOrDefault(x => x.ArchiveType == art);
            string chKey = r["Channel"].ToString();
            ChannelDef ch = Channels.FirstOrDefault(x => x.Prefix == chKey);
            ArchiveType art = ArchiveType.FromString(r["ArchiveType"].ToString());

            int ord = (int)r["Ordinal"];

            string sDataType = "System." + r["DataType"].ToString();
            Type t = Type.GetType(sDataType, true);

            string sDbType = Convert.ToString(r["DbType"]);
            string name = r["Name"].ToString();
            string desc = r["Description"].ToString();

            object oStdType = r["VarT"];
            StdVar stv = (oStdType is string && !string.IsNullOrEmpty((string)oStdType)) ? (StdVar)Enum.Parse(typeof(StdVar), (string)oStdType) : StdVar.unknown;

            //int? depth = r["Depth"] == DBNull.Value ? null : (int?)r["Depth"];

            return new ArchiveFieldDef6(ch, art, name, desc, ord, stv, t, sDbType, null);
        }

        internal override ChannelKind getChannelKind(int channelStart, int channelCount, string channelName)
        {
            if (channelStart == 0 && channelCount == 1)
                return ChannelKind.Common;

            if (channelName == "т" || channelName== "к")
                return ChannelKind.Channel;

            else if (channelName == "п" || channelName=="г")
                return ChannelKind.Group;

            return ChannelKind.Undefined;
        }

        public override bool SupportedByProlog4 => true;
    }
}
