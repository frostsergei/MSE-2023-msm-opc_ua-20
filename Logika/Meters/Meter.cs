using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using System.Resources;
using System.Diagnostics;


#if false
    [DataContract]
    public enum MeterType
    {        
        [EnumMember]
        SPT941,
        [EnumMember]
        SPT941_10,
        [EnumMember]
        SPT941_20,
        [EnumMember]
        SPT942,
        [EnumMember]
        SPT943,
        [EnumMember]
        SPT961,
        [EnumMember]
        SPT961M,
        [EnumMember]
        SPT961_1,
        [EnumMember]
        SPG741,
        [EnumMember]
        SPG742,
        [EnumMember]
        SPG761,
        [EnumMember]
        SPG762,
        [EnumMember]
        SPG763,
        [EnumMember]
        SPG761_1,
        [EnumMember]
        SPG762_1,
        [EnumMember]
        SPG763_1,
        [EnumMember]
        SPE542,
        [EnumMember]
        SPT961_1M,  //модернизированный под новые правила 961.1/2
        [EnumMember]
        SPT943rev3,    //модернизированный под новые правила 943
        [EnumMember]
        SPT944,    
        [EnumMember]
        SPT962,
        [EnumMember]
        LGK410,     //расходомер ЛГК410
    }
#endif

namespace Logika.Meters
{
    public abstract class Meter // : ExpandableObjectConverter
    {

        [Browsable(false)]
        public abstract MeasureKind MeasureKind
        {
            get;
        }


        [DisplayName("Название")]
        public abstract string Caption
        {
            get;
        }

        [Browsable(false)]
        public abstract int MaxChannels
        {
            get;
        }

        [Browsable(false)]
        public abstract int MaxGroups
        {
            get;
        }

        public override bool Equals(System.Object obj)
        {
            Meter p = obj as Meter;
            if (p == null)
                return false;

            return this.GetType().Equals(p.GetType());
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected static Meter[] getDefinedMeterTypes(Type type)
        {
            if (!typeof(Meter).IsAssignableFrom(type))
                throw new Exception("wrong type");
            FieldInfo[] fia = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            List<Meter> lm = new List<Meter>();
            foreach (var fi in fia) {
                if (fi.IsInitOnly && typeof(Meter).IsAssignableFrom(fi.FieldType)) {
                    lm.Add((Meter)fi.GetValue(null));
                }
            }
            return lm.ToArray();
        }

        protected Meter()
        {
        }

        public abstract bool SupportedByProlog4 { get; }

        public virtual bool Outdated { get { return false; } }

        #region devices "enum"
        public static readonly Logika4L SPT941 = new TSPT941();
        public static readonly Logika4L SPG741 = new TSPG741();
        public static readonly Logika4L SPT942 = new TSPT942();
        public static readonly Logika4L SPT943 = new TSPT943();
        public static readonly Logika4L SPT941_10 = new TSPT941_10();

        public static readonly Logika4M SPG742 = new TSPG742();
        public static readonly Logika4M SPT941_20 = new TSPT941_20();
        public static readonly Logika4M SPT943rev3 = new TSPT943rev3();  //модернизированный под новые правила 943        
        public static readonly Logika4M SPT944 = new TSPT944();
        public static readonly Logika4M LGK410 = new TLGK410();          //расходомер ЛГК410
        public static readonly Logika4M SPT940 = new TSPT940();
        public static readonly Logika4M SPG740 = new TSPG740();

        public static readonly Logika6 SPT961 = new TSPT961();
        public static readonly Logika6 SPG761 = new TSPG761();
        public static readonly Logika6 SPG762 = new TSPG762();
        public static readonly Logika6 SPG763 = new TSPG763();
        public static readonly Logika6 SPT961M = new TSPT961M();
        public static readonly Logika6 SPE542 = new TSPE542();

        public static readonly Logika6N SPT961_1 = new TSPT961_1();
        public static readonly Logika6N SPG761_1 = new TSPG761_1();
        public static readonly Logika6N SPG762_1 = new TSPG762_1();
        public static readonly Logika6N SPG763_1 = new TSPG763_1();
        public static readonly Logika6N SPT961_1M = new TSPT961_1M();    //модернизированный под новые правила 961.1/2        
        public static readonly Logika6N SPT962 = new TSPT962();
        public static readonly Logika6N SPT963 = new TSPT963();
        public static readonly Logika6N SPE543 = new TSPE543();
        #endregion

        static object mLock = new object();
        static Dictionary<string, Meter> meterDict = new Dictionary<string, Meter>(StringComparer.OrdinalIgnoreCase);

        #region MDB tags    //obsolete
#if false
        static DataTable mdbTags;
        static void mdbTags_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            e.Row.RejectChanges();  //make table pseudo read-only 
        }

