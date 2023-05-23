using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;


namespace Logika.Comms.Connections
{
    public enum ConnectionType : int    //тип подключения
    {
        [Description("Отключено")]
        Offline = -1,
        [Description("COM порт")]
        Serial = 0,
        [Description("Модем")]
        Modem = 1,
        [Description("TCP")]
        TCP = 2,
        [Description("UDP")]
        UDP = 3,
        [Description("Радиус")]
        Radius = 4,
    }

    [Flags]
    public enum PurgeFlags
    {
        RX,
        TX
    }

    public enum ConnectionState : int
	{		
        NotConnected = 0,       //Core.Runtime.Bus.BusState в OPC сервере должно соответствовать этим числовым значениям 
        Connecting = 1,         
		Connected = 2,          
		Disconnecting = 3,
    }

    abstract public class Connection : IDisposable
    {        
        public readonly string address;

        protected ManualResetEvent closingEvent;

        public event Action busTrackerResetEvent;
        protected void resetBusStateTracker()
        {
            System.Diagnostics.Debug.Print("resetting bus state");
            busTrackerResetEvent.Invoke();
        }
        
        public abstract void Dispose(bool Disposing);
        public void Dispose()
        {            
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected int txByteCnt;
        public int TxByteCount
        {
            get { return txByteCnt; }
        }
        protected int rxByteCnt;
        public int RxByteCount
        {
            get { return rxByteCnt; }
        }

        public event EventHandler OnBeforeDisconnect;
        public event EventHandler OnAfterConnect;
        public event EventHandler OnConnectRequired;

        public enum MonitorEventType
        {            
            [Description("канал связи открыт")]
            Open,
            [Description("канал связи закрыт")]
            Close,
            [Description("изменение свойств канала связи")]
            ChannelPropertiesChanged,

            [Description("данные отправлены")]
            Tx,
            [Description("данные приняты")]
            Rx,
            
            [Description("сброс буферов приёма/передачи")]
            Purge,
            
            [Description("ошибка")]
            Error,
            
            [Description("?")]
            Undefined,
        }

        public class MonitorEvent {
            public DateTime timestamp;
            public MonitorEventType evtType = MonitorEventType.Undefined;
            public string address;
            public byte[] data;
            public string info;
            
            public MonitorEvent()
            {                
            }

            public MonitorEvent Clone()
            {
                var me = new MonitorEvent() { timestamp = this.timestamp, evtType = this.evtType, address = this.address, info = this.info };
                if (data != null) {
                    me.data = new byte[data.Length];
                    this.data.CopyTo(me.data, 0);
                }
                return me;
            }

            public override string ToString()
            {
                return evtType.ToString() + (data!=null ? " " + data.Length + " b" : "");
            }
        }

        protected void Mon(MonitorEventType Event, byte[] Data, string Info)
        {
            Monitor.Instance.OnMonitorEvent(this, new MonitorEvent() { timestamp = DateTime.Now, evtType = Event, address = this.address, data = Data, info = Info });
        }

        public delegate void LogEventHandler(CommsLogLevel level, string message, Exception exc);

        public event LogEventHandler OnLogEvent;
        public void Log(CommsLogLevel level, string msg, Exception exc = null)
        {
            if (OnLogEvent != null) {
                try {
                    OnLogEvent(level, msg, exc);
                    System.Diagnostics.Debug.Print("LogMsg level {0}: {1} ", level, msg);
                } catch {
                }
            }
        }        

        void checkIfClosing()
        {
            if (closingEvent.WaitOne(0))
                throw new ECommException(ExcSeverity.Stop, CommError.NotConnected);
        }        

        //----------------------------------------------------------------------------------------
        private void checkIfConnected()
        {
            lock (this) {
                if (State == ConnectionState.NotConnected) 
                    OnConnectRequired?.Invoke(this, new EventArgs());
                
                if (State != ConnectionState.Connected)                 
                    throw new ECommException(ExcSeverity.Error, CommError.NotConnected);                
            }
        }

        private DateTime mLastRXTime;

        protected ConnectionState mState;
        
        protected abstract void InternalOpen(out string connectDetails);
        protected abstract void InternalClose();

        protected abstract int InternalRead(byte[] buf, int Start, int MaxLength);
        protected abstract void InternalWrite(byte[] buf, int Start, int nBytes);

        public virtual string ResourceName { get { return null; } }
        //protected abstract int InternalGetDataAvailable(); //returns length of data waiting in input buffer
        //virtual UInt32 GetOutputCount() = 0;	//returns length of data waiting in output buffer	
        
        //dont need that. when we want cancelling - we should call Close();
        //public CancellationToken cancellationToken;

        //----------------------------------------------------------------------------------------
        public Connection(string address, int readTimeout)
        {
            //this.owner = owner;
            this.address = address;
            
            this.closingEvent = new ManualResetEvent(false);
            this.ReadTimeout = readTimeout;            

            mState = ConnectionState.NotConnected;
            mLastRXTime = DateTime.MinValue;            
        }

        //----------------------------------------------------------------------------------------
        public void Open()
        {
            lock (this) {
                closingEvent.Reset();
                State = ConnectionState.Connecting;

                try {
                    string connstr = "установка соединения" + ((string.IsNullOrEmpty(address) ? "" : " с " + address));
                    if (ResourceName != address && !string.IsNullOrEmpty(ResourceName))
                        connstr += " (" + ResourceName + ")";
                    Log(CommsLogLevel.Info, connstr);

                    InternalOpen(out string connDetails);

                    State = ConnectionState.Connected;

                    Mon(MonitorEventType.Open, null, "соединение с '" + address + "' установлено");
                    Log(CommsLogLevel.Info, "соединение установлено" + (string.IsNullOrEmpty(connDetails) ? "" : " (" + connDetails + ")"));

                    if (OnAfterConnect != null) {
                        try {
                            OnAfterConnect(this, null);
                        } catch {
                        }
                    }

                } catch (Exception e) {

                    Log(CommsLogLevel.Error, "", e);
                    Mon(MonitorEventType.Error, null, e.Message);

                    //State = ConnectionState.NotConnected;     //should not be set here, should be set at Close()
                    throw;
                }
            }
        }

        //----------------------------------------------------------------------------------------
        public void Close()
        {
            closingEvent.Set();     //даём возможность закрыть соединение в т.ч и во время его открытия

            lock (this) {      
                if (State == ConnectionState.Connected) {
                    try {
                        State = ConnectionState.Disconnecting;

                        if (OnBeforeDisconnect != null) {
                            try {
                                OnBeforeDisconnect(this, null);
                            } catch {
                            }
                        }             
                        InternalClose();
                        Mon(MonitorEventType.Close, null, "соединение с '" + address + "' завершено");
                        Log(CommsLogLevel.Info, "соединение завершено");

                    } catch (Exception e) {
                        Log(CommsLogLevel.Warn, "ошибка при завершении соединения", e);
                        Mon(MonitorEventType.Error, null, "ошибка при отключении: " + e.Message);
                    }
                }
                State = ConnectionState.NotConnected;                
            }            
        }

        protected abstract void InternalPurgeComms(PurgeFlags what);

        public void PurgeComms(PurgeFlags what)
        {
            if (State == ConnectionState.Connected) {
                InternalPurgeComms(what);
                string sp = "# purge ";
                if (what.HasFlag(PurgeFlags.RX))
                    sp += "RX ";
                if (what.HasFlag(PurgeFlags.TX))
                    sp += "TX";
                
                Mon(MonitorEventType.Purge, null, sp);
            }
        }

        //----------------------------------------------------------------------------------------
        //StringBuilder rxLogAcc = new StringBuilder();                
        //public void FlushRxLog()
        //{
        //    if (rxLogAcc.Length > 0) {
        //        Log(LogLevel.Trace, string.Format("<-RX {0,-4}", rxLogAcc.Length / 3), rxLogAcc.ToString());
        //        rxLogAcc.Clear();
        //    }
        //}

        public int ReadAvailable(Byte[] buf, int Start, int MaxLength)
        {
            checkIfConnected();
            int nRead = 0;
            try {
                nRead = InternalRead(buf, Start, MaxLength);
                if (nRead > 0) {
                    rxByteCnt += nRead;
                    byte[] rr = new byte[nRead];
                    Array.Copy(buf, Start, rr, 0, nRead);
                    Mon(MonitorEventType.Rx, rr, null);
                }
            } catch (Exception e) {
                //FlushRxLog();
                Mon(MonitorEventType.Error, null, "! " + e.GetType().ToString() + " : " + e.Message);
                throw;
            }

            //for (int b = 0; b < nRead; b++)     //append received bytes to log
            //    rxLogAcc.AppendFormat("{0:X2} ", buf[Start + b]);

            mLastRXTime = DateTime.Now;

            return nRead;
        }
        
		public void Read(byte[] buf, int Start, int Length) 
		{
			int nRead = 0;

            while (nRead < Length) {
                checkIfClosing();
                nRead += ReadAvailable(buf, Start + nRead, Length - nRead);
            }
        }

        //----------------------------------------------------------------------------------------
        public void Write(byte[] buf, int Start, int nBytes) 
		{
            //FlushRxLog();   //for safety, log should normally be flushed by packet receivers in protocols

            checkIfConnected();
            checkIfClosing();
            try {
                InternalWrite(buf, Start, nBytes);
                txByteCnt += nBytes;
                if (nBytes > 0) {
                    byte[] wr = new byte[nBytes];
                    Array.Copy(buf, Start, wr, 0, nBytes);
                    Mon(MonitorEventType.Tx, wr, null);
                }
            } catch (Exception e) {
                Mon(MonitorEventType.Error, null, "!" + e.GetType().ToString() + " : " + e.Message);
                throw;
            }

            //StringBuilder sb = new StringBuilder();
            //for (int b = 0; b < nBytes; b++) 
            //    sb.AppendFormat("{0:X2} ", buf[Start+b]);
            //Log(LogLevel.Trace, string.Format("->TX {0,-4}", nBytes), sb.ToString());                        
		}

        public delegate void StateChangeDelegate(ConnectionState newState);
        public event StateChangeDelegate OnConnectionStateChange;
        public ConnectionState State 
		{
			get 
			{
				return mState;
			}
			set 
			{
                //if (value!=mState)
                //    Log(CommsLogLevel.Trace, string.Format("connection state change: {0} -> {1}", mState, value));
                mState = value;
                OnConnectionStateChange?.Invoke(value);
            }
		}        

        int mReadTimeout;

        public int ReadTimeout 
		{
			get 
			{
				return mReadTimeout;
			}
            set
            {
                mReadTimeout = value;
                onSetReadTimeout(value);
                Mon(MonitorEventType.ChannelPropertiesChanged, null, "@ ReadTimeout = " + value + " ms");
            }
        }

        protected virtual void onSetReadTimeout(int newTimeout)
        {                        
        }

        protected abstract bool isConflictingWith(Connection target);

        public bool ConflictsWith(Connection Target)
        {
            if (State == ConnectionState.NotConnected || !Target.GetType().Equals(this.GetType()))
                return false;
            return isConflictingWith(Target);
        }

		public DateTime LastRXTime 
		{
			get 
			{
				return mLastRXTime;
			}
		}

        public void ResetStatistics()
        {
            txByteCnt = 0;
            rxByteCnt = 0;
        }
    }
}
