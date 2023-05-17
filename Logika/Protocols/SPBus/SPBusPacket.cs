using System;
using System.Collections.Generic;
using System.Text;

namespace Logika.Comms.Protocols.SPBus
{
    public class SPBusPacket
    {
        public class ReadonlyFieldCollection
        {
            List<string> fList;
            public int FieldCount { get { return fList.Count; } }
            public ReadonlyFieldCollection(string[] fields)
            {
                fList = new List<string>(fields);
            }
            public ReadonlyFieldCollection Fields { get { return this; } }
            public string this[int i] { get { return fList[i]; } }

            public override string ToString()
            {
                if (fList == null)
                    return "<null>";
                StringBuilder sb = new StringBuilder();
                foreach (string s in fList)
                    sb.AppendFormat("'{0}', ", s);
                if (sb.Length > 0)
                    sb.Remove(sb.Length - 2, 2);
                return sb.ToString();
            }
            public string[] ToArray()
            {
                return fList.ToArray();
            }
        }

        public class ReadonlyRecordCollection
        {
            List<ReadonlyFieldCollection> rList = new List<ReadonlyFieldCollection>();
            public int Count { get { return rList.Count; } }
            public ReadonlyRecordCollection(List<string[]> records)
            {
                foreach (var r in records)
                    rList.Add(new ReadonlyFieldCollection(r));
            }
            public ReadonlyFieldCollection this[int i] { get { return rList[i]; } }
        }

        // структура пакета
        //10 01	[DAD] [SAD] 10 1F FUNC ...DATAHEAD... 10 02 ...DATA... 10 03 CRC1 CRC2

        public const byte DLE = 0x10;
        public int Length { get; private set; }      //length of whole packet (with 10 01 .. 10 03 CRC1 CRC2)                        

        public byte? DAD { get; private set; }  //destination address
        public byte? SAD { get; internal set; }  //source address
        public string DataHead { get; set; }
        public BusOperation Func { get; private set; }
        public string DataField { get; private set; }

        public ReadonlyRecordCollection Records { get; private set; }
        
        private SPBusPacket(int? sad, int? dad, string dataHead, BusOperation opCode)
        {
            if (sad.HasValue) {
                if (sad.Value < 0 || sad.Value > 0xFF)
                    throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "некорректный сетевой адрес отправителя: " + sad.Value.ToString());                
            }

            if (dad.HasValue) {
                if (dad.Value < 0 || dad.Value > 0xFF)
                    throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "некорректный сетевой адрес получателя: " + dad.Value.ToString());
            }
            this.DataHead = dataHead;
            this.SAD = (sad.HasValue && sad.Value >= 0) ? (byte?)sad.Value : null;
            this.DAD = (dad.HasValue && dad.Value >= 0) ? (byte?)dad.Value : null;