        public static DataTable MdbTags
        {
            get
            {
                if (mdbTags == null) {
                    mdbTags = new DataTable();
                    mdbTags.BeginLoadData();
                    using (Stream ztagsStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Logika.Meters.logikaTags.gz")) {
                        GZipStream gzs = new GZipStream(ztagsStream, CompressionMode.Decompress);
                        MemoryStream tableStream = new MemoryStream();
                        gzs.CopyTo(tableStream);
                        tableStream.Seek(0, SeekOrigin.Begin);

                        mdbTags.ReadXml(tableStream);
                    }
                    mdbTags.EndLoadData();
                    mdbTags.RowChanged += mdbTags_RowChanged;
                }

                mdbTags.DefaultView.Sort = "Device.Key, Ordinal, Index";
                return mdbTags;
            }
        }
#endif
        #endregion


        static Meter()
        {
            lock (mLock) {
                Meter[] mtrs = getDefinedMeterTypes(typeof(Meter));

                foreach (var m in mtrs)
                    meterDict.Add(m.GetType().Name, m);
            }
        }

        public static Meter FromTypeString(string meterTypeString)
        {
            if (meterTypeString == null)
                return null;
            return meterDict[meterTypeString];
        }

        public static Meter[] SupportedMeters
        {
            get { return meterDict.Values.ToArray(); }
        }

        [Browsable(false)]
        public string VendorID
        {
            get { return "ЛОГИКА"; }
        }

        [DisplayName("Производитель")]
        public string Vendor
        {
            get { return "ЗАО НПФ ЛОГИКА"; }
        }

        [Browsable(false)]
        public abstract BusProtocolType BusType
        {
            get;
        }

        public override string ToString()
        {
            return Caption;
        }

        /// <summary>
        /// карта полей для интервальных архивов прибора (для импорта Prolog.mdb в новую базу)
        /// </summary>
        /// <returns></returns>

        //public abstract string[] mdb_GetSyncArchiveMap();
        //public abstract string[] mdb_GetTotalsMap();


        public abstract string GetEventPrefixForTV(int TV);

        //public abstract MeterFieldInfo[] GetArchiveStoreMap(IntervalArchiveType archiveType, string header);

//public abstract FieldPtr[] PrepareIntervalData(
//    ArchiveType archiveType, object []headers, HistoricalSeries[] dataSeries, System.Data.DataSet container);

#if false
        internal class CommonTagDef
        {
            internal string[] channels { get; }
            internal string[] keys { get; }

            internal CommonTagDef(string channel, string key)
            {
                this.channels = new string[] { channel };
                this.keys = new string[] { key };
            }

            internal CommonTagDef(string[] channels, string[] keys)
            {                
                this.channels = channels;
                this.keys = keys;
            }
        }
#endif
        /// <summary>
        /// returns array of tag names which are essential to interoperation with this meter(device) and
        /// are common to any logika device
        /// </summary>
        /// <param name="pathPrefix"></param>
        /// <returns></returns>

        public Dictionary<ImportantTag, DataTag[]> GetWellKnownTags()
        {
            var tdefs = GetCommonTagDefs();
            Dictionary<ImportantTag, DataTag[]> wtd = new Dictionary<ImportantTag, DataTag[]>();
            foreach (var tdef in tdefs) {
                DataTag[] dta = lookupCommonTags(tdef.Value);
                wtd.Add(tdef.Key, dta);
            }
            return wtd;
        }

