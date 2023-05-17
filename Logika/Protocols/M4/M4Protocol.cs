using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using Logika.Comms.Connections;
using Logika.Meters;

namespace Logika.Comms.Protocols.M4
{
    public partial class M4Protocol : Protocol
    {
        public const byte BROADCAST = 0xFF;   //NT для безадресных запросов

        public const byte FRAME_START = 0x10;
        public const byte FRAME_END = 0x16;

        public const byte EXT_PROTO = 0x90;

        public const int MAX_RAM_REQUEST = 0x40;
        public const int MAX_TAGS_AT_ONCE = 24;

        public const ushort PARTITION_CURRENT = 0xFFFF;

        public ushort ArchivePartition = PARTITION_CURRENT; //архивный раздел который будет читаться этим экземпляром протокола

        BusState busState;
        BaudRate? desiredBaudrate;

        public override void ResetInternalBusState()
        {
            busState = new BusState();
        }

        public M4Protocol(BaudRate? targetBaudrate = null)
            : base()
        {
            this.desiredBaudrate = targetBaudrate;            
        }

        protected override void internalCloseCommSession(byte? notUsed, byte? nt)
        {            
            //SendLegacyPacket(nt, M4Opcode.SessionClose, new byte[] { 0, 0, 0, 0 });
            DoLegacyRequest(nt, M4Opcode.SessionClose, new byte[] { 0, 0, 0, 0 }, 0, RecvFlags.DontThrowOnErrorReply);  //в зависимости от ответа bsu поправить также и старый пролог                                                 
        }

        #region common

        public static readonly byte[] WAKEUP_SEQUENCE = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        //----------------------------------------------------------------------------------------------
        /// <param name="slowWake"> Совсем старые приборы, им нужна пауза между байтами FF (которую сложно выдержать на разных типах соединений) </param>
        public void SendAttention(bool slowWake)
        {
            connection.PurgeComms(PurgeFlags.RX | PurgeFlags.TX);

            if (slowWake) {
                for (int i = 0; i < WAKEUP_SEQUENCE.Length; i++) {
                    connection.Write(WAKEUP_SEQUENCE, i, 1);
                    Wait(20);
                }

            } else {
                connection.Write(WAKEUP_SEQUENCE, 0, WAKEUP_SEQUENCE.Length);
            }
        }
        

        internal static byte[] genRawHandshake(byte? destNT)
        {           
            byte[] hsArgs = new byte[] { 0, 0, 0, 0 };
            byte[] pBuf = new byte[3 + hsArgs.Length + 2];

            pBuf[0] = M4Protocol.FRAME_START;
            pBuf[1] = destNT.HasValue ? destNT.Value : M4Protocol.BROADCAST;
            pBuf[2] = (byte)M4Opcode.Handshake;

            pBuf[3 + hsArgs.Length] = Logika4.Checksum8(pBuf, 1, hsArgs.Length + 2);
            pBuf[3 + hsArgs.Length + 1] = M4Protocol.FRAME_END; //end of frame

            //byte[] b = new byte[WAKEUP_SEQUENCE.Length + pBuf.Length];
            //Array.Copy(WAKEUP_SEQUENCE, b, WAKEUP_SEQUENCE.Length);
            //Array.Copy(pBuf, 0, b, WAKEUP_SEQUENCE.Length, pBuf.Length);

            //return b;

            return pBuf;
        }


        public byte[] SelectDeviceAndChannel(Logika4 mtr, byte? nt, byte? tv = null)
        {
            if (mtr == null)
                throw new ArgumentException();

            if (!nt.HasValue)
                nt = BROADCAST;

            //если прибор не выбран на шине,
            //если обмен с прибором был больше N минут назад, FFы с принудительными паузами
            if (busState.meter != null && (DateTime.Now - busState.lastIOTime).TotalSeconds > (busState.meter.SessionTimeout.TotalSeconds * 0.9)) {
                busState.Reset();
            }

            //test if we're working at increased baudrate
            if (busState.meter != null && busState.initialBaudRate != busState.currentBaudRate) {
                //прибор восстанавливает скорость при отсутствии обмена в течение 10 с
                if ((DateTime.Now - busState.lastIOTime).TotalSeconds > 9.8) {
                    if (connection is SerialConnection)
                        (connection as SerialConnection).BaudRate = busState.initialBaudRate.Value;
                    busState.currentBaudRate = busState.initialBaudRate;
                    System.Diagnostics.Debug.Print("восстановлена начальная скорость обмена " + busState.currentBaudRate + " bps");
                }
            }

            if (!busState.activeNT.HasValue || (busState.activeNT.Value != nt.Value) ||  
                (tv.HasValue && busState.selectedTV != tv) ) {   //выбор канала актуален только для 4L приборов, и только для записи параметров

                bool alreadySelected = (busState.activeNT.HasValue && busState.activeNT == nt);

                //выдерживаем паузы между FFами и после них, только если устройство этого требует,
                //и оно еще не активно
                bool slowFFs = !mtr.SupportsFastSessionInit && !alreadySelected;                
                M4Packet hsPkt = Handshake(nt, tv.HasValue ? tv.Value : (byte)0, slowFFs);

                Meter detectedType = Logika4.MeterTypeFromResponse(hsPkt.Data[0], hsPkt.Data[1], hsPkt.Data[2]);
                //cntr.fwVersion = pkt.Data[2];
                //Meter dm = MeterFactory.GetMeter(detectedType);
                if (detectedType != mtr) {
                    busState.Reset();
                    throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, string.Format("Несоответствие типа прибора. Ожидаемый тип прибора: {0}, фактический: {1} (NT={2})", mtr.Caption, detectedType.Caption, nt));
                }
                busState.meter = mtr;
                busState.devReply = hsPkt.Data;
                if (connection is SerialConnection) {
                    SerialConnection sc = connection as SerialConnection;
                    busState.initialBaudRate = busState.currentBaudRate = (BaudRate)sc.BaudRate;
                }

                //устанавливаем повышенную скорость обмена
                if (desiredBaudrate.HasValue && connection is SerialConnection) {
                    //CCounter cct = cntr.cfgNode as CCounter;
                    if (busState.currentBaudRate != desiredBaudrate.Value)
                        SetBusSpeed(busState.meter, nt, desiredBaudrate.Value);
                }
            }

