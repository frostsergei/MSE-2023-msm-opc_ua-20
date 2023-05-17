using Logika.Meters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Logika.Meters
{

    /// <summary>
    /// tag instance, with channel number and value
    /// </summary>
    /// 

    //public interface IDataTag : IChanneledTag
    //{
    //    object Value { get; }
    //}
    
    [DebuggerDisplay("{ToString()}")]
    public class DataTag : Tag
    {
        public int? Index {
            get {
                return def is DataTagDef6 ? ((DataTagDef6)def).Index : null;
            }
        }
        
        public object Value { get; set; }        
        public DateTime TimeStamp;
        public bool Oper;
        public string ErrorDesc;

        public string DisplayFormat { get { return def.DisplayFormat; } }

        string addr;
        public override string Address
        {
            get {
                return addr;                
            }
        }

        public DataTag(DataTagDef refTag, int channelNo)
            :base(refTag, channelNo)
        {
            if (def is DataTagDef6) {
                addr = ((DataTagDef6)def).Address;
                if (channelNo > 0)
                    addr += refTag.ChannelDef.Prefix + channelNo;
            } else {
                TagDef4 td = (TagDef4)def;
                addr = (channelNo>0 ? td.ChannelDef.Prefix + channelNo + "_" : "") + td.Name;
            }
        }

        public DataTag(DataTag t)
          : base(t)
        {            
            this.Value = t.Value;
            this.EU = t.EU;
            this.Oper = t.Oper;
            this.addr = t.addr;
        }

        public override string ToString()
        {            
            string idxStr = "";
            if (Index.HasValue)
                idxStr = string.Format("н{0:D2}", Index);
            
            string euStr = string.IsNullOrWhiteSpace(EU) ? "" : "[" + EU.Trim() + "]";
            return string.Format("{0}.{1:D3}{2}({3}) = {4} {5}", Channel.Name, def.Ordinal, idxStr, def.Name, Value, euStr);
        }
    }

    [DebuggerDisplay("{ToString()}")]
    public class DataTag6Container : Tag   //структура / массив x6x приборов
    {
        public readonly DataTag[] tags;
        public DataTag6Container(Logika.Meters.DataTagDef refTag, int channelNo, DataTagDef6[] leafs)
            : base(refTag, channelNo)
        {
            tags = new DataTag[leafs.Length];
            for (int i=0; i<leafs.Length; i++)
                tags[i] = new DataTag(leafs[i], channelNo);
        }
        public override string Address
        {
            get {
                return ((DataTagDef6)def).Address;
            }
        }

        public override string ToString()
        {
            string containerType = (def as DataTagDef6).NodeType.ToString().Substring(0,1);                                    
            return string.Format("<{0}> {1}.{2:D3} ({3})", containerType, Channel.Name, def.Ordinal, def.Description);
        }
    }
}
