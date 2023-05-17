using System;
using System.Text;

namespace Logika.Comms.Protocols.M4
{
    public class M4ArchiveRecord
    {
        public DateTime intervalMark;   // метка интервала (время без РД РЧ)
        public DateTime dt;             // метка времени записи (полная, с РД/РЧ)

        public object[] values;
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(dt.ToString("dd.MM.yy HH:mm:ss.fff"));
            sb.Append(" ");
            if (values.Length > 1)
                sb.Append("{ ");
            for (int i = 0; i < values.Length; i++) {
                if (values[i] != null)
                    sb.Append(values[i].ToString());
                else
                    sb.Append("null");
                if (i < values.Length - 1)
                    sb.Append(", ");
            }
            if (values.Length > 1)
                sb.Append("}");
            return sb.ToString();
        }

    }
}
