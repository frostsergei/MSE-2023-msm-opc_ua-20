using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Comms
{
    public enum CommsLogLevel : int      //also used by OPC server
    {
        Trace = 0, 
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5,
    }
}
