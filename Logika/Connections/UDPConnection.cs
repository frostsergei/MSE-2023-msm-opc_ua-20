using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Logika.Utils.Collections;

namespace Logika.Comms.Connections
{
	public class UDPConnection : NetConnection
	{
        UdpClient uc;
        ByteQueue inQue = new ByteQueue(65535);

        public UDPConnection(int readTimeout, /*WaitHandle cancelEvent, */string host, ushort port)
            : base(readTimeout, /*cancelEvent, */host, port)
        {
        }

        public override void Dispose(bool disposing)
        {
            if (disposing) {
                if (uc != null) {
                    uc.Close();
                    uc = null;
                }
            }
        }

        //----------------------------------------------------------------------------------------
        protected override bool isConflictingWith(Connection Target)
        {
            if (!(Target is UDPConnection))
                return false;
            UDPConnection TarCon = Target as UDPConnection;
            return (string.Equals(TarCon.mSrvHostName, mSrvHostName) && TarCon.mSrvPort == mSrvPort);
		}
        
        protected override void InternalWrite(byte[] buf, int start, int len)
        {            
            uc.Client.Send(buf, start, len, SocketFlags.None);
        }

        protected override void onSetReadTimeout(int newTimeout)
        {            
            if (uc != null)
                uc.Client.ReceiveTimeout = newTimeout;
        }

        protected override void InternalOpen(out string connectDetails)            
        {
            connectDetails = null;
            uc = new UdpClient();
            uc.Client.ReceiveTimeout = this.ReadTimeout;
            
            try {
                uc.Connect(mSrvHostName, mSrvPort);

            } catch (SocketException se) {
                try {
                    InternalClose();
                } catch { }
                uc = null;
                if (se.SocketErrorCode == SocketError.HostNotFound)
                    throw new ECommException(ExcSeverity.Stop, CommError.SystemError, se.Message);

                throw new ECommException(ExcSeverity.Reset, CommError.SystemError, se.Message);
            }
        }

        IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Any, 0);
        protected override int InternalRead(byte[] buf, int start, int maxLength)
        {
            int ptr = start;

            int nRead = inQue.Dequeue(buf, start, maxLength);
            ptr += nRead;

            if (nRead < maxLength) {

                if (uc.Client.Poll(ReadTimeout * 1000, SelectMode.SelectRead) == false)
                    throw new ECommException(ExcSeverity.Error, CommError.Timeout);

                byte[] dgram = uc.Receive(ref ipEndpoint);
                if (dgram.Length > 0) {
                    inQue.Enqueue(dgram, 0, dgram.Length);

                    nRead += inQue.Dequeue(buf, ptr, maxLength-nRead);                                        
                }
            }

            return nRead;
        }

        protected override void InternalClose()
        {
            if (uc != null) {
                uc.Close();
                uc = null;
            }
        }

        protected override void InternalPurgeComms(PurgeFlags what)
        {
            while (uc!=null && uc.Available>0) {
                uc.Receive(ref ipEndpoint);
            }
            
        }
    }
}
