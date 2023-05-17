using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Logika.Meters
{
    [TypeConverter(typeof(Utils.EnumDescriptionConverter))]
    public enum RDay : byte
    {
        [Description("1")]
        d1 = 1,
        [Description("2")]
        d2,
        [Description("3")]
        d3,
        [Description("4")]
        d4,
        [Description("5")]
        d5,
        [Description("6")]
        d6,
        [Description("7")]
        d7,
        [Description("8")]
        d8,
        [Description("9")]
        d9,
        [Description("10")]
        d10,
        [Description("11")]
        d11,
        [Description("12")]
        d12,
        [Description("13")]
        d13,
        [Description("14")]
        d14,
        [Description("15")]
        d15,
        [Description("16")]
        d16,
        [Description("17")]
        d17,
        [Description("18")]
        d18,
        [Description("19")]
        d19,
        [Description("20")]
        d20,
        [Description("21")]
        d21,
        [Description("22")]
        d22,
        [Description("23")]
        d23,
        [Description("24")]
        d24,
        [Description("25")]
        d25,
        [Description("26")]
        d26,
        [Description("27")]
        d27,
        [Description("28")]
        d28
    }

    [TypeConverter(typeof(Utils.EnumDescriptionConverter))]
    public enum RHour : byte
    {
        [Description("00:00")]
        h00 = 0,
        [Description("01:00")]
        h01,
        [Description("02:00")]
        h02,
        [Description("03:00")]
        h03,
        [Description("04:00")]
        h04,
        [Description("05:00")]
        h05,
        [Description("06:00")]
        h06,
        [Description("07:00")]
        h07,
        [Description("08:00")]
        h08,
        [Description("09:00")]
        h09,
        [Description("10:00")]
        h10,
        [Description("11:00")]
        h11,
        [Description("12:00")]
        h12,
        [Description("13:00")]
        h13,
        [Description("14:00")]
        h14,
        [Description("15:00")]
        h15,
        [Description("16:00")]
        h16,
        [Description("17:00")]
        h17,
        [Description("18:00")]
        h18,
        [Description("19:00")]
        h19,
        [Description("20:00")]
        h20,
        [Description("21:00")]
        h21,
        [Description("22:00")]
        h22,
        [Description("23:00")]
        h23
    }
}
