using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Logika.Utils
{
    public class Conversions
    {
        public static byte? strToLimitedByte(string value, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            int result;
            if (!int.TryParse(value, out result))
                return null;
            if (result < min || result > max)
                return null;
            return (byte)result;
        }
        /*
                static Dictionary<char, char> weirdMatchesDict = new Dictionary<char, char>() {  //RUS -> LAT
                    { 'А', 'A' },
                    { 'В', 'B' },
                    { 'С', 'C' },
                    { 'Е', 'E' },
                    { 'Н', 'H' },
                    { 'К', 'K' },
                    { 'М', 'M' },
                    { 'О', 'O' },
                    { 'Р', 'P' },
                    { 'Т', 'T' },
                    { 'Х', 'X' },

                    { 'а', 'a' },
                    { 'с', 'c' },
                    { 'е', 'e' },
                    { 'о', 'o' },
                    { 'р', 'p' },
                    { 'х', 'x' },
                    { 'у', 'y' },
                };

                public static string getLatinizedString(string str)
                {
                    if (str == null)
                        return null;
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < str.Length; i++) {
                        char rc;
                        if (weirdMatchesDict.TryGetValue(str[i], out rc))
                            sb.Append(rc);
                        else
                            sb.Append(str[i]);
                    }
                    return sb.ToString();
                }
        */
        static readonly char[] sr = { 'А', 'В', 'Е', 'К', 'М', 'Н', 'О', 'Р', 'С', 'Т', 'Х', 'а', 'е', 'о', 'р', 'с', 'у', 'х' };
        static readonly char[] sl = { 'A', 'B', 'E', 'K', 'M', 'H', 'O', 'P', 'C', 'T', 'X', 'a', 'e', 'o', 'p', 'c', 'y', 'x' };
        public static string RusStringToStableAlphabet(string s)
        {
            if (s == null)
                return null;

            char[] res = s.ToCharArray();

            for (int i = 0; i < res.Length; i++) {
                int ci = Array.IndexOf(sr, res[i]);
                if (ci >= 0)
                    res[i] = sl[ci];
            }
            return new string(res);
        }

        public static bool StringsLookEqual(string a, string b)
        {
            //return string.Equals(getLatinizedString(a), getLatinizedString(b));
            return string.Equals(RusStringToStableAlphabet(a), RusStringToStableAlphabet(b));
        }

        public static string normalizeEu(string eu)
        {
            if (eu == "'C" || eu == "'С")  // Latin/Cyrillic
                eu = "°C";  //Latin
            return eu;
        }

        public static string GetEnumDescription(Enum ct)
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

        public static string ArrayToString<T>(IEnumerable<T> ba, string delimiter)
        {
            StringBuilder fsb = new StringBuilder();
            foreach (T b in ba)
                fsb.AppendFormat("{0}{1}", Convert.ToString(b), delimiter);
            if (fsb.Length > 0)
                fsb.Remove(fsb.Length - delimiter.Length, delimiter.Length);
            return fsb.ToString();
        }

        public static string ROT_N(string text, int n)
        {
            if (text == null)
                return null;
            byte[] bta = Encoding.ASCII.GetBytes(text);

            for (int i = 0; i < bta.Length; i++) {
                if (bta[i] < ' ' || bta[i] > 127)
                    throw new Exception("недопустимый символ в строке сообщения");
                int rc;
                rc = bta[i] + n;
                if (n > 0 && rc > 127)
                    rc = rc - 127 + ' ';
                if (n < 0 && rc < ' ')
                    rc = rc + 127 - ' ';

                bta[i] = (byte)rc;
            }
            return Encoding.ASCII.GetString(bta);
        }

        public static byte[] StructureToByteArray(object obj)
        {
            int sz = Marshal.SizeOf(obj);
            byte[] b = new byte[sz];
            IntPtr ptr = Marshal.AllocHGlobal(sz);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, b, 0, sz);
            Marshal.FreeHGlobal(ptr);
            return b;
        }

        public static T ByteArrayToStructure<T>(byte[] data)
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, 0, ptr, size);

            T result = (T)Marshal.PtrToStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);

            return result;
        }
    }
}