        internal abstract Dictionary<ImportantTag, object> GetCommonTagDefs();
        internal DataTag[] lookupCommonTags(object tlist)
        {
            string[] tagAddrs;
            if (tlist is string)
                tagAddrs = new string[] { (string)tlist };
            else if (tlist is string[])
                tagAddrs = tlist as string[];
            else
                throw new Exception("unknown object passed as common tag address");
            DataTag[] dta = new DataTag[tagAddrs.Length];
            for (int i = 0; i < tagAddrs.Length; i++) {
                string[] ap = tagAddrs[i].Split('.');
                string chType = "";
                int chNo;
                string tagName;
                if (ap.Length == 1) {
                    tagName = ap[0];
                    chNo = 0;
                    chType = this.Channels.First(x => x.Start == 0 && x.Count == 1).Prefix;
                } else if (ap.Length == 2) {
                    chType = new string(ap[0].ToCharArray().Where(x => Char.IsLetter(x)).ToArray());
                    if (chType.Length == ap[0].Length)
                        chNo = 0;
                    else
                        chNo = Convert.ToInt32(ap[0].Substring(chType.Length));
                    tagName = ap[1];
                } else
                    throw new Exception("incorrect common tag address");
                DataTagDef dd = FindTag(chType, tagName);                
                if (dd == null)
                    throw new Exception(string.Format("common tag {0} not found", tagAddrs[i]));
                dta[i] = new DataTag(dd, chNo);
            }
            return dta;
        }
        //{
        //    throw new Exception("_getWellKnownTagNames should be implemented for " + this.GetType().ToString());
        //}

        /// <summary>
        /// transforms common tags (those which returned by GetCommonTagsNames) values to CommonTags
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        //public abstract CommonIdent FillCommonIdent(Tag[] tags);

        public abstract DateTime advanceReadPtr(ArchiveType archiveType, DateTime time);

        public const string dfTemperature = "0.00";

        public abstract string GetDisplayFormat(TagDef fi);

        //public abstract ColumnInfo[] getArchiveTableDefinition(ArchiveType ar);
        int compareFI(ArchiveFieldDef a, ArchiveFieldDef b)
        {
            throw new Exception("no ordinal anymore");
            //return a.ChannelNo * 10000000 + a.Def.Ordinal - (b.ChannelNo * 10000000 + b.Def.Ordinal);
        }

        ArchiveDef[] _archives;
        public ArchiveDef[] Archives { get
            {
                lock (tagsLock) {
                    if (_archives == null)
                        loadMetadata();
                    return _archives;
                }
            }
        }
        public bool HasArchive(ArchiveType at)
        {
            return Archives.Any(x => x.ArchiveType == at);
        }

        ArchiveFieldDef[] refArchiveFields;
        internal abstract ArchiveFieldDef readArchiveFieldDef(DataRow r);
        public ArchiveFieldDef[] ArchiveFields
        {
            get
            {
                lock (tagsLock) {
                    if (refArchiveFields == null)
                        loadMetadata();
                }
                return refArchiveFields;
            }
        }

        public ArchiveFieldDef FindArchiveFieldDef(ArchiveType archiveType, int ordinal)
        {
            throw new Exception("no ordinal anymore");
            //return ArchiveTags.FirstOrDefault(x=>x.ArchiveType==archiveType && x.Ordinal==ordinal);            
        }

        public class TagVault
        {
            readonly DataTagDef[] refTags;

            //Dictionary<string, RefTag> tagIdDict = new Dictionary<string, RefTag>();
            Dictionary<Tuple<string, string>, DataTagDef> tagKeyDict = new Dictionary<Tuple<string, string>, DataTagDef>();
            //Dictionary<Tuple<string, int, int>, RefTag> tagOrdDict = new Dictionary<Tuple<string, int, int>, RefTag>();

