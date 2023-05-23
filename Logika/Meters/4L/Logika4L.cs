using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;


namespace Logika.Meters
{
    public abstract class Logika4L : Logika4
    {
        public override bool SupportedByProlog4 => true;
        
        public override bool Outdated => true;


        public const int FLASH_PAGE_SIZE = 0x40;

        public enum BinaryType : byte
        {
            undefined = 0,

            r32, //single microchip-float
            r32x3, //triple consequtive microchip floats, sum to obtain result
            time, //HH MM SS (3 bytes)
            date, //YY MM DD (3 bytes)
            MMDD, //ММ-DD-xx-xx (32-bit) (дата перехода на летнее/зимнее время)
            bitArray32, //сборки НС
            bitArray24,
            bitArray16,
            bitArray8,
            dbentry, //параметр БД приборов 942, 741, (943?), структура, используется строка 
            dbentry_byte, //параметр БД приборов, используется бинарное представление ([P], [dP] - единицы измерения 741)
            u8,  //unsigned 8bit char
            i32r32, //int32+float во FLASH (+ float приращение за текущий час в ОЗУ, не читаем и не добавляем)
            MMHH, //minutes, hours (941: 'ТО' )
            NSrecord,
            IZMrecord,
            archiveStruct,     //архивный срез (структура, определяемая прибором)
            modelChar,  //код модели прибора
            u24,        //серийный номер прибора                        
            
            svcRecordTimestamp,  //метка времени записи сервисного архива
        }

        public static object GetValue(Logika4L.BinaryType binaryType, byte[] buffer, int offset)
        {
            return GetValue(binaryType, buffer, offset, out bool dummyOperFlag);
        }

