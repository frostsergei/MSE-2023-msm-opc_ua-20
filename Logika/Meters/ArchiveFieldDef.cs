using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Logika.Meters
{
    public delegate string CustomDisplayValueDelegate(object value);

    public interface IArchivingElement
    {
        ArchiveType ArchiveType { get; }
    }

    public abstract class ArchiveFieldDef : TagDef, IArchivingElement
    {        
        public ArchiveType ArchiveType { get; }
                                
        public override string ToString()
        {
            return string.Format("{0} {1}", ChannelDef.Prefix, Name);
            //return string.Format("{0}({1})->{2}{3}.{4}", Caption, Ordinal);
        }

        public ArchiveFieldDef(ChannelDef channel, int ordinal, ArchiveType at, string Name, string Description, StdVar StndVar, Type dataType, string dbType, string displayFormat/*, CustomDisplayValueDelegate DisplayProc = null*/)
            : base(channel, ordinal, Name, StndVar, Description, dataType, dbType, displayFormat)
        {
            this.ArchiveType = at;                                     
        }
/*       
        public virtual ArchiveFieldInfo Clone() {
            ArchiveFieldInfo mf = new ArchiveFieldInfo(name, caption, ord, channelKind, stdVar, displayFormat, DataType, DbType, displayProc, channelSuffix);
            mf.channelSuffix = this.channelSuffix;
            //mf.idx = this.idx;
            return mf;
        }
 */
    }

    public class ArchiveFieldDef6 : ArchiveFieldDef
    {        
        public string NameSuffixed { get; }
        
        public string Address { get { return Ordinal.ToString("000"); } }
        public override string Key => Address;

        public ArchiveFieldDef6(ChannelDef ch, ArchiveType at, string name, string desc, int ordinal, StdVar standardVariable, Type dataType, string dbType, string displayFormat)
            : base(ch, ordinal, at, name, desc, standardVariable, dataType, dbType, displayFormat)
        {
            NameSuffixed = name;
            int ptPos = name.IndexOf('(');
            if (ptPos > 0 && name.EndsWith(")"))
                Name = name.Substring(0, ptPos);            // M(c) -> M
        }
    }

    public abstract class ArchiveFieldDef4 : ArchiveFieldDef
    {
        public ArchiveDef Archive { get; }
        public string Units { get; }    //predefined EU
        public override string Key => Name;

        public ArchiveFieldDef4(ArchiveDef ar, string name, string desc, StdVar stdVar, Type dataType, string dbType, string displayFormat, string units)
            : base(ar.ChannelDef, -1, ar.ArchiveType, name, desc, stdVar, dataType, dbType, displayFormat)
        {
            this.Archive = ar;
            this.Units = units;
        }
    }

    public class ArchiveFieldDef4L : ArchiveFieldDef4
    {
        public Logika4L.BinaryType InternalType { get; }
        public int FieldOffset { get; }
        public ArchiveFieldDef4L(ArchiveDef ar, string name, string desc, StdVar stdVar, Type dataType, string dbType, string units, string displayFormat, Logika4L.BinaryType binType, int fldOffset)
            : base(ar, name, desc, stdVar, dataType, dbType, displayFormat, units)
        {
            InternalType = binType;
            FieldOffset = fldOffset;
        }
    }

    public class ArchiveFieldDef4M : ArchiveFieldDef4
    {
        public int FieldIndex { get; }
        public ArchiveFieldDef4M(ArchiveDef ar, int fieldIndex, string name, string desc, StdVar standardVariable, Type dataType, string dbType, string displayFormat, string units)
            :base(ar, name, desc, standardVariable, dataType, dbType, displayFormat, units)
        {
            FieldIndex = fieldIndex;
        }
        public override string ToString()
        {
            return string.Format("{0} {1}", ChannelDef.Prefix, Name);
            //return string.Format("{0}({1})->{2}{3}.{4}", Caption, Ordinal);
        }

    }
}
