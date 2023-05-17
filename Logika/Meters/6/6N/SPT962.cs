using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT962 : Logika6N
    {
        internal TSPT962() { }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ962"; } }

        #region MDB maps
        private static int[] mdb_R_ords = { 91, 86, 72, 75, 79, 83 };
        private static int[] mdb_P_ords = { 201, 206, 239, 243, 211, 216, 221,  227, 224, 230 };
        private static int[] mdb_C_ords = { 401, 406, 411, 416, 421, 426, 431, 436, 441 };
        
        #endregion


    }
}