        public static object GetValue(Logika4L.BinaryType binaryType, byte[] buffer, int offset, out bool operFlag)
        {
            operFlag = false;
            switch (binaryType) {

                case BinaryType.r32:
                    float Val = GetMFloat(buffer, offset);
                    return Val;

                case BinaryType.time:
                    DateTime t = new DateTime(2000, 1, 1, buffer[offset], buffer[offset + 1], buffer[offset + 2], DateTimeKind.Local);
                    return t.ToString("HH:mm:ss"); //long time pattern

                case BinaryType.date:
                    DateTime d = new DateTime(2000 + buffer[offset], buffer[offset + 1], buffer[offset + 2], 0, 0, 0, DateTimeKind.Local);
                    return d.ToString("dd/MM/yy");

                case BinaryType.MMDD:  //ММ-ДД                        
                    DateTime Dt = new DateTime(2000, buffer[offset + 0], buffer[offset + 1], 0, 0, 0);
                    return Dt.ToString("dd/MM");

                case BinaryType.bitArray8:
                    return BitNumbers(buffer[offset], 8, 0);                   

                case BinaryType.bitArray16:
                    ushort usv = BitConverter.ToUInt16(buffer, (int)offset);
                    return BitNumbers(usv, 16, 8);   //сдвиг номеров НС на 8 (НС по каналам 942, 943)                    

                case BinaryType.bitArray24:
                    byte[] bExt = { 0, 0, 0, 0 };
                    Array.Copy(buffer, offset, bExt, 0, 3);
                    ulong ulv = BitConverter.ToUInt32(bExt, 0);
                    return BitNumbers(ulv, 24, 0);                    

                case BinaryType.bitArray32:
                    ulong u32v = BitConverter.ToUInt32(buffer, (int)offset);
                    return BitNumbers(u32v, 32, 0);                    

                case BinaryType.dbentry: //настроечный параметр со значением в строковом поле                    
                    const int PARAM_BIN_PART_LEN = 4;
                    const int PARAM_STR_LEN = 8;
                    int strPartOffset = offset + PARAM_BIN_PART_LEN;
                    //для некоторых приборов в новых версиях прошивок добавлялись настроечные параметры => на приборах со старыми прошивками эти параметры не инициализированы (строковая часть заполнена 0xFF)                    
                    if (buffer[strPartOffset] == 0xFF) { 
                        operFlag = false;
                        return "";
                    } 
                    operFlag = (buffer[offset] & 0x01) > 0;
                    char[] c = new char[PARAM_STR_LEN];
                    Array.Copy(buffer, strPartOffset, c, 0, PARAM_STR_LEN);                    
                    return new string(c).Trim('\0', ' ');   //бывают незаполненные параметры состоящие из '\0';                    
                    
                case BinaryType.dbentry_byte:  //настроечный параметр со значением в бинарном поле
                    operFlag = (buffer[offset] & 0x01) > 0;
                    return buffer[(int)offset + 12];

                case BinaryType.u8:
                    return buffer[offset];

                case BinaryType.r32x3: {
                        double v1 = GetMFloat(buffer, offset);
                        double v2 = GetMFloat(buffer, offset + 4);
                        double v3 = GetMFloat(buffer, offset + 8);
                        return v1 + v2 + v3;
                    }

                case BinaryType.i32r32: {
                        Int32 intPart = BitConverter.ToInt32(buffer, (int)offset);
                        float floatPart = GetMFloat(buffer, offset + 4);

                        double vtv = (double)intPart + (double)floatPart;
                        return vtv;
                    }

                case BinaryType.MMHH: {
                        DateTime mh = new DateTime(2000, 1, 1, buffer[offset + 1], buffer[offset], 0);
                        return mh.ToString("HH:mm");    //long time pattern
                    }
                case BinaryType.u24:
                    return BitConverter.ToUInt32(buffer, (int)offset) & 0x00FFFFFF;

                case BinaryType.modelChar:
                    return new string(new char[] { (char)buffer[offset] });
                    
                /// <returns> 
                /// null - if record is empty (erased) 
                /// DateTime.MinValue if record header is corrupted (cannot be converted to valid time)
                /// DateTime if record header ok
                /// </returns>
                case BinaryType.svcRecordTimestamp:  //метка времени записи сервисного архива
                    if (buffer[offset] != 0x10)   //0x10 marks filled record 
                        return null;

                    byte year = buffer[offset + 1];  //year is 2000-based in opposite to syncArchive headers, where year is 1900-based (!)
                    byte mon = buffer[offset + 2];
                    byte day = buffer[offset + 3];
                    byte hour = buffer[offset + 4];
                    byte min = buffer[offset + 5];

                    if (year == 0xFF || mon == 0 || mon > 12 || day == 0 || day > 31 || hour > 23 || min > 59)
                        return DateTime.MinValue;
                    try {
                        return new DateTime(year + 2000, mon, day, hour, min, 0, DateTimeKind.Local);    //year is 2000-based 
                    } catch (ArgumentOutOfRangeException) {
                        return DateTime.MinValue;
                    }

                case BinaryType.NSrecord:
                    return "НС" + buffer[offset + 6].ToString("00") + (((buffer[offset + 7] & 1) > 0) ? "+" : "-"); //номер нештатки + "+/-"

                case BinaryType.IZMrecord:
                    return LcdCharsToString(buffer, offset + 8, 16).Trim();

                default:
                    throw new Exception($"unsupported binary type in GetValue: '{binaryType}'");
            }
        }

        /// returns size of flash part
        public static int SizeOf(Logika4L.BinaryType dataType)
        {
            switch (dataType) {

                case BinaryType.u8:
                    return 1;
                case BinaryType.bitArray8:
                    return 1;
                case BinaryType.bitArray16:
                    return 2;
                case BinaryType.bitArray24:
                    return 3;
                case BinaryType.bitArray32:
                    return 4;

                case BinaryType.MMHH:
                    return 2;

                case BinaryType.time:         //HH MM SS 
                case BinaryType.date:         //YY MM DD 
                case BinaryType.r32:          //single microchip-float
                case BinaryType.MMDD:         //ММ-DD-xx-xx	(32-bit)                
                    return 4;

                case BinaryType.i32r32:
                    return 8; //sequence of { int32, mfloat32 }

                case BinaryType.NSrecord:
                    return 8;

                case BinaryType.r32x3:
                    return 12; //triple consequtive microchip floats,	add	to got result

                case BinaryType.dbentry:
                case BinaryType.dbentry_byte:
                    return 16;//параметр БД приборов 942, 741,	(943?),	структура (строка +	бинарное)	

                case BinaryType.IZMrecord:
                    return 24;
                case BinaryType.modelChar:
                    return 1;

                case BinaryType.u24:
                    return 4;   //not 3, high char is a sort of checksum

                default: throw new Exception("unknown type");
            }
        }
        

