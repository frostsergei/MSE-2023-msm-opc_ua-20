using Logika.Meters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.M4
{
    public partial class M4Protocol
    {
        public class MeterInstance
        {
            public readonly M4Protocol proto;
            public readonly Logika4 mtr;
            public readonly byte nt;

            string model = null;
            public string Model
            {
                get {
                    if (model == null) {
                        if (vipTags.TryGetValue(ImportantTag.Model, out DataTag[] mdlTag)) {
                            proto.updateTags(nt, mdlTag, updTagsFlags.DontGetEUs);
                            model = Convert.ToString(mdlTag[0].Value);
                        } else
                            model = "";
                    }
                    return model;
                }
            }

            public byte? sp;   //для трансляции параметров 741

            int rd = -1;
            internal int RD
            {
                get {
                    if (rd < 0 || rh < 0)
                        readRDRH();
                    return rd;
                }
            }

            int rh = -1;
            internal int RH
            {
                get {
                    if (rd < 0 || rh < 0)
                        readRDRH();
                    return rh;
                }
            }

            void readRDRH()
            {
                DataTag[] rdta = new DataTag[] { vipTags[ImportantTag.RDay][0], vipTags[ImportantTag.RHour][0] };
                proto.updateTags(nt, rdta, updTagsFlags.DontGetEUs);
                rd = Convert.ToInt32(rdta[0].Value);
                rh = Convert.ToInt32(rdta[1].Value);
            }

            Dictionary<string, string> eus;
            internal Dictionary<string, string> EUDict
            {
                get {
                    if (eus == null) {
                        if (vipTags.ContainsKey(ImportantTag.EngUnits)) {
                            proto.updateTags(nt, vipTags[ImportantTag.EngUnits], updTagsFlags.DontGetEUs);
                            eus = mtr.BuildEUDict(vipTags[ImportantTag.EngUnits]);
                        }
                    }
                    return eus;
                }
            }

            TimeSpan timeDiff = TimeSpan.MaxValue;
            public DateTime CurrentDeviceTime
            {
                get {
                    if (timeDiff == TimeSpan.MaxValue) {
                        DataTagDef tTime = mtr.Tags.Find("ОБЩ", "T");
                        DataTagDef tDate = mtr.Tags.Find("ОБЩ", "Д");
                        if (tTime == null || tDate == null)
                            return DateTime.MinValue;   //устройство без часов, напр. ЛГК410
                        DataTag[] dta = new DataTag[] { new DataTag(tDate, 0), new DataTag(tTime, 0) };
                        proto.updateTags(nt, dta, updTagsFlags.DontGetEUs /*| updTagsFlags.UseLocalTime*/);
                        DateTime devTime = Logika4.CombineDateTime(dta[0].Value.ToString(), dta[1].Value.ToString());
                        timeDiff = DateTime.Now - devTime;
                    }
                    return DateTime.Now - timeDiff;
                }
            }

            internal bool[] pageMap;
            internal byte[] flash;      //flash cache only for parameters and totals

            Dictionary<ImportantTag, DataTag[]> vipTags;
            internal MeterInstance(M4Protocol owner, Logika4 m, byte nt)
            {
                proto = owner;
                this.mtr = m;
                if (m is Logika4L) {
                    int lastTotalAddr = m.Tags.All.Where((x) => x.Kind == TagKind.TotalCtr).Cast<TagDef4L>().Max((t) => t.address.Value + (t.channelOffset ?? 0) + Logika4L.SizeOf(t.internalType));
                    int paramsFlashSize = lastTotalAddr += Logika4L.FLASH_PAGE_SIZE - 1;  //запас для хвостов
                    flash = new byte[paramsFlashSize];
                    pageMap = new bool[flash.Length / Logika4L.FLASH_PAGE_SIZE];
                }
                vipTags = m.GetWellKnownTags();

                this.nt = nt;
            }
        }

        Dictionary<byte, MeterInstance> metadataCache = new Dictionary<byte, MeterInstance>();  //кеш информации о приборе для высокоуровневых функций чтения, per-NT

        MeterInstance getMeterInstance(Logika4 m, byte? nt)
        {
            byte _nt = nt.HasValue ? nt.Value : (byte)0xFF;

            if (!metadataCache.TryGetValue(_nt, out MeterInstance mi)) {
                mi = new MeterInstance(this, m, _nt);
                metadataCache[_nt] = mi;
            }

            return mi;
        }

        public const int CHANNEL_NBASE = 100000;

        public override void UpdateTags(byte? src, byte? dst, DataTag[] tags)
        {
            if (tags.Length == 0)
                return;
            updateTags(dst, tags, updTagsFlags.None);
        }

        [Flags]
        enum updTagsFlags
        {
            None,
            DontGetEUs,
            //UseLocalTime,    //will be marked by comp time, else device time will be used            
        }

        void updateTags(byte? nt, DataTag[] tags, updTagsFlags flags)
        {
            MeterInstance mmd = getMeterInstance(tags[0].def.Meter as Logika4, nt);
            Meter m = tags[0].def.Meter;

            if (m is Logika4L)
                update4LTagsValues(nt, tags, mmd, flags);
            else if (m is Logika4M)
                updateTags4M(nt, tags, mmd, flags);
        }

        void getFlashPagesToCache(Logika4L mtr, byte? nt, int startPageNo, int count, MeterInstance mi)
        {
            if (count <= 0 || startPageNo < 0)
                throw new ArgumentException();
            int st = -1;
            int ct = 0;
            for (int i = 0; i < count; i++) {
                int p = startPageNo + i;
                bool r = false;
                if (!mi.pageMap[p]) {
                    if (st < 0) {
                        st = p;
                        ct = 1;
                    } else
                        ct++;
                }

                if (i == count - 1 && ct > 0)
                    r = true;

                if (r) {
                    System.Diagnostics.Debug.Print("req pages {0}..{1}", st, st + ct - 1);
                    byte[] pg = ReadFlashPages(mtr, nt, st, ct);
                    Array.Copy(pg, 0, mi.flash, st * Logika4L.FLASH_PAGE_SIZE, ct * Logika4L.FLASH_PAGE_SIZE);
                    for (int f = 0; f < ct; f++)
                        mi.pageMap[st + f] = true;
                }
            }
        }
        internal int get4LRealAddr(MeterInstance mi, DataTag t)
        {
            TagDef4L def = t.def as TagDef4L;
            if (mi.mtr == Meter.SPG741 && def.Ordinal >= 200 && def.Ordinal < 300)  //специальные параметры СПГ741, переезжающие в зависимости от СП
                return TSPG741.GetMappedDBParamAddr(def.Key, get741sp(mi.nt));
            else                                                                 //все остальные параметры
                return def.address.Value + (def.channelOffset.HasValue && t.Channel.No == 2 ? def.channelOffset.Value : 0);
        }

        void update4LTagsValues(byte? nt, DataTag[] tags, MeterInstance mi, updTagsFlags flags)
        {
            Logika4L mtr = tags[0].def.Meter as Logika4L;
            
            for (int i = 0; i < tags.Length; i++) {
                DataTag t = tags[i];

                TagDef4L def = t.def as TagDef4L;
                t.EU = def.Units;

                int addr = get4LRealAddr(mi, t);

                int stp = addr / Logika4L.FLASH_PAGE_SIZE;
                //int enp = (addr + Logika4L.SizeOf(def.internalType) - 1) / Logika4L.FLASH_PAGE_SIZE;

                if (def.inRAM) {    //RAM vars
                    //todo: maybe performance optimization, coalesce RAM regions
                    byte[] rbuf = ReadRAM(mtr, nt, addr, Logika4L.SizeOf(def.internalType));
                    t.Value = Logika4L.GetValue(def.internalType, rbuf, 0);

                } else {    //flash (or flash + ram) vars
                    int pfCnt = stp < mi.pageMap.Length - 1 ? stp % 2 : 0;  //some naive read-ahead, to reduce request count                                        
                    getFlashPagesToCache(mtr, nt, stp, 1 + pfCnt, mi);

                    t.Value = Logika4L.GetValue(def.internalType, mi.flash, addr, out t.Oper);

                    if (def.addonAddress.HasValue) { //тотальные счетчики из двух частей (i32r32 во Flash + r32 в RAM)
                        int raddr = def.addonAddress.Value + (def.addonChannelOffset.HasValue && t.Channel.No == 2 ? def.addonChannelOffset.Value : 0);
                        byte[] rbuf = ReadRAM(mtr, nt, raddr, Logika4L.SizeOf(Logika4L.BinaryType.r32));
                        float ramFloatAddon = Logika4L.GetMFloat(rbuf, 0);
                        t.Value = (double)t.Value + ramFloatAddon;
                    }
                }
                if (!flags.HasFlag(updTagsFlags.DontGetEUs))
                    t.EU = Logika4.getEU(mi.EUDict, def.Units);

                //if (flags.HasFlag(updTagsFlags.UseLocalTime))
                t.TimeStamp = DateTime.Now;
                //else
                //    t.TimeStamp = mi.CurrentDeviceTime;

                postProcessValue(t);
            }
        }

        public void InvalidateFlashCache4L(byte? nt, DataTag[] tags)
        {
            MeterInstance mmd = getMeterInstance(tags[0].def.Meter as Logika4, nt);
            for (int i = 0; i < tags.Length; i++) {
                DataTag t = tags[i];
                TagDef4L def = t.def as TagDef4L;

                int addr = get4LRealAddr(mmd, t);                     
                int stp = addr / Logika4L.FLASH_PAGE_SIZE;
                int enp = (addr + Logika4L.SizeOf(def.internalType) - 1) / Logika4L.FLASH_PAGE_SIZE;
                for (int p = stp; p <= enp; p++)
                    mmd.pageMap[p] = false;
            }
        }

        void postProcessValue(DataTag t)    //место для всяческих хаков
        {
            if (t.def.Meter == Meter.SPT941_10 && t.Name.ToLower()=="model" && t.Value != null && ((string)t.Value).Length == 1)
                t.Value = "1" + t.Value.ToString();    //СПТ941.10/11 хранит код модели последней цифрой 
        }

        void updateTags4M(byte? nt, DataTag[] tags, MeterInstance mi, updTagsFlags flags)
        {
            Logika4M mtr = tags[0].def.Meter as Logika4M;

            List<int> chs = new List<int>(MAX_TAGS_AT_ONCE);
            List<int> ords = new List<int>(MAX_TAGS_AT_ONCE);

            int blkSt = 0;
            foreach (DataTag t in tags) {
                TagDef4M td = t.def as TagDef4M;
                chs.Add(t.Channel.No);
                ords.Add(t.Ordinal);

                if (ords.Count == MAX_TAGS_AT_ONCE || t == tags.Last()) {
                    object[] va = readTagsM4(mtr, nt, chs.ToArray(), ords.ToArray(), out bool[] opFlags);
                    for (int z = 0; z < ords.Count; z++) {
                        DataTag vt = tags[blkSt + z];
                        vt.Value = va[z];
                        if (vt.Value == null)
                            vt.ErrorDesc = Logika4M.ND_STR;
                        if (!flags.HasFlag(updTagsFlags.DontGetEUs))
                            vt.EU = Logika4.getEU(mi.EUDict, (vt.def as TagDef4M).Units);
                        vt.Oper = opFlags[z];
                        //if (flags.HasFlag(updTagsFlags.UseLocalTime))
                        vt.TimeStamp = DateTime.Now;
                        //else
                        //    vt.TimeStamp = mi.CurrentDeviceTime;   
                    }

                    blkSt += ords.Count;
                    chs.Clear();
                    ords.Clear();
                }
            }
        }
        class Logika4MArchiveRequestState
        {
            internal DateTime tPtr;
            internal ArchiveDef4M arDef;
            internal ArchiveFieldDef4M[] fieldDefs;
        }

        public override IntervalArchive ReadIntervalArchiveDef(Meter m, byte? srcNt, byte? dstNt, ArchiveType arType, out object state)
        {
            Logika4 mtr4 = m as Logika4;
            if (!arType.IsIntervalArchive)
                throw new ArgumentException("wrong archive type");

            IntervalArchive ar = new IntervalArchive(m, arType);

            MeterInstance mi = getMeterInstance(mtr4, dstNt);

            ArchiveDef ard;
            if (m == Meter.SPT942) {
                bool tiny42 = mi.Model == "4" || mi.Model == "6";                                
                ard = m.Archives.Single(x => x.ArchiveType == arType && ((ArchiveDef4L)x).poorMans942==tiny42);
            } else
                ard = m.Archives.Single(x => x.ArchiveType == arType);
            ArchiveFieldDef4[] fieldDefs = m.ArchiveFields.Where(x => x.ArchiveType == arType).Cast<ArchiveFieldDef4>().ToArray();    //по одному вводу

            int chStart = ard.ChannelDef.Start;   
            int chEnd = chStart + ard.ChannelDef.Count - 1;

            for (int ch = chStart; ch <= chEnd; ch++) {
                foreach (var fd in fieldDefs) {
                    ArchiveField af = new ArchiveField(fd, ch);
                    af.EU = Logika4.getEU(mi.EUDict, fd.Units);
                    string fldName = fd.Name;
                    if (ard.ChannelDef.Kind == ChannelKind.TV)
                        fldName = string.Format("{0}{1}_{2}", ard.ChannelDef.Prefix, ch, fd.Name);

                    DataColumn dc = ar.Table.Columns.Add(fldName, fd.ElementType);
                    dc.ExtendedProperties[Archive.FLD_EXTPROP_KEY] = af;
                }
            }

            state = null;
            if (m is Logika4L) {
                Logika4LTVReadState[] ars = new Logika4LTVReadState[ard.ChannelDef.Count];
                for (int i = 0; i < ard.ChannelDef.Count; i++) {
                    ars[i] = new Logika4LTVReadState();
                    ars[i].headersRead = false;
                    ars[i].idx = -1;
                    ars[i].fArchive = new SyncFlashArchive4(mi, ard as ArchiveDef4L, ard.ChannelDef.Start + i, mi);
                }
                Logika4LArchiveRequestState rs = new Logika4LArchiveRequestState(ars);
                state = rs;

            } else {
                var rs = new Logika4MArchiveRequestState();
                rs.arDef = ard as ArchiveDef4M;
                rs.fieldDefs = fieldDefs.Cast<ArchiveFieldDef4M>().ToArray();
                state = rs;
            }

            return ar;
        }

        public override bool ReadIntervalArchive(Meter m, byte? srcNt, byte? nt, IntervalArchive ar, DateTime start, DateTime end, ref object state, out float progress)
        {
            if (m is Logika4L)
                return readFlashArchive4L(m as Logika4L, nt, ar, start, end, ref state, out progress);

            else if (m is Logika4M)
                return readIntervalArchive4M(m as Logika4M, nt, ar, start, end, ref state, out progress);

            else
                throw new ArgumentException("wrong meter type");
        }

        private bool readFlashArchive4L(Logika4L m, byte? nt, Archive ar, DateTime start, DateTime end, ref object stateObj, out float progress)
        {
            Logika4LArchiveRequestState state = (Logika4LArchiveRequestState)stateObj;

            int PCT_HEADERS;   //percentage of headers to data (progress calc)            
            int PCT_DATA;

            if (ar.ArchiveType.IsIntervalArchive) {
                PCT_HEADERS = 10 / state.ars.Length;
                PCT_DATA = 100 / state.ars.Length - PCT_HEADERS;
            } else {
                if (stateObj == null)
                    stateObj = state = init4LServiceArchiveReadState(m, nt, ar.ArchiveType);
                PCT_HEADERS = 100 / state.ars.Length;
                PCT_DATA = 0;
            }

            for (int i = 0; i < state.ars.Length; i++) {
                Logika4LTVReadState trs = state.ars[i];
                FlashArchive4 fa = trs.fArchive;
                if (trs.idx < 0)
                    fa.headers.ManageOutdatedElements(true, out int[] new_headers, out trs.idx);   //just read current index ptr                                                                                                    //a.InvalidateData(new_headers);    //не требуется, тк кешированные данные не используются повторно, в отличие от OPC сервера

                double pctHdrRead = 0;
                if (!trs.headersRead) {

                    if (fa.headers.GetElementIndexesInRange(start, end, trs.idx, ref trs.restartPoint, trs.indexes, out pctHdrRead)) {
                        trs.headersRead = true;

                        trs.dirtyIndexes = new List<FRBIndex>(trs.indexes);
                        trs.dirtyIndexes.Sort(FRBIndex.compareByIdx);
                        trs.dirtyIndexesInitialCount = trs.dirtyIndexes.Count;

                    } else {
                        progress = (float)(i * (PCT_HEADERS + PCT_DATA) + (pctHdrRead * PCT_HEADERS / 100.0));
                        //System.Diagnostics.Debug.Print("ch {0} reading headers {1:0}%, total progress {2:0}%", i, pctHdrRead, progress);
                        return true;   //headers not read completely yet
                    }
                }

                fa.UpdateData(trs.dirtyIndexes);

                if (trs.dirtyIndexes.Count > 0) {    //data update not completed yet
                    if (trs.dirtyIndexesInitialCount > 0) {
                        double pctDataRead = 100.0 * (trs.dirtyIndexesInitialCount - trs.dirtyIndexes.Count) / trs.dirtyIndexesInitialCount;
                        progress = (float)(i * (PCT_HEADERS + PCT_DATA) + PCT_HEADERS + PCT_DATA * pctDataRead / 100.0);
                        //System.Diagnostics.Debug.Print("ch {0} updating data {1:0}%, total progress {2:0}%", i, pctDataRead, progress);
                    } else
                        progress = 0;

                    return true;  //read not completed
                }
            }
            //data update for all TVs completed                
            progress = 100;

            if (ar.ArchiveType.IsIntervalArchive)
                processIntervalData4L(state, ar as IntervalArchive);
            else
                processServiceArchiveData4L(state, ar as ServiceArchive);
            
            return false;   //no more data
        }

        private void processIntervalData4L(Logika4LArchiveRequestState state, IntervalArchive ar)
        {
            ar.Table.Rows.Clear();
            for (int tv = 0; tv < state.ars.Length; tv++) {
                Logika4LTVReadState trs = state.ars[tv];

                for (int i = 0; i < trs.indexes.Count; i++) {
                    VQT hdp = trs.fArchive.GetDataPoint(trs.indexes[i].idx);

                    DataRow row = ar.Table.Rows.Find(hdp.Timestamp);    //locate by PK
                    if (i == 0)
                        continue;       //record with non-unique timestamp (due to corrupt headers)

                    object[] oa;
                    object[] fields = hdp.Value as object[];
                    if (row == null) {
                        oa = new object[1 + fields.Length * state.ars.Length];
                        oa[0] = hdp.Timestamp;
                        Array.Copy(fields, 0, oa, 1 + tv * fields.Length, fields.Length);
                        ar.Table.Rows.Add(oa);
                    } else {
                        oa = row.ItemArray;
                        Array.Copy(fields, 0, oa, 1 + tv * fields.Length, fields.Length);
                        row.ItemArray = oa;
                    }
                }
            }
        }

        private void processServiceArchiveData4L(Logika4LArchiveRequestState state, ServiceArchive svcArchive)
        {
            svcArchive.Records.Clear();         

            for (int tv = 0; tv < state.ars.Length; tv++) {
                Logika4LTVReadState trs = state.ars[tv];

                for (int ch = 0; ch < trs.indexes.Count; ch++) {
                    VQT hdp = trs.fArchive.GetDataPoint(trs.indexes[ch].idx);
                    if (hdp != null) {
                        string evt = hdp.Value.ToString();
                        string desc = null;
                        if (trs.fArchive.ArchiveType == ArchiveType.ErrorsLog)
                            desc = (svcArchive.Meter as Logika4).GetNSDescription(evt);

                        if (state.ars.Length>1)     //приборы c двумя ТВ 
                            evt = (tv+1).ToString() + "-" + evt;

                        ServiceRecord sr = new ServiceRecord() { tm = hdp.Timestamp, @event = evt, description = desc };
                        svcArchive.Records.Add(sr);
                    }
                }
            }
        }

        private Logika4LArchiveRequestState init4LServiceArchiveReadState(Logika4L m, byte? nt, ArchiveType arType)
        {
            MeterInstance mi = getMeterInstance(m, nt);
            ArchiveDef ard = (ArchiveDef)m.Archives.Single(x => x.ArchiveType == arType);
            Logika4LTVReadState[] tvsa = new Logika4LTVReadState[ard.ChannelDef.Count];

            GetObjectDelegate recordGetter = null;
            if (arType == ArchiveType.ErrorsLog)
                recordGetter = new GetObjectDelegate((_ar, b, o) => Logika4L.GetValue(Logika4L.BinaryType.NSrecord, b, o));
            else if (arType == ArchiveType.ParamsLog)
                recordGetter = new GetObjectDelegate((_ar, b, o) => Logika4L.GetValue(Logika4L.BinaryType.IZMrecord, b, o));

            for (int i = 0; i < ard.ChannelDef.Count; i++) {
                tvsa[i] = new Logika4LTVReadState();
                tvsa[i].fArchive = new AsyncFlashArchive4(mi, ard as ArchiveDef4L, ard.ChannelDef.Start + i, recordGetter);
                tvsa[i].headersRead = false;
                tvsa[i].idx = -1;                
            }

            return new Logika4LArchiveRequestState(tvsa);            
        }

        bool readIntervalArchive4M(Logika4M m, byte? nt, IntervalArchive ar, DateTime start, DateTime end, ref object state, out float progress)
        {
            MeterInstance mtd = getMeterInstance(m as Logika4, nt);
            Logika4MArchiveRequestState rs = (Logika4MArchiveRequestState)state;

            int chStart = rs.arDef.ChannelDef.Start;
            int chEnd = rs.arDef.ChannelDef.Start + rs.arDef.ChannelDef.Count - 1;

            DateTime[] nextPtrs = new DateTime[rs.arDef.ChannelDef.Count];
            DateTime tStart = rs.tPtr != DateTime.MinValue ? rs.tPtr : start;

            M4ArchiveId archiveCode;
            if (ar.ArchiveType == ArchiveType.Hour)
                archiveCode = M4ArchiveId.Hour;
            else if (ar.ArchiveType == ArchiveType.Day)
                archiveCode = M4ArchiveId.Day;
            else if (ar.ArchiveType == ArchiveType.Month)
                archiveCode = M4ArchiveId.Mon;
            else if (ar.ArchiveType == ArchiveType.Control)
                archiveCode = M4ArchiveId.Ctrl;
            else
                throw new Exception("unsupported archive type");

            for (int ch = chStart; ch <= chEnd; ch++) {

                this.readArchiveM4(m, nt, 0, ArchivePartition, (byte)ch, archiveCode, tStart, end, 64, out M4ArchiveRecord[] data, out DateTime nextPtr);

                foreach (M4ArchiveRecord r in data) {
                    if (r.dt == DateTime.MinValue) {  //отсутствует полная метка времени - необходимо прибавить к отметке интервала расчетные день и час
                        if (ar.ArchiveType == ArchiveType.Hour)
                            r.dt = r.intervalMark;
                        else if (ar.ArchiveType == ArchiveType.Day || ar.ArchiveType == ArchiveType.Control)
                            r.dt = r.intervalMark.AddHours(mtd.RH);
                        else if (ar.ArchiveType == ArchiveType.Month)
                            r.dt = r.intervalMark.AddDays(mtd.RD - 1).AddHours(mtd.RH);
                        else
                            throw new Exception("unsupported archive");
                    }

                    DataRow row = ar.Table.Rows.Find(r.dt);
                    if (row == null)
                        row = ar.Table.Rows.Add(r.dt);
                    object[] oa = row.ItemArray;

                    int idst = 1 + rs.fieldDefs.Length * (ch - chStart);
                    Array.Copy(r.values, 0, oa, idst, r.values.Length);

                    row.ItemArray = oa;
                }
                nextPtrs[ch - chStart] = nextPtr;
            }

            DateTime tPtr = DateTime.MinValue;
            foreach (DateTime np in nextPtrs) {
                if (np != DateTime.MinValue && np > tPtr)
                    tPtr = np;
            }

            DateTime firstRecTime = ar.Table.Rows.Count > 0 ? (DateTime)ar.Table.Rows[0][0] : start;
            progress = (float)Math.Min(100, tPtr == DateTime.MinValue ? 100 : (tPtr - firstRecTime).TotalSeconds * 100 / (end - firstRecTime).TotalSeconds);

            bool moreData = tPtr != DateTime.MinValue && tPtr < end;
            rs.tPtr = tPtr;

            return moreData;
        }

        public override bool ReadServiceArchive(Meter m, byte? srcNt, byte? nt, ServiceArchive ar, DateTime start, DateTime end, ref object state, out float progress)
        {
            if (!ar.ArchiveType.IsServiceArchive)
                throw new ArgumentException("wrong archive type");

            if (m is Logika4M)
                return readServiceArchive4M(m as Logika4M, nt, ar, start, end, ref state, out progress);

            else if (m is Logika4L)
                return readFlashArchive4L(m as Logika4L, nt, ar, start, end, ref state, out progress);

            else
                throw new ArgumentException("wrong meter type");
        }

        bool readServiceArchive4M(Logika4M m, byte? nt, ServiceArchive ar, DateTime start, DateTime end, ref object state, out float progress)
        {
            M4ArchiveId archiveCode;
            if (ar.ArchiveType == ArchiveType.ParamsLog)
                archiveCode = M4ArchiveId.ParamsLog;
            else if (ar.ArchiveType == ArchiveType.ErrorsLog)
                archiveCode = M4ArchiveId.NSLog;
            else
                throw new Exception("unsupported archive type");

            Logika4M m4m = m as Logika4M;
            ArchiveDef4M ard = (ArchiveDef4M)m.Archives.Single(x => x.ArchiveType == ar.ArchiveType);
            int chStart = ard.ChannelDef.Start;
            int chEnd = ard.ChannelDef.Start + ard.ChannelDef.Count - 1;

            DateTime tPtr = state != null ? (DateTime)state : DateTime.MinValue;

            DateTime[] nextPtrs = new DateTime[ard.ChannelDef.Count];
            DateTime tStart = tPtr != DateTime.MinValue ? tPtr : start;
            List<ServiceRecord> tmpList = new List<ServiceRecord>();

            for (int ch = chStart; ch <= chEnd; ch++) {

                this.readArchiveM4(m4m, nt, 0, ArchivePartition, (byte)ch, archiveCode, tStart, end, 64, out M4ArchiveRecord[] data, out DateTime nextPtr);

                foreach (M4ArchiveRecord r in data) {
                    ServiceRecord evt = archiveRecToServiceRec(m4m, ar.ArchiveType, ch, r);
                    tmpList.Add(evt);
                }
                nextPtrs[ch - chStart] = nextPtr;
            }

            tPtr = DateTime.MinValue;
            foreach (DateTime np in nextPtrs) {
                if (np != DateTime.MinValue && np > tPtr)
                    tPtr = np;
            }

            ar.Records.AddRange(tmpList);

            DateTime firstRecTime = ar.Records.Count > 0 ? ar.Records[0].tm : start;

            state = tPtr;
            if (tPtr == DateTime.MinValue)
                progress = 100;
            else
                progress = (float)((tPtr - firstRecTime).TotalSeconds * 100 / (end - firstRecTime).TotalSeconds);

            return tPtr != DateTime.MinValue && tPtr < end;
        }

        public static ServiceRecord archiveRecToServiceRec(Logika4M mtr, ArchiveType at, int channel, M4ArchiveRecord aRec)
        {
            string sEvent = Convert.ToString(aRec.values[0]);
            string eventDesc = null;
            if (at == ArchiveType.ErrorsLog)
                eventDesc = mtr.GetNSDescription(sEvent);

            if (channel > 0)
                sEvent = channel + "-" + sEvent;

            return new ServiceRecord() { tm = aRec.dt, @event = sEvent, description = eventDesc };
        }

        public override DateTime GetDeviceClock(Meter meter, byte? src, byte? dst)
        {
            MeterInstance mtd = getMeterInstance(meter as Logika4, dst);
            return mtd.CurrentDeviceTime;
        }
    }
}