            public TagVault(DataTagDef[] tags)
            {
                refTags = tags;
                foreach (var t in tags) {
                    tagKeyDict.Add(new Tuple<string, string>(t.ChannelDef.Prefix, Utils.Conversions.RusStringToStableAlphabet(t.Key)), t);
                }
            }

            public DataTagDef Find(string channelKind, string key)
            {
                DataTagDef r = null;
                tagKeyDict.TryGetValue(new Tuple<string, string>(channelKind, Utils.Conversions.RusStringToStableAlphabet(key)), out r);
                return r;
            }
                        
            public DataTagDef[] All
            {
                get { return refTags; }
            }
            
        }

        //static Dictionary<Meter, TagVault> metersTags = new Dictionary<Meter, TagVault>();
        object tagsLock = new object();
        TagVault _tagVault = null;

        static DataTable channelsTable;
        
        protected ChannelDef[] _channels;
        public ChannelDef[] Channels {
            get {
                if (_channels == null)
                    loadMetadata();
                return _channels;
            }
        }

        public abstract string FamilyName { get; }

        static DataSet metadata = new DataSet("metadata");        

        static ResourceReader _rr;
        static ResourceReader resReader
        {
            get
            {
                if (_rr == null) {
                    var mrs = Assembly.GetExecutingAssembly().GetManifestResourceStream("Logika.Tags.resources");
                    _rr = new ResourceReader(mrs);
                }
                return _rr;
            }
        }

        internal void readCommonDef(DataRow r, out string chKey, out string name, out int ordinal, out TagKind kind, out bool isBasicParam, out int updRate, out Type dataType, out StdVar stv, out string desc, out string descEx, out string range)
        {
            chKey = r["Channel"].ToString();
            name = r["Name"].ToString();
            ordinal = (int)r["Ordinal"];
            desc = Convert.ToString(r["Description"]);

            kind = TagKind.Undefined;
            Enum.TryParse<TagKind>(r["Kind"].ToString(), out kind);

            isBasicParam = Convert.ToBoolean(r["Basic"]);
            updRate = (int)r["UpdateRate"];

            dataType = null;
            string sDataType = Convert.ToString(r["DataType"]);
            if (!string.IsNullOrEmpty(sDataType))
                dataType = Type.GetType("System." + sDataType, true);

            stv = r["VarT"] == DBNull.Value ? StdVar.unknown : (StdVar)Enum.Parse(typeof(StdVar), r["VarT"].ToString());
            descEx = Convert.ToString(r["DescriptionEx"]);
            range = Convert.ToString(r["Range"]);
        }

        internal abstract DataTagDef readTagDef(DataRow r);
           
        protected abstract string tagsSort { get; }
        protected abstract string archiveFieldsSort { get; }
        public void perfDebugReset()
        {
            _tagVault = null;
            channelsTable = null;
            _channels = null;
            _archives = null;
            metadata.Tables.Clear();

        }


        void loadMetadata()
        {
            string devName = this.GetType().Name.Remove(0, 1);  // TSPTxxx -> SPTxxx

            lock (tagsLock) {
#region loading channels
                if (channelsTable == null) 
                    channelsTable = loadResTable("Channels");

                if (_channels == null) {
                    DataRow[] cr = channelsTable.Select("Device='" + devName + "'");
                    List<ChannelDef> lc = new List<ChannelDef>();
                    foreach (var row in cr) {
                        int st = (int)row["Start"];
                        int ct = (int)row["Count"];
                        lc.Add(new ChannelDef(this, row["Key"].ToString(), st, ct, row["Description"].ToString()));
                    }
                    _channels = lc.ToArray();
                }
#endregion

#region loading tags
                if (_tagVault == null) {
                    
                    string tableName = this.FamilyName + "Tags";
                    DataTable dt = metadata.Tables[tableName];
                    if (dt == null) 
                        dt = loadResTable(tableName);

                    List<DataTagDef> lt = new List<DataTagDef>();
                    foreach (DataRow r in dt.Select("Device='" + devName + "'", tagsSort)) {
                        DataTagDef rt = readTagDef(r);                        
                        lt.Add(rt);                        
                    }
                    _tagVault = new TagVault(lt.ToArray());
                }
#endregion

#region loading archives
                string arTableName = this.FamilyName + "Archives";
                DataTable dta = metadata.Tables[arTableName];
                if (dta == null) 
                    dta = loadResTable(arTableName);
                
                if (_archives == null) {                    
                    string aclassName = this.GetType().Name;
                    DataRow[] rows = dta.Select("Device='" + devName + "'", "Device, ArchiveType");                    
                    _archives = readArchiveDefs(rows);                                            
                }

#endregion

#region loading archive fields
                string afTableName = this.FamilyName + "ArchiveFields";
                DataTable dtf = metadata.Tables[afTableName];
                if (dtf == null)
                    dtf = loadResTable(afTableName);

                if (refArchiveFields == null) {
                    string aclassName = this.GetType().Name;
                    DataRow[] rows = dtf.Select("Device='" + devName + "'", archiveFieldsSort);
                    List<ArchiveFieldDef> lf = new List<ArchiveFieldDef>();
                    foreach (DataRow r in rows) {
                        ArchiveFieldDef rf = readArchiveFieldDef(r);
                        lf.Add(rf);
                    }
                    refArchiveFields = lf.ToArray();
                }

#endregion
            }
        }