            return busState.devReply;
        }

        public override Meter GetMeterType(byte? srcNT, byte? dstNT)
        {
            M4Packet hsPkt = Handshake(dstNT, 0, false);            
            return Logika4.MeterTypeFromResponse(hsPkt.Data[0], hsPkt.Data[1], hsPkt.Data[2]); 
        }

        //----------------------------------------------------------------------------------------------
        public const int WAKE_SESSION_DELAY = 100; //мс, задержка между серией FF и запросом сеанса (нужна только для АДС99 в режиме TCP сервер, без неё он всячески таймаутится)

        public M4Packet Handshake(byte? nt, byte channel, bool bSlowFFs)
        {
            if (nt != busState.activeNT)
                busState.Reset();

            SendAttention(bSlowFFs);
            Wait(WAKE_SESSION_DELAY);   //пауза обязательна для АДС99 в режиме TCP сервер, без неё он всячески таймаутится, остальным приборам +/- всё равно

            connection.PurgeComms(PurgeFlags.RX);

            byte[] reqData = new byte[] { channel, 0, 0, 0 };
            M4Packet response = DoLegacyRequest(nt, M4Opcode.Handshake, reqData, 3);
           
            busState.activeNT = response.NT;
            busState.selectedTV = channel;
            busState.lastIOTime = DateTime.Now;
                
            return response;
        }
        
        M4Packet DoLegacyRequest(byte? nt, M4Opcode reqFunc, byte[] data, int expectedDataLen, RecvFlags flags = 0)
        {            
            SendLegacyPacket(nt, reqFunc, data);
            return RecvPacket(nt, reqFunc, null, expectedDataLen, flags);
        }

        int idCtr = 0;
        M4Packet DoM4Request(byte? nt, M4Opcode reqFunc, byte[] data, byte? pktId = null, RecvFlags flags = 0)
        {
            if (pktId == null)
                pktId = (byte)(idCtr++);
            SendExtendedPacket(nt, pktId.Value, reqFunc, data);
            M4Packet p = RecvPacket(nt, reqFunc, pktId, 0, flags);

            return p;
        }

