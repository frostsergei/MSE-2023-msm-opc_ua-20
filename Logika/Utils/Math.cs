using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Logika.Utils
{
    public class Round
    {
        public static double RoundToSignificantDigits(double value, int digits)
        {
            if (value == 0.0) // otherwise it will return 'nan' due to the log10() of zero
                return 0.0;

            double factor = Math.Pow(10.0, digits - Math.Ceiling(Math.Log10(Math.Abs(value))));
            return Math.Round(value * factor) / factor;
        }
    }
}