        protected abstract ArchiveDef[] readArchiveDefs(DataRow[] rows);

        private static DataTable loadResTable(string tableName)
        {
            const int RES_OFFSET = 4;

            DataTable dt;
            string resType;
            byte[] resData;
            resReader.GetResourceData(tableName, out resType, out resData);
            dt = Deserializer.DeserializeDataTable(resData, RES_OFFSET, Encoding.Unicode);
            dt.TableName = tableName;
            metadata.Tables.Add(dt);
            return dt;
        }
        /*
        public static void testLoad6()
        {
            const int RES_OFFSET = 4;

            DataTable dt;
            string resType;
            byte[] resData;

            Stopwatch sw0 = new Stopwatch();            
            sw0.Start();
            resReader.GetResourceData("X6Tags", out resType, out resData);
            sw0.Stop();
            //System.Diagnostics.Debug.Print("getresourcedata done in {0} ms", sw0.ElapsedMilliseconds);


            sw0.Restart();
            dt = Deserializer.DeserializeDataTable(resData, RES_OFFSET, Encoding.Unicode);
            //System.Diagnostics.Debug.Print("DeserializeDataTable done in {0} ms", sw0.ElapsedMilliseconds);
            sw0.Stop();
            //dt.TableName = tableName;
            //metadata.Tables.Add(dt);
            //return dt;
        }
        */
        public TagVault Tags {
            get
            {
                lock (tagsLock) {
                    if (_tagVault == null)
                        loadMetadata();
                }                
                return _tagVault;
            }
        }

        public bool SupportsParamsDbChecksum {
            get {
                return GetCommonTagDefs().ContainsKey(ImportantTag.ParamsCSum);                
            }
        }

        //public RefTag[] AllTags()
        //{
        //    return GetTagVault().ToArray();
        //}

        /*
        public RefTag[] FindTagsByName(string channelKind, string name)
        {
            TagVault tags = GetTagVault();
            return tags.FindName(channelKind, name);
        }
        */

        public DataTagDef FindTag(string chKind, string key)
        {            
            return Tags.Find(chKind, key);
        }
        
        public abstract byte? GetNTFromTag(string tagValue);

        internal abstract ChannelKind getChannelKind(int channelStart, int channelCount, string channelName);

        public DataTag[] getBasicParams()
        {
            List<DataTag> dts = new List<DataTag>();
            var paramTagDefs = Tags.All.Where(x => (x.Kind == TagKind.Parameter || x.Kind == TagKind.Info) && x.isBasicParam);
            foreach (var chDef in Channels) {
                for (int chNo = chDef.Start; chNo < chDef.Start + chDef.Count; chNo++)
                    foreach (var td in paramTagDefs) {
                        if (td.ChannelDef==chDef)
                            dts.Add(new DataTag(td, chNo));
                    }
            }
            return dts.ToArray();
        }

}

}