        [Flags]
        public enum RecvFlags
        {
            DontThrowOnErrorReply = 0x01,
        }
        //----------------------------------------------------------------------------------------------
        //should receive both legacy and extended packets. RC4 encryption (some legacy devs) is not supported
        public M4Packet RecvPacket(byte? expectedNT, M4Opcode? expectedOpcode, byte? expectedId, int expectedDataLength, RecvFlags flags = 0)
        {            
            byte[] buf = new byte[8];
            byte[] check = new byte[2];
        
            M4Packet p = new M4Packet();
            try {
        resyncFrame:
                DateTime readStartTime = DateTime.Now;
                while (true) { //read byte stream, until frame start found or timeout elapses
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    connection.Read(buf, 0, 1);
                    if (buf[0] == M4Protocol.FRAME_START)                         
                        break;                    

                    TimeSpan Elapsed = DateTime.Now - readStartTime;
                    if (Elapsed.TotalMilliseconds > connection.ReadTimeout) {
                        busState.Reset();         //force session re-establish
                        throw new ECommException(ExcSeverity.Error, CommError.Timeout);
                    }
                }
                connection.Read(buf, 1, 2); //read NT and function code                
                p.NT = buf[1];
                p.FunctionCode = (M4Opcode)buf[2];
                
                //это может быть как ответ другого прибора (очень маловероятно), так и случайная синхронизация по 0x10 в потоке данных
                if (expectedNT.HasValue && p.NT != expectedNT.Value)  
                    goto resyncFrame;

                //либо задержавшийся в буферах устройств связи ответ на предыдущий запрос(наиболее вероятно), либо случайно случившиеся в потоке 10 NT(маловероятно), либо ответ другого прибора(крайне маловероятно)
                if (expectedOpcode.HasValue && p.FunctionCode != expectedOpcode.Value && p.FunctionCode != M4Opcode.Error && (byte)p.FunctionCode != EXT_PROTO) {
                    if (expectedOpcode == M4Opcode.ReadFlash) {   //для многостраничного чтения Flash ошибки в приеме последовательности пакетов недопустимы, поэтому никаких переприёмов.
                        busState.Reset();
                        throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "нарушение последовательности обмена");
                    } 
                    goto resyncFrame;
                }

                if ((byte)p.FunctionCode == EXT_PROTO) {   //extended protocol 
                    p.Extended = true;
                    connection.Read(buf, 3, 5);

                    p.ID = buf[3];
                    p.Attributes = buf[4];
                    int payload_len = buf[5] + (buf[6] << 8);
                    p.Data = new byte[payload_len - 1];   //payload = opcode + data
                    p.FunctionCode = (M4Opcode)buf[7];
                    if (expectedOpcode.HasValue && p.FunctionCode != expectedOpcode.Value && p.FunctionCode!=M4Opcode.Error)
                        goto resyncFrame;

                    if (expectedId.HasValue && p.ID != expectedId.Value) {                        
                        Log(CommsLogLevel.Warn, string.Format("нарушение порядка обмена: ожидаемый ID пакета: 0x{0:X2}, принятый: 0x{1:X2}", expectedId.Value, p.ID));
                        goto resyncFrame;
                    }

                } else {    //standard protocol
                    p.Extended = false;
                    if (p.FunctionCode == M4Opcode.Error) //header of error packet
                        p.Data = new byte[1];   //just error code
                    else
                        p.Data = new byte[expectedDataLength];
                }

                connection.Read(p.Data, 0, p.Data.Length); // data + CSUM + frame end
                connection.Read(check, 0, 2);  //legacy: checksum8 + frame end, M4: CRC16
                if (p.Extended)
                    p.Check = (ushort)((check[0] << 8) | check[1]);   //MSB first
                else
                    p.Check = (ushort)(check[0] | (check[1] << 8));  //also check END_OF_FRAME byte (0x16)
                busState.lastIOTime = DateTime.Now;
                
            } catch (Exception) {
                busState.Reset();    //re-establish device link on next attempt
                throw;
            }

            ushort calculatedCheck = 0;

            if (p.Extended) {   //CRC16 check                                
                Protocol.CRC16(ref calculatedCheck, buf, 1, 7);
                Protocol.CRC16(ref calculatedCheck, p.Data, 0, p.Data.Length);
            } else {    //checksum8                
                calculatedCheck = 0x1600;   //end-of=frame byte
                calculatedCheck |= (byte)~(~Logika4.Checksum8(buf, 1, 2) + ~Logika4.Checksum8(p.Data, 0, p.Data.Length));  // hdr + data under checksum                                
            }
            if (p.Check != calculatedCheck) {
                reportProtoEvent(ProtoEvent.rxCrcError);
                throw new ECommException(ExcSeverity.Error, CommError.Checksum);
            }

            reportProtoEvent(ProtoEvent.packetReceived);

            if (p.FunctionCode == M4Opcode.Error) {
                ErrorCode ec = (ErrorCode)p.Data[0];

                reportProtoEvent(ProtoEvent.genericError);
                if (flags.HasFlag(RecvFlags.DontThrowOnErrorReply)==false)
                    throw new ECommException(ExcSeverity.Error, CommError.Unspecified, string.Format("прибор вернул код ошибки: {0}", (int)ec));
            }

