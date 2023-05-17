using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Logika.Comms.Connections
{
    public abstract class NetConnection : Connection
    {
        protected string mSrvHostName;
        protected UInt16 mSrvPort;


        public NetConnection(int readTimeout, string host, ushort port)
            : base(host + ":" + port, readTimeout)
        {
            mSrvHostName = host;
            mSrvPort = port;
        }               
    }
}
