using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

//using System.ComponentModel;



namespace Logika.Comms.Connections
{
    
	public abstract class SerialConnection : Connection 
	{
        public virtual bool CanChangeBaudrate { get { return true; } }

        public SerialConnection(int readTimeout, /*WaitHandle cancelHandle, */string portName)
            : base(portName, readTimeout/*, cancelHandle*/)
        {
        }

        public abstract BaudRate BaudRate {
            get;
            set;
        }
        //----------------------------------------------------------------------------------------
        public abstract void SetStopBits(StopBits stopBits);

        //----------------------------------------------------------------------------------------
        public abstract void SetParams(BaudRate baudRate, int DataBits, StopBits StopBits, Parity Parity);
    }

    public enum BaudRate : int
    {
        [Description("?")]
        Undefined = 0,
        [Description("1200")]
        b1200 = 1200,
        [Description("2400")]
        b2400 = 2400,
        [Description("4800")]
        b4800 = 4800,
        [Description("9600")]
        b9600 = 9600,
        [Description("19200")]
        b19200 = 19200,
        [Description("38400")]
        b38400 = 38400,
        [Description("57600")]
        b57600 = 57600,
        [Description("115200")]
        b115200 = 115200
    }

    //values should match SetCommState API (for DCB)
    public enum StopBits
    {
        [Description("1")]
        One = 0,

        //[Description("1.5")]
        //OneAndHalf = 1,   //1.5

        [Description("2")]
        Two = 2, 

    }

    //values should match SetCommState API (for DCB)
    public enum Parity
    {
        None = 0, 
        Odd = 1,
        Even = 2,
    }
}
