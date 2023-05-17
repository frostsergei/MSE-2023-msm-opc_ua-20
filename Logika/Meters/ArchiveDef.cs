using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    public class ArchiveDef : ItemDefBase
    {
        public ArchiveType ArchiveType { get; }
        public int Capacity { get; }        
        
        public ArchiveDef(ChannelDef ChannelDef, int Ordinal, ArchiveType ArchType, Type ElementType, int Capacity, string Name, string Description)
        :base(ChannelDef, Ordinal, Name, Description, ElementType)
        {            
            this.ArchiveType = ArchType;                                    
            this.Capacity = Capacity;
        }

        public override string ToString()
        {
            return ArchiveType.ToString() + " " + Name + " (" + Description + ")";
        }
    }
    
    //------------------------------------------------------------------------------------------------
    public class ArchiveDef6 : ArchiveDef
    {        
        public string Address { get; private set; }
        
        public ArchiveDef6(ChannelDef ChannelDef, ArchiveType ArchType, Type RecordType, int Capacity, string Name, string Description, int Ordinal)
            : base(ChannelDef, Ordinal, ArchType, RecordType, Capacity, Name, Description)
        {            
            Address = Ordinal.ToString("000");
        }
    }

    public class MultipartArchiveDef6 : ArchiveDef  //архив состоящий из нескольких частей (СПЕ542)
    {
        public override int Ordinal {
            get {
                throw new Exception("'ordinal' is not available for class");
            }
        }
        public int[] Ordinals { get; }
        
        public MultipartArchiveDef6(ChannelDef ChannelDef, ArchiveType ArchType, Type RecordType, int Capacity, string Name, string Description, int[] Ordinals)
            : base(ChannelDef, -1, ArchType, RecordType, Capacity, Name, Description)
        {
            this.Ordinals = Ordinals;
        }
    }

    //------------------------------------------------------------------------------------------------
    public class ArchiveDef4L : ArchiveDef
    {
        public bool poorMans942 { get; } //models 4, 6 (single-channel)

        public int RecordSize { get; }

        public int IndexAddr { get; }
        public int? HeadersAddr { get; }
        public int RecordsAddr { get; }

        public int? IndexAddr2 { get; }
        public int? HeadersAddr2 { get; }
        public int? RecordsAddr2 { get; }

        public ArchiveDef4L(ChannelDef ChannelDef, ArchiveType ArchType, Type RecordType, int Capacity, string Name, string Description, int RecSize, int IndexAddr, int? HeadersAddr, int RecordsAddr, int? IndexAddr2, int? HeadersAddr2, int? RecordsAddr2, bool isTiny42)
            : base(ChannelDef, -1, ArchType, RecordType, Capacity, Name, Description)
        {
            poorMans942 = isTiny42;

            this.RecordSize = RecSize;
            this.IndexAddr = IndexAddr;
            this.HeadersAddr = HeadersAddr;
            this.RecordsAddr = RecordsAddr;

            this.IndexAddr2 = IndexAddr2;
            this.HeadersAddr2 = HeadersAddr2;
            this.RecordsAddr2 = RecordsAddr2;
        }
    }

    //------------------------------------------------------------------------------------------------
    public class ArchiveDef4M : ArchiveDef
    {
        public ArchiveDef4M(ChannelDef ChannelDef, ArchiveType ArchType, Type RecordType, int Capacity, string Name, string Description)
            : base(ChannelDef, -1, ArchType, RecordType, Capacity, Name, Description)
        {            
        }
    }
    //------------------------------------------------------------------------------------------------
}
