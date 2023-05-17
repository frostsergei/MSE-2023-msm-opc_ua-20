using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.M4
{
    public enum ErrorCode : byte
    {
        [Description("нарушение структуры запроса")]
        BadRequest = 0,      
        [Description("защита от записи")]
        WriteProtected = 1,      
        [Description("недопустимое значение")]
        ArgumentError  = 2,      
    }
}
