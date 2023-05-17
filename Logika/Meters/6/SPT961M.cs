using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPT961M : Logika6
    {
        internal TSPT961M() { }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.T; } }
        public override string Caption { get { return "СПТ961М"; } }
        public override int MaxChannels { get { return 6; } }
        public override int MaxGroups { get { return 3; } }

        #region MDB maps
        private static int[] mdb_R_ords = { 91, 72, 75, 79, 83, 87 };
        private static int[] mdb_P_ords = { 235, 196, 201, 206, 239, 243, 211, 216, 221 };
        private static int[] mdb_C_ords = { 401, 406 };
        //private static Enum[] mdb_Totals = { StvT.P.M, StvT.P.W, StvT.P.V, StvT.C.dM, StvT.C.dW };
        
        #endregion
        
     
    }
}
