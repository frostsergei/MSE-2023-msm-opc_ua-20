using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace Logika.Comms
{

    //---------------------------------------------------------------------------------------------
    public enum ExcSeverity
    {
        Error,  //resumable
        Reset,  //connection should be re-established
        Stop,   //cannection cannot be re-established, bus should leave stopped
        WaitRadius,   //radius - connection to server is ok, but bus is temporarily unavailable (adapter not connected to srv)
    };

    public enum CommError
    {
        [Description("таймаут")]
        Timeout,
        [Description("ошибка CRC")]
        Checksum,   
        [Description("нет соединения")]
        NotConnected,   //
        [Description("ошибка")]
        SystemError,
        [Description("?")]
        Unspecified,
    };

    public class ECommException : System.Exception
    {
        public readonly ExcSeverity Severity;
        public readonly CommError Reason;
        public readonly string ExtendedInfo;

        public ECommException(ExcSeverity s, CommError r)
            : base(GetEnumDescription(r)) 
        {
                Severity = s;
                Reason = r;            
        }

        public ECommException(ExcSeverity s, CommError r, string msg)
            : base(msg)
        {
            Severity = s;
            Reason = r;
        }

        public ECommException(ExcSeverity s, CommError r, string msg, string extInfo)
        : base(msg)
        {
            Severity = s;
            Reason = r;
            ExtendedInfo = extInfo; //череp это поле путешествуют логи TAPI при неудачных попытках соединиться через модем
        }

        static string GetEnumDescription(Enum ct)
        {
            Type type = ct.GetType();
            MemberInfo[] memInfo = type.GetMember(ct.ToString());
            if (memInfo != null && memInfo.Length > 0) {
                object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attrs != null && attrs.Length > 0)
                    return ((DescriptionAttribute)attrs[0]).Description;
            }
            return ct.ToString();
        }
    }

}