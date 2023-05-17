using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Utils
{
    public static class RusGrammar
    {
        public static string RecordsWord(int cnt)
        {
            string suffix;
            int ce = cnt % 10;
            if ((cnt % 100) >= 10 && (cnt % 100) <= 20)
                suffix = "ей";
            else if (ce == 1)
                suffix = "ь";
            else if (ce == 2 || ce == 3 || ce == 4)
                suffix = "и";
            else
                suffix = "ей";

            return $"запис{suffix}";

        }
    }
}
