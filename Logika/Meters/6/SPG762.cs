using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;

namespace Logika.Meters
{
    public class TSPG762 : Logika6
    {
        internal TSPG762() { }
        //public override MeterType Type { get { return MeterType.SPG762; } }
        public override Logika.Meters.MeasureKind MeasureKind { get { return MeasureKind.G; } }
        public override string Caption { get { return "СПГ762"; } }
        public override int MaxChannels { get { return 3; } }
        public override int MaxGroups { get { return 1; } }
        internal override Dictionary<ImportantTag, object> GetCommonTagDefs()
        {
            var dt = base.GetCommonTagDefs();
            dt[ImportantTag.EngUnits] = "030";
            return dt;
        }

        #region MDB maps
        private static int[] mdb_R_ords = { 81 };
        private static int[] mdb_P_ords = { 251, 201, 206, 211, 221, 231 };
        private static int[] mdb_C_ords = { 411, 421 };
        #endregion
    }
}
