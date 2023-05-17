using Logika.Meters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.M4
{
    #region архивы x4x - общая часть

    internal class Logika4LTVReadState
    {
        internal int idx = -1;
        internal int restartPoint = -1;

        internal List<FRBIndex> indexes = new List<FRBIndex>();
        internal bool headersRead = false;

        internal List<FRBIndex> dirtyIndexes;
        internal int dirtyIndexesInitialCount;
        internal FlashArchive4 fArchive;
    }

    internal class Logika4LArchiveRequestState
    {

        internal readonly Logika4LTVReadState[] ars; //one per TV
        internal Logika4LArchiveRequestState(Logika4LTVReadState[] ars)
        {
            this.ars = ars;
        }
    }

    internal delegate object GetObjectDelegate(FlashArchive4 archive, byte[] buffer, int offset);

    internal abstract class FlashArchive4
    {
        public readonly M4Protocol.MeterInstance mi;

        protected readonly ArchiveDef4L def;
        internal readonly FlashRingBuffer headers;
        
        public Logika4L Meter { get { return mi.mtr as Logika4L; } }
        public ArchiveType ArchiveType { get { return def.ArchiveType; } }

        public void Reset()
        {
            headers.Reset();
        }
        public FlashArchive4(M4Protocol.MeterInstance mi, ArchiveDef4L arDef, int channelNo, int elementSize, GetObjectDelegate HeaderTimeGetter, GetObjectDelegate HeaderValueGetter)
        {
            this.mi = mi;
            def = arDef;
            int idxAddr = channelNo == 2 ? def.IndexAddr2.Value : def.IndexAddr;
            int dataAddr;
            if (arDef.ArchiveType.IsIntervalArchive)
                dataAddr = channelNo == 2 ? def.HeadersAddr2.Value : def.HeadersAddr.Value;
            else
                dataAddr = channelNo == 2 ? def.RecordsAddr2.Value : def.RecordsAddr;

            headers = new FlashRingBuffer(this, idxAddr, dataAddr, def.Capacity, elementSize, HeaderTimeGetter, HeaderValueGetter);
        }
        
        internal virtual VQT GetDataPoint(int index)
        {
            DateTime? nts = headers.Times[index];
            if (!nts.HasValue) //empty / erased header
                return null;

            VQT hdp = new VQT() { Quality = 0, Timestamp = nts.Value };

            if (headers.Values != null) //ValueGetter supplied
                hdp.Value = headers.Values[index];

            return hdp;
        }
        
        internal virtual void InvalidateData(int[] outdated_indexes) { }

        /// <summary>
        /// updates data for given indexes, updated indexes removed from 'indexesToRead'
        /// </summary>
        /// <param name="indexesToRead"></param>
        /// <returns> list of indexes which are updated yet</returns>
        internal abstract void UpdateData(List<FRBIndex> indexesToRead);
    }
    #endregion

    #region синхронные (интервальные) архивы
    internal sealed class SyncFlashArchive4 : FlashArchive4
    {
        FlashArray data;
        byte RD;
        byte RH;
        ArchiveFieldDef4L[] fields;

        static object getHeaderTime(FlashArchive4 fa, byte[] buffer, int offset) {
            var sfa = (SyncFlashArchive4)fa;
            return Logika4L.syncHeaderToDatetime(sfa.ArchiveType, sfa.RD, sfa.RH, buffer, offset);
        }

        public SyncFlashArchive4(M4Protocol.MeterInstance mi, ArchiveDef4L arDef, int channelNo, M4Protocol.MeterInstance mtrInfo)
            : base(mi, arDef, channelNo, 4, getHeaderTime, null)
        {  
            data = new FlashArray(mi, channelNo==2 ? def.RecordsAddr2.Value : def.RecordsAddr, def.Capacity, def.RecordSize);
            this.RD = Convert.ToByte(mi.RD);
            this.RH = Convert.ToByte(mi.RH);
            
            fields = mi.mtr.ArchiveFields.Where(x => x.ArchiveType == arDef.ArchiveType).Cast<ArchiveFieldDef4L>().ToArray();
        }

        internal override void UpdateData(List<FRBIndex> indexes)
        {
            data.UpdateElements(indexes);
        }

        internal override void InvalidateData(int[] outdated_indexes)
        {
            foreach (int i in outdated_indexes)
                data.InvalidateElement(i);
        }

        internal override VQT GetDataPoint(int index)
        {
            VQT nhdp = base.GetDataPoint(index);
            if (nhdp != null) {
                byte[] buf;
                int offset;
                data.GetElement(index, out buf, out offset);

                object[] varArray = new object[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                    varArray[i] = Logika4L.GetValue(fields[i].InternalType, buf, offset + fields[i].FieldOffset);

                //часовая запись старого 941 урезанная и требует особого подхода
                //if (this.Counter.Meter == LogikaMeter.SPT941 && item.ArchiveType == OpcArchiveType.hour)
                //    nhdp.Value = SPT941.ExpandHourRecord(varArray);
                //else

                nhdp.Value = varArray;
            }
            return nhdp;
        }
    }
#endregion

#region asynchronous archives

    internal class AsyncFlashArchive4 : FlashArchive4
    {
        /// <returns> 
        /// null - if record is empty (erased) 
        /// DateTime.MinValue if record header is corrupted (cannot be converted to valid time)
        /// DateTime if record header ok
        /// </returns>
        internal static object getAsyncRecordTime(FlashArchive4 archive, byte[] buffer, int offset)
        {
            return Logika4L.GetValue(Logika4L.BinaryType.svcRecordTimestamp, buffer, offset);
        }

        internal AsyncFlashArchive4(M4Protocol.MeterInstance mi, ArchiveDef4L arDef, int channelNo, GetObjectDelegate ValueGetter)
        : base(mi, arDef, channelNo, arDef.RecordSize, getAsyncRecordTime, ValueGetter)
        {
        }

        internal override void UpdateData(List<FRBIndex> indexes)
        {
            //async archives have no data bound to headers - so indicate that we don't need to update anymore
            indexes.Clear();
        }
    }

#endregion
}
