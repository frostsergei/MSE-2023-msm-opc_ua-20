using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using Logika.Comms.Connections;
using Logika.Comms.Protocols.M4;
using Logika.Comms.Protocols.SPBus;
using Logika.Meters;
using static Logika.Comms.Protocols.M4.M4Protocol;

namespace Logika.Comms.Protocols
{
    public abstract class Protocol
    {
        public delegate void ProtocolEventDelegate(ProtoEvent eventType);
        public static readonly DateTime LGK_ERA_START = new DateTime(2000, 01, 1);   //should not request any data before this date

        Connection cn;
        public Connection connection {
            get { return cn; }
            set {
                if (value != null) {
                    value.busTrackerResetEvent += ResetInternalBusState;
                } else if (cn != null)
                    cn.busTrackerResetEvent -= ResetInternalBusState;
                cn = value;
            }
        }

        CancellationTokenSource cts;
        protected CancellationToken cancellationToken;

        public event ProtocolEventDelegate OnEvent;

        public enum ProtoEvent
        {
            packetTransmitted,
            packetReceived,

            rxTimeout,
            rxCrcError,
            //rxLatePacket,
            genericError,
        }

        internal void Log(CommsLogLevel level, string msg, Exception exc = null)
        {
            cn?.Log(level, msg, exc);
        }

        public int packetsSent { get; private set; }
        public int packetsRcvd { get; private set; }
        public int rxTimeouts { get; private set; }
        public int rxCRCs { get; private set; }
        public int rxLatePkts { get; private set; }
        public int genErrs { get; private set; }

        public abstract void ResetInternalBusState();

        public void Reset()
        {
            packetsRcvd = 0;
            packetsSent = 0;
            rxTimeouts = 0;
            rxCRCs = 0;
            rxLatePkts = 0;
            genErrs = 0;

            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
            
            ResetInternalBusState();
        }

        protected void reportProtoEvent(ProtoEvent evType)
        {
            try {
                switch (evType) {
                    case ProtoEvent.packetReceived: packetsRcvd++; break;
                    case ProtoEvent.packetTransmitted: packetsSent++; break;
                    case ProtoEvent.rxTimeout: rxTimeouts++; break;
                    case ProtoEvent.rxCrcError: rxCRCs++; break;
                    //case ProtoEvent.rxLatePacket:
                    case ProtoEvent.genericError:
                    default:
                        genErrs++;
                        break;
                }
                OnEvent?.Invoke(evType);
            } catch {
            }
        }

        public Protocol()
        {
            Reset();
        }

        protected abstract void internalCloseCommSession(byte? srcNt, byte? dstNt);
        
        public void CloseCommSession(byte? srcNt, byte? dstNt)
        {            
            try {
                if (connection != null && connection.State == Connections.ConnectionState.Connected) {
                    internalCloseCommSession(srcNt, dstNt);
                }
            } catch {
            } 
        }
    
        public void Cancel()
        {
            try {
                cts.Cancel();
                if (connection != null) {
                    connection.Close();
                }
            } catch { }
        }
        
        public bool IsCancelled{
            get { return cts.IsCancellationRequested; }
        }

        protected bool Wait(int ms)  //should return true if cancelled
        {
            //if (cancelHandle != null)
            //    return cancelHandle.WaitOne(ms);
            if (cancellationToken != null) {
                if (!cancellationToken.IsCancellationRequested)
                    cancellationToken.WaitHandle.WaitOne(ms);                
               return cancellationToken.IsCancellationRequested;
            } else {
                Thread.Sleep(ms);
                return false;
            }
        }

        static Meter detectX6(SPBusProtocol bus, out byte[] dump, out string model)
                {
            SPBus.SPBusPacket req = SPBus.SPBusPacket.BuildReadTagsPacket(null, null, "", new int[] { 0 }, new int[] { 99 });
                    byte[] reqBytes = req.AsByteArray();
                    bus.connection.Write(reqBytes, 0, reqBytes.Length);
                    dump = bus.ReadPacket6();
            SPBus.SPBusPacket pkt = SPBus.SPBusPacket.Parse(dump, 0, SPBus.SPBusPacket.ParseFlags.None);
                    if (pkt.Records.Count != 2)
                        throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "некорректная структура пакета");

