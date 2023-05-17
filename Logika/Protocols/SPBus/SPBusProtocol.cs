using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Logika.Comms.Connections;
using Logika.Comms.Protocols.M4;
using Logika.Meters;
using static Logika.Comms.Protocols.M4.M4Protocol;

namespace Logika.Comms.Protocols.SPBus
{
    public partial class SPBusProtocol : Protocol
    {
        public const int MAX_DEV_PACKET_LENGTH = 5500;    // (примерно), ограничение в приборах на размер пакета
        readonly bool BroadcastMode;

        public SPBusProtocol(bool broadcastMode)
            : base()
        {
            this.BroadcastMode = broadcastMode;
        }

        //protected override void InternalCloseSession(byte? srcNT, byte? dstNT)
        //{
        //    //maybe "hangupModem" cmd should be utilized )
        //    if (connection != null && connection is ModemConnection) {
        //    }
        //}

        int pktSeqGen = 0;
        public SPBusPacket DoRequest(SPBusPacket req, bool ignoreResponse = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(req.DataHead)) {
                req.DataHead = pktSeqGen.ToString();
                pktSeqGen = (pktSeqGen + 1) % 100;
            }
            
            if (req.DAD.HasValue && !req.SAD.HasValue) {
                req.SAD = (byte)(GatewayNetAddr | 0x80);
                if (req.DAD.Value != GatewayNetAddr) {      //запрос идёт в магистраль через прибор - шлюз                
                    if (GatewayNetAddr > GatewayMaxNetAddr) 
                        Log(CommsLogLevel.Warn, $"ошибка конфигурации магистрали: адрес прибора-шлюза ({GatewayNetAddr}) больше чем максимальный адрес, заданный в его конфигурации ({GatewayMaxNetAddr})");
                    
                    if (req.DAD.Value > GatewayMaxNetAddr) 
                        throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, $"адрес прибора ({req.DAD.Value}) больше чем максимальный адрес, заданный в конфигурации магистрали прибора-шлюза ({GatewayMaxNetAddr})");                                            
                }
            }

