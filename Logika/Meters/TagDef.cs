using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    public abstract class TagDef : ItemDefBase // reference tag definition (from internal DB) base
    {
        public StdVar StdVar { get; }

        readonly string dbType;
        public string DbType
        {
            get {
                if (!string.IsNullOrEmpty(dbType))
                    return dbType;

                switch (ElementType.Name) {
                    case "Byte": return "tinyint";
                    case "Int32": return "int";
                    case "Int64": return "bigint";
                    case "Single": return "real";  //=> float(24) (4-byte)
                    case "Double": return "float"; //=> float(53) (8-byte) 
                    case "String":
                        return "varchar(128)";
                    //case DbType.String:
                    //    string dbTypeStr = "nvarchar";
                    //    if (fieldSize > 0)
                    //        dbTypeStr += string.Format("({0})", fieldSize);
                    //    return dbTypeStr;         

                    //case "Byte[]":
                    //    return string.Format("varbinary({0})", fieldSize.Value); //биты НС

                    //case "Char[]": return string.Format("char({0})", fieldSize.Value);
                    default: throw new NotImplementedException("cannot map DataType to DbType");
                }
            }

            //public readonly int? FieldSize;
        }

        public string DisplayFormat { get; }
        public abstract string Key { get; }

        public TagDef(ChannelDef channelDef, int ordinal, string name, StdVar stdVar, string desc, Type dataType, string dbType, string displayFormat)
        :base(channelDef, ordinal, name, desc, dataType)
        {                        
            this.StdVar = stdVar;                        
            this.dbType = dbType;
            this.DisplayFormat = displayFormat;
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    public abstract class DataTagDef : TagDef   //DA tags
    {
        public TagKind Kind { get; protected set; }
        public string DescriptionEx { get; }    //для настроечных параметров
        public string Range { get; }            //для настроечных параметров

        public bool isBasicParam { get; protected set; }

        public int UpdateRate { get; protected set; }

        public DataTagDef(ChannelDef channel, string name, StdVar stdVar, string desc, Type dataType, string dbType, string displayFormat, TagKind tagKind, bool basicParam, int updateRate, int ord, string descEx, string range)
            : base(channel, ord, name, stdVar, desc, dataType, dbType, displayFormat)
        {
            this.Kind = tagKind;
            this.isBasicParam = basicParam;
            this.UpdateRate = updateRate;
            this.DescriptionEx = descEx;
            this.Range = range;
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", Key, Ordinal, Name);
        }
    }


    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    public enum Tag6NodeType : int
    {
        Tag = 0,
        Array = 1,
        Structure = 2,
    }

    [DebuggerDisplay("{ToString()}")]
    public class DataTagDef6 : DataTagDef
    {
        public override string Key { get { return Address; } }
        public Tag6NodeType NodeType { get; private set; }
        public int? Count { get; private set; }
        public int? Index { get; private set; }
        public string Address { get; private set; }

        public DataTagDef6(ChannelDef ownerChannel, Tag6NodeType nodeType, string name, StdVar stdVar, TagKind tagKind, bool basicParam, int updateRate, int ord, string desc, Type dataType, string sDbType, int? index, int? count, string descEx, string range)
            : base(ownerChannel, name, stdVar, desc, dataType, sDbType, null, tagKind, basicParam, updateRate, ord, descEx, range)
        {
            this.NodeType = nodeType;
            this.Index = index;
            this.Count = count;
            //Ordinal.ToString("000");
            //if (Index >= 0)
            //    tid += string.Format("н{0:D2}", Index);

            Address = ord.ToString("000") + (Index.HasValue ? "н" + Index.Value.ToString("00") : "");
        }


        public override string ToString()
        {
            if (NodeType == Tag6NodeType.Structure)
                return string.Format("structure {0} {1}", Name, Description);
            else if (NodeType == Tag6NodeType.Array)
                return string.Format("array {0} {1}", Name, Description);
            else
                return base.ToString();
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    public class TagDef4 : DataTagDef
    {
        public override string Key { get { return Name; } }
        public string Units { get; protected set; }     //predefined EU for tag (from database)

        public TagDef4(ChannelDef ch, string name, StdVar stdVar, TagKind tagKind, bool basicParam, int updateRate, int ord, string desc, Type dataType, string sDbType, string units, string displayFormat, string descEx, string range)
            : base(ch, name, stdVar, desc, dataType, sDbType, displayFormat, tagKind, basicParam, updateRate, ord, descEx, range)
        {
            Units = units;
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    public class TagDef4L : TagDef4
    {
        public Logika4L.BinaryType internalType { get; protected set; }

        public bool inRAM { get; protected set; }
        public int? address { get; protected set; }  //some of 741 params are 'virtual' and their address cannot be defined, so they're null
        public int? channelOffset { get; protected set; }

        public int? addonAddress { get; protected set; }
        public int? addonChannelOffset { get; protected set; }

        public TagDef4L(ChannelDef parentChannel, string name, StdVar stdVar, TagKind tagKind, bool basicParam, int updateRate, int ord, string desc, Type dataType, string sDbType, string units, string displayFormat, string descEx, string range,
                        Logika4L.BinaryType binType, bool inRam, int? addr, int? chnOffs, int? addonAddr, int? addonChnOffs)
            : base(parentChannel, name, stdVar, tagKind, basicParam, updateRate, ord, desc, dataType, sDbType, units, displayFormat, descEx, range)
        {
            if (addr < 0 || channelOffset < 0 || addonAddr < 0 || addonChannelOffset < 0)
                throw new ArgumentException("значение nullable адреса не может быть < 0");

            this.internalType = binType;
            this.inRAM = inRam;
            this.address = addr;
            this.channelOffset = chnOffs;
            this.addonAddress = addonAddr;
            this.addonChannelOffset = addonChnOffs;
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3}", Ordinal, ChannelDef.Prefix, Name, Kind);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    //---------------------------------------------------------------------------------------------------------------------------
    
    public class TagDef4M : TagDef4
    {
        //public byte? TagChannel { get; }   //канал тега в приборах в которых каналы не обозначают ввод (ТВ) (742, 410 напр)

        public TagDef4M(ChannelDef parentChannel, string name, StdVar stdVar, TagKind tagKind, bool basicParam, int updateRate, /*byte? tagChannel, */int ord, string desc, Type dataType, string sDbType, string units, string displayFormat, string descEx, string range)
            : base(parentChannel, name, stdVar, tagKind, basicParam, updateRate, ord, desc, dataType, sDbType, units, displayFormat, descEx, range)
        {
            //TagChannel = tagChannel;
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3}", Ordinal, ChannelDef.Prefix, Name, Kind);
        }

    }
    
}