        /// <summary>
        /// convert microchip float to PC float
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static float GetMFloat(byte[] buf, int offset)
        {
            uint i = BitConverter.ToUInt32(buf, (int)offset);

            uint sign = (i >> 23) & 1;
            uint exponent = i >> 24;
            uint mantissa = i & 0x007FFFFF;

            i = (exponent << 23) | (sign << 31) | mantissa;

            // validity checks - to avoid manipulating nan's 
            // check if exponent is NaN
            if ((i & 0x7F800000) == 0x7F800000)  //exponent is 0xFF ?
                i &= 0xFF7FFFFF; //clear exponent LSB - de-nan
            //we've got maximum representable real float (still signed).

            return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
        }

        //----------------------------------------------------------------------------------------------
        public struct SyncRecordField
        {            
            public int offset;
            public Logika4L.BinaryType binType;
        }                
               
        //LCD characters -> readable string translation
        static readonly char[] lcd_char_map = { 'Б', 'Г', 'ё', 'Ж', 'З', 'И', 'Й', 'Л', 'П', 'У', 'Ф', 'Ч', 'Ш', 'Ъ', 'Ы', 'Э', 'Ю', 'Я', 'б', 'в', 'г', 'ё', 'ж', 'з', 'и', 'й', 'к', 'л', 'м', 'н', 'п', 'т', 'ч', 'ш', 'ъ', 'ы', 'ь', 'э', 'ю', 'я', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', 'Д', 'Ц', 'Щ', 'д', 'ф', 'ц', 'щ' };
        public static string LcdCharsToString(byte[] buf, int offset, int length)
        {
            char[] result = new char[length];

            for (int i = 0; i < length; i++) {
                char rCh = (char)buf[offset + i];
                result[i] = ((rCh < 0xA0 || rCh > 0xEF) ? rCh : lcd_char_map[rCh - 0xA0]);
            }
            return new string(result);
        }

        /// <returns> 
        /// null if header is empty (erased) 
        /// DateTime.MinValue if header is corrupted (cannot be converted to valid time
        /// DateTime if header ok
        /// </returns>
        public static DateTime? syncHeaderToDatetime(ArchiveType arType, byte rd, byte rh, byte[] buffer, int offset)
        {
            uint rawhdr = BitConverter.ToUInt32(buffer, offset);
            if (rawhdr == 0x00000000 || rawhdr == 0xFFFFFFFF)
                return null;

            byte year = buffer[offset + 0];
            byte mon = buffer[offset + 1];
            byte day = arType == ArchiveType.Month ? rd : buffer[offset + 2];
            byte hour = arType == ArchiveType.Hour ? buffer[offset + 3] : rh;

            try {
                if (mon<1 || mon>12 || day==0 || day>31 || hour > 23)
                    return null;    //битый заголовок
                return new DateTime(year + 1900, mon, day, hour, 0, 0, DateTimeKind.Local);
            } catch (ArgumentOutOfRangeException) {
                return null;    //битый заголовок
            }
        }


        #region ADS files support

        public class ADSFlashRun
        {
            public int Start;
            public int Length;
        }

        public abstract string getModelFromImage(byte[] flashImage);
                
        public abstract ADSFlashRun[] getAdsFileLayout(bool all, string model);

        public const int PARAMS_FLASH_ADDR = 0x0200;

        #endregion

        public override string FamilyName { get { return "L4"; } }

        protected override string tagsSort { get { return "Device, Channel, Ordinal"; } }        
        protected override string archiveFieldsSort { get { return "Device, ArchiveType, FieldOffset"; } }

        internal override DataTagDef readTagDef(DataRow r)
        {
            readCommonDef(r, out string chKey, out string name, out int ordinal, out TagKind kind, out bool isBasicParam, out int updRate, out Type dataType, out StdVar stv, out string desc, out string descriptionEx, out string range);

            ChannelDef ch = this.Channels.FirstOrDefault(x => x.Prefix == chKey);
            
            string dbType = r["dbType"] == DBNull.Value ? null : Convert.ToString(r["dbType"]);     
            string units = Convert.ToString(r["Units"]);
            string displayFormat = Convert.ToString(r["DisplayFormat"]);

            string sNativeType = Convert.ToString(r["InternalType"]);
            Logika4L.BinaryType nativeType = (Logika4L.BinaryType)Enum.Parse(typeof(Logika4L.BinaryType), sNativeType, true);
            bool inRam = Convert.ToBoolean(r["InRAM"]);
            int? addr = r["Address"] == DBNull.Value ? null : (int?)r["Address"]; 
            int? chOfs = r["ChannelOffset"] == DBNull.Value ? null : (int?)r["ChannelOffset"];
            int? addonAddr = r["Addon"] == DBNull.Value ? null : (int?)r["Addon"];
            int? addonChOfs = r["AddonOffset"] == DBNull.Value ? null : (int?)r["AddonOffset"];
            
            return new TagDef4L(ch, name, stv, kind, isBasicParam, updRate, ordinal, desc, dataType, dbType, units, displayFormat, descriptionEx, range, nativeType, inRam, addr, chOfs, addonAddr, addonChOfs);
        }

        protected override ArchiveDef[] readArchiveDefs(DataRow[] rows)
        {
            var d = new List<ArchiveDef>();
            foreach (DataRow r in rows) {
                string chKey = r["Channel"].ToString();
                ChannelDef ch = Channels.FirstOrDefault(x => x.Prefix == chKey);            
                ArchiveType art = ArchiveType.FromString(r["ArchiveType"].ToString());
                string name = r["Name"].ToString();
                string desc = r["Description"].ToString();

                string sRecType = "System." + r["RecordType"].ToString();
                Type recType = Type.GetType(sRecType, true);

                int recSize = (int)r["RecordSize"];
                int count = (int)r["Count"];
                int idx1 = (int)r["Index1"];
                int? hdr1 = r["Headers1"]==DBNull.Value? null :(int?)r["Headers1"];
                int rec1 = (int)r["Records1"];

                int? idx2 = r["Index2"] == DBNull.Value ? null : (int?)r["Index2"];
                int? hdr2 = r["Headers2"] == DBNull.Value ? null : (int?)r["Headers2"];
                int? rec2 = r["Records2"] == DBNull.Value ? null : (int?)r["Records2"];
                ArchiveDef4L ra = new ArchiveDef4L(ch, art, recType, count, name, desc, recSize, idx1, hdr1, rec1, idx2, hdr2, rec2, false);
                d.Add(ra);
            }
            return d.ToArray();
        }

        internal override ArchiveFieldDef readArchiveFieldDef(DataRow r)
        {
            ArchiveType art = ArchiveType.FromString(r["ArchiveType"].ToString());
            ArchiveDef ra = Archives.FirstOrDefault(x => x.ArchiveType == art);

            string sDataType = "System." + r["DataType"].ToString();
            Type t = Type.GetType(sDataType);

            string sDbType = Convert.ToString(r["DbType"]);
            string name = r["Name"].ToString();            
            string desc = r["Description"].ToString();
            string stdType = r["VarT"].ToString();
            //int? depth = r["Depth"] == DBNull.Value ? null : (int?)r["Depth"];
            string units = r["Units"].ToString();
            string displayFormat = null;    //r["DisplayFormat"].ToString();    //not used yet for 4L
            Logika4L.BinaryType nativeType = (Logika4L.BinaryType)Enum.Parse(typeof(Logika4L.BinaryType), r["InternalType"].ToString(), true);            
            int offset = (int)r["FieldOffset"];

            StdVar stv = r["VarT"] == DBNull.Value ? StdVar.unknown : (StdVar)Enum.Parse(typeof(StdVar), r["VarT"].ToString());
            return new ArchiveFieldDef4L(ra, name, desc, stv, t, sDbType, units, displayFormat, nativeType, offset);
        }        
    }
    //public class FlashRun
    //{
    //    public readonly int start;
    //    public readonly int end;
    //    public FlashRun(int Start, int End)
    //    {
    //        start = Start;
    //        end = End;
    //    }
    //}
}