using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPG763_1 : Logika6N
    {
        internal TSPG763_1() { }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.G; } }
        public override string Caption { get { return "СПГ763.1"; } }

        #region MDB maps

        private static int[] mdb_R_ords = { 91, 86, 79, 83 };
        private static int[] mdb_P_ords = { 235, 196, 201, 206, 239, 243, 211, 216, 221, 246 };
        private static int[] mdb_C_ords = { 401, 406, 411 };
        //private static Enum[] mdb_Totals = { StvG.P.M, StvG.P.V, StvG.P.Vr, StvG.C.V, StvG.C.M };

        #endregion

    }
}
