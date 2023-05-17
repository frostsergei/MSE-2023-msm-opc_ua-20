using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Meters
{   
    
    
    public enum StdVar
    {
        unknown,
        SP,         //схема потребления, int
        NS,         //битовые сборки НС/ДС
        AVG,        //generic, average in summaries
        G,          //мгновенный расход
        //hg,         //удельная объемная теплота сгорания -> AVG
        //auxInt,
        M,          
        P,          
        dP,
        T,          //температура
        ti,         //интервал времени
        V,
        W,
    }
}
