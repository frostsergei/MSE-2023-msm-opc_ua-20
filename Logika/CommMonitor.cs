using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Logika.Comms.Connections;

namespace Logika.Comms
{

    public class Monitor
    {
        public static Monitor Instance { get; private set; }
        List<Connection> connectionList = new List<Connection>();

        public delegate void MonitorEventHandler(Connection sender, Connection.MonitorEvent mEvt);
        object mgrLock = new object();
        public event MonitorEventHandler MonitorEvent;

        static Monitor()
        {
            Instance = new Monitor();
        }

        private Monitor()   //singleton should be used only by .Instance
        {
        }

        public void OnMonitorEvent(Connection sender, Connection.MonitorEvent mEvt)
        {
            try {
                lock (mgrLock)
                    MonitorEvent?.Invoke(sender, mEvt);
            } catch {
            }
        }

        public void RegisterConnection(Connection c)
        {
            lock (mgrLock)
                connectionList.Add(c);
        }

        public void UnregisterConnection(Connection c)
        {
            lock (mgrLock)
                connectionList.Remove(c);
        }

        public Connection[] GetConnections()
        {
            lock (mgrLock)
                return connectionList.ToArray();
        }

        public void Reset()
        {
            lock (mgrLock) {
                MonitorEvent = null;
                connectionList.Clear();
            }
        }
    }

}