            return p;            
        }

        //----------------------------------------------------------------------------------------------
        public void SetBusSpeed(Logika4 mtr, byte? nt, BaudRate baudRate)
        {            
            //                       0     1     2     3      4      5       6
            int[] fdvBaudRates = { 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
            int nbr = Array.IndexOf(fdvBaudRates, (int)baudRate);
            if (nbr < 0)
                throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "запрошенная скорость обмена не поддерживается");

            M4Packet rsp = DoLegacyRequest(nt, M4Opcode.SetSpeed, new byte[4] { (byte)nbr, 0, 0, 0 }, 0);

            (connection as SerialConnection).BaudRate = baudRate;
            busState.currentBaudRate = baudRate;

            Wait(250);
            connection.PurgeComms(PurgeFlags.RX | PurgeFlags.TX);

            Log(CommsLogLevel.Info, "установлена скорость обмена " + (int)baudRate + " bps");            
        }        

        #endregion


        #region legacy (4L) 
        //----------------------------------------------------------------------------------------------
        public void SendLegacyPacket(byte? nt, M4Opcode func, byte[] data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            M4Packet pkt = new M4Packet();
            byte[] pBuf = new byte[3 + data.Length + 2]; 

            pBuf[0] = FRAME_START;
            pBuf[1] = nt.HasValue ? nt.Value : BROADCAST;
            pBuf[2] = (byte)func; 

            Array.Copy(data, 0, pBuf, 3, data.Length);

            pBuf[3 + data.Length] = Logika4.Checksum8(pBuf, 1, data.Length + 2);
            pBuf[3 + data.Length + 1] = FRAME_END; //end of frame

            int pktTotalLen = 3 + data.Length + 2;
            connection.Write(pBuf, 0, pktTotalLen);

            reportProtoEvent(ProtoEvent.packetTransmitted);
        }

        //---------------------------------------------------------------------------------------------
        //pre-M4 devices
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mtr"></param>
        /// <param name="nt"></param>
        /// <param name="channel"></param>
        /// <param name="nParam"></param>
        /// <param name="value"></param>
        /// <returns> protocol error code, or null if write successful </returns>
        public ErrorCode? WriteParameterL4(Logika4L mtr, byte? nt, byte channel, int nParam, string value, bool? operFlag)
        {

            if (mtr is TSPG741 && nParam >= 200 && nParam < 300) {   //транслируемые через СП параметры СПГ741
                DataTagDef td = Meter.SPG741.Tags.All.SingleOrDefault(x => x.Ordinal == nParam);
                byte sp = get741sp(nt);
                int? mappedOrdinal = TSPG741.GetMappedDBParamOrdinal(td.Key, sp);
                if (!mappedOrdinal.HasValue)  //параметр не существует при текущем СП
                    return null;
                else
                    nParam = mappedOrdinal.Value;
            }

            SelectDeviceAndChannel(mtr, nt, channel); //переключить активный канал
            if (channel == 1 || channel == 2) //при записи указывается относительный номер параметра по каналу
                nParam -= 50;

            byte[] reqData = new byte[4] { (byte)(nParam & 0xFF), (byte)((nParam >> 8) & 0xFF), 0, 0 };
            M4Packet pkt = DoLegacyRequest(nt, M4Opcode.WriteParam, reqData, 0, RecvFlags.DontThrowOnErrorReply);

            if (pkt.FunctionCode == M4Opcode.Error)
                return (ErrorCode)pkt.Data[0];

            //actual data write operation
            reqData = new byte[64];
            
            for (int i = 0; i < reqData.Length; i++) {	//memset(buf2, 0x20, buf.Length);
                if (i < value.Length)
                    reqData[i] = (byte)value[i];
                else
                    reqData[i] = 0x20;
            }
            if (operFlag.HasValue)
                reqData[reqData.Length - 1] = operFlag.Value ? (byte)'*' : (byte)0x00;

            pkt = DoLegacyRequest(nt, M4Opcode.WriteParam, reqData, 0, RecvFlags.DontThrowOnErrorReply);
            if (pkt.FunctionCode == M4Opcode.Error)
                return (ErrorCode)pkt.Data[0];

            return null;
        }

        public static int? GetLegacyResponseDataLen(M4Opcode functionCode)
        {
            switch (functionCode) {
                case M4Opcode.Handshake:
                    return 3;
                case M4Opcode.Error:
                    return 1;
                case M4Opcode.ReadFlash:
                    return Logika4L.FLASH_PAGE_SIZE;
                case M4Opcode.WriteParam:
                    return 0;
                case M4Opcode.SetSpeed:
                    return 0;
                case M4Opcode.ReadRam:
                default:
                    return null;
            }
        }

        private byte get741sp(byte? nt)
        {
            MeterInstance mmd = getMeterInstance(Meter.SPG741, nt);
            if (!mmd.sp.HasValue) {
                const int SP_741_ADDR = 0x200;
                getFlashPagesToCache(Meter.SPG741, nt, SP_741_ADDR / Logika4L.FLASH_PAGE_SIZE, 1, mmd);
                mmd.sp = Convert.ToByte(Logika4L.GetValue(Logika4L.BinaryType.dbentry, mmd.flash, SP_741_ADDR));
            }
            return mmd.sp.Value;
        }

        //---------------------------------------------------------------------------------------------
        public byte[] ReadRAM(Logika4L mtr, byte? nt, int StartAddr, int nBytes)
        {
            if (nBytes > MAX_RAM_REQUEST)    //device limit
                throw new ArgumentException("too much data requested from RAM");

            SelectDeviceAndChannel(mtr, nt);

            byte[] reqData = new byte[4] { (byte)(StartAddr & 0xFF), (byte)((StartAddr >> 8) & 0xFF), (byte)(nBytes), 0};
            M4Packet pkt = DoLegacyRequest(nt, M4Opcode.ReadRam, reqData, nBytes);

            return pkt.Data;
        }

        //---------------------------------------------------------------------------------------------
        public const int MAX_PAGE_BLOCK = 8;
        /// <summary>
        /// read flash pages directly from L4 device, not using cache
        /// </summary>        
        public byte[] ReadFlashPages(Logika4L mtr, byte? nt, int startPage, int pageCount)
        {
            if (pageCount <= 0)
                throw new ArgumentException("ReadFlashPages: zero page count");

            SelectDeviceAndChannel(mtr, nt);
            Byte[] cmdbuf = new Byte[4];

            byte[] retbuf = new byte[pageCount * Logika4L.FLASH_PAGE_SIZE];

            for (int p = 0; p < (pageCount + MAX_PAGE_BLOCK - 1) / MAX_PAGE_BLOCK; p++) {

                int pagesToReq = pageCount - p * MAX_PAGE_BLOCK;
                if (pagesToReq > MAX_PAGE_BLOCK)
                    pagesToReq = MAX_PAGE_BLOCK;
                int pageBlockStart = startPage + p * MAX_PAGE_BLOCK;

                byte[] reqData = new byte[4] { (byte)(pageBlockStart & 0xFF), (byte)((pageBlockStart >> 8) & 0xFF), (byte)pagesToReq, 0 };
                //System.Diagnostics.Debug.Print($"read flash pages {pageBlockStart} .. {pageBlockStart + pagesToReq}");
                SendLegacyPacket(nt, M4Opcode.ReadFlash, reqData);

                for (int i = 0; i < pagesToReq; i++) {
                    M4Packet pkt;
                    try {
                        pkt = RecvPacket(nt, M4Opcode.ReadFlash, null, Logika4L.FLASH_PAGE_SIZE);
                    } catch {
                        if (pageCount > 1)  //при ошибке в многостраничных запросах флеша - пересинхронизируем поток чтения, чтобы не словить опоздавшую страницу с другим адресом
                            busState.Reset();   //будет запрошен хендшейк
                        throw;
                    }
                    if (pkt.FunctionCode != M4Opcode.ReadFlash || pkt.Data.Length != Logika4L.FLASH_PAGE_SIZE)
                        throw new ECommException(ExcSeverity.Error, CommError.Unspecified, string.Format("принят некорректный пакет, код функции 0x{0:X2}", (byte)pkt.FunctionCode));

                    Array.Copy(pkt.Data, 0, retbuf, (p * MAX_PAGE_BLOCK + i) * Logika4L.FLASH_PAGE_SIZE, Logika4L.FLASH_PAGE_SIZE);
                }
            }

            return retbuf;
        }

        //----------------------------------------------------------------------------------------------
        /// <summary>
        /// read block of flash memory directly from L4 device, not using cache
        /// </summary>        
        public byte[] ReadFlashBytes(Logika4L mtr, byte? nt, int StartAddr, int Length)
        {
            if (Length <= 0)
                throw new ArgumentException("read length invalid");

            int StartPage = StartAddr / Logika4L.FLASH_PAGE_SIZE;
            int EndPage = (StartAddr + Length - 1) / Logika4L.FLASH_PAGE_SIZE;
            int PageCount = EndPage - StartPage + 1;
            byte[] mem = ReadFlashPages(mtr, nt, StartPage, PageCount);
            byte[] retbuf = new byte[Length];
            Array.Copy(mem, StartAddr % Logika4L.FLASH_PAGE_SIZE, retbuf, 0, Length);

            return retbuf;
        }

        #endregion

        #region modern (M4)
        //---------------------------------------------------------------------------------------------

        //---------------------------------------------------------------------------------------------
        public void SendExtendedPacket(byte? nt, byte packetId, M4Opcode opcode, byte[] data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const int CRC_LEN = 2;
            const int HDR_LEN = 8;

            byte[] buf = new byte[HDR_LEN + data.Length + CRC_LEN];

            buf[0] = M4Protocol.FRAME_START;
            buf[1] = nt.HasValue ? nt.Value : BROADCAST;
            buf[2] = 0x90;  //extended protocol ID
            buf[3] = packetId;    //packet identifier
            buf[4] = 0x00;  //packet attributes                   

            ushort payload_len = (ushort)(1 + data.Length);  //opcode + data
            buf[5] = (byte)(payload_len & 0xFF);    //data length: 2 byte field
            buf[6] = (byte)(payload_len >> 8);

            buf[7] = (byte)opcode;
            Array.Copy(data, 0, buf, 8, data.Length);

            //calculate CRC16
            ushort check = 0;
            Protocol.CRC16(ref check, buf, 1, HDR_LEN - 1 + data.Length);
            buf[HDR_LEN + data.Length] = (byte)(check >> 8);
            buf[HDR_LEN + data.Length + 1] = (byte)(check & 0xFF);

            connection.Write(buf, 0, buf.Length);
            reportProtoEvent(ProtoEvent.packetTransmitted);
        }

        //---------------------------------------------------------------------------------------------
        public object[] readTagsM4(Logika4M m, byte? nt, int[] channels, int[] ordinals, out bool[] opFlags)
        {
            SelectDeviceAndChannel(m, nt);
            if (channels == null || channels.Length == 0 || ordinals == null || ordinals.Length == 0 || channels.Length != ordinals.Length)
                throw new ArgumentException("некорректные входные параметры функции readTagsM4");

            List<byte> lb = new List<byte>();
            for (int i = 0; i < ordinals.Length; i++) {
                byte ch = Convert.ToByte(ordinals[i] < CHANNEL_NBASE ? channels[i] : ordinals[i] / CHANNEL_NBASE);  //extract pseudo-channel info from ordinal, where available                                    
                ushort ord = Convert.ToUInt16(ordinals[i] % CHANNEL_NBASE);
                appendPNUM(lb, ch, ord);
            }

            M4Packet p = DoM4Request(nt, M4Opcode.ReadTags, lb.ToArray());
            lb.Clear();                                              

            object[] oa = parseM4TagsPacket(p, out opFlags);

            if (m == Meter.SPG742 || m == Meter.SPT941_20) { //серийный номер СПxx4x (M4) - отрезаем старший байт
                for (int i = 0; i < ordinals.Length; i++) {                    
                    if (ordinals[i] == 8256 && oa[i] is uint)
                        oa[i] = (uint)oa[i] & 0x00FFFFFF;
                }
            }

            return oa;
        }

        public static object[] parseM4TagsPacket(M4Packet p, out bool[] opFlags)
        {
            if (!p.Extended || p.FunctionCode != M4Opcode.ReadTags) 
                throw new ArgumentException("некорректный пакет");

            List<object> valuesList = new List<object>();
            List<bool> opFlagsList = new List<bool>();

            int tp = 0;
            while (tp < p.Data.Length) { 
                object o;
                int tagLen = Logika4M.ParseTag(p.Data, tp, out o);

                if (o is Logika4M.OperParamFlag) { //признак оперативности предыдущего параметра                    
                    opFlagsList[opFlagsList.Count - 1] = ((Logika4M.OperParamFlag)o) == Logika4M.OperParamFlag.Yes ? true : false;
                    tp += tagLen;
                    continue;
                }
               
                valuesList.Add(o);
                opFlagsList.Add(false);
                 
                tp += tagLen;                
            }

            opFlags = opFlagsList.ToArray();
            return valuesList.ToArray();
        }

        public static void appendPNUM(List<byte> lb, byte channel, ushort ordinal)
        {
            lb.Add(0x4A);
            lb.Add(0x03);

            lb.Add(channel);
            lb.Add((byte)(ordinal & 0xFF));
            lb.Add((byte)((ordinal >> 8) & 0xFF));
        }

        public byte?[] WriteParamsM4(Logika4M mtr, byte? nt, TagWriteData[] wda)
        {
            SelectDeviceAndChannel(mtr, nt);

            List<byte> lb = new List<byte>();
            for (int i = 0; i < wda.Length; i++) {
                TagWriteData twd = wda[i];
                object v = twd.value;

                byte ch = Convert.ToByte(twd.ordinal < CHANNEL_NBASE ? twd.channel : twd.ordinal / CHANNEL_NBASE);  //extract pseudo-channel info from ordinal, where available                                    
                ushort ord = Convert.ToUInt16(twd.ordinal % CHANNEL_NBASE);
                appendPNUM(lb, ch, ord);

                if (v == null || (v is string && ((string)v).Length == 0)) {
                    lb.Add(0x05);   //add null
                    lb.Add(0);
                } else if (v is string) {
                    lb.Add(0x16);   //ASCII string
                    string sv = v.ToString();
                    lb.Add((byte)sv.Length);
                    for (int z = 0; z < sv.Length; z++)
                        lb.Add((byte)sv[z]);
                } else if (v is uint) {
                    lb.Add(0x41);   //unsigned int                    
                    lb.Add(0x04);   //length
                    byte[] buf = BitConverter.GetBytes((uint)v);
                    foreach (byte b in buf)
                        lb.Add(b);
                } else if (v is byte[]) {
                    lb.Add(0x04);   //octet string
                    byte[] ba = (byte[])v;
                    if (ba.Length < 0x80) {
                        lb.Add((byte)ba.Length);   //single-byte length
                    } else if (ba.Length < 0x10000) {
                        lb.Add(0x82);   //two-byte length field follows
                        byte[] lenBytes = BitConverter.GetBytes((ushort)ba.Length);
                        lb.Add(lenBytes[1]);
                        lb.Add(lenBytes[0]);
                    } else
                        throw new Exception("octet string too large");

                    foreach (byte b in ba)
                        lb.Add(b);
                } else
                    throw new Exception("неподдерживаемый тип данных в запросе записи переменной");

                if (twd.oper.HasValue) {
                    lb.Add(0x45);   //oper tag
                    lb.Add(0x01);   //len
                    lb.Add(twd.oper.Value ? (byte)0x01 : (byte)0x00);
                }
            }

            M4Packet p = DoM4Request(nt, M4Opcode.WriteTags, lb.ToArray());                       
            byte?[] errors = new byte?[wda.Length];

            int tp = 0;
            for (int i = 0; i < wda.Length; i++) {
                byte tID = p.Data[tp];
                byte tagLength = p.Data[tp + 1];
                if (tID == 0x46) {          //ACK
                    tagLength = 0;
                    errors[i] = null;
                } else if (tID == 0x55) {   //ERR                                        
                    errors[i] = p.Data[tp + 2];
                } else
                    errors[i] = 0xFF;         //неизвестный код тэга
                tp += 2 + tagLength;
            }

            return errors;
        }


        //----------------------------------------------------------------------------------------------
        //год кодируется у FDV в запросах одним байтом, начиная с 2000
        DateTime restrictTime(DateTime dt)
        {
            if (dt != DateTime.MinValue) {
                int y = dt.Year - 2000;
                if (y < 0)
                    return new DateTime(2000, 1, 1, 0, 0, 0, dt.Kind);
                else if (y > 255)
                    return new DateTime(2255, 1, 31, 23, 59, 59, 999, dt.Kind);
            }
            return dt;
        }

        //---------------------------------------------------------------------------------------------
        void appendDateTag(List<byte> lb, DateTime dt, bool useYearAndMonthOnly)
        {
            lb.Add(0x49);   //datetime tag
            lb.Add((byte)(useYearAndMonthOnly ? 2 : 8));
            lb.Add((byte)(dt.Year - 2000));
            lb.Add((byte)dt.Month);
            if (!useYearAndMonthOnly) {
                lb.Add((byte)dt.Day);
                lb.Add((byte)dt.Hour);
                lb.Add((byte)dt.Minute);
                lb.Add((byte)dt.Second);
                lb.Add((byte)(dt.Millisecond & 0xFF));
                lb.Add((byte)(dt.Millisecond >> 8));
            }
        }

        internal enum CompressionType : byte
        {
            FLZLimitedLength = 0x10,    //FLZ compression, result length limited to ~1024 b
                                        //в 944 реализовано с ошибкой, выдает испорченные блоки
                                        //FLZ = 0x20,         //FLZ compression, result length not limited
        };

        //---------------------------------------------------------------------------------------------
        public M4Packet readArchiveM4(Logika4M mtr, byte? nt, byte? pktId, ushort partition, byte channel, M4ArchiveId archiveKind, DateTime from, DateTime to, int numValues, /*bool limitFLZblock, */out M4ArchiveRecord[] result, out DateTime nextRecord)
        {
            SelectDeviceAndChannel(mtr, nt);

            from = restrictTime(from);  //год кодируется у FDV в запросах одним байтом, начиная с 2000 
            to = restrictTime(to);      //подгоняем
            if (to != DateTime.MinValue && from > to) {
                Log(CommsLogLevel.Warn, $"протокол M4 не поддерживает чтение в обратном порядке, запрос [{from} .. {to}]");
                //throw new ArgumentException("существующая реализация протокола M4 не поддерживает чтение в обратном порядке");
                result = new M4ArchiveRecord[0];
                nextRecord = DateTime.MinValue;
                return null;
            }

            List<byte> lb = new List<byte>();

            lb.Add(0x04);       //octet string
            lb.Add(0x05);       //length
            lb.Add((byte)(partition & 0xFF));      //раздел(отсчет) L 
            lb.Add((byte)(partition >> 8));        //раздел(отсчет) H 
            lb.Add(channel);

            byte compFlags_archId = (byte)archiveKind;
            if (mtr.SupportsFLZ) {
                //compFlags_archId |= (byte)(limitFLZblock ? M4.CompressionType.FLZLimitedLength : M4.CompressionType.FLZ);    //сжатие в СПТ944 работает с ошибками
                compFlags_archId |= (byte)CompressionType.FLZLimitedLength;  //пока в 944 не починят сжатие - так
            }

            lb.Add(compFlags_archId);  //опции сжатия и код архива

            if ((uint)numValues > 0xFF)
                numValues = 0xFF;
            lb.Add((byte)numValues);  //максимальное кол-во записей в ответе

            //обход ошибки в СПТ943r3, из за которой может не выдаваться запись при указании даты в запросе месячного архива
            bool t943M_hack = mtr is TSPT943rev3 && archiveKind == M4ArchiveId.Mon;

            appendDateTag(lb, from, t943M_hack);
            if (to != DateTime.MinValue)
                appendDateTag(lb, to, t943M_hack);
            
            string logDateFormat = "dd-MM-yyyy HH:mm:ss";
            
            string sFrom = from.ToString(logDateFormat);
            string sTo = to == DateTime.MinValue ? "[null]" : to.ToString(logDateFormat);
            
            Log(CommsLogLevel.Trace, $"M4 запрос архива: part={partition}, ar={archiveKind}, ch={channel}, from={sFrom}, to={sTo}, maxCnt={numValues}");
            M4Packet p = DoM4Request(nt, M4Opcode.ReadArchive, lb.ToArray(), pktId);
            lb.Clear();
                         
            result = parseArchivePacket(p, out nextRecord);
            Log(CommsLogLevel.Trace, $"M4 ответ: {result.Length} записей, указатель:{nextRecord}");

            return p;
        }

        public static M4ArchiveRecord[] parseArchivePacket(M4Packet p, out DateTime nextRecord)
        {
            if (!p.Extended || p.FunctionCode != M4Opcode.ReadArchive) 
                throw new ArgumentException("некорректный пакет");

            List<M4ArchiveRecord> lr = new List<M4ArchiveRecord>();

            nextRecord = DateTime.MinValue;

            object oFirstTag;   //can be 0x49 (datetime stamp) or 0x04 (FLZ compressed records)
            int zLen = Logika4M.ParseTag(p.Data, 0, out oFirstTag);
            byte[] decompData;
            if (oFirstTag is byte[]) { //compressed payload
                int tailLength = p.Data.Length - zLen;                
                byte[] decompRecords;

                byte[] buf = oFirstTag as byte[];
                decompRecords = FLZ.decompress(buf, 0, buf.Length);

                decompData = new byte[decompRecords.Length + tailLength];
                Array.Copy(decompRecords, decompData, decompRecords.Length);
                Array.Copy(p.Data, zLen, decompData, decompRecords.Length, tailLength);                
            } else
                decompData = p.Data;    //plain data (non-compressed)

            int tp = 0;
            while (tp < decompData.Length) {

                object oTime;       //0x49 tag (sync archives : shortened datetime, async archives: full datetime)
                tp += Logika4M.ParseTag(decompData, tp, out oTime);
                                
                int lenLen = Logika4M.GetTagLength(decompData, tp + 1, out int recLen);

                if (recLen == 0) { //empty tag indicates the end of packet
                    nextRecord = Convert.ToDateTime(oTime);
                    break;
                }

                M4ArchiveRecord r = new M4ArchiveRecord();

                r.intervalMark = DateTime.SpecifyKind(Convert.ToDateTime(oTime), DateTimeKind.Local);

                if (decompData[tp] == 0x30) {   //sync archive
                    tp += 1 + lenLen;

                    int st = tp;

                    List<object> lo = new List<object>();
                    while ((tp - st) < recLen) {
                        object o;
                        tp += Logika4M.ParseTag(decompData, tp, out o);
                        lo.Add(o);
                    }

                    if (lo[0] is string && lo[1] is string && lo[0].ToString().Length == 8 && lo[1].ToString().Length == 8) {
                        string[] ta = (lo[0] as string).Split('-', ':');
                        string[] da = (lo[1] as string).Split('-');
                        r.dt = new DateTime(2000 + Convert.ToByte(da[2]), Convert.ToByte(da[1]), Convert.ToByte(da[0]), Convert.ToByte(ta[0]), Convert.ToByte(ta[1]), Convert.ToByte(ta[2]), DateTimeKind.Local);
                        lo.RemoveRange(0, 2);   //remove t, d from fields.
                    } else {
                        r.dt = DateTime.MinValue;   //в записи нет полной метки времени   
                    }
                    r.values = lo.ToArray();
                } else {            //async archive
                    object o;
                    tp += Logika4M.ParseTag(decompData, tp, out o);
                    r.dt = r.intervalMark;
                    r.values = new object[] { o };
                }

                lr.Add(r);
            }

            if (nextRecord != DateTime.MinValue && lr.Count > 0 && nextRecord <= lr[lr.Count - 1].dt) {
                throw new ECommException(ExcSeverity.Stop, CommError.Unspecified, "зацикливание датировки архивных записей");
                //nextRecord = lr[lr.Count - 1].dt.AddMilliseconds(1);    //обход ошибок в приборах когда выдается некорректный указатель на следующую запись
                //в СПГ742 запрос с добавленной миллисекундой приводит к рестарту прибора (таймаут запроса)
            }
            
            return lr.ToArray();
        }
        
        //---------------------------------------------------------------------------------------------
        #endregion
    }

    public class BusState
    {
        public byte? activeNT = null;
        public Logika4 meter;
        public byte? selectedTV;    //имеет значение только для 4L и только для записи параметров (тк запросы архивов 4L не поддерживаются)
        public byte[] devReply;    //ответ прибора на запрос сеанса
        public BaudRate? initialBaudRate;
        public BaudRate? currentBaudRate;
        public DateTime lastIOTime;
        public void Reset()
        {
            meter = null;
            activeNT = null;
            selectedTV = null;
            devReply = null;
            initialBaudRate = null;
            currentBaudRate = null;
            lastIOTime = DateTime.MinValue;
        }
        public BusState()
        {
            Reset();
        }
    }
}