                    string p099 = string.Copy(pkt.Records[1].Fields[0]);
                    return SPBusProtocol.MeterTypeFromResponse(p099, out model);
                }

        static Meter detectM4(M4Protocol bus, out byte[] dump, out string model)
        {
            model = "";
            M4.M4Packet reply = bus.Handshake(0xFF, 0, false);
            dump = reply.getDump();
            Meter mtr = Logika4.MeterTypeFromResponse(reply.Data[0], reply.Data[1], reply.Data[2]);

            if (mtr == Meter.SPT942) {
                byte[] modelBytes = bus.ReadFlashBytes(mtr as Logika4L, 0xFF, 0x30, 1);
                model = new string((char)modelBytes[0], 1);
            }

            return mtr;
        }

# if false //seems that _fast version is stable enough

        public static Meter AutodetectSPT_Stable(Connection conn, BaudRate fixedBaudRate, bool tryM4, bool trySPBus, bool tryMEK, out byte[] dump, out int devBaudRate, out string model)
        {
            Meter m = null;
            model = "";

            M4Protocol bus4 = new M4Protocol();
            SPBusProtocol bus6 = new SPBusProtocol(true);

            bus4.connection = conn;
            bus6.connection = conn;

            bool canChangeBaudrate = conn is SerialConnection;
            int detectedBaud = 0;

            int savedTimeout = conn.ReadTimeout;
            conn.ReadTimeout = 500;
            try {
                int[] baudRateList = canChangeBaudrate ? new int[] { 2400, 57600, 4800, 19200, 9600, 38400, 115200 } : new int[] { 0 };
                if (fixedBaudRate != BaudRate.Undefined)
                    baudRateList = new int[] { (int)fixedBaudRate };

                for (int i = 0; i < baudRateList.Length; i++) {

                    if (canChangeBaudrate) {
                        ((SerialConnection)conn).SetParams((BaudRate)baudRateList[i], 8, StopBits.One, Parity.None);
                        detectedBaud = baudRateList[i];
                        System.Diagnostics.Debug.Print("trying {0} bps...", detectedBaud);
                    }

                    if (tryM4) {
                        try {
                            m = detectM4(bus4, out dump, out model);

                            devBaudRate = detectedBaud;
                            return m;

                        } catch (System.Exception) {
                        }
                    }

                    if (trySPBus) { //у новых x6 приборов на оптическом интерфейсе сразу протокол СПСеть
                        try {
                            m = detectX6(bus6, out dump, out model);
                            devBaudRate = detectedBaud;
                            return m;
                        } catch (System.Exception) {
                        }
                    }
                }

                if (tryMEK && trySPBus && canChangeBaudrate) {
                    conn.ReadTimeout = 1000;
                    try {
                        detectedBaud = bus6.MEKHandshake();
                        if (detectedBaud > 0) {
                            devBaudRate = detectedBaud;
                            return detectX6(bus6, out dump, out model);
                        }
                    } catch (Exception) {
                    }
                }

            } finally {
                conn.ReadTimeout = savedTimeout;
            }

            devBaudRate = 0;
            dump = null;
            return null;
        }
#endif
        
