using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    public abstract class Logika4M : Logika4
    {
        public const string ND_STR = "#н/д";

        public override bool SupportsFastSessionInit
        {
            get { return true; }
        }

        public abstract bool SupportsFLZ { get; }

        //поддержка т.н. "разделов", возникающих при пуске прибора
        public abstract bool SupportsArchivePartitions { get; }

        public override string FamilyName { get { return "M4"; } }

        protected override string tagsSort { get { return "Device, Channel, Ordinal"; } }
        protected override string archiveFieldsSort { get { return "Device, ArchiveType, Index"; } }

        //public abstract void getEUParamsAddresses(out int[] channels, out int[] ordinals);
        //public abstract string getEU(int[] euParams, string euValue);  //can be [P], [Q] etc. which should be translated using euParams

        public enum OperParamFlag : int
        {
            No = 0,
            Yes = 1,
        }

        public static int GetTagLength(byte[] buf, int idx, out int length)
        {
            int tl = buf[idx];
            if ((tl & 0x80) > 0) {
                tl &= 0x7F;
                if (tl == 1) {
                    length = buf[idx + 1];

                } else if (tl == 2) {
                    length = (buf[idx + 1] << 8) | buf[idx + 2];
                } else
                    throw new Exception("length field >1 byte");
                return tl + 1;
            } else {
                length = tl;
                return 1;
            }
        }

        public static int ParseTag(byte[] buf, int idx, out object v)
        {            
            byte tID = buf[idx];
            int tLen;

            int lenLen = GetTagLength(buf, idx + 1, out tLen);
            int iSt = idx + 1 + lenLen;

            switch (tID) {
                case 0x05:
                    v = null;
                    break;

                case 0x43: //float
                    v = BitConverter.ToSingle(buf, iSt);
                    break;

                case 0x41: //little endian unsigned integer
                    if (tLen == 1)
                        v = (UInt32)buf[iSt];
                    else if (tLen == 2)
                        v = (UInt32)BitConverter.ToUInt16(buf, iSt);
                    else if (tLen == 4)
                        v = BitConverter.ToUInt32(buf, iSt);
                    else
                        throw new Exception("неподдерживаемая длина тэга с типом 'uint'");
                    break;

                case 0x04: //byte array
                    v = new byte[tLen];
                    Array.Copy(buf, iSt, (byte[])v, 0, tLen);
                    break;

                case 0x16: //IA5String
                    v = Encoding.GetEncoding(1251).GetString(buf, iSt, tLen);
                    break;

                case 0x44:  //total (int32 + float) (should be returned as double)
                    v = (double)(BitConverter.ToInt32(buf, iSt)) + BitConverter.ToSingle(buf, iSt + 4);
                    break;

                case 0x45:  //флаг оперативности тэга (NB! bool не должен быть интерпретирован как значение тэга)
                    if (tLen > 0)
                        v = buf[iSt] > 0 ? OperParamFlag.Yes : OperParamFlag.No;
                    else
                        v = OperParamFlag.No;
                    break;

                case 0x46: //ACK
                    if (tLen == 1)
                        v = buf[iSt];   //???
                    else
                        v = null;
                    break;

                case 0x47:  //time (4 bytes: с/256 – с – мин – час)
                    v = string.Format("{0:D2}:{1:D2}:{2:D2}", buf[iSt + 3], buf[iSt + 2], buf[iSt + 1]);
                    break;

                case 0x48:  //date (4 bytes: день – месяц – год – день недели
                    v = string.Format("{0:D2}-{1:D2}-{2:D2}", buf[iSt], buf[iSt + 1], buf[iSt + 2]);
                    break;

                case 0x49:  //метка времени y m d h m s ms_l ms_h
                    //trailing fields can be absent, but datetime still should be valid
                    byte[] tv = new byte[8] { 0, 1, 1, 0, 0, 0, 0, 0 };    //should be initialized
                    //int tagLen = buf[idx + 1];
                    if (tLen > 0) {
                        for (int t = 0; t < Math.Min(tLen, tv.Length); t++)
                            tv[t] = buf[iSt + t];

                        int ms = (tv[7] << 8) | tv[6];
                        if (ms > 999) {
                            //Log(LogLevel.Warn, Facilities.Bus, this.NamePath, "миллисекунды в метке времени > 999");                            
                            throw new Exception("некорректное поле миллисекунд в метке времени архивной записи : " + ms);
                            //ms = 999;
                        }
                        v = new DateTime(2000 + tv[0], tv[1], tv[2], tv[3], tv[4], tv[5], ms, DateTimeKind.Local);
                    } else {
                        v = DateTime.MinValue;
                    }
                    break;

                case 0x4A: //номер параметра PNUM (CH ORDL ORDH), тэг может появляться только в запросах ()
                    v = new Tuple<byte, ushort>(buf[iSt], BitConverter.ToUInt16(buf, iSt + 1));
                    break;

                case 0x4B:      //битовая сборка (НС/ДС)
                    if (tLen <= 16) {                        
                        v = Logika4.BitNumbers(buf, iSt, tLen * 8);
                    } else
                        throw new Exception("FLAGS tag length unsupported");
                    break;

                case 0x55:  //ERR
                    v = buf[iSt];
                    break;
                default:
                    throw new Exception(string.Format("unknown tag type 0x{0:X2}", tID));
            }
            return 1 + lenLen + tLen;   //tag code + length field + payload
        }

        internal override DataTagDef readTagDef(DataRow r)
        {
            readCommonDef(r, out string chKey, out string name, out int ordinal, out TagKind kind, out bool isBasicParam, out int updRate, out Type dataType, out StdVar stv, out string desc, out string descriptionEx, out string range);

            ChannelDef ch = this.Channels.FirstOrDefault(x => x.Prefix == chKey);
            //byte? tagCh = r["TagChn"]==DBNull.Value ? null : (byte?)r["tagChn"];
                       
            string sDbType = r["dbType"] == DBNull.Value ? null : Convert.ToString(r["dbType"]);
            string units = Convert.ToString(r["Units"]);
            string displayFormat = Convert.ToString(r["DisplayFormat"]);
            
            return new TagDef4M(ch, name, stv, kind, isBasicParam, updRate, /*tagCh, */ordinal, desc, dataType, sDbType, units, displayFormat, descriptionEx, range);
        }

        protected override ArchiveDef[] readArchiveDefs(DataRow[] rows)
        {
            var d = new List<ArchiveDef>();
            foreach (DataRow r in rows) {
                string chKey = r["Channel"].ToString();
                ChannelDef ch = Channels.FirstOrDefault(x => x.Prefix == chKey);
                ArchiveType art = ArchiveType.FromString(r["ArchiveType"].ToString());
                string sRecType = "System." + r["RecordType"].ToString();
                Type recType = Type.GetType(sRecType, true);
                string name = r["Name"].ToString();
                string desc = r["Description"].ToString();
                int capacity = (int)r["capacity"];
                ArchiveDef4M ra = new ArchiveDef4M(ch, art, recType, capacity, name, desc);
                d.Add(ra);
            }
            return d.ToArray();
        }

        internal override ArchiveFieldDef readArchiveFieldDef(DataRow r)
        {
            ArchiveType art = ArchiveType.FromString(r["ArchiveType"].ToString());
            ArchiveDef ra = Archives.FirstOrDefault(x => x.ArchiveType == art);

            //string chKey = r["Channel"].ToString();
            //Channel ch = Channels.First(x => x.Name == chKey);

            int idx = (int)r["Index"];
            string sDataType = "System." + r["DataType"].ToString();
            Type t = Type.GetType(sDataType, true);

            string sDbType = Convert.ToString(r["DbType"]);
            string name = r["Name"].ToString();            
            string desc = r["Description"].ToString();

            object oStdType = r["VarT"];
            StdVar stv = (oStdType is string && !string.IsNullOrEmpty((string)oStdType)) ? (StdVar)Enum.Parse(typeof(StdVar), (string)oStdType) : StdVar.unknown;

            string units = r["Units"].ToString();
            string displayFormat = r["DisplayFormat"].ToString();

            return new ArchiveFieldDef4M(ra, idx, name, desc, stv, t, sDbType, displayFormat, units);
        }

        public class AdsTagBlock
        {
            public readonly int id;

            public readonly int[] chns;
            public readonly int[] ords;

            public AdsTagBlock(int id, int channel, int start, int count)
            {
                this.id = id;
                chns = new int[count];
                ords = new int[count];
                for (int i = 0; i < count; i++) {
                    chns[i] = channel;
                    ords[i] = start + i;
                }
            }

            public AdsTagBlock(int id, string[] tags)
            {
                this.id = id;
                chns = new int[tags.Length];
                ords = new int[tags.Length];
                for (int i = 0; i < tags.Length; i++) {
                    String s = tags[i];
                    if (s[1] == '.') {
                        chns[i] = int.Parse(s.Substring(0, 1));
                        ords[i] = int.Parse(s.Substring(2));
                    } else {
                        chns[i] = 0;
                        ords[i] = int.Parse(s);
                    }
                }
            }

        }
        public abstract AdsTagBlock[] getADSTagBlocks();
        public override bool SupportedByProlog4 => true;
    }
}
