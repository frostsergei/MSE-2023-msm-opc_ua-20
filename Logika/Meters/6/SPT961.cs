using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT961 : Logika6
    {
        internal TSPT961() { }
        
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ961"; } }
        public override int MaxChannels { get { return 5; } }
        public override int MaxGroups { get { return 2; } }

        #region MDB maps
        private static int[] mdb_R_ords = { 71, 75 };
        private static int[] mdb_P_ords = { 201, 206, 211, 216, 231, 241, 221 };
        private static int[] mdb_C_ords = { 401, 406 };
        //private static Enum[] mdb_Totals = { StvT.P.M, StvT.P.W, StvT.P.V, StvT.C.dM, StvT.C.dW };

        #endregion
    }
}