            this.Func = opCode;
        }

        //-------------------------------------------------------------------------------------
        [Flags]
        public enum ParseFlags
        {
            None,
            ExpectNoHeader, //пакет с отрезанным заголовком из файла АДС90
        }

        public static SPBusPacket Parse(byte[] buf, int start, ParseFlags flags = ParseFlags.None) //decode packet from received data
        {
            string ErrStr = "некорректная структура пакета СПсети";

            List<string[]> decodeResult = new List<string[]>();   //Records.Clear(); //clear decode result;

            byte? dad = null;
            byte? sad = null;
            byte fnc = 0xFF;
            string dataHead = null;

            int p = start;

            if (flags.HasFlag(ParseFlags.ExpectNoHeader) == false) {

                p = FindMarker(buf, p, buf.Length - p, 0x10, 0x01); //find header
                if (p < 0)
                    throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);

                int iHeader = p;
                p += 2;

                //if (*p==0x10 && *(p+1)==0x1F) {	//безадресный пакет
                if (buf[p] == 0x10 && buf[p + 1] == 0x1F) {
                    dad = null;
                    sad = null;
                } else {
                    dad = buf[p];
                    if (dad == 0x10) {
                        p++;
                        if (buf[p] != 0x10)
                            throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);
                    }
                    p++;
                    sad = buf[p];
                    if (sad == 0x10) {
                        p++;
                        if (buf[p] != 0x10)
                            throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);
                    }
                    p++;

                    if (buf[p] != 0x10 || buf[p + 1] != 0x1F)
                        throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);
                }

                p += 2;

                fnc = buf[p++];

                int iDataHeadStart = p;

                //p+=2;
                p = FindMarker(buf, p, buf.Length - p, 0x10, 0x02); //find data area start
                if (p == -1) //не найдено 10 02
                    throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);

                //дальнейший код не делает де-стаффинг DataHead и Data
                //(предполагается, что мы не посылаем символов 0x10 в этом поле запросов, а
                //приборы в поле данных 0x10 не употребляют)

                dataHead = new string(System.Text.Encoding.ASCII.GetChars(buf, iDataHeadStart, p - iDataHeadStart));
                p += 2; //skip 10 02
            }

            SPBusPacket pkt = new SPBusPacket(sad, dad, dataHead, (BusOperation)fnc);

            int DataStart = p;

            p = FindMarker(buf, p, buf.Length - p, 0x10, 0x03); //find end-of-packet (10 03)
            if (p == -1)
                throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);
            int DataEnd = p;

            pkt.DataField = new string(System.Text.Encoding.GetEncoding(866).GetChars(buf, DataStart, DataEnd - DataStart));
            pkt.Length = DataEnd + 4 - start;

            pkt.RawSource = new byte[pkt.Length];
            Array.Copy(buf, start, pkt.RawSource, 0, pkt.Length);

            //парсинг поля данных пакета
            //предполагаем что в поле данных символы 0x00 и 0x10 не встречаются

            pkt.DataField = pkt.DataField.Replace("\x0D\x0A", "\x0C"); //заменяем альтернативный разделитель записей на стандартный
                                                                       //удаляем последний разделитель
            if (pkt.DataField.Length == 0 || pkt.DataField[pkt.DataField.Length - 1] != 0x0C)
                throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);
            pkt.DataField = pkt.DataField.Remove(pkt.DataField.Length - 1, 1);

            string[] Lines = pkt.DataField.Split("\x0C".ToCharArray());
            if (Lines.Length == 0)
                throw new ECommException(ExcSeverity.Error, CommError.Unspecified, ErrStr);

            for (int i = 0; i < Lines.Length; i++) {
                string Line = Lines[i];

                if (Line.Length != 0 && Line[0] == '\t') //remove first HT, if any
                    Line = Line.Remove(0, 1);

                string[] recordFields = Line.Split("\t".ToCharArray());
                decodeResult.Add(recordFields);
            }

            pkt.Records = new ReadonlyRecordCollection(decodeResult);
            return pkt;
        }

        /// <summary>
        /// чтение списка тегов
        /// </summary>
        public static SPBusPacket BuildReadTagsPacket(int? src, int? dst, string packetId, int[] channels, int[] ordinals)
        {
            if (channels == null || ordinals == null || channels.Length == 0 || ordinals.Length == 0 || channels.Length != ordinals.Length)
                throw new ArgumentException("inconsistent parameters");

            SPBusPacket p = new SPBusPacket(src, dst, packetId, BusOperation.ReadParam);

            List<string[]> ra = new List<string[]>();
            for (int i = 0; i < ordinals.Length; i++) {
                if (channels[i] < 0 || ordinals[i] < 0)
                    throw new ArgumentException("incorrect channel/ordinal in parameters");

                ra.Add(new string[] { channels[i].ToString(), ordinals[i].ToString() });
            }

            p.Records = new ReadonlyRecordCollection(ra);
            return p;
        }

        /// <summary>
        /// запись тэга
        /// </summary>
        public static SPBusPacket BuildWriteTagPacket(int? src, int? dst, string packetId, int channel, int ordinal, int? index, string value)
        {
            bool indexed = index.HasValue && index.Value >= 0;

            List<string[]> ra = new List<string[]>();
            SPBusPacket p;
            if (indexed) {
                p = new SPBusPacket(src, dst, packetId, BusOperation.WriteIdxParam);
                ra.Add(new string[] { channel.ToString(), ordinal.ToString(), index.Value.ToString(), "1" });
            } else {
                p = new SPBusPacket(src, dst, packetId, BusOperation.WriteParam);
                ra.Add(new string[] { channel.ToString(), ordinal.ToString() });
            }
            ra.Add(new string[] { value });
            p.Records = new ReadonlyRecordCollection(ra);

            return p;
        }

        public static SPBusPacket BuildReadArrayPacket(int? src, int? dst, string packetId, int channel, int ordinal, int startIndex, int count)
        {
            SPBusPacket p = new SPBusPacket(src, dst, packetId, BusOperation.ReadIdxParam);

            List<string[]> ra = new List<string[]>();
            ra.Add(new string[] { channel.ToString(), ordinal.ToString(), startIndex.ToString(), count.ToString() });
            p.Records = new ReadonlyRecordCollection(ra);

            return p;
        }

        /// <summary>
        /// чтение архивного среза за указанное время
        /// </summary>
        public static SPBusPacket BuildReadArchiveRecordPacket(int? src, int? dst, string packetId, int archiveOrd, DateTime time)
        {
            SPBusPacket p = new SPBusPacket(src, dst, packetId, BusOperation.ReadTableRow);

            List<string[]> ra = new List<string[]>();
            ra.Add(new string[] { "0", archiveOrd.ToString() });
            ra.Add(new string[] { time.ToString("dd"), time.ToString("MM"), time.ToString("yyyy"), time.ToString("HH"), time.ToString("mm"), time.ToString("ss") });
            p.Records = new ReadonlyRecordCollection(ra);

            return p;
        }

        /// <summary>
        /// чтение оглавления архива
        /// </summary>
        public static SPBusPacket BuildReadArchiveDescriptorPacket(int? src, int? dst, string packetId, int archiveOrd)
        {
            SPBusPacket p = new SPBusPacket(src, dst, packetId, BusOperation.ReadTableDescriptor);

            List<string[]> ra = new List<string[]>();
            ra.Add(new string[] { "0", archiveOrd.ToString() });
            p.Records = new ReadonlyRecordCollection(ra);

            return p;
        }

        /// <summary>
        /// чтение архива за указанный интервал
        /// </summary>
        public static SPBusPacket BuildReadArchivePacket(int? src, int? dst, string packetId, int channel, int archiveOrd, DateTime startTime, DateTime endTime)
        {
            SPBusPacket p = new SPBusPacket(src, dst, packetId, BusOperation.ReadArchive);

            List<string[]> ra = new List<string[]>();
            ra.Add(new string[] { channel.ToString(), archiveOrd.ToString() });
            ra.Add(new string[] { endTime.ToString("dd"), endTime.ToString("MM"), endTime.ToString("yyyy"), endTime.ToString("HH"), endTime.ToString("mm"), endTime.ToString("ss") });
            ra.Add(new string[] { startTime.ToString("dd"), startTime.ToString("MM"), startTime.ToString("yyyy"), startTime.ToString("HH"), startTime.ToString("mm"), startTime.ToString("ss") });
            p.Records = new ReadonlyRecordCollection(ra);

            return p;
        }

        public static SPBusPacket BuildHangupModemPacket()
        {
            return new SPBusPacket(null, null, "", BusOperation.HangupModem);
        }


        List<byte> encoder = new List<byte>();

        public byte[] AsByteArray()
        {
            encoder.Clear();

            //frame start
            append(DLE, 0x01);

            //addresses
            if (SAD.HasValue && DAD.HasValue) {
                append(DAD.Value);

                if (DAD == DLE) //стаффинг
                    append(DLE);

                if (SAD == DLE)
                    append(DLE);

                append(SAD.Value);
            }

            if (DataHead == null)
                DataHead = "";

            //header, opcode, data start
            append(DLE, 0x1F, Func, DataHead, DLE, 0x02);

            //records 
            if (Records != null) {
                for (int i = 0; i < Records.Count; i++) {
                    var rec = Records[i];
                    for (int j = 0; j < rec.FieldCount; j++)
                        append(0x09, rec[j]);  //field separator, field
                    append(0x0C);  //record separator
                }
            }
            //data end
            append(DLE, 0x03);

            //CRC16
            byte[] tmp = encoder.ToArray();

            ushort crc = 0;
            Protocol.CRC16(ref crc, tmp, 2, tmp.Length - 2);   //skip first two bytes (DLE, 0x01)
            byte[] cb = BitConverter.GetBytes(crc);

            append(cb[1], cb[0]);

            Length = encoder.Count;

            return encoder.ToArray();
        }

        void append(params object[] objs)
        {
            foreach (object o in objs)
                if (o is byte || o is int || o is BusOperation) {
                    encoder.Add(Convert.ToByte(o));

                } else if (o is string) {
                    string s = o as string;
                    byte[] str866 = Encoding.Convert(Encoding.Unicode, Encoding.GetEncoding(866), Encoding.Unicode.GetBytes(s));  //конвертим строки в DOS 866                        
                    encoder.AddRange(str866);

                } else {
                    throw new Exception("unsupported object in packet build list");
                }
        }

        public static int FindMarker(Byte[] buf, int start, int length, byte b1, byte b2)
        {
            if (length < 2)
                return -1;

            int end = start + length;

            for (int i = start; i < end - 1; i++) {
                if ((buf[i] == b1 && buf[i + 1] == b2) && !(i > 0 && buf[i - 1] == 0x10)) //match found
                    return i;
            }

            return -1;
        }

        public byte[] RawSource
        {
            get; private set;
        }

        //-------------------------------------------------------------------------------------------------

    }
}