            if (req.SAD.HasValue && req.DAD.HasValue && req.SAD.Value == req.DAD.Value)
                throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "адрес прибора совпадает с \"собственным\" адресом");

            if (req.SAD.HasValue && !req.DAD.HasValue)
                throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "задан \"собственный\" адрес, но не указан адрес прибора");

            byte[] reqBytes = req.AsByteArray();
            connection.Write(reqBytes, 0, reqBytes.Length);
            reportProtoEvent(ProtoEvent.packetTransmitted);
            if (ignoreResponse)
                return null;

            repeatReceive:
            cancellationToken.ThrowIfCancellationRequested();

            byte[] bPkt = ReadPacket6();
            reportProtoEvent(ProtoEvent.packetReceived);

            SPBusPacket inPkt = SPBusPacket.Parse(bPkt, 0);

            if (req.DAD.HasValue && inPkt.SAD.HasValue && inPkt.SAD.Value != req.DAD.Value) { //пакет не от того устройства, которому отправлен запрос				
                Log(CommsLogLevel.Warn, $"ответ от другого прибора: ожидаемый адрес отправителя: '{req.DAD.Value}', принятый: '{inPkt.SAD.Value}'");
                this.reportProtoEvent(ProtoEvent.genericError);
                goto repeatReceive;
            }

            if (inPkt.DataHead != req.DataHead) { //не соответствующий запросу идентификатор пакета				
                Log(CommsLogLevel.Warn, string.Format("нарушение порядка обмена: ожидаемый ID пакета: '{0}', принятый: '{1}'", req.DataHead, inPkt.DataHead));
                //this.reportProtoEvent(ProtoEvent.rxLatePacket); //обычно пакеты задерживаются в модемных недрах и доходят после таймаутов, поэтому пытаемся принять актуальный ответ
                goto repeatReceive;
            }

            return inPkt;
        }

        byte[] inBuf = new byte[8192];
        public byte[] ReadPacket6()
        {
            int iSTX = -1;
            int iETX = -1;
            int ipb = 0;

            DateTime ReadT = DateTime.Now;
            while (iETX == -1 || ipb - iETX < 4) {

                if (ipb >= inBuf.Length)
                    throw new Exception("переполнение буфера пакета");

                int nBytes = connection.ReadAvailable(inBuf, ipb, inBuf.Length - ipb);
                if (nBytes == 0)
                    throw new ECommException(ExcSeverity.Error, CommError.Timeout);

                int search_start = ipb != 0 ? ipb - 1 : ipb;
                int search_len = ipb != 0 ? nBytes + 1 : nBytes;

                if (iSTX == -1) {
                    iSTX = SPBusPacket.FindMarker(inBuf, search_start, search_len, 0x10, 0x01);
                }

                if (iSTX != -1 && iETX == -1) {
                    if (search_start < iSTX) {
                        search_start = iSTX + 2;
                        search_len = ipb + nBytes - search_start;
                    }

                    iETX = SPBusPacket.FindMarker(inBuf, search_start, search_len, 0x10, 0x03);

                }

                if (iSTX == -1) { //no stx in timeout time, although some data is arriving
                    TimeSpan Elapsed = DateTime.Now - ReadT;
                    if (Elapsed.TotalMilliseconds > connection.ReadTimeout)
                        throw new ECommException(ExcSeverity.Error, CommError.Timeout);
                }

                ipb += nBytes;
            }

            //connection.FlushRxLog();   //flush packet logging buffer

            //trim junk data before STX (if any)
            Array.Copy(inBuf, iSTX, inBuf, 0, iETX - iSTX + 4);
            iETX -= iSTX;
            iSTX = 0;

            UInt16 CRC = 0;
            CRC16(ref CRC, inBuf, iSTX + 2, iETX + 2);       // проверяем CRC принятого пакета
            if (CRC != 0)
                throw new ECommException(ExcSeverity.Error, CommError.Checksum);

            int len = iETX + 4; //length till ETX, 2 bytes 10 03, 2 bytes CRC            

            //return inBuf.Take(len).ToArray();
            byte[] rb = new byte[len];
            Array.Copy(inBuf, 0, rb, 0, len);
            return rb;
        }


        // reads single tag from gateway device (addressless request)
        public string RequestParamDirect(int ParamNo)
        {
            SPBusPacket req = SPBusPacket.BuildReadTagsPacket(null, null, "", new int[] { 0 }, new int[] { ParamNo });
            SPBusPacket pkt = DoRequest(req);

            if (pkt.Records.Count != 2)
                throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "неверная структура пакета");

            return string.Copy(pkt.Records[1].Fields[0]);
        }

        //-------------------------------------------------------------------------------------
        public void HangupModem()
        {
            SPBusPacket req = SPBusPacket.BuildHangupModemPacket();
            byte[] ra = req.AsByteArray();
            try {
                connection.Write(ra, 0, ra.Length);
                connection.Read(ra, 0, 1);   //read first '+' of callee hangup sequence, or wait for a timeout
            } catch (Exception) {   //ignore possible timeout
            }
        }

        public Logika6 DetectDevice(int src, int dst)
        {
            SPBusPacket req = SPBusPacket.BuildReadTagsPacket(src, dst, "", new int[] { 0 }, new int[] { 99 });
            SPBusPacket pkt = DoRequest(req);

            string p099 = pkt.Records[1].Fields[0];
            string model;
            Logika6 dev = MeterTypeFromResponse(p099, out model);

            return dev;
        }

        public int MEKHandshake()
        {
            SerialConnection sc = connection as SerialConnection;
            BaudRate prevBaudRate = sc.BaudRate;
            try {
                sc.SetParams((BaudRate)300, 8, StopBits.One, Parity.None);

                byte[] bts = calcParityBuf("\r\n");
                sc.Write(bts, 0, bts.Length);
                Wait(3000);
                sc.PurgeComms(PurgeFlags.RX);

                bts = calcParityBuf("/?!\r\n");
                sc.Write(bts, 0, bts.Length);

                byte[] inbuf = new byte[64];
                sc.Read(inbuf, 0, 13);

                int ptr = 13;

                while (true) {
                    try {
                        sc.Read(inbuf, ptr, 1);
                        if (inbuf[ptr++] == '\n')
                            break;

                    } catch (ECommException ec) {
                        if (ec.Reason == CommError.Timeout)
                            break;
                        else
                            throw;
                    }
                }

                string mekId = trimParity(inbuf, ptr).TrimEnd();
                sc.PurgeComms(PurgeFlags.RX | PurgeFlags.TX);

                if (mekId[11] == '0')
                    throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "Магистральный протокол не включен");
                char baudrateCode = (char)mekId[12];

                //change protocol to SPBus
                bts = calcParityBuf(string.Format("\x06\x30{0}\x33\r\n", baudrateCode));    //sprintf(Buf, "\x06\x30%c\x33\r\n", Speed);
                sc.Write(bts, 0, bts.Length);

                sc.Read(inbuf, 0, 1);
                if (inbuf[0] != 0x06)   //bus protocol established
                    throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "ошибка согласования магистрального протокола");
                Wait(1500);        //без этой задержки прибор не успевает среагировать на смену протокола                       

                int[] baudRates = { 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
                int baudRate = baudRates[baudrateCode - '0'];

                sc.SetParams((BaudRate)baudRate, 8, StopBits.Two, Parity.None);

                return baudRate;

            } catch (Exception) {
                sc.SetParams(prevBaudRate, 8, StopBits.Two, Parity.None);
                throw;
            }
        }

        byte[] calcParityBuf(string str)  //calculate even parity bit for each byte, write to port
        {
            byte[] buf = System.Text.ASCIIEncoding.ASCII.GetBytes(str);

            for (int z = 0; z < str.Length; z++) {
                byte parity = 0;
                for (int i = 0; i < 7; i++)
                    parity ^= (byte)((buf[z] >> i) & 1);
                buf[z] |= (byte)(parity << 7);
            }

            return buf;
        }

        void readGatewayConfig()
        {
            for (int i = 0; i < 3; i++) {
                string gw003 = RequestParamDirect(3);   //gateway's 003 param   //readTags(null, null, "gw3", new int[] { 0 }, new int[] { 3 }, out string[] va, out string[] eua);                
                if (!string.IsNullOrEmpty(gw003) && !gw003.EndsWith("?") && gw003.Length == 10) {   //иногда гадский прибор может ответить "Нет данных?" на запрос любого параметра, в т.ч. 003                    
                    gwNetAddr = Convert.ToByte(gw003.Substring(5, 2));
                    gwMaxNetAddr = Convert.ToByte(gw003.Substring(7, 2));
                    return;
                }
                Thread.Sleep(500);
            }
            throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "ошибка чтения параметра 003");
        }
        
        byte? gwNetAddr;
        byte? gwMaxNetAddr;
        
        public int GatewayMaxNetAddr { 
            get {
                if (!gwMaxNetAddr.HasValue)
                    readGatewayConfig();
                return gwMaxNetAddr.Value;
            } 
        }
    
        public int GatewayNetAddr {
            get {
                if (!gwNetAddr.HasValue)
                    readGatewayConfig();
                return gwNetAddr.Value;
            }
        }

        string trimParity(byte[] buf, int len)
        {
            for (int i = 0; i < buf.Length; i++)
                buf[i] &= 0x7F;

            return System.Text.ASCIIEncoding.ASCII.GetString(buf, 0, len);
        }

        public static Logika6 MeterTypeFromResponse(string p099n00, out string model)
        {
            string type = p099n00.Substring(0, 3);

            string subtype = p099n00.Substring(3);
            bool dot12v = subtype.ToLower().StartsWith(".1v") || subtype.StartsWith(".2v");
            model = dot12v ? subtype.Substring(1, 1) : "";

            switch (type) {
                case "961":
                    if (subtype.StartsWith(".M") || subtype.StartsWith(".М"))     //first M is english, second is ANSI 1251 russian "M"
                        return Meter.SPT961M;
                    else {
                        if (subtype.StartsWith(".1V") || subtype.StartsWith(".2V"))     //961_1/2M (V instead of v in 099н00)
                            return Meter.SPT961_1M;
                        return dot12v ? Meter.SPT961_1 : Meter.SPT961;
                    }
                case "962": return Meter.SPT962;
                case "963": return Meter.SPT963;
                case "761": return dot12v ? Meter.SPG761_1 : Meter.SPG761;
                case "762": return dot12v ? Meter.SPG762_1 : Meter.SPG762;
                case "763": return dot12v ? Meter.SPG763_1 : Meter.SPG763;
                case "542": return Meter.SPE542;
                case "543": return Meter.SPE543;
                default: throw new Exception("неподдерживаемый тип прибора: " + p099n00);
            }
        }


        public override DateTime GetDeviceClock(Meter m, byte? src, byte? dst)
        {
            SPBusPacket req = SPBusPacket.BuildReadTagsPacket(src, dst, "rtc", new int[] { 0, 0 }, new int[] { 60, 61 });
            SPBusPacket pkt = DoRequest(req);

            string[] df = pkt.Records[1].Fields[0].Split("-".ToCharArray(), 3);    // 30-09-07 [dd-MM-yy]
            string[] tf = pkt.Records[3].Fields[0].Split(":".ToCharArray(), 3);    // 14:19:06 [HH:mm:ss]

            int year = Logika6.NormalizeYear(Convert.ToUInt16(df[2])); //if year > 2 digis - do nothing
                        
            return new DateTime(year, Convert.ToInt32(df[1]), Convert.ToInt32(df[0]), Convert.ToInt32(tf[0]), Convert.ToInt32(tf[1]), Convert.ToInt32(tf[2]), DateTimeKind.Local);
        }


        public bool WriteRTC(int? src, int? dst, DateTime DateTime)
        {
            string TimeStr = string.Format("{0:HH}-{0:mm}-{0:ss}", DateTime);
            string DateStr = string.Format("{0:dd}-{0:MM}-{0:yy}", DateTime);

            string DiagsT = writeTag(src, dst, "st", 0, 21, -1, TimeStr);
            string DiagsD = writeTag(src, dst, "sd", 0, 20, -1, DateStr);

            return (String.IsNullOrEmpty(DiagsT) && String.IsNullOrEmpty(DiagsD));
        }

        public string writeTag(int? src, int? dst, string packetId, int channel, int ordinal, int? index, string value, bool ignoreResponse = false)
        {
            SPBusPacket req = SPBusPacket.BuildWriteTagPacket(src, dst, packetId, channel, ordinal, index, value);

            SPBusPacket pkt = DoRequest(req, ignoreResponse);
            if (ignoreResponse)
                return null;

            return pkt.Records[1].Fields[0];
        }

        //чтение списка обычных тегов
        public void readTags(int? src, int? dst, string packetId, int[] channels, int[] ordinals, out string[] values, out string[] eus)
        {
            if (channels.Length != ordinals.Length)
                throw new ArgumentException("channels.Length != ordinals.Length");
            SPBusPacket req = SPBusPacket.BuildReadTagsPacket(src, dst, packetId, channels, ordinals);

            SPBusPacket pkt = DoRequest(req);
            values = new string[ordinals.Length];
            eus = new string[ordinals.Length];

            for (int i = 0; i < ordinals.Length; i++) {
                string prevEU = "";

                int RecNo = 1 + i * 2;
                values[i] = pkt.Records[RecNo].Fields[0];

                string eu = pkt.Records[RecNo].FieldCount > 1 ? pkt.Records[RecNo].Fields[1] : "";
                if (string.IsNullOrEmpty(eu)) { //особенность протокола СПСеть - не выдавать единицы измерения если они совпадают с предыдущими
                    eu = prevEU;
                } else {
                    prevEU = eu;
                }
                eus[i] = eu;
            }
        }

        internal static byte[] genRawHandshake(byte? srcAddr, byte? dstAddr)
        {
            //0x10, 0x01, 0x10, 0x1F, 0x1D, 0x10, 0x02, 0x09, 0x30, 0x09, 0x39, 0x39, 0x0C, 0x10, 0x03, 0x6B, 0x50
            SPBusPacket req = SPBusPacket.BuildReadTagsPacket(srcAddr, dstAddr, "", new int[] { 0 }, new int[] { 99 });
            return req.AsByteArray();
        }

        //чтение массива/структуры (теги с индексами)
        public void readIndexTags(int? src, int? dst, string packetId, int channel, int ordinal, int startIndex, int count, out string[] values, out string[] eus)
        {
            SPBusPacket req = SPBusPacket.BuildReadArrayPacket(src, dst, packetId, channel, ordinal, startIndex, count);
            SPBusPacket pkt = DoRequest(req);

            values = new string[count];
            eus = new string[count];

            for (int i = 0; i < count; i++) {
                string prevEU = "";

                int RecNo = 1 + i;
                values[i] = pkt.Records[RecNo].Fields[0];

                string eu = pkt.Records[RecNo].FieldCount > 1 ? pkt.Records[RecNo].Fields[1] : "";
                if (string.IsNullOrEmpty(eu)) { //особенность протокола СПСеть - не выдавать единицы измерения если они совпадают с предыдущими
                    eu = prevEU;
                } else {
                    prevEU = eu;
                }
                eus[i] = eu;
            }
        }

        /// <summary>
        /// 
        /// </summary>        
        /// <returns>array of tuples: <time, value, EU>, sorted as arrived from device </record> </returns>
        public ArchiveRecord[] readArchive(int? srcAddr, int? dstAddr, string packetId, 
            int channel, int ordinal, DateTime startTime, DateTime endTime, 
            out bool maxPacketRcvd, out DateTime Prev, out DateTime Next)
        {
            SPBusPacket req = SPBusPacket.BuildReadArchivePacket(srcAddr, dstAddr, packetId, channel, ordinal, startTime, endTime);
            SPBusPacket pkt = DoRequest(req);

            maxPacketRcvd = pkt.Length > SPBusProtocol.MAX_DEV_PACKET_LENGTH;

            Prev = DateTime.MinValue;
            Next = DateTime.MinValue;

            int arcCode;
            List<ArchiveRecord> lr = ParseArchivePacket(pkt, out arcCode, out string warn);
            if (!string.IsNullOrEmpty(warn))
                Log(CommsLogLevel.Warn, warn);

            for (int i = 0; i < lr.Count; i++) {
                var r = lr[i];
                if (r.time < startTime || r.time > endTime) {
                    if (r.time < startTime) {
                        Prev = r.time;
                    } else if (r.time > endTime) {
                        Next = r.time;
                    }
                    lr.RemoveAt(i);
                    i--;
                    continue;
                }
            }
            
            return lr.ToArray();
        }

        public static List<ArchiveRecord> ParseArchivePacket(SPBusPacket pkt, out int archiveCode, out string warning)
        {
            List<ArchiveRecord> data = new List<ArchiveRecord>();
            archiveCode = Convert.ToInt32(pkt.Records[0].Fields[1]);

            if (pkt.Records.Count < 3) //записи 0,1,2 - адрес переменной и два указателя времени
                throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "некорректный ответ на запрос архива");
            
            warning = null;
            string prevEu = "";

            for (int i = 3; i < pkt.Records.Count; i++) {
                var fields = pkt.Records[i];
                if (fields.FieldCount != 3) { //"Дата/время?" совсем старый прибор не понимает формат даты или год его пугает
                    if (fields.FieldCount == 1 && fields[0].Equals("Дата/время?", StringComparison.OrdinalIgnoreCase)) {
                        warning = $"прибор не может обработать запрос архива с границами [{pktTimeRecToDatetime(pkt.Records[1])} .. {pktTimeRecToDatetime(pkt.Records[2])}] - '{fields[0]}'";
                        return data;
                    } else
                        throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "некорректный ответ прибора на запрос архива");
                }
                string r_value = fields[0];
                string r_eu = fields[1].Trim();
                DateTime r_time = Logika6.TimeStringToDateTime(fields[2]);
                if (string.IsNullOrEmpty(r_eu))
                    r_eu = prevEu;
                else
                    prevEu = r_eu;

                ArchiveRecord rec = new ArchiveRecord() { time = r_time, value = r_value, eu = r_eu };
                data.Insert(0, rec);    //records come from device in order from now to past
            }

            return data;
        }

        static DateTime pktTimeRecToDatetime(SPBusPacket.ReadonlyFieldCollection timeRec)
        {
            ushort[] p = new ushort[6];
            for (int i = 0; i < 6; i++)
                p[i] = Convert.ToUInt16(timeRec.Fields[i]);
            DateTime dt = new DateTime(Logika6.NormalizeYear(p[2]), p[1], p[0], p[3], p[4], p[5]);
            
            return DateTime.SpecifyKind(dt, DateTimeKind.Local);
        }

        public static string[] ParseArchiveRecordPacket(SPBusPacket pkt, out DateTime recordTimestamp, out DateTime prevPtr, bool strippedHeader = false)
        {
            recordTimestamp = DateTime.MinValue;
            int ri = strippedHeader ? 0 : 2;    //start record index

            int nRecords = pkt.Records.Count - (ri + 2); //(0,1) - адрес переменной, время из запроса и (2,3) - две метки времени (метка текущей записи и указатель на предыдущую)
            if (nRecords <= 0) //ошибочный пакет либо "Дата/время?"
                throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "некорректный пакет архивного среза");

            recordTimestamp = pktTimeRecToDatetime(pkt.Records[ri]);
            prevPtr = pktTimeRecToDatetime(pkt.Records[ri + 1]);   //указатель не на предыдущую _существующую_ запись, а на предыдущий временнОй интервал (за который данных может и не быть).
            
            string[] values = new string[nRecords];
            for (int i = 0; i < nRecords; i++) {
                string s = pkt.Records[ri + 2 + i].Fields[0];
                string sl = s.ToLower();
                if (sl.EndsWith("?") || sl.EndsWith("nan") || sl.EndsWith("inf")) //приборную диагностику (xxxxx?) и NaN-ы зарубаем на корню
                    s = null;
                values[i] = s;
            }
            
            return (values.Length == 0 || (values.Length == 1 && values[0] == null)) ? null : values;            
        }

        public static ArchiveDescriptorElement[] ParseArchiveDescriptorPacket(SPBusPacket pkt, out int archiveOrdinal)
        {
            List<ArchiveDescriptorElement> aCols = new List<ArchiveDescriptorElement>();
            string prevEU = "";

            archiveOrdinal = Convert.ToInt32(pkt.Records[0].Fields[1]);

            for (int i = 1; i < pkt.Records.Count; i++) {
                string _name = pkt.Records[i].Fields[0];
                ArchiveDescriptorElement col = new ArchiveDescriptorElement() { name = _name };
                string eu = pkt.Records[i].Fields[1];
                if (eu != "") {
                    col.eu = eu;
                    prevEU = eu;
                } else
                    col.eu = prevEU;
                col.channel = Convert.ToInt32(pkt.Records[i].Fields[2]);
                col.ordinal = Convert.ToInt32(pkt.Records[i].Fields[3]);
                col.archiveOrd = archiveOrdinal;
                aCols.Add(col);
            }

            return aCols.ToArray();
        }


        public ArchiveDescriptorElement[] readRecordsDescriptor(int? src, int? dst, string packetId, int archiveOrdinal)
        {            
            SPBusPacket req = SPBusPacket.BuildReadArchiveDescriptorPacket(src, dst, packetId, archiveOrdinal);
            SPBusPacket pkt = DoRequest(req);
            
            return ParseArchiveDescriptorPacket(pkt, out int arOrd);
        }

        /// чтение архивного среза

        public string[] readArchiveRecord(int? src, int? dst, string packetId, int archiveOrdinal, DateTime atTime, out DateTime recordTimestamp, out DateTime prevPtr)
        {
            prevPtr = DateTime.MinValue;
            recordTimestamp = DateTime.MinValue;

            //new gen devices (x61.1/2) interpret year 00 as 2100.            
            if (atTime < LGK_ERA_START)                                 
                return null;
                                    
            SPBusPacket req = SPBusPacket.BuildReadArchiveRecordPacket(src, dst, packetId, archiveOrdinal, atTime);
            //System.Diagnostics.Debug.Print("-> x6 req @ {0}", atTime);
            SPBusPacket pkt = DoRequest(req);
            string [] res = ParseArchiveRecordPacket(pkt, out recordTimestamp, out prevPtr);
            //System.Diagnostics.Debug.Print("<- {0}, next @ {1}", res==null?"no data":"data returned", prevPtr);

            return res;
        }

        int vTagIdxSortComparer(DataTag a, DataTag b)
        {
            if (!a.Index.HasValue || !b.Index.HasValue)
                throw new ArgumentException("non-index tags not needed be sorted");

            if (a.Channel.No != b.Channel.No)
                return a.Channel.No - b.Channel.No;
            
            if (a.Ordinal != b.Ordinal)
                return a.Ordinal - b.Ordinal;

            return a.Index.Value - b.Index.Value;
        }

        public override void ResetInternalBusState()
        {
            //SPbus have no hidden bus state, like M4 devs
            //so - do nothing            
        }

        protected override void internalCloseCommSession(byte? srcAddr, byte? dstAddr)
        {
            try {
                //if modem conn
                //BusOperation.HangupModem
            } catch { 
            }
        }

    }
}