        static Meter detectResponse(Connection c, out byte[] dump, out string model, out bool rxDetected)
        {
            rxDetected = false;
            dump = null;
            model = null;

            byte[] buf = new byte[64];
            DateTime ReadStart = DateTime.Now;
            try {
        waitFrameStart:
                //read in stream until frame start found or timeout elapses
                while (true) {                    
                    c.Read(buf, 0, 1);

                    rxDetected = true;
                    if (buf[0] == 0x10)
                        break;
                   
                    TimeSpan Elapsed = DateTime.Now - ReadStart;
                    if (Elapsed.TotalMilliseconds > c.ReadTimeout) {

                        throw new ECommException(ExcSeverity.Error, CommError.Timeout);
                    }
                }
                
                c.Read(buf, 1, 5);  //L4 приборы могут среагировать на пакет спсети двумя пакетами об ошибке "разрушенный запрос", пропускаем
                if (buf[2]==(byte)M4Opcode.Error && buf[3]==(byte)M4.ErrorCode.BadRequest && buf[5]==M4Protocol.FRAME_END) {  
                    goto waitFrameStart;
                }
                
                //read 01 for spbus, 0xFF/destNT for M4
                if (buf[1] == 0x01 && buf[2]!=(byte)M4Opcode.Handshake) {  //SPbus  
                    int p = 6;
                    int iETX = -1;
                    do {
                        c.Read(buf, p, 1);
                        p++;
                        if (iETX == -1)
                            iETX = SPBus.SPBusPacket.FindMarker(buf, 2, p, 0x10, 0x03);
                    } while (iETX < 0 || (iETX > 0 && p < iETX + 4));
                    ushort crc = 0;
                    Protocol.CRC16(ref crc, buf, 0/*iSTX*/ + 2, iETX + 2);       // проверяем CRC принятого пакета
                    if (crc != 0)
                        throw new ECommException(ExcSeverity.Error, CommError.Checksum);

                    dump = new byte[iETX + 4];
                    Array.Copy(buf, 0, dump, 0, dump.Length);
                    var pkt = SPBus.SPBusPacket.Parse(buf, 0);
                    if (pkt.Records.Count != 2)
                        throw new ECommException(ExcSeverity.Error, CommError.Unspecified, "некорректная структура пакета");

                    string p099 = string.Copy(pkt.Records[1].Fields[0]);
                    return SPBusProtocol.MeterTypeFromResponse(p099, out model);

                } else if (buf[2] == (byte)M4Opcode.Handshake) {    //M4
                    c.Read(buf, 6, 2);  //read the rest of "hello" pkt
                    int cs = buf[6];
                    byte calculatedCheck = (byte)~(~Logika4.Checksum8(buf, 1, 5));
                    if (cs != calculatedCheck)
                        throw new ECommException(ExcSeverity.Error, CommError.Checksum);
                    Meter m = Logika4.MeterTypeFromResponse(buf[3], buf[4], buf[5]);
                    if (m == Meter.SPT942) {
                        M4Protocol bus4 = new M4Protocol();
                        bus4.connection = c;
                        byte[] modelBytes = bus4.ReadFlashBytes(m as Logika4L, 0xFF, 0x30, 1);
                        model = new string((char)modelBytes[0], 1);
                    } else
                        model = "";
                    dump = new byte[8];
                    Array.Copy(buf, 0, dump, 0, dump.Length);
                    return m;

                } else
                    goto waitFrameStart;

            } catch (Exception e){
                System.Diagnostics.Debug.Print(e.Message);
            }
            return null;
        }

