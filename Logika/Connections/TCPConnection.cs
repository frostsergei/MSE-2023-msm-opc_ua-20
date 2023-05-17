using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace Logika.Comms.Connections
{
	public class TCPConnection : NetConnection 
	{
        protected Socket socket;
        const int WSAETIMEDOUT = 10060;

        public TCPConnection(int readTimeout, /*WaitHandle cancelEvent, */string host, ushort port)
            : base(readTimeout, /*cancelEvent, */host, port)
        {
        }

        public override void Dispose(bool disposing)
        {
            if (disposing) {
                if (socket != null) {
                    socket.Close();
                    socket = null;
                }
            }
        }

        ManualResetEvent connectEnded = new ManualResetEvent(false);
        Exception connectException;


        protected override void InternalOpen(out string connectDetails)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Blocking = true;

            connectDetails = null;

            connectEnded.Reset();
            connectException = null;
            try {
                IAsyncResult connResult = socket.BeginConnect(mSrvHostName, mSrvPort, new AsyncCallback(onConnect), socket);

                //todo: заменить на WaitHandle.WaitAny(cancelEvent, connectEnded)
                bool timeout = !connectEnded.WaitOne(Math.Max(ReadTimeout, 15000));  //wait for connect for Timeout secs, but not less than 15s
                if (timeout) {
                    socket.Close();
                    socket = null;
                    throw new SocketException(WSAETIMEDOUT);
                } else {
                    if (connectException != null)
                        throw connectException;
                }

            } catch (SocketException se) {
                if (se.SocketErrorCode == SocketError.HostNotFound)
                    throw new ECommException(ExcSeverity.Stop, CommError.SystemError, se.Message);

                throw new ECommException(ExcSeverity.Reset, CommError.SystemError, se.Message);
            }
        }

        protected override void InternalClose()
        {
            socket?.Close();
            socket = null;
        }

        protected override void onSetReadTimeout(int newTimeout)
        {
            if (socket!=null)
                socket.ReceiveTimeout = newTimeout;
        }

        void onConnect(IAsyncResult iar)
        {
            try {
                socket?.EndConnect(iar);

                //throw new ECommException(ExcSeverity.Reset, ExcReason.SystemError, se.Message);
                //if (!socket.Connected)
                //    throw new ECommException(ExcSeverity.Reset, ExcReason.SystemError, "");

            }
            catch (Exception e) {
                connectException = e;
            }
            finally {
                connectEnded.Set();
            }
        }


		//----------------------------------------------------------------------------------------
        protected override bool isConflictingWith(Connection Target)
        {
            if (!(Target is TCPConnection))
                return false;
            TCPConnection TarCon = Target as TCPConnection;
            return (string.Equals(TarCon.mSrvHostName, mSrvHostName) && TarCon.mSrvPort == mSrvPort);
		}

        protected override int InternalRead(byte[] buf, int Start, int MaxLength)
        {
            if (socket.Poll(ReadTimeout * 1000, SelectMode.SelectRead) == false)
                throw new ECommException(ExcSeverity.Error, CommError.Timeout);

            if (this.State != ConnectionState.Connected || socket==null)    //prevent most of "object disposed" exceptions
                return 0;

            SocketError errcode = new SocketError();
            int avBytes = socket.Available;
            int nBytes = socket.Receive(buf, Start, MaxLength, SocketFlags.None, out errcode);
            if (nBytes == 0)
                throw new ECommException(ExcSeverity.Reset, CommError.SystemError, "соединение завершено удаленной стороной");

            if (errcode != SocketError.Success) {
                throw new ECommException(ExcSeverity.Reset, CommError.SystemError, errcode.ToString());
            }

            return nBytes;
        }

        protected override void InternalWrite(byte[] buf, int Start, int len)
        {
            SocketError errcode = new SocketError();

            socket.Send(buf, Start, len, SocketFlags.None, out errcode);
            if (errcode != SocketError.Success) {
                throw new ECommException(ExcSeverity.Reset, CommError.SystemError, errcode.ToString());
            }
        }

        protected override void InternalPurgeComms(PurgeFlags flg)
        {
            int nBytes;

            if (State != ConnectionState.Connected)
                return;

            if (flg.HasFlag(PurgeFlags.RX)) {
                while ((nBytes = /*DataAvailable*/socket.Available) != 0) {
                    byte[] mem = new byte[nBytes];
                    socket.Receive(mem);
                }
            }
            if (flg.HasFlag(PurgeFlags.TX)) {
                //no methods for aborting tcp tx
            }
        }

    }
}
