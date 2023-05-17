using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;



namespace Logika.Comms.Connections
{    
#if false  //ordinary UDPConnection should work now

    public class UDPConnectionXamarin : NetConnection 
	{
        byte[] rcvBuf = new byte[65535];
        Queue<byte> inQue = new Queue<byte>(65535);

        public UDPConnectionXamarin(int readTimeout, /*WaitHandle cancelEvent, */string host, ushort port)
            : base(readTimeout, /*cancelEvent, */host, port)
        {
        }

        //----------------------------------------------------------------------------------------
        protected override Socket CreateSocket()
        {            
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.DontFragment = false;
            return s;
        }

        protected override int InternalRead(byte[] buf, int Start, int Length)
        {
            int ptr = Start;
            int nRead = 0;


            while (inQue.Count > 0 && nRead < Length) {
                buf[ptr++] = inQue.Dequeue();
                nRead++;
            }

            if (nRead < Length) {
                if (socket.Poll(ReadTimeout * 1000, SelectMode.SelectRead) == false)
                    throw new ECommException(ExcSeverity.Error, CommError.Timeout);

                SocketError errcode = new SocketError();
                if (socket.Available > 0) {
                    int nBytes = socket.Receive(rcvBuf, 0, socket.Available, SocketFlags.None, out errcode);

                    if (errcode != SocketError.Success)
                        throw new ECommException(ExcSeverity.Reset, CommError.SystemError, errcode.ToString());

                    for (int i = 0; i < nBytes; i++)
                        inQue.Enqueue(rcvBuf[i]);

                    while (inQue.Count > 0 && nRead < Length) {
                        buf[ptr++] = inQue.Dequeue();
                        nRead++;
                    }
                }
            }

            return nRead;
        }

        protected override void InternalPurgeComms(PurgeFlags flg)
        {
            base.InternalPurgeComms(flg);
            if (flg.HasFlag(PurgeFlags.RX))
                inQue.Clear();
        }

        //----------------------------------------------------------------------------------------
        protected override bool isConflictingWith(Connection Target)
        {
            UDPConnectionXamarin TarCon = Target as UDPConnectionXamarin;
            return (string.Equals(TarCon.mSrvHostName, mSrvHostName) && TarCon.mSrvPort == mSrvPort);
        }

    }
#endif
}