        //it's not fast anymore, thanks to magnificent ADS99
        public static Meter AutodetectSPT(Connection conn, BaudRate fixedBaudRate, int waitTimeout, bool tryM4, bool trySPBus, bool tryMEK, byte? srcAddr, byte? dstAddr, out byte[] dump, out int devBaudRate, out string model)
        {
            Meter m = null;
            model = "";

            bool canChangeBaudrate = (conn is SerialConnection) ? ((SerialConnection)conn).CanChangeBaudrate : false;
            int currentBaudRate = 0;            

        restartDetect:
            int savedTimeout = conn.ReadTimeout;
            
            conn.ReadTimeout = waitTimeout;
            
            try {
                int[] baudRateList;
                if (fixedBaudRate > 0 && canChangeBaudrate) {
                    baudRateList = new int[] { (int)fixedBaudRate };
                    tryMEK = false;     //если скорость фиксирована, пробовать MЭK нет смысла
                } else
                    baudRateList = canChangeBaudrate ? new int[] { 2400, 57600, 4800, 19200, 9600, 38400, 115200 } : new int[] { 0 };

                byte[] X6Request = SPBusProtocol.genRawHandshake(srcAddr, dstAddr);
                byte[] M4Request = M4Protocol.genRawHandshake(dstAddr);

                for (int i = 0; i < baudRateList.Length; i++) {
                    if (canChangeBaudrate) {
                        ((SerialConnection)conn).SetParams((BaudRate)baudRateList[i], 8, StopBits.Two, Parity.None);

                        currentBaudRate = baudRateList[i];
                        if (currentBaudRate != 0)
                            System.Diagnostics.Debug.Print("trying {0} bps...", currentBaudRate);
                    }                    

                    devBaudRate = currentBaudRate;
                    
                    try {
                        if (trySPBus) {
                            conn.Write(X6Request, 0, X6Request.Length);
                        }

                        if (trySPBus && tryM4) //делаем паузу между запросами в разных протоколах, без неё АДС99 плохо транслирует данные 
                            Thread.Sleep(100);   //также .NET UDP сокеты иногда втихую не посылают быстро идущие подряд датаграммы 8()                        

                        if (tryM4) {                            
                            conn.Write(M4Protocol.WAKEUP_SEQUENCE, 0, M4Protocol.WAKEUP_SEQUENCE.Length);
                            Thread.Sleep(M4Protocol.WAKE_SESSION_DELAY);    //фактически, эта пауза нужна только для АДС99 в режиме TCP сервер, без неё он глючит
                            conn.Write(M4Request, 0, M4Request.Length);
                        }
                        
                        Thread.Sleep(50);                        

                        m = detectResponse(conn, out dump, out model, out bool receivedSomething);

                        if (m != null)
                            return m;

                    } catch {
                    }

                    conn.PurgeComms(PurgeFlags.RX | PurgeFlags.TX);
                }

                if (tryMEK && trySPBus && canChangeBaudrate) {
                    conn.ReadTimeout = 1000;
                    SPBusProtocol bus6 = new SPBusProtocol(true);
                    bus6.connection = conn;
                    try {
                        currentBaudRate = bus6.MEKHandshake();
                        if (currentBaudRate > 0) {
                            devBaudRate = currentBaudRate;
                            tryM4 = false;
                            tryMEK = false;
                            canChangeBaudrate = false;
                            goto restartDetect;
                        }
                    } catch (Exception) {
                    }
                }

            } finally {
                conn.ReadTimeout = savedTimeout;
            }

            devBaudRate = 0;
            dump = null;
            return null;
        }

        public static int GetDefaultTimeout(BusProtocolType proto, ConnectionType connType)   //ms
        {
            switch (connType) {
                case ConnectionType.Offline:
                case ConnectionType.Serial:
                    if (proto == BusProtocolType.SPbus)
                        return 15000;                    
                    return 5000;    //новые M4 приборы могут за 3 секунды не успеть ответить на запрос архивов (прогресс!)

                case ConnectionType.Modem:
                    if (proto == BusProtocolType.SPbus)
                        return 15000;
                    return 10000;

                case ConnectionType.UDP:
                    return 10000;

                case ConnectionType.TCP:
                case ConnectionType.Radius:
                default:
                    return 15000;   //legacy prlg: 25000
            }
        }

        //CRC16 implementation used both in SPBus and M4 
        public static void CRC16(ref ushort crc, byte[] buf, int offset, int len)
        {
            while (len-- > 0) {
                crc ^= (ushort)(buf[offset++] << 8);
                for (int j = 0; j < 8; j++) {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
        }

        public abstract Meter GetMeterType(byte? srcNT, byte? dstNT, out object xtraInfo);
        public abstract DateTime GetDeviceClock(Meter meter, byte? src, byte? dst);
        public abstract void UpdateTags(byte? src, byte? dst, DataTag[] tags);

        public abstract IntervalArchive ReadIntervalArchiveDef(Meter m, byte? src, byte? dst, ArchiveType arType, out object state);

        /// <summary>
        /// блочное чтение интервального архива, true = чтение не завершено, продолжать вызовы
        /// </summary>               
        public abstract bool ReadIntervalArchive(Meter m, byte? src, byte? dst, IntervalArchive ar, DateTime start, DateTime end, ref object state, out float progress);
        public abstract bool ReadServiceArchive(Meter m, byte? src, byte? dst, ServiceArchive ar, DateTime start, DateTime end, ref object state, out float progress);

    }
}
