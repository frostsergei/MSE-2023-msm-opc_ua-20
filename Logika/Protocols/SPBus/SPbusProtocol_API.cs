using Logika.Comms.Connections;
using Logika.Meters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.SPBus
{
    public partial class SPBusProtocol
    {
        public const int MAX_READ_TAGS_AT_ONCE = 24;

        public override void UpdateTags(byte? src, byte? dst, DataTag[] tags)
        {
            string pktId = null;

            List<DataTag> stdTags = new List<DataTag>();
            List<DataTag> idxTags = new List<DataTag>();

            foreach (DataTag t in tags) {
                if (t.Index.HasValue)
                    idxTags.Add(t);
                else
                    stdTags.Add(t);
            }

            int nReadTags = 0;

            if (stdTags.Count > 0) {
                List<DataTag[]> stdTagsReqs = new List<DataTag[]>();

                List<DataTag> at = null;
                foreach (DataTag t in stdTags) {
                    if (at == null)
                        at = new List<DataTag>();
                    at.Add(t);
                    if (at.Count == MAX_READ_TAGS_AT_ONCE || t == stdTags.Last()) {
                        stdTagsReqs.Add(at.ToArray());
                        at = null;
                    }
                }

                for (int r = 0; r < stdTagsReqs.Count; r++) {
                    DataTag[] vta = stdTagsReqs[r];

                    int[] channels = vta.Select(x => x.Channel.No).ToArray();
                    int[] ordinals = vta.Select(x => x.Ordinal).ToArray();

                    readTags(src, dst, pktId, channels, ordinals, out string[] values, out string[] eus);
                    for (int z = 0; z < vta.Length; z++) {
                        vta[z].TimeStamp = DateTime.Now;
                        vta[z].EU = eus[z];
                        if (values[z].EndsWith("?")) {
                            vta[z].Value = null;
                            vta[z].ErrorDesc = values[z];
                        } else
                            vta[z].Value = values[z];
                    }
                    nReadTags += ordinals.Length;
                }
            }

            if (idxTags.Count > 0) {
                idxTags.Sort(vTagIdxSortComparer);

                List<DataTag[]> idxTagsReqs = new List<DataTag[]>();

                List<DataTag> at = new List<DataTag>();
                foreach (DataTag t in idxTags) {
                    bool tagContinuesSequence = at.Count > 0 && at.Last().Channel.No == t.Channel.No && at.Last().Ordinal == t.Ordinal && at.Last().Index.Value == t.Index.Value - 1;
                    if (!tagContinuesSequence && at.Count > 0) {
                        idxTagsReqs.Add(at.ToArray());
                        at = new List<DataTag>();
                    }
                    at.Add(t);
                    if (at.Count == MAX_READ_TAGS_AT_ONCE || t == idxTags.Last()) {
                        idxTagsReqs.Add(at.ToArray());
                        at = new List<DataTag>();
                    }
                }

                for (int r = 0; r < idxTagsReqs.Count; r++) {
                    DataTag[] vta = idxTagsReqs[r];

                    int channel = vta[0].Channel.No;
                    int ordinal = vta[0].Ordinal;
                    int startIdx = vta[0].Index.Value;
                    readIndexTags(src, dst, pktId, channel, ordinal, startIdx, vta.Length, out string[] values, out string[] eus);

                    for (int z = 0; z < vta.Length; z++) {
                        vta[z].TimeStamp = DateTime.Now;
                        if (!values[z].EndsWith("?")) {
                            vta[z].Value = values[z];
                            vta[z].EU = eus[z];
                        } else {
                            vta[z].Value = null;
                        }
                    }

                    nReadTags += vta.Length;
                }
            }
        }

        public override IntervalArchive ReadIntervalArchiveDef(Meter m, byte? src, byte? dst, ArchiveType arType, out object state)
        {
            ArchiveDef arDef = m.Archives.SingleOrDefault(x => x.ArchiveType == arType);

            IntervalArchive ar = new IntervalArchive(m, arType);

            List<int> ords = new List<int>();
            if (arDef is ArchiveDef6)
                ords.Add(((ArchiveDef6)arDef).Ordinal);
            else if (arDef is MultipartArchiveDef6)
                ords.AddRange(((MultipartArchiveDef6)arDef).Ordinals);

            List<ArchiveDescriptorElement> headerFields = new List<ArchiveDescriptorElement>();
            foreach(int ord in ords)
                headerFields.AddRange(readRecordsDescriptor(src, dst, null, ord));
            
            //ArchiveField[] columns = new ArchiveField[headerFields.Count];

            ArchiveFieldDef6[] refATags = m.ArchiveFields.Cast<ArchiveFieldDef6>().ToArray();
            for (int i = 0; i < headerFields.Count; i++) {
                ArchiveDescriptorElement hf = headerFields[i];
                var rt = refATags.Single(x => x.ArchiveType == arType && x.Ordinal == hf.ordinal);
                ArchiveField fld = /*columns[i] = */new ArchiveField(rt, hf.channel);
                Logika6.SplitVarCaption(hf.name, out string caption, out string chType, out int chNo);  //преобразуем приборные имена переменных от вида "Wт01" к "W"                    
                fld.Caption = caption;
                fld.EU = hf.eu;
                fld.archiveOrd = hf.archiveOrd;
                DataColumn dc = ar.Table.Columns.Add(hf.name, rt.ElementType);
                ar.Fields[dc] = fld;
            }

            state = null;
            return ar;
        }

        public override bool ReadIntervalArchive(Meter m, byte? src, byte? dst, IntervalArchive ar, DateTime start, DateTime end, ref object state, out float progress)
        {
            if (start > end)
                throw new ArgumentException("параметр 'start' должен быть <= 'end'");
            //срез архива x6 может состоять из нескольких частей (пока только у СПЕ)
            //.Distinct() : "order of elements is preserved - however this is not documented behavior"
            int[] ords = ar.Fields.Where(x=>x!=null).Select(x => x.archiveOrd).Distinct().ToArray(); //ArchiveDef6 ard = (ArchiveDef6)m.Archives.Single(x => x.ArchiveType == ar.ArchiveType);

            ar.Table.BeginLoadData();

            DateTime tPtr = state == null ? end : (DateTime)state;

            DateTime recTime = DateTime.MinValue;
            DateTime prevPtr = DateTime.MinValue;

            object[] oa = null;
            for (int p = 0; p < ords.Length; p++) {     //multi-part archive processing (SPE)
                string[] record = readArchiveRecord(src, dst, null, ords[p], tPtr, out recTime, out DateTime pp);
                if (p == 0) {    //take only first part ptr
                    prevPtr = pp;
                    if (record == null) //отсутствие записи в одной секции среза должно означать, что в других тоже данных нет
                        break;
                    oa = new object[ar.Fields.Count];
                    oa[0] = recTime;
                }
                
                if (record != null) {
                    int pbi;    //index of part beginning
                    for (pbi = 1; pbi < ar.Fields.Count; pbi++) {
                        if (ar.Fields[pbi].archiveOrd == ords[p]) 
                            break;                       
                    }

                    for (int i = 0; i < record.Length; i++) {                       
                        Type destType = ar.Fields[pbi + i].def.ElementType; //columns[i].def.DataType;
                        string s = record[i];
                        object o = null;

                        if (s == null || s.EndsWith("?")) {
                            o = null;

                        } else if (destType == typeof(float) || destType == typeof(double)) {     //приборы x6 (внезапно) могут возвращать NaN, интерпретируем как отсутствие [вменяемых] данных
                            if (Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double cdv))    //o = Convert.ToDouble(s, CultureInfo.InvariantCulture);
                                o = cdv;

                        } else {
                            o = Convert.ChangeType(record[i], destType);
                        }
                        oa[pbi + i] = o;
                    }
                } 
            }
            
            if (oa!=null && recTime >= start && recTime <= end)
                ar.Table.Rows.Add(oa);

            tPtr = prevPtr;

            ar.Table.EndLoadData();

            state = tPtr;
            progress = (float)Math.Min(100, (end - tPtr).TotalSeconds * 100 / (end - start).TotalSeconds);

            return start <= tPtr;
        }
        
        //функция должна возвращать уже отсортированный по времени архив, с сохранением порядка следования записей с одинаковой секундой
        public override bool ReadServiceArchive(Meter m, byte? src, byte? dst, ServiceArchive ar, DateTime start, DateTime end, ref object state, out float progress)
        {
            ArchiveDef6 a = (ArchiveDef6)m.Archives.Single(x => x.ArchiveType == ar.ArchiveType);

            DateTime tPtr = state == null ? end : (DateTime)state;

            ArchiveRecord[] recs = readArchive(src, dst, "", a.ChannelDef.Start, a.Ordinal, start, tPtr, out bool maxPkt, out DateTime prev, out DateTime next);
            
            if (maxPkt && prev == DateTime.MinValue) {  //при максимальном размере пакета - подсказка по времени прибором не возвращается, вычисляем из записей. Также предпринимаем меры против пропуска записей на границе больших пакетов.
                //prev = records.Min(x => x.time).AddSeconds(-1);
                
                //порядок следования дат в ответном пакете прибора - обратный, от текущего времени в прошлое. т.е последняя запись из пакета при нормальной сортировке - первая в списке.
                DateTime lastPktRecTimestamp = recs[0].time;  //удаляем записи, у которых время совпадает с последней записью из пакета, т.к они могли поместиться в пакет не все. Следующее чтение начинаем с этой отметки времени.
                recs = recs.Where(x => x.time != lastPktRecTimestamp).ToArray();
                prev = lastPktRecTimestamp;
            }

            tPtr = prev;
            
            //foreach (var r in records) {
            //    if (r.time > start && r.time <= end) {  //'start' time points to last record stored in database
            //        ServiceRecord sr = new ServiceRecord() { tm = r.time, @event = r.value, description = r.eu };
            //        ar.Records.Add(sr);
            //    }
            //}
            var goodSRs = recs.Where(r => r.time > start && r.time <= end)   //'start' time points to last record stored in database
                .Select(r => new ServiceRecord() { tm = r.time, @event = r.value, description = r.eu });
            ar.Records.InsertRange(0, goodSRs);

            state = tPtr;
            progress = (float)Math.Min(100, (end - tPtr).TotalSeconds * 100 / (end - start).TotalSeconds);
            return start <= tPtr;
        }

        public override Meter GetMeterType(byte? srcNT, byte? dstNT, out object extraData)
        {
            readTags(srcNT, dstNT, "", new int[] { 0 }, new int[] { 99 }, out string[] va, out string[] eua);
            string p099 = va[0];
            Meter m = SPBusProtocol.MeterTypeFromResponse(p099, out string model);
            extraData = model;
            return m;
        }
    }
}
