using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Logika.Utils
{
    public static class EnumDescription
    {
        public static string GetDescription(this Enum ct)
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
